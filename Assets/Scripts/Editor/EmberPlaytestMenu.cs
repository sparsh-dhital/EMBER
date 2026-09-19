using UnityEditor;
using UnityEditor.SceneManagement;

// EMBER > Run Automated Playtest: opens the main scene, enters Play Mode and runs every acceptance test.
public static class EmberPlaytestMenu
{
    [MenuItem("EMBER/Run Automated Playtest")]
    public static void RunPlaytest()
    {
        if (EditorApplication.isPlaying) { EmberPlaytest.Run(); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(EmberSceneBuilder.ScenePath);
        SessionState.SetBool(EmberPlaytest.SessionKey, true);
        EditorApplication.isPlaying = true;
    }
}
