using System;
using UnityEngine;

/// <summary>
/// The distinct puzzle mechanics. Each radio part is assigned a different one so no two
/// repairs feel the same, and each reads as something you would plausibly do to a broken
/// wartime radio.
/// </summary>
public enum PuzzleKind
{
    /// <summary>Tune two dials until the received waveform matches the reference trace.</summary>
    SignalCalibration,
    /// <summary>Rotate junction tiles until current flows from the cell to the antenna.</summary>
    CircuitRouting,
    /// <summary>Play back a tone sequence heard over the speaker.</summary>
    ToneSequence,
    /// <summary>Rotate concentric dials so their markers line up on the index.</summary>
    DialAlignment,
    /// <summary>Set the valve switches to satisfy the printed logic on the service plate.</summary>
    ValveLogic,
}

/// <summary>How a puzzle attempt ended.</summary>
public enum PuzzleOutcome { Solved, Abandoned }

/// <summary>
/// Everything a puzzle needs to build itself: which mechanic, how hard, and a seed so the
/// same part always poses the same challenge within a run but differs between runs.
/// </summary>
public readonly struct PuzzleRequest
{
    public readonly PuzzleKind kind;
    /// <summary>0 = easiest, 1 = normal, 2 = hardest. Comes from the difficulty profile.</summary>
    public readonly int tier;
    public readonly int hints;
    public readonly int seed;
    public readonly string title;
    public readonly string subtitle;

    public PuzzleRequest(PuzzleKind kind, int tier, int hints, int seed, string title, string subtitle)
    {
        this.kind = kind;
        this.tier = Mathf.Clamp(tier, 0, 2);
        this.hints = Mathf.Max(0, hints);
        this.seed = seed;
        this.title = title;
        this.subtitle = subtitle;
    }

    /// <summary>Builds a request for a radio part from the current difficulty.</summary>
    public static PuzzleRequest ForPart(PuzzleKind kind, string partName, int seed)
    {
        var profile = GameConfig.Current;
        return new PuzzleRequest(kind, profile.puzzleComplexity, profile.puzzleHints, seed,
                                 partName, Describe(kind));
    }

    public static string Describe(PuzzleKind kind)
    {
        switch (kind)
        {
            case PuzzleKind.SignalCalibration: return "Match the carrier to the reference trace";
            case PuzzleKind.CircuitRouting: return "Route power from the cell to the antenna";
            case PuzzleKind.ToneSequence: return "Repeat the sequence the set plays back";
            case PuzzleKind.DialAlignment: return "Align every dial marker to the index";
            case PuzzleKind.ValveLogic: return "Set the valves to satisfy the service plate";
            default: return string.Empty;
        }
    }
}

/// <summary>
/// One playable puzzle. Implementations build their own controls into the host rect and
/// report completion through <see cref="Solved"/>; the panel owns all framing, audio and
/// the pause handling, so a puzzle only has to be a puzzle.
/// </summary>
public interface IPuzzle
{
    /// <summary>Raised the moment the board reaches a solved state.</summary>
    event Action Solved;

    /// <summary>Builds the board. Called once, before the panel fades in.</summary>
    void Build(RectTransform host, PuzzleRequest request, PuzzleStyle style);

    /// <summary>Reveals part of the answer. Called when the player spends a hint.</summary>
    /// <returns>False if there is nothing left to reveal.</returns>
    bool RevealHint();

    /// <summary>Tears the board down. The host's children are destroyed by the panel.</summary>
    void Dispose();
}

/// <summary>
/// The shared visual language for every puzzle board, so all five look like parts of the
/// same machine and match the rest of the HUD. Supplied by the UI builder.
/// </summary>
[Serializable]
public class PuzzleStyle
{
    public TMPro.TMP_FontAsset regular;
    public TMPro.TMP_FontAsset semibold;

    public Sprite panel;        // rounded, sliced
    public Sprite bar;          // rounded, sliced
    public Sprite circle;
    public Sprite ring;
    public Sprite ringThick;
    public Sprite glow;
    public Sprite chevron;

    public Color warm = new Color(1f, 0.72f, 0.36f);
    public Color cream = new Color(0.94f, 0.92f, 0.86f);
    public Color dim = new Color(0.62f, 0.60f, 0.57f);
    public Color good = new Color(0.55f, 0.88f, 0.55f);
    public Color bad = new Color(0.92f, 0.38f, 0.32f);
    public Color inkPanel = new Color(0.06f, 0.055f, 0.05f, 0.94f);
    public Color inkSlot = new Color(1f, 1f, 1f, 0.07f);
}
