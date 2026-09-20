using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The set plays a call sign; repeat it back on the keys.
///
/// Classic call-and-response, themed as a radio handshake. It grows one step at a time so
/// an early mistake costs little, and the whole sequence replays from the start after an
/// error rather than ending the attempt: failure here should cost patience, not progress.
/// </summary>
public class ToneSequencePuzzle : MonoBehaviour, IPuzzle
{
    public event Action Solved;

    static readonly string[] KeyNames = { "A", "B", "C", "D", "E" };

    PuzzleButton[] keys;
    PuzzleStyle style;
    TMP_Text status;
    UnityEngine.UI.Image[] progressDots;

    readonly List<int> sequence = new List<int>();
    int inputIndex;
    int revealUpTo = -1;
    int targetLength;
    int keyCount;
    bool playingBack;
    bool finished;

    public void Build(RectTransform host, PuzzleRequest request, PuzzleStyle s)
    {
        style = s;
        var rng = new System.Random(request.seed);

        // Longer call signs and more keys with difficulty: both the span to remember and
        // the alphabet it is drawn from grow.
        keyCount = request.tier == 0 ? 3 : request.tier == 1 ? 4 : 5;
        targetLength = request.tier == 0 ? 4 : request.tier == 1 ? 6 : 8;

        for (int i = 0; i < targetLength; i++) sequence.Add(rng.Next(0, keyCount));

        PuzzleWidgets.Label("Prompt", host, s, "LISTEN, THEN REPEAT", 16f, s.dim,
            new Vector2(0f, 128f), new Vector2(560f, 22f));

        // Drawn dots rather than filled/hollow circle glyphs, which the UI font lacks.
        progressDots = PuzzleWidgets.DotRow("Progress", host, s, targetLength,
            new Vector2(0f, 78f), 34f, 16f);

        keys = new PuzzleButton[keyCount];
        float spacing = 104f;
        float originX = -(keyCount - 1) * spacing * 0.5f;
        for (int i = 0; i < keyCount; i++)
        {
            int index = i;
            keys[i] = PuzzleWidgets.Button("Key" + i, host, s, KeyNames[i],
                new Vector2(originX + i * spacing, -12f), new Vector2(88f, 88f),
                () => Press(index), 30f);
        }

        PuzzleWidgets.Button("Replay", host, s, "REPLAY", new Vector2(0f, -110f),
            new Vector2(180f, 44f), Replay, 18f);

        status = PuzzleWidgets.Label("Status", host, s, "", 17f, s.dim,
            new Vector2(0f, -162f), new Vector2(560f, 24f));

        StartCoroutine(PlayBack(1.0f));
    }

    void Press(int key)
    {
        if (finished || playingBack) return;

        AudioManager.PlayVariant(Sfx.PuzzleTone, key, 0.9f);
        Flash(key, style.warm);

        if (sequence[inputIndex] == key)
        {
            inputIndex++;
            UpdateProgress();

            if (inputIndex >= sequence.Count)
            {
                finished = true;
                status.text = "CALL SIGN ACCEPTED";
                status.color = style.good;
                Solved?.Invoke();
            }
            return;
        }

        // Wrong key: reset the attempt and replay, keeping the same sequence.
        AudioManager.Play(Sfx.PuzzleFail, 0.9f);
        inputIndex = 0;
        UpdateProgress();
        status.text = "WRONG TONE — LISTEN AGAIN";
        status.color = style.bad;
        StartCoroutine(PlayBack(0.7f));
    }

    void Replay()
    {
        if (finished || playingBack) return;
        inputIndex = 0;
        UpdateProgress();
        StartCoroutine(PlayBack(0.35f));
    }

    IEnumerator PlayBack(float delay)
    {
        playingBack = true;
        SetKeysInteractable(false);
        if (status && status.color != style.bad)
        {
            status.text = "TRANSMITTING…";
            status.color = style.dim;
        }

        yield return WaitUnscaled(delay);

        for (int i = 0; i < sequence.Count; i++)
        {
            int key = sequence[i];
            AudioManager.PlayVariant(Sfx.PuzzleTone, key, 0.85f);
            Flash(key, style.cream);
            yield return WaitUnscaled(0.34f);
            Unflash(key);
            yield return WaitUnscaled(0.12f);
        }

        playingBack = false;
        SetKeysInteractable(true);
        if (status)
        {
            status.text = "YOUR TURN";
            status.color = style.dim;
        }
        UpdateProgress();
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        // The game is paused behind the board, so scaled time would never advance.
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    void Flash(int key, Color colour)
    {
        if (keys == null || key >= keys.Length || keys[key] == null) return;
        keys[key].SetResting(new Color(colour.r, colour.g, colour.b, 0.34f));
        if (keys[key].Label) keys[key].Label.color = colour;
    }

    void Unflash(int key)
    {
        if (keys == null || key >= keys.Length || keys[key] == null) return;
        bool revealed = key <= revealUpTo;
        keys[key].SetResting(style.inkSlot);
        if (keys[key].Label) keys[key].Label.color = revealed ? style.warm : style.cream;
    }

    void SetKeysInteractable(bool value)
    {
        if (keys == null) return;
        foreach (var k in keys) if (k) k.SetInteractable(value);
    }

    void UpdateProgress()
    {
        if (progressDots == null) return;
        // Drawn dots rather than filled/hollow circle glyphs, which the UI font lacks:
        // green for steps already entered, warm for a step a hint revealed, dim for the rest.
        for (int i = 0; i < progressDots.Length && i < sequence.Count; i++)
        {
            if (!progressDots[i]) continue;
            progressDots[i].color = i < inputIndex ? style.good
                                  : i <= revealUpTo ? style.warm
                                  : style.dim;
            progressDots[i].transform.localScale = Vector3.one * (i < inputIndex ? 1f : 0.72f);
        }
    }

    public bool RevealHint()
    {
        // Spell out the next unrevealed step of the call sign.
        if (revealUpTo + 1 >= sequence.Count) return false;
        revealUpTo++;
        UpdateProgress();
        return true;
    }

    public void Dispose() => StopAllCoroutines();
}
