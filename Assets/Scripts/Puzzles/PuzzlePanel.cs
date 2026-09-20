using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The frame every radio-part puzzle plays inside.
///
/// It owns everything that is not the puzzle itself: suspending gameplay, the chrome and
/// title, the hint budget, the solve/abandon flow and the audio. A puzzle implementation
/// only has to build a board and raise Solved, which is what keeps five different mechanics
/// from each reinventing this.
///
/// Gameplay is suspended with timeScale 0 and the board runs on unscaled time, so nothing
/// can creep up on the player while they are reading a board they cannot see past.
/// </summary>
public class PuzzlePanel : MonoBehaviour
{
    public static PuzzlePanel Instance { get; private set; }
    /// <summary>True while a puzzle is on screen, so other systems can stand down.</summary>
    public static bool IsOpen => Instance && Instance.open;

    [Header("Wiring")]
    public CanvasGroup group;
    public RectTransform board;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_Text hintText;
    public PuzzleButton hintButton;
    public PuzzleButton closeButton;
    public Image backdrop;

    [Header("Style")]
    public PuzzleStyle style = new PuzzleStyle();

    [Header("Feel")]
    public float fadeSeconds = 0.22f;

    bool open;
    int hintsLeft;
    IPuzzle active;
    GameObject activeHost;
    Action<PuzzleOutcome> onClosed;
    float previousTimeScale = 1f;
    bool cursorWasLocked;

    void Awake()
    {
        Instance = this;
        if (group)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        gameObject.SetActive(true);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Opens a puzzle. <paramref name="onClosed"/> receives Solved only when the board was
    /// actually completed, so callers can gate a reward on it without trusting the panel.
    /// </summary>
    public void Open(PuzzleRequest request, Action<PuzzleOutcome> onClosed)
    {
        if (open) { onClosed?.Invoke(PuzzleOutcome.Abandoned); return; }

        this.onClosed = onClosed;
        open = true;
        hintsLeft = request.hints;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
        InputReader.SetCursorLocked(false);
        if (InputReader.Exists) InputReader.Instance.GameplayLookEnabled = false;

        if (titleText) titleText.text = request.title.ToUpperInvariant();
        if (subtitleText) subtitleText.text = request.subtitle;

        BuildBoard(request);
        RefreshHintUi();

        AudioManager.Play(Sfx.PuzzleOpen);
        StopAllCoroutines();
        StartCoroutine(Fade(1f));
    }

    void BuildBoard(PuzzleRequest request)
    {
        ClearBoard();

        activeHost = new GameObject("Board", typeof(RectTransform));
        var host = (RectTransform)activeHost.transform;
        host.SetParent(board, false);
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = host.offsetMax = Vector2.zero;

        active = CreatePuzzle(request.kind, activeHost);
        if (active == null)
        {
            Debug.LogError("PuzzlePanel: no implementation for " + request.kind);
            Close(PuzzleOutcome.Solved);   // never trap the player behind a missing board
            return;
        }

        active.Solved += HandleSolved;
        active.Build(host, request, style);
    }

    static IPuzzle CreatePuzzle(PuzzleKind kind, GameObject host)
    {
        switch (kind)
        {
            case PuzzleKind.SignalCalibration: return host.AddComponent<SignalCalibrationPuzzle>();
            case PuzzleKind.CircuitRouting: return host.AddComponent<CircuitRoutingPuzzle>();
            case PuzzleKind.ToneSequence: return host.AddComponent<ToneSequencePuzzle>();
            case PuzzleKind.DialAlignment: return host.AddComponent<DialAlignmentPuzzle>();
            case PuzzleKind.ValveLogic: return host.AddComponent<ValveLogicPuzzle>();
            default: return null;
        }
    }

    void HandleSolved()
    {
        if (!open) return;
        AudioManager.Play(Sfx.PuzzleSolved);
        Haptics.Pulse(0.7f, 0.2f);
        StartCoroutine(CloseAfter(0.65f, PuzzleOutcome.Solved));
    }

    IEnumerator CloseAfter(float seconds, PuzzleOutcome outcome)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        Close(outcome);
    }

    /// <summary>Spends a hint, if the difficulty allowed any and the board has one to give.</summary>
    public void UseHint()
    {
        if (!open || active == null || hintsLeft <= 0) return;
        if (!active.RevealHint())
        {
            // Nothing left to reveal: do not charge the player for it.
            if (hintText) hintText.text = "NO HINTS LEFT";
            return;
        }
        hintsLeft--;
        AudioManager.Play(Sfx.PuzzleHint);
        RefreshHintUi();
    }

    void RefreshHintUi()
    {
        bool usable = hintsLeft > 0;
        if (hintText) hintText.text = usable ? "HINT  (" + hintsLeft + ")" : "NO HINTS";
        if (hintButton)
        {
            hintButton.SetInteractable(usable);
            if (hintButton.Label) hintButton.Label.text = usable ? "HINT  " + hintsLeft : "NO HINTS";
        }
    }

    /// <summary>Backs out without solving. The part stays where it is and can be retried.</summary>
    public void Abandon()
    {
        if (!open) return;
        AudioManager.Play(Sfx.PuzzleClose);
        Close(PuzzleOutcome.Abandoned);
    }

    void Close(PuzzleOutcome outcome)
    {
        if (!open) return;
        open = false;

        var callback = onClosed;
        onClosed = null;

        Time.timeScale = previousTimeScale;
        if (InputReader.Exists) InputReader.Instance.GameplayLookEnabled = true;
        if (cursorWasLocked) InputReader.SetCursorLocked(true);

        StopAllCoroutines();
        StartCoroutine(FadeThenClear());

        // Fire last: the callback may open UI of its own.
        callback?.Invoke(outcome);
    }

    IEnumerator FadeThenClear()
    {
        yield return Fade(0f);
        ClearBoard();
    }

    void ClearBoard()
    {
        if (active != null)
        {
            active.Solved -= HandleSolved;
            active.Dispose();
            active = null;
        }
        activeHost = null;

        // Sweep every child rather than just the one host we remember. Destroy is deferred to
        // the end of the frame, so a board opened and closed several times inside a single
        // frame would otherwise leave the outgoing boards parented and stacking up. Detaching
        // first makes the removal take effect immediately.
        if (!board) return;
        for (int i = board.childCount - 1; i >= 0; i--)
        {
            var child = board.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    IEnumerator Fade(float target)
    {
        if (!group) yield break;
        group.blocksRaycasts = target > 0.5f;
        group.interactable = target > 0.5f;

        float start = group.alpha;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeSeconds);
            if (board) board.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, group.alpha);
            yield return null;
        }
        group.alpha = target;
        if (board) board.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, target);
    }

    void Update()
    {
        if (!open) return;
        // Escape backs out of a board rather than opening the pause menu behind it.
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            Abandon();
    }
}
