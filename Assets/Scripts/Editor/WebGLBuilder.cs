using UnityEditor;
using System.Linq;

public class WebGLBuilder
{
    public static void FixSettingsAndBuild()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;
        
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        
        BuildPipeline.BuildPlayer(scenes, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
    }
}
