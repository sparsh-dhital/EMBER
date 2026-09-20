using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tune the set until the live trace matches the reference trace.
///
/// Each dial drives one property of a drawn waveform. The player never sees the numbers,
/// only the two traces, so this is read-and-adjust rather than guess-the-value: the live
/// trace converges on the reference as the dials get closer, which makes the feedback
/// continuous and means it cannot be brute-forced blindly.
/// </summary>
public class SignalCalibrationPuzzle : MonoBehaviour, IPuzzle
{
    public event Action Solved;

    const int TraceSamples = 48;

    class Dial
    {
        public string name;
        public int steps;
        public int target;
        public int value;
        public TMP_Text readout;
        public Image fill;
        public bool revealed;
        public bool Matched => value == target;
    }

    Dial[] dials;
    Image[] liveBars;
    Image[] refBars;
    PuzzleStyle style;
    TMP_Text status;
    bool finished;
    float settleTimer = -1f;

    public void Build(RectTransform host, PuzzleRequest request, PuzzleStyle s)
    {
        style = s;
        var rng = new System.Random(request.seed);

        // Harder tiers add a dial and subdivide each one more finely, so the search space
        // grows in both dimensions rather than just getting fiddlier.
        int dialCount = request.tier == 0 ? 2 : request.tier == 1 ? 3 : 3;
        int steps = request.tier == 0 ? 7 : request.tier == 1 ? 11 : 15;

        dials = new Dial[dialCount];
        string[] names = { "CARRIER", "GAIN", "PHASE" };

        // Reference trace across the top.
        PuzzleWidgets.Label("RefLabel", host, s, "REFERENCE", 15f, s.dim,
            new Vector2(-250f, 168f), new Vector2(200f, 20f), TextAlignmentOptions.Left);
        refBars = BuildTrace(host, s, 118f, s.dim);

        PuzzleWidgets.Label("LiveLabel", host, s, "RECEIVED", 15f, s.warm,
            new Vector2(-250f, 62f), new Vector2(200f, 20f), TextAlignmentOptions.Left);
        liveBars = BuildTrace(host, s, 12f, s.warm);

        for (int i = 0; i < dialCount; i++)
        {
            var d = new Dial { name = names[i], steps = steps };
            // Never start on the answer, and keep targets off the extremes so both
            // directions of travel are always meaningful.
            d.target = rng.Next(1, steps - 1);
            do { d.value = rng.Next(0, steps); } while (d.value == d.target);
            dials[i] = d;

            float y = -78f - i * 54f;
            PuzzleWidgets.Label(d.name + "Name", host, s, d.name, 16f, s.cream,
                new Vector2(-250f, y), new Vector2(150f, 22f), TextAlignmentOptions.Left);

            PuzzleWidgets.Sprite(d.name + "Track", host, s.bar, s.inkSlot,
                new Vector2(10f, y), new Vector2(300f, 6f), sliced: true);
            var fill = PuzzleWidgets.Sprite(d.name + "Fill", host, s.bar, s.warm,
                new Vector2(10f, y), new Vector2(300f, 6f), sliced: true);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            d.fill = fill;

            int index = i;
            PuzzleWidgets.IconButton(d.name + "Down", host, s, s.chevron, new Vector2(-150f, y),
                new Vector2(40f, 40f), new Vector2(14f, 14f), 90f, () => Nudge(index, -1));
            PuzzleWidgets.IconButton(d.name + "Up", host, s, s.chevron, new Vector2(172f, y),
                new Vector2(40f, 40f), new Vector2(14f, 14f), -90f, () => Nudge(index, +1));

            d.readout = PuzzleWidgets.Label(d.name + "Read", host, s, "", 15f, s.dim,
                new Vector2(238f, y), new Vector2(80f, 22f), TextAlignmentOptions.Right);
        }

        status = PuzzleWidgets.Label("Status", host, s, "", 17f, s.dim,
            new Vector2(0f, -78f - dialCount * 54f - 14f), new Vector2(560f, 24f));

        RedrawReference();
        Refresh();
    }

