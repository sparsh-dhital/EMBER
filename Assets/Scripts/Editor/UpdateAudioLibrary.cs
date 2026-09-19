using UnityEngine;
using UnityEditor;
using System.Linq;

public class UpdateAudioLibrary
{
    [MenuItem("Tools/Integrate Mixkit Audio")]
    public static void IntegrateAudio()
    {
        var am = Object.FindFirstObjectByType<AudioManager>();
        if (!am)
        {
            Debug.LogError("Could not find AudioManager in the active scene.");
            return;
        }

        Undo.RecordObject(am, "Integrate Mixkit Audio");

        // Helper to find or create an entry
        SfxEntry GetOrCreateEntry(Sfx id, float vol = 1f)
        {
            if (am.sounds == null) am.sounds = new SfxEntry[0];
            var e = am.sounds.FirstOrDefault(x => x != null && x.id == id);
            if (e == null)
            {
                e = new SfxEntry { id = id, volume = vol, pitchVariance = 0.05f };
                ArrayUtility.Add(ref am.sounds, e);
            }
            return e;
        }

        // 1. Opening
        var openingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-terror-sweep-of-darkness-2630.wav");
        if (openingClip)
        {
            var e = GetOrCreateEntry(Sfx.Opening, 1f);
            e.clips = new[] { openingClip };
        }

        // 2. Ambience (Crackle) -> loop_lantern_crackle replacement
        var crackleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-campfire-night-wind-1736.wav");
        if (crackleClip)
        {
            am.lanternCrackleLoop = crackleClip;
        }

        // 3. Enemy Growl -> VampireHiss
        var growlClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-zombie-monster-growl-1973.wav");
        if (growlClip)
        {
            var e = GetOrCreateEntry(Sfx.VampireHiss);
            e.clips = new AudioClip[] { growlClip };
        }

        // 4. Enemy Attack -> VampireScreech & VampireAttack
        var screamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-angry-monster-scream-1963.wav");
        if (screamClip)
        {
            var e = GetOrCreateEntry(Sfx.VampireScreech);
            e.clips = new AudioClip[] { screamClip };
        }

        var roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-giant-monster-roar-1972.wav");
        if (roarClip)
        {
            var e = GetOrCreateEntry(Sfx.VampireAttack);
            e.clips = new AudioClip[] { roarClip };
        }

        // 5. Radio Static
        var staticClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-radio-static-fx-2561.wav");
        if (staticClip)
        {
            var rc = Object.FindFirstObjectByType<RadioCentre>();
            if (rc)
            {
                if (rc.staticLoop != null)
                {
                    Undo.RecordObject(rc.staticLoop, "Integrate Radio Static");
                    rc.staticLoop.clip = staticClip;
                }
                else
                {
                    Debug.LogWarning("RadioCentre staticLoop reference is missing.");
                }
            }
        }

        // 6. Defeat
        var defeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-player-losing-or-failing-2042.wav");
        if (defeatClip)
        {
            var e = GetOrCreateEntry(Sfx.Defeat, 1f);
            e.clips = new[] { defeatClip };
        }

        // 7. Victory
        var victoryClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/mixkit-fantasy-game-success-notification-270.wav");
        if (victoryClip)
        {
            var e = GetOrCreateEntry(Sfx.Victory, 1f);
            e.clips = new[] { victoryClip };
        }

        // 8. Menu Music (main menu background horror loop)
        var menuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/final audio.mp3");
        if (menuMusicClip)
        {
            am.menuMusicClip = menuMusicClip;
            Debug.Log("Menu music clip assigned: " + menuMusicClip.name);
        }
        else
        {
            Debug.LogWarning("Menu music clip not found at Assets/Audio/Music/final audio.mp3");
        }

        EditorUtility.SetDirty(am);
        Debug.Log("Mixkit Audio successfully integrated into AudioManager!");
    }
}
