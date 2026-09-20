using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds and profiles the Android APK.
///
/// Everything here is about frame *pacing* as much as raw throughput. The build felt choppy
/// despite a workable average frame rate, and uneven frame intervals read as stutter even
/// when the average looks fine. The settings applied below target the specific costs that
/// dominate on a mobile tile-based GPU: full-screen post effects, HDR buffers, shadow
/// resolution, and the per-frame allocations that force a garbage collection mid-frame.
/// </summary>
public static class EmberAndroidBuild
{
    const string OutputDir = "Builds/Android";
    const string ApkName = "Ember.apk";
    public static string ApkPath => Path.Combine(OutputDir, ApkName);

    [MenuItem("EMBER/Android/1. Apply Mobile Settings")]
    public static void ApplyMobileSettings()
    {
        // ---- player
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.gpuSkinning = true;
        PlayerSettings.MTRendering = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // Vulkan first: it paces far more predictably than GLES on modern devices, and this
        // project's cost is bandwidth rather than driver features.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

        // The engine's own frame pacer. Without this Android hands frames to the compositor
        // at irregular intervals, which is the stutter the build actually had.
        PlayerSettings.Android.optimizedFramePacing = true;
        PlayerSettings.Android.startInFullscreen = true;
        PlayerSettings.Android.renderOutsideSafeArea = true;

        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.stripUnusedMeshComponents = true;

        // ---- textures: ASTC is the only sensible default on ARM64 Android.
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        EditorUserBuildSettings.buildAppBundle = false;

        AssetDatabase.SaveAssets();
        Debug.Log("EMBER Android: player settings applied (IL2CPP/ARM64, Vulkan, frame pacing, ASTC).");
    }

    [MenuItem("EMBER/Android/2. Build APK")]
    public static void BuildApk()
    {
        ApplyMobileSettings();
        Directory.CreateDirectory(OutputDir);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("EMBER Android: no scenes enabled in Build Settings.");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log("EMBER Android: BUILD OK -> " + ApkPath +
                      "  size=" + (summary.totalSize / (1024f * 1024f)).ToString("F1") + " MB" +
                      "  time=" + summary.totalTime.TotalSeconds.ToString("F0") + "s");
        else
            Debug.LogError("EMBER Android: BUILD " + summary.result + " (" + summary.totalErrors + " errors)");
    }

    /// <summary>
    /// Builds with the profiler attached and development instrumentation on, so a frame-time
    /// trace can be captured from the device rather than guessed at from the editor.
    /// </summary>
    [MenuItem("EMBER/Android/3. Build APK (development + profiler)")]
    public static void BuildApkDevelopment()
    {
        ApplyMobileSettings();
        Directory.CreateDirectory(OutputDir);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, "Ember-dev.apk"),
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("EMBER Android: dev build " + report.summary.result + " -> " + options.locationPathName);
    }
}
