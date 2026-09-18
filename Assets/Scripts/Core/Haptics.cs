using UnityEngine;
using UnityEngine.InputSystem;

// Short vibration feedback on gamepads and Android phones. Does nothing where unsupported.
// GameManager calls Tick() every frame to switch gamepad rumble off again.
public static class Haptics
{
    public static bool Enabled = true;
    static float stopRumbleAt;

    public static void Pulse(float strength, float seconds)
    {
        if (!Enabled) return;

        var pad = Gamepad.current;
        if (pad != null)
        {
            pad.SetMotorSpeeds(strength * 0.6f, strength);
            stopRumbleAt = Time.unscaledTime + seconds;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                vibrator.Call("vibrate", (long)Mathf.Clamp(seconds * 1000f * strength, 15f, 400f));
            }
        }
        catch
        {
            if (strength > 0.7f) Handheld.Vibrate();
        }
#endif
    }

    public static void Tick()
    {
        if (stopRumbleAt <= 0f || Time.unscaledTime < stopRumbleAt) return;
        stopRumbleAt = 0f;
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }
}
