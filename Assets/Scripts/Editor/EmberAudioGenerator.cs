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
        // Surface-dependent foley: the same gait over four different grounds.
        for (int i = 0; i < 3; i++) Save(SfxDir, "footstep_dirt_" + i, Surfaced(i, 90f, 0.06f, 0.55f, 0.30f));
        for (int i = 0; i < 3; i++) Save(SfxDir, "footstep_leaves_" + i, Surfaced(i, 150f, 0.02f, 0.18f, 1.00f));
        for (int i = 0; i < 3; i++) Save(SfxDir, "footstep_wood_" + i, Surfaced(i, 190f, 0.16f, 0.85f, 0.22f));
        for (int i = 0; i < 3; i++) Save(SfxDir, "footstep_stone_" + i, Surfaced(i, 240f, 0.09f, 0.45f, 0.50f));
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
        // Sword combat.
        for (int i = 0; i < 3; i++) Save(SfxDir, "sword_swing_" + i, SwordSwing(i));
        for (int i = 0; i < 3; i++) Save(SfxDir, "sword_hit_" + i, SwordHit(i));
        Save(SfxDir, "sword_parry", Parry());
        Save(SfxDir, "sword_pickup", SwordPickup());
        Save(SfxDir, "lethal_strike", LethalStrike());
        for (int i = 0; i < 2; i++) Save(SfxDir, "vampire_stagger_" + i, VampireStagger(i));
        Save(SfxDir, "vampire_death", VampireDeath());
        // Repair-puzzle interface: bakelite switchgear, not glassy modern UI.
        Save(SfxDir, "puzzle_open", PuzzleOpen());
        Save(SfxDir, "puzzle_close", PuzzleClose());
        for (int i = 0; i < 3; i++) Save(SfxDir, "puzzle_click_" + i, PuzzleClick(i));
        for (int i = 0; i < 5; i++) Save(SfxDir, "puzzle_tone_" + i, PuzzleTone(i));
        Save(SfxDir, "puzzle_solved", PuzzleSolved());
        Save(SfxDir, "puzzle_fail", PuzzleFail());
        Save(SfxDir, "puzzle_hint", PuzzleHint());
        // Boat and shoreline.
        Save(SfxDir, "boat_board", BoatBoard());
        Save(SfxDir, "boat_dock", BoatDock());
        for (int i = 0; i < 3; i++) Save(SfxDir, "boat_row_" + i, BoatRow(i));
        for (int i = 0; i < 2; i++) Save(SfxDir, "water_lap_" + i, WaterLap(i));
        Save(SfxDir, "victory", Victory());
        Save(SfxDir, "defeat", Defeat());
        Save(SfxDir, "heartbeat", Heartbeat());

        Save(LoopDir, "loop_wind", Loop(8f, Wind));
        Save(LoopDir, "loop_insects", Loop(6f, Insects));
        Save(LoopDir, "loop_tension_drone", Loop(10f, Drone));
        Save(LoopDir, "loop_prayer_choir", Loop(8f, Choir));
        Save(LoopDir, "loop_lantern_crackle", Loop(5f, Crackle));
        Save(LoopDir, "loop_radio_static", Loop(4f, Static));
        Save(LoopDir, "loop_shore", Loop(9f, Shore));

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

    // ------------------------------------------------------------------ foley: surfaces

    // One footstep over a named surface. The four numbers are what actually separates
    // gravel from a rotten floorboard: body pitch, how much it rings, how long it rings,
    // and how much dry crunch rides on top.
    static float[] Surfaced(int v, float bodyFreq, float resonance, float ring, float crunch)
    {
        int n = S(0.3f);
        var o = new float[n];
        float lp = 0f, hp = 0f, prev = 0f, low = 0f, band = 0f;
        float jitter = 0.9f + 0.2f * (float)rng.NextDouble();
        bodyFreq *= jitter;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();

            // Dry scatter: grit, leaf litter, grains of stone.
            hp = 0.92f * (hp + noise - prev); prev = noise;
            float scatter = hp * Env(t, 0.001f, 0.035f + 0.05f * crunch) * crunch;

            // The body of the step: a short pitched thump that dies away.
            float thump = Mathf.Sin(Ph(bodyFreq, t) * (1f - t * 0.6f)) * Env(t, 0.002f, 0.05f);

            // The surface ringing back: boards boom, stone taps, soil does neither.
            Bandpass(ref low, ref band, noise * 0.5f, bodyFreq * 4.5f, 2.5f + resonance * 14f);
            float tone = band * resonance * Env(t, 0.003f, ring * 0.12f);

            lp += (noise - lp) * 0.07f;
            o[i] = thump * 0.6f + scatter * 0.5f + tone * 1.4f + lp * 0.5f * Env(t, 0.002f, 0.05f);
        }
        return Norm(o, 0.72f);
    }

    // ------------------------------------------------------------------ foley: the sword

    // Air torn by a blade: a noise band that sweeps up as the sword accelerates, then past you.
    static float[] SwordSwing(int v)
    {
        int n = S(0.42f);
        var o = new float[n];
        float low = 0f, band = 0f;
        float peak = 1600f + v * 350f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = Mathf.Clamp01(t / 0.24f);
            // Doppler-ish sweep: rises into the strike, drops away after it.
            float freq = Mathf.Lerp(420f, peak, Mathf.Sin(u * Mathf.PI));
            Bandpass(ref low, ref band, N(), freq, 3.2f);
            float whoosh = band * Mathf.Sin(u * Mathf.PI) * Env(t, 0.03f, 0.13f);
            // A faint metallic edge so it reads as steel, not just wind.
            float edge = Mathf.Sin(Ph(2950f + v * 120f, t)) * Env(t - 0.1f, 0.004f, 0.05f) * 0.12f;
            o[i] = whoosh * 1.9f + edge;
        }
        return Norm(o, 0.68f);
    }

    // Silver into a vampire: a wet cut, a low body impact, and the sear of holy light.
    static float[] SwordHit(int v)
    {
        int n = S(0.55f);
        var o = new float[n];
        float low = 0f, band = 0f, lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();

            // The cut: a fast burst of filtered noise.
            Bandpass(ref low, ref band, noise, 900f + v * 160f, 1.6f);
            float cut = band * Env(t, 0.001f, 0.055f) * 1.8f;

            // The body behind it.
            lp += (noise - lp) * 0.05f;
            float thud = (lp * 1.4f + Mathf.Sin(Ph(78f, t) * (1f - t))) * Env(t, 0.002f, 0.085f);

            // Searing: bright hiss that outlasts the impact, like water on a hot plate.
            hp = 0.95f * (hp + noise - prev); prev = noise;
            float sear = hp * Env(t - 0.02f, 0.01f, 0.2f) * 0.45f;

            o[i] = cut * 0.7f + thud * 0.8f + sear;
        }
        return Norm(o, 0.85f);
    }

    // Parry: two edges clashing. Bright inharmonic partials with a spark of noise.
    static float[] Parry()
    {
        int n = S(0.75f);
        var o = new float[n];
        float hp = 0f, prev = 0f;
        // Deliberately non-integer ratios: metal rings, it does not sing a chord.
        float[] partials = { 1860f, 2790f, 3610f, 5230f, 7040f };
        float[] decays = { 0.28f, 0.2f, 0.14f, 0.09f, 0.05f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float ring = 0f;
            for (int k = 0; k < partials.Length; k++)
                ring += Mathf.Sin(Ph(partials[k], t)) * Env(t, 0.0008f, decays[k]) / (k + 1.6f);

            float noise = N();
            hp = 0.96f * (hp + noise - prev); prev = noise;
            float spark = hp * Env(t, 0.0005f, 0.02f) * 0.8f;

            o[i] = ring * 1.3f + spark;
        }
        return Norm(o, 0.88f);
    }

    // Drawing the sword: the long metallic scrape of steel leaving a scabbard, then a ring.
    static float[] SwordPickup()
    {
        int n = S(1.1f);
        var o = new float[n];
        float low = 0f, band = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float draw = Mathf.Clamp01(t / 0.32f);
            Bandpass(ref low, ref band, N(), Mathf.Lerp(1400f, 5200f, draw), 4f);
            float scrape = band * Env(t, 0.02f, 0.14f) * 1.6f;
            float ring = (Mathf.Sin(Ph(2340f, t)) * 0.6f + Mathf.Sin(Ph(3510f, t)) * 0.3f)
                         * Env(t - 0.3f, 0.005f, 0.42f);
            o[i] = scrape * 0.7f + ring * 0.7f;
        }
        return Norm(o, 0.8f);
    }

    // The killing blow: the cut, then a bloom of holy light swallowing the shriek.
    static float[] LethalStrike()
    {
        int n = S(1.4f);
        var o = new float[n];
        float low = 0f, band = 0f, hp = 0f, prev = 0f;
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();

            Bandpass(ref low, ref band, noise, 700f, 1.4f);
            float cut = band * Env(t, 0.001f, 0.07f) * 2f;

            // Rising choral shimmer: the light taking it.
            float bloom = 0f;
            for (int k = 0; k < chord.Length; k++)
                bloom += Mathf.Sin(Ph(chord[k], t) + Mathf.Sin(t * 5f) * 0.3f);
            bloom *= Env(t - 0.06f, 0.09f, 0.45f) * 0.22f;

            hp = 0.95f * (hp + noise - prev); prev = noise;
            float ash = hp * Env(t - 0.12f, 0.06f, 0.5f) * 0.3f;

            o[i] = cut * 0.6f + bloom + ash;
        }
        return Norm(o, 0.9f);
    }

    // Knocked off balance: a wet, breath-torn grunt, no pitch to speak of.
    static float[] VampireStagger(int v)
    {
        int n = S(0.7f);
        var o = new float[n];
        float low = 0f, band = 0f, lp = 0f;
        float baseF = 118f - v * 16f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            // Growl body, sagging in pitch as the breath goes out of it.
            float f = baseF * (1f - t * 0.35f);
            float growl = Mathf.Sin(Ph(f, t)) + 0.5f * Mathf.Sin(Ph(f * 2.03f, t)) + 0.3f * Mathf.Sin(Ph(f * 3.11f, t));
            growl *= Env(t, 0.012f, 0.16f);

            Bandpass(ref low, ref band, noise, 1150f, 2f);
            float rasp = band * Env(t, 0.02f, 0.2f) * 0.55f;

            lp += (noise - lp) * 0.03f;
            o[i] = growl * 0.7f + rasp + lp * 0.3f * Env(t, 0.03f, 0.22f);
        }
        return Norm(o, 0.82f);
    }

    // Burning to ash: a shriek collapsing into a long dry hiss that simply runs out.
    static float[] VampireDeath()
    {
        int n = S(1.9f);
        var o = new float[n];
        float low = 0f, band = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();

            // Shriek: falls away over the first third of a second.
            float f = Mathf.Lerp(1250f, 210f, Mathf.Clamp01(t / 0.34f));
            float shriek = (Mathf.Sin(Ph(f, t)) + 0.4f * Mathf.Sin(Ph(f * 1.97f, t)))
                           * Env(t, 0.006f, 0.15f);

            // The hiss of a body coming apart, fading to nothing.
            hp = 0.96f * (hp + noise - prev); prev = noise;
            Bandpass(ref low, ref band, hp, Mathf.Lerp(4200f, 900f, Mathf.Clamp01(t / 1.4f)), 1.2f);
            float crumble = band * Env(t - 0.08f, 0.14f, 0.55f) * 0.85f;

            o[i] = shriek * 0.65f + crumble;
        }
        return Norm(o, 0.86f);
    }

    // ------------------------------------------------------------------ repair-puzzle interface

    // A panel swinging open: a low wooden knock under a rising filtered sweep.
    static float[] PuzzleOpen()
    {
        int n = S(0.75f);
        var o = new float[n];
        float low = 0f, band = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float knock = Mathf.Sin(Ph(96f, t) * (1f - t * 0.4f)) * Env(t, 0.002f, 0.07f);
            Bandpass(ref low, ref band, N(), Mathf.Lerp(500f, 2400f, Mathf.Clamp01(t / 0.3f)), 2.4f);
            float sweep = band * Env(t, 0.03f, 0.16f) * 0.8f;
            o[i] = knock * 0.7f + sweep;
        }
        return Norm(o, 0.7f);
    }

    // The same motion reversed: the sweep falls away and the panel settles.
    static float[] PuzzleClose()
    {
        int n = S(0.55f);
        var o = new float[n];
        float low = 0f, band = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            Bandpass(ref low, ref band, N(), Mathf.Lerp(2200f, 420f, Mathf.Clamp01(t / 0.25f)), 2.4f);
            float sweep = band * Env(t, 0.01f, 0.12f) * 0.8f;
            float clunk = Mathf.Sin(Ph(78f, t)) * Env(t - 0.2f, 0.003f, 0.06f);
            o[i] = sweep + clunk * 0.7f;
        }
        return Norm(o, 0.65f);
    }

    // A stiff toggle switch: dry, short, slightly different every press.
    static float[] PuzzleClick(int v)
    {
        int n = S(0.12f);
        var o = new float[n];
        float low = 0f, band = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            hp = 0.9f * (hp + noise - prev); prev = noise;
            Bandpass(ref low, ref band, hp, 1700f + v * 420f, 3.5f);
            float tick = band * Env(t, 0.0004f, 0.012f) * 1.6f;
            float body = Mathf.Sin(Ph(220f + v * 40f, t)) * Env(t, 0.001f, 0.02f) * 0.35f;
            o[i] = tick + body;
        }
        return Norm(o, 0.6f);
    }

    // The call-sign tones: a warm valve-radio sine with a little second harmonic.
    static float[] PuzzleTone(int v)
    {
        int n = S(0.4f);
        var o = new float[n];
        // A pentatonic run, so any order the puzzle plays them in sounds intentional.
        float[] notes = { 392f, 440f, 523.25f, 587.33f, 698.46f };
        float f = notes[Mathf.Clamp(v, 0, notes.Length - 1)];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float tone = Mathf.Sin(Ph(f, t)) + 0.25f * Mathf.Sin(Ph(f * 2f, t)) + 0.08f * Mathf.Sin(Ph(f * 3f, t));
            o[i] = tone * Env(t, 0.008f, 0.13f) * 0.55f + N() * 0.012f * Env(t, 0.005f, 0.08f);
        }
        return Norm(o, 0.72f);
    }

    // The set coming back to life: a rising arpeggio settling into a clean carrier.
    static float[] PuzzleSolved()
    {
        int n = S(1.5f);
        var o = new float[n];
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;
            for (int k = 0; k < chord.Length; k++)
            {
                // Each note enters a beat after the last.
                float onset = k * 0.11f;
                sum += Mathf.Sin(Ph(chord[k], t)) * Env(t - onset, 0.012f, 0.5f);
            }
            // The carrier the repair restores, fading up underneath.
            float carrier = Mathf.Sin(Ph(261.63f, t)) * Env(t - 0.35f, 0.25f, 0.45f) * 0.4f;
            o[i] = sum * 0.3f + carrier;
        }
        return Norm(o, 0.82f);
    }

    // A rejected input: a flat buzz that stops abruptly.
    static float[] PuzzleFail()
    {
        int n = S(0.34f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float gate = Gate(t, 0f, 0.2f);
            float buzz = (Square(104f, t) * 0.5f + Square(157f, t) * 0.3f) * gate;
            o[i] = buzz * Env(t, 0.004f, 0.4f) * 0.7f + N() * 0.05f * gate;
        }
        return Norm(o, 0.62f);
    }

    // A hint: a soft two-note chime, deliberately unobtrusive.
    static float[] PuzzleHint()
    {
        int n = S(0.7f);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float a = Mathf.Sin(Ph(880f, t)) * Env(t, 0.006f, 0.16f);
            float b = Mathf.Sin(Ph(1174.66f, t)) * Env(t - 0.13f, 0.006f, 0.22f);
            o[i] = (a + b) * 0.42f;
        }
        return Norm(o, 0.6f);
    }

    // ------------------------------------------------------------------ boat and water

    // Stepping into a wooden boat: a hollow knock on the hull, then water slapping as she
    // takes the weight and settles.
    static float[] BoatBoard()
    {
        int n = S(1.1f);
        var o = new float[n];
        float low = 0f, band = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            // Hollow wooden thump - a low resonance, not a thud.
            float knock = (Mathf.Sin(Ph(132f, t)) + 0.4f * Mathf.Sin(Ph(198f, t))) * Env(t, 0.002f, 0.09f);
            // Water displaced by the weight.
            Bandpass(ref low, ref band, noise, 900f, 1.1f);
            float slosh = band * Env(t - 0.08f, 0.05f, 0.3f) * 0.8f;
            lp += (noise - lp) * 0.02f;
            o[i] = knock * 0.8f + slosh + lp * 0.25f * Env(t - 0.1f, 0.08f, 0.35f);
        }
        return Norm(o, 0.75f);
    }

    // Grounding on shingle: a scraping rush, then the hull settling still.
    static float[] BoatDock()
    {
        int n = S(1.3f);
        var o = new float[n];
        float low = 0f, band = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            hp = 0.9f * (hp + noise - prev); prev = noise;
            // Gravel dragging under the keel, thinning as she slows.
            Bandpass(ref low, ref band, hp, Mathf.Lerp(2600f, 700f, Mathf.Clamp01(t / 0.5f)), 1.6f);
            float scrape = band * Env(t, 0.02f, 0.28f) * 1.3f;
            float settle = Mathf.Sin(Ph(118f, t)) * Env(t - 0.45f, 0.01f, 0.12f) * 0.5f;
            o[i] = scrape + settle;
        }
        return Norm(o, 0.7f);
    }

    // One oar stroke: the catch, the pull, and the drip as the blade comes clear.
    static float[] BoatRow(int v)
    {
        int n = S(1.0f);
        var o = new float[n];
        float low = 0f, band = 0f;
        float pitch = 1f + v * 0.08f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // Rowlock creak.
            float creak = Mathf.Sin(Ph(310f * pitch * (1f + t * 0.4f), t)) * Env(t, 0.02f, 0.07f) * 0.3f;
            // The blade pulling through water.
            Bandpass(ref low, ref band, N(), 1400f * pitch, 1.3f);
            float pull = band * Env(t - 0.06f, 0.06f, 0.18f) * 1.1f;
            // A drip off the blade at the end of the stroke.
            float drip = Mathf.Sin(Ph(1900f * pitch * (1f - t * 0.3f), t)) * Env(t - 0.42f, 0.002f, 0.05f) * 0.25f;
            o[i] = creak + pull + drip;
        }
        return Norm(o, 0.6f);
    }

    // A single wave turning over on the shore.
    static float[] WaterLap(int v)
    {
        int n = S(1.6f);
        var o = new float[n];
        float low = 0f, band = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = N();
            // Swells in, breaks, then drains back through the shingle.
            float swell = Env(t, 0.35f, 0.5f);
            Bandpass(ref low, ref band, noise, Mathf.Lerp(600f, 2800f, Mathf.Clamp01(t / 0.9f)), 0.9f + v * 0.3f);
            lp += (noise - lp) * 0.03f;
            o[i] = (band * 1.2f + lp * 0.5f) * swell;
        }
        return Norm(o, 0.5f);
    }

    // Shoreline ambience: overlapping swells with no obvious period, so it never
    // announces its loop point.
    static float Shore(float t, int i)
    {
        float v = 0f;
        // Three waves at mutually irrational-ish rates.
        v += Mathf.Sin(t * 0.37f * Mathf.PI * 2f) * 0.5f + 0.5f;
        v *= 0.6f + 0.4f * (Mathf.Sin(t * 0.19f * Mathf.PI * 2f) * 0.5f + 0.5f);
        v *= 0.7f + 0.3f * (Mathf.Sin(t * 0.11f * Mathf.PI * 2f) * 0.5f + 0.5f);
        return N() * 0.42f * v;
    }

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
