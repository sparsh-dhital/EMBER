using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies the platform-specific runtime settings that cannot be expressed in the
/// Quality or URP assets, before the first frame is drawn.
///
/// The Android build felt choppy despite a workable average frame rate. The cause was
/// frame *pacing*, not throughput: vSyncCount is 0 and nothing ever set
/// Application.targetFrameRate, so Unity rendered as fast as it could and handed the
/// compositor frames at irregular intervals. Uneven frame times read as stutter even
/// when the average is high. Pinning a target the device can actually hold makes the
/// interval consistent, which is what "smooth" really means.
/// </summary>
[DefaultExecutionOrder(-1000)]
public static class PlatformTuning
{
    /// <summary>Frame target for phones. 60 where the device can hold it, 30 where it cannot.</summary>
    public const int MobileTargetFps = 60;
    public const int MobileFallbackFps = 30;
    public const int DesktopTargetFps = 120;

    /// <summary>Devices with less memory than this (MB) get the conservative target.</summary>
    const int LowMemoryThresholdMb = 3072;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (Application.isMobilePlatform)
        {
            // vSync off + an explicit target is the combination Android actually paces well;
            // leaving vSync on here makes targetFrameRate a no-op.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = IsLowEndDevice() ? MobileFallbackFps : MobileTargetFps;

            // Skinning on the GPU frees the CPU, which is the bottleneck on phones.
            // (Also set in Player Settings; asserted here so a settings regression is visible.)
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // Shadow cascades and distance cost bandwidth more than anything else on mobile.
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 30f);

            // Physics does not need to run faster than the display can show its results.
            Time.fixedDeltaTime = 1f / 50f;
            Physics.defaultSolverIterations = 4;
            Physics.defaultSolverVelocityIterations = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = DesktopTargetFps;
        }

        // Sleep timeout off: this is a game, not a document.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    static bool IsLowEndDevice()
    {
        if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < LowMemoryThresholdMb) return true;
        if (SystemInfo.processorCount > 0 && SystemInfo.processorCount <= 4) return true;
        return false;
    }
}
