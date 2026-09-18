using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Synthesises EMBER's placeholder sound effects and ambience loops as .wav files.
// They are simple but tuned to the mood; replace any of them with real recordings later
// (just drop new clips into the AudioManager slots).
public static class EmberAudioGenerator
{
    const int Rate = 44100;
    public const string SfxDir = "Assets/Audio/SFX";
    public const string LoopDir = "Assets/Audio/Music";
    static System.Random rng = new System.Random(7);

    [MenuItem("EMBER/Build/4. Generate Audio")]
    public static void GenerateAll()
    {
        EmberArt.CreateFolderRecursive(SfxDir);
        EmberArt.CreateFolderRecursive(LoopDir);
        rng = new System.Random(7);

        for (int i = 0; i < 4; i++) Save(SfxDir, "footstep_" + i, Footstep(i));
        Save(SfxDir, "fuel_pickup", FuelPickup());
        Save(SfxDir, "radio_pickup", RadioPickup());
        Save(SfxDir, "locket_pickup", LocketPickup());
        Save(SfxDir, "interact", Click(0.07f, 2200f, 0.6f));
        Save(SfxDir, "ui_click", Click(0.05f, 3000f, 0.35f));
        Save(SfxDir, "denied", Denied());
        Save(SfxDir, "low_fuel_warning", LowFuel());
        Save(SfxDir, "lantern_out", LanternOut());
        Save(SfxDir, "lantern_relight", Relight());
        for (int i = 0; i < 3; i++) Save(SfxDir, "vampire_hiss_" + i, Hiss(i));
        for (int i = 0; i < 2; i++) Save(SfxDir, "vampire_screech_" + i, Screech(i));
        Save(SfxDir, "vampire_attack", Swipe());
        Save(SfxDir, "player_hurt", Hurt());
        Save(SfxDir, "player_death", Death());
        Save(SfxDir, "prayer_start", PrayerStart());
        Save(SfxDir, "prayer_tick", Bell(1046.5f, 0.6f, 0.45f));
        Save(SfxDir, "prayer_end", PrayerEnd());
        Save(SfxDir, "radio_beep", RadioBeep());
        Save(SfxDir, "radio_signal", RadioSignal());
        Save(SfxDir, "radio_voice", RadioVoice());
        Save(SfxDir, "flare", Flare());
        Save(SfxDir, "victory", Victory());
        Save(SfxDir, "defeat", Defeat());
        Save(SfxDir, "heartbeat", Heartbeat());

        Save(LoopDir, "loop_wind", Loop(8f, Wind));
        Save(LoopDir, "loop_insects", Loop(6f, Insects));
        Save(LoopDir, "loop_tension_drone", Loop(10f, Drone));
        Save(LoopDir, "loop_prayer_choir", Loop(8f, Choir));
        Save(LoopDir, "loop_lantern_crackle", Loop(5f, Crackle));
        Save(LoopDir, "loop_radio_static", Loop(4f, Static));

        AssetDatabase.Refresh();
        ConfigureImport();
        Debug.Log("EMBER: audio generated.");
    }

    // ------------------------------------------------------------------ one-shots

