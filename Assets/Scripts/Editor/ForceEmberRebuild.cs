using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ForceEmberRebuild
{
    static ForceEmberRebuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool("EmberSwordForcedRebuild_v1", false)) return;
            EditorPrefs.SetBool("EmberSwordForcedRebuild_v1", true);
            
            Debug.Log("EMBER: Forcing scene rebuild to instantiate the sword mesh into the player...");
            EmberSceneBuilder.BuildScene();
        };
    }
}