    Image[] BuildTrace(RectTransform host, PuzzleStyle s, float y, Color colour)
    {
        var holder = PuzzleWidgets.Rect("Trace", host);
        PuzzleWidgets.Place(holder, new Vector2(0f, y), new Vector2(560f, 86f));
        PuzzleWidgets.Panel("Screen", holder, s, new Color(1f, 1f, 1f, 0.04f), Vector2.zero,
            new Vector2(560f, 86f));

        var bars = new Image[TraceSamples];
        float width = 540f / TraceSamples;
        for (int i = 0; i < TraceSamples; i++)
        {
            float x = -270f + width * (i + 0.5f);
            bars[i] = PuzzleWidgets.Sprite("S" + i, holder, s.bar, colour,
                new Vector2(x, 0f), new Vector2(Mathf.Max(2f, width - 1.5f), 4f), sliced: true);
        }
        return bars;
    }

    void Nudge(int index, int direction)
    {
        if (finished) return;
        var d = dials[index];
        d.value = Mathf.Clamp(d.value + direction, 0, d.steps - 1);
        Refresh();
    }

    // The waveform a set of dial values produces. Deterministic, and every dial changes
    // the shape in a visibly different way so the traces are actually readable.
    float Sample(int i, float carrier, float gain, float phase)
    {
        float u = i / (float)(TraceSamples - 1);
        float w = Mathf.Sin((u * Mathf.PI * 2f * (1f + carrier * 3f)) + phase * Mathf.PI * 2f);
        float envelope = 0.35f + 0.65f * gain;
        return w * envelope;
    }

    float Normalised(Dial d) => d.steps <= 1 ? 0f : d.value / (float)(d.steps - 1);
    float TargetNormalised(Dial d) => d.steps <= 1 ? 0f : d.target / (float)(d.steps - 1);

    void RedrawReference()
    {
        float c = TargetNormalised(dials[0]);
        float g = dials.Length > 1 ? TargetNormalised(dials[1]) : 1f;
        float p = dials.Length > 2 ? TargetNormalised(dials[2]) : 0f;
        DrawTrace(refBars, c, g, p);
    }

    void DrawTrace(Image[] bars, float carrier, float gain, float phase)
    {
        for (int i = 0; i < bars.Length; i++)
        {
            float v = Sample(i, carrier, gain, phase);
            var r = (RectTransform)bars[i].transform;
            float h = Mathf.Max(4f, Mathf.Abs(v) * 38f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, h);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, v * 19f);
        }
    }

    void Refresh()
    {
        float c = Normalised(dials[0]);
        float g = dials.Length > 1 ? Normalised(dials[1]) : 1f;
        float p = dials.Length > 2 ? Normalised(dials[2]) : 0f;
        DrawTrace(liveBars, c, g, p);

        int matched = 0;
        foreach (var d in dials)
        {
            d.fill.fillAmount = d.steps <= 1 ? 0f : d.value / (float)(d.steps - 1);
            d.fill.color = d.Matched ? style.good : style.warm;
            if (d.Matched) matched++;

            // Warmer as you close in: direction without giving away the number.
            int delta = Mathf.Abs(d.value - d.target);
            string hint = d.Matched ? "LOCKED"
                        : delta <= 1 ? "almost"
                        : delta <= 3 ? "close"
                        : "off";
            if (d.revealed && !d.Matched) hint = d.value < d.target ? "raise" : "lower";
            d.readout.text = hint;
            d.readout.color = d.Matched ? style.good : style.dim;
        }

        if (status)
        {
            status.text = matched + " of " + dials.Length + " locked";
            status.color = matched == dials.Length ? style.good : style.dim;
        }

        if (matched == dials.Length && !finished)
        {
            // A short settle so the player sees both traces converge before the panel closes.
            finished = true;
            settleTimer = 0.45f;
        }
    }

    void Update()
    {
        if (settleTimer < 0f) return;
        settleTimer -= Time.unscaledDeltaTime;
        if (settleTimer > 0f) return;
        settleTimer = -1f;
        Solved?.Invoke();
    }

    public bool RevealHint()
    {
        foreach (var d in dials)
        {
            if (d.Matched || d.revealed) continue;
            d.revealed = true;
            Refresh();
            return true;
        }
        return false;
    }

    public void Dispose() { }
}