    static float[] Footstep(int v)
    {
        int n = S(0.22f);
        var o = new float[n];
        float lp = 0f, hp = 0f, prev = 0f;
        float thudFreq = 70f + v * 12f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            lp += (noise - lp) * 0.08f;
            hp = 0.9f * (hp + noise - prev); prev = noise;
            float crunch = hp * Env(t, 0.002f, 0.05f) * (0.35f + 0.25f * (float)rng.NextDouble());
            float thud = Mathf.Sin(2f * Mathf.PI * thudFreq * t * (1f - t)) * Env(t, 0.003f, 0.06f);
            o[i] = lp * 1.8f * Env(t, 0.002f, 0.08f) + crunch * 0.4f + thud * 0.5f;
        }
        return Norm(o, 0.7f);
    }

    static float[] FuelPickup()
    {
        int n = S(0.9f);
        var o = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += (N() - lp) * (0.04f + 0.03f * Mathf.Sin(t * 50f));
            float slosh = lp * 2.5f * Env(t, 0.01f, 0.18f) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 9f));
            float f = Mathf.Lerp(392f, 587f, Mathf.Clamp01(t / 0.25f));
            float chime = (Mathf.Sin(Ph(f, t)) + 0.35f * Mathf.Sin(Ph(f * 2f, t)) + 0.15f * Mathf.Sin(Ph(f * 3f, t))) * Env(t - 0.05f, 0.02f, 0.4f);
            o[i] = slosh * 0.5f + chime * 0.35f;
        }
        return Norm(o, 0.75f);
    }

    static float[] RadioPickup()
    {
        int n = S(0.55f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float b1 = Square(880f, t) * Gate(t, 0f, 0.08f);
            float b2 = Square(1320f, t) * Gate(t, 0.1f, 0.2f);
            float b3 = Mathf.Sin(Ph(1760f, t)) * Env(t - 0.22f, 0.005f, 0.2f);
            o[i] = (b1 + b2) * 0.25f + b3 * 0.4f + N() * 0.03f * Env(t, 0.001f, 0.03f);
        }
        return Norm(o, 0.6f);
    }

    static float[] LocketPickup()
    {
        int n = S(2.2f);
        var o = new float[n];
        float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
        float[] notes = { 659.3f, 830.6f, 987.8f, 1318.5f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float tk = t - k * 0.09f;
                if (tk < 0f) continue;
                for (int r = 0; r < ratios.Length; r++)
                    s += Mathf.Sin(Ph(notes[k] * ratios[r], tk)) * Mathf.Exp(-tk * (2.2f + r * 2.5f)) / (1f + r * 1.6f);
            }
            o[i] = s * 0.25f;
        }
        return Norm(o, 0.7f);
    }

    static float[] Click(float dur, float freq, float gain)
    {
        int n = S(dur);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            o[i] = (Mathf.Sin(Ph(freq, t)) * 0.6f + N() * 0.4f) * Env(t, 0.0005f, dur * 0.25f);
        }
        return Norm(o, gain);
    }

    static float[] Denied()
    {
        int n = S(0.3f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float g = Gate(t, 0f, 0.1f) + Gate(t, 0.14f, 0.24f);
            o[i] = (Square(140f, t) * 0.5f + Mathf.Sin(Ph(280f, t)) * 0.3f) * g;
        }
        return Norm(Lowpass(o, 0.15f), 0.45f);
    }

    static float[] LowFuel()
    {
        int n = S(1.1f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float a = Mathf.Sin(Ph(659.3f, t)) * Env(t, 0.02f, 0.3f);
            float b = Mathf.Sin(Ph(440f, t)) * Env(t - 0.35f, 0.02f, 0.5f);
            float trem = 0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 6f);
            o[i] = (a + b) * trem * 0.5f;
        }
        return Norm(o, 0.55f);
    }

    static float[] LanternOut()
    {
        int n = S(1.8f);
        var o = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float cutoff = Mathf.Lerp(0.3f, 0.01f, Mathf.Clamp01(t / 0.6f));
            lp += (N() - lp) * cutoff;
            float whoosh = lp * Env(t, 0.03f, 0.5f) * 2f;
            float hiss = N() * 0.15f * Env(t - 0.2f, 0.1f, 0.6f);
            float thump = Mathf.Sin(Ph(60f, t)) * Env(t, 0.005f, 0.15f);
            o[i] = whoosh + hiss + thump * 0.6f;
        }
        return Norm(o, 0.75f);
    }

    static float[] Relight()
    {
        int n = S(0.9f);
        var o = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += (N() - lp) * Mathf.Lerp(0.01f, 0.25f, Mathf.Clamp01(t / 0.3f));
            o[i] = lp * Env(t, 0.15f, 0.35f) * 2f + Mathf.Sin(Ph(90f + t * 60f, t)) * Env(t, 0.05f, 0.3f) * 0.5f;
        }
        return Norm(o, 0.65f);
    }

    static float[] Hiss(int v)
    {
        int n = S(1f + v * 0.15f);
        var o = new float[n];
        float bp1 = 0f, bp1b = 0f, bp2 = 0f, bp2b = 0f;
        float f1 = 2400f + v * 400f, f2 = 4200f - v * 300f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = N();
            Bandpass(ref bp1, ref bp1b, x, f1, 6f);
            Bandpass(ref bp2, ref bp2b, x, f2, 5f);
            float trem = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * (11f + v * 3f));
            float growl = Mathf.Sin(Ph(95f + v * 15f, t) + Mathf.Sin(Ph(47f, t)) * 2f) * 0.15f;
            o[i] = (bp1 * 0.6f + bp2 * 0.5f + growl) * Env(t, 0.06f, 0.45f + v * 0.1f) * trem;
        }
        return Norm(o, 0.8f);
    }

    static float[] Screech(int v)
    {
        int n = S(0.9f);
        var o = new float[n];
        float bp = 0f, bpb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(950f + v * 150f, 520f, Mathf.Clamp01(t / 0.8f)) * (1f + 0.03f * Mathf.Sin(t * 2f * Mathf.PI * 22f));
            float tone = Mathf.Sin(Ph(f, t) + 2.5f * Mathf.Sin(Ph(f * 1.51f, t)));
            float x = N();
            Bandpass(ref bp, ref bpb, x, 3000f, 3f);
            o[i] = (tone * 0.5f + bp * 0.6f) * Env(t, 0.03f, 0.5f);
        }
        return Norm(o, 0.75f);
    }

    static float[] Swipe()
    {
        int n = S(0.45f);
        var o = new float[n];
        float bp = 0f, bpb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = t < 0.12f ? Mathf.Lerp(600f, 3500f, t / 0.12f) : Mathf.Lerp(3500f, 500f, (t - 0.12f) / 0.3f);
            Bandpass(ref bp, ref bpb, N(), f, 2f);
            float thud = Mathf.Sin(Ph(80f, t)) * Env(t - 0.12f, 0.003f, 0.12f);
            o[i] = bp * Env(t, 0.05f, 0.15f) * 1.5f + thud * 0.6f;
        }
        return Norm(o, 0.8f);
    }

    static float[] Hurt()
    {
        int n = S(0.55f);
        var o = new float[n];
        float bp = 0f, bpb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float thump = Mathf.Sin(Ph(Mathf.Lerp(120f, 50f, Mathf.Clamp01(t / 0.2f)), t)) * Env(t, 0.003f, 0.18f);
            Bandpass(ref bp, ref bpb, N(), 650f + 200f * Mathf.Sin(t * 20f), 4f);
            float grunt = bp * Env(t - 0.03f, 0.02f, 0.25f) * 1.5f;
            o[i] = thump * 0.8f + grunt * 0.5f;
        }
        return Norm(o, 0.85f);
    }

    static float[] Death()
    {
        int n = S(3f);
        var o = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += (N() - lp) * 0.02f;
            float boom = Mathf.Sin(Ph(Mathf.Lerp(70f, 35f, Mathf.Clamp01(t / 1.5f)), t)) * Env(t, 0.01f, 1.2f);
            float drone = (Mathf.Sin(Ph(110f * Mathf.Lerp(1f, 0.7f, t / 3f), t)) + Mathf.Sin(Ph(116.5f * Mathf.Lerp(1f, 0.7f, t / 3f), t))) * 0.2f * Env(t - 0.2f, 0.3f, 1.8f);
            o[i] = boom * 0.8f + lp * 2f * Env(t, 0.02f, 0.9f) + drone;
        }
        return Norm(o, 0.85f);
    }

    static float Pad(float[] freqs, float t, float vibrato)
    {
        float s = 0f;
        foreach (float f in freqs)
        {
            float vib = 1f + vibrato * Mathf.Sin(t * 2f * Mathf.PI * 5f + f);
            for (int h = 1; h <= 4; h++) s += Mathf.Sin(Ph(f * h * vib, t)) / (h * h) * 0.6f;
            s += Mathf.Sin(Ph(f * 1.003f * vib, t)) * 0.4f;
        }
        return s / freqs.Length;
    }

    static float[] PrayerStart()
    {
        int n = S(4f);
        var o = new float[n];
        float[] chord = { 261.6f, 329.6f, 392f, 523.3f, 587.3f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float swell = Mathf.SmoothStep(0f, 1f, t / 1.6f) * Mathf.Clamp01((4f - t) / 1.5f);
            float bell = 0f;
            for (int r = 0; r < 3; r++) bell += Mathf.Sin(Ph(1046.5f * (r == 0 ? 1f : r == 1 ? 2.76f : 5.4f), t)) * Mathf.Exp(-t * (1.5f + r * 2f)) / (1 + r);
            o[i] = Pad(chord, t, 0.004f) * swell * 0.8f + bell * 0.3f;
        }
        return Norm(o, 0.75f);
    }

    static float[] PrayerEnd()
    {
        int n = S(2.4f);
        var o = new float[n];
        float[] chord = { 220f, 261.6f, 329.6f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float fall = Mathf.Lerp(1f, 0.94f, t / 2.4f);
            float[] c = { chord[0] * fall, chord[1] * fall, chord[2] * fall };
            o[i] = Pad(c, t, 0.003f) * Env(t, 0.2f, 1.2f);
        }
        return Norm(o, 0.6f);
    }

    static float[] Bell(float f, float dur, float gain)
    {
        int n = S(dur);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            o[i] = (Mathf.Sin(Ph(f, t)) + 0.4f * Mathf.Sin(Ph(f * 2.76f, t)) * Mathf.Exp(-t * 6f)) * Mathf.Exp(-t * 7f);
        }
        return Norm(o, gain);
    }

    static float[] RadioBeep()
    {
        int n = S(0.14f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            o[i] = (Mathf.Sin(Ph(1400f, t)) * 0.7f + Square(1400f, t) * 0.15f) * Gate(t, 0f, 0.1f) * Env(t, 0.003f, 0.2f);
        }
        return Norm(o, 0.6f);
    }

    static float[] RadioSignal()
    {
        int n = S(4f);
        var o = new float[n];
        float bp = 0f, bpb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(300f, 1200f, t / 4f);
            float pulse = Mathf.Repeat(t * 4f, 1f) < 0.5f ? 1f : 0.3f;
            Bandpass(ref bp, ref bpb, N(), 2000f, 1.5f);
            o[i] = Mathf.Sin(Ph(f, t)) * 0.35f * pulse * Env(t, 0.2f, 3f) + bp * 0.3f + (rng.NextDouble() < 0.001 ? N() * 0.8f : 0f);
        }
        return Norm(o, 0.6f);
    }

    static float[] RadioVoice()
    {
        int n = S(1.8f);
        var o = new float[n];
        float a = 0f, ab = 0f, b = 0f, bb = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float syll = Mathf.Max(0f, Mathf.Sin(t * 2f * Mathf.PI * 4.5f + Mathf.Sin(t * 7f) * 2f));
            float buzz = Square(120f + 30f * Mathf.Sin(t * 3f), t) * 0.5f + N() * 0.5f;
            Bandpass(ref a, ref ab, buzz, 700f + 300f * Mathf.Sin(t * 9f), 5f);
            Bandpass(ref b, ref bb, buzz, 1700f + 400f * Mathf.Sin(t * 6f), 6f);
            float stat = N() * 0.12f;
            o[i] = ((a + b * 0.6f) * syll * 1.4f + stat) * Env(t, 0.05f, 1.5f);
        }
        return Norm(o, 0.6f);
    }

    static float[] Flare()
    {
        int n = S(3f);
        var o = new float[n];
        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = N();
            hp = 0.95f * (hp + x - prev); prev = x;
            float pop = Mathf.Sin(Ph(180f, t)) * Env(t, 0.002f, 0.08f) + N() * Env(t, 0.001f, 0.05f);
            float fizz = hp * (0.4f + 0.3f * Mathf.Sin(t * 60f)) * Env(t - 0.05f, 0.1f, 2.2f);
            o[i] = pop * 0.8f + fizz * 0.6f;
        }
        return Norm(o, 0.75f);
    }

    static float[] Victory()
    {
        int n = S(7f);
        var o = new float[n];
        float[][] chords = { new[] { 261.6f, 329.6f, 392f }, new[] { 349.2f, 440f, 523.3f }, new[] { 392f, 493.9f, 587.3f }, new[] { 261.6f, 329.6f, 392f, 523.3f } };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int ci = Mathf.Min(3, (int)(t / 1.6f));
            float local = t - ci * 1.6f;
            float amp = Mathf.SmoothStep(0f, 1f, local / 0.4f) * (ci == 3 ? Mathf.Clamp01((7f - t) / 2.5f) : 1f);
            float bell = Mathf.Sin(Ph(chords[ci][chords[ci].Length - 1] * 2f, local)) * Mathf.Exp(-local * 3f) * 0.25f;
            o[i] = Pad(chords[ci], t, 0.003f) * amp * 0.8f + bell;
        }
        return Norm(o, 0.75f);
    }

    static float[] Defeat()
    {
        int n = S(4.5f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float fall = Mathf.Lerp(1f, 0.85f, t / 4.5f);
            float s = Mathf.Sin(Ph(55f * fall, t)) + Mathf.Sin(Ph(65.4f * fall, t)) * 0.7f + Mathf.Sin(Ph(82.4f * fall * 1.01f, t)) * 0.5f + Mathf.Sin(Ph(87.3f * fall, t)) * 0.3f;
            o[i] = s * Env(t, 0.3f, 2.5f) * 0.4f;
        }
        return Norm(o, 0.8f);
    }

    static float[] Heartbeat()
    {
        int n = S(0.55f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float lub = Mathf.Sin(Ph(Mathf.Lerp(62f, 42f, Mathf.Clamp01(t / 0.12f)), t)) * Env(t, 0.004f, 0.08f);
            float t2 = t - 0.2f;
            float dub = t2 > 0f ? Mathf.Sin(Ph(Mathf.Lerp(58f, 40f, Mathf.Clamp01(t2 / 0.1f)), t2)) * Env(t2, 0.004f, 0.07f) * 0.7f : 0f;
            o[i] = lub + dub;
        }
        return Norm(Lowpass(o, 0.1f), 0.95f);
    }

    // ------------------------------------------------------------------ loops

    static float Wind(float t, int i)
    {
        windLp += (N() - windLp) * 0.012f;
        windLp2 += (windLp - windLp2) * 0.05f;
        float gust = 0.55f + 0.45f * Mathf.PerlinNoise(t * 0.35f, 3.3f);
        return windLp2 * 6f * gust;
    }
    static float windLp, windLp2;

    static float Insects(float t, int i)
    {
        float s = 0f;
        for (int c = 0; c < 3; c++)
        {
            float period = 0.55f + c * 0.17f;
            float local = Mathf.Repeat(t + c * 0.23f, period);
            float trill = local < 0.12f ? Mathf.Max(0f, Mathf.Sin(local * 2f * Mathf.PI * 30f)) : 0f;
            s += Mathf.Sin(Ph(4300f + c * 350f, t)) * trill * (0.6f - c * 0.15f);
        }
        return s * 0.5f + N() * 0.01f;
    }

    static float Drone(float t, int i)
    {
        droneLp += (N() - droneLp) * 0.004f;
        float swell = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI / 5f);
        float beat = Mathf.Sin(Ph(55f, t)) + Mathf.Sin(Ph(58.3f, t)) * 0.8f + Mathf.Sin(Ph(110f * 1.414f, t)) * 0.12f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f));
        return beat * 0.3f * swell + droneLp * 3f;
    }
    static float droneLp;

    static float Choir(float t, int i)
    {
        float[] chord = { 196f, 261.6f, 329.6f, 392f };
        float shimmer = 0.85f + 0.15f * Mathf.Sin(t * 2f * Mathf.PI / 4f);
        return Pad(chord, t, 0.004f) * shimmer;
    }

    static float Crackle(float t, int i)
    {
        crackleLp += (N() - crackleLp) * 0.3f;
        float pop = rng.NextDouble() < 0.0004 ? 1f : 0f;
        crackleEnv = Mathf.Max(crackleEnv * 0.995f, pop * (0.3f + (float)rng.NextDouble() * 0.7f));
        return N() * crackleEnv * 0.8f + crackleLp * 0.04f;
    }
    static float crackleLp, crackleEnv;

    static float Static(float t, int i)
    {
        Bandpass(ref staticBp, ref staticBpb, N(), 1800f, 1.2f);
        float crack = rng.NextDouble() < 0.0008 ? N() * 3f : 0f;
        return staticBp * 0.8f + crack;
    }
    static float staticBp, staticBpb;

    // Generates a seamless loop by crossfading the tail into the head.
    static float[] Loop(float seconds, Func<float, int, float> gen)
    {
        int n = S(seconds), fade = S(0.8f);
        var raw = new float[n + fade];
        for (int i = 0; i < raw.Length; i++) raw[i] = gen(i / (float)Rate, i);
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = raw[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            o[i] = raw[i] * k + raw[n + i] * (1f - k);
        }
        return Norm(o, 0.8f);
    }

    // ------------------------------------------------------------------ DSP helpers

    static int S(float seconds) => Mathf.CeilToInt(seconds * Rate);
    static float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float Ph(float f, float t) => 2f * Mathf.PI * f * t;
    static float Square(float f, float t) => Mathf.Sin(Ph(f, t)) > 0f ? 1f : -1f;
    static float Gate(float t, float start, float end) => t >= start && t < end ? 1f : 0f;
    static float Env(float t, float attack, float decay) => t < 0f ? 0f : t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));

    static void Bandpass(ref float low, ref float band, float x, float freq, float q)
    {
        // Chamberlin state-variable filter.
        float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(freq, Rate / 6f) / Rate);
        low += f * band;
        float high = x - low - band / q;
        band += f * high;
    }

    static float[] Lowpass(float[] s, float k)
    {
        float y = 0f;
        for (int i = 0; i < s.Length; i++) { y += (s[i] - y) * k; s[i] = y; }
        return s;
    }

    static float[] Norm(float[] s, float peak)
    {
        float max = 0.0001f;
        foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
        float g = peak / max;
        for (int i = 0; i < s.Length; i++) s[i] *= g;
        // Short fades so nothing clicks.
        int f = Mathf.Min(64, s.Length / 4);
        for (int i = 0; i < f; i++) { s[i] *= i / (float)f; s[s.Length - 1 - i] *= i / (float)f; }
        return s;
    }

    static void Save(string dir, string name, float[] samples)
    {
        string path = dir + "/" + name + ".wav";
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }

    static void ConfigureImport()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxDir, LoopDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            bool loop = path.StartsWith(LoopDir);
            var s = imp.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = loop ? 0.5f : 0.7f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.loadInBackground = loop;
            imp.SaveAndReimport();
        }
    }
}
