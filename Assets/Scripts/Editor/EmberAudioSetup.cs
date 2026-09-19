using UnityEngine;
using UnityEditor;
using UnityEditor.Audio;
using UnityEngine.Audio;
using System.Reflection;

public class EmberAudioSetup
{
    [MenuItem("Tools/Generate EMBER Audio Mixer")]
    public static void GenerateMixer()
    {
        string mixerPath = "Assets/Audio/EmberMixer.mixer";
        
        // Use reflection to call AudioMixerController.CreateMixerController
        var assembly = Assembly.GetAssembly(typeof(AudioMixer));
        var controllerType = assembly.GetType("UnityEditor.Audio.AudioMixerController");
        
        if (controllerType == null)
        {
            Debug.LogError("Could not find AudioMixerController type.");
            return;
        }
        
        var createMethod = controllerType.GetMethod("CreateAudioMixer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (createMethod == null)
        {
            createMethod = controllerType.GetMethod("CreateMixerController", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (createMethod == null)
        {
            Debug.LogError("Could not find Create method for AudioMixer.");
            return;
        }

        // Invoke creation
        bool createdNew = false;
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
        if (mixer == null)
        {
            mixer = createMethod.Invoke(null, new object[] { mixerPath }) as AudioMixer;
            createdNew = true;
        }
        
        if (mixer == null)
        {
            Debug.LogError("Failed to create or load AudioMixer.");
            return;
        }
        
        Debug.Log("Audio Mixer generated successfully at " + mixerPath + ". NOTE: You will need to manually add 'SFX' and 'Ambience' groups as Unity's internal API makes adding groups via script very brittle. After adding them, drag them into the AudioManager script.");
        
        // We will just let the user create the groups manually since Unity's internal graph API for Mixers is heavily protected.
    }
}
