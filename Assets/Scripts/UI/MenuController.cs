using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Title screen, How To Play, Pause, Victory and Defeat screens. Each screen is a CanvasGroup that fades.
public class MenuController : MonoBehaviour
{
    [Header("Screens")]
    public CanvasGroup titleScreen;
    public CanvasGroup howToPlayScreen;
    public CanvasGroup pauseScreen;
    public CanvasGroup victoryScreen;
    public CanvasGroup defeatScreen;

    [Header("Title")]
    public Button startButton;
    public Button howToPlayButton;
    public Button quitButton;
    public TMP_Text bestText;

    [Header("How To Play")]
    public Button howToPlayBackButton;
    public TMP_Text controlsText;

    [Header("Pause")]
    public Button resumeButton;
    public Button pauseRestartButton;
    public Button pauseMenuButton;

    [Header("Victory")]
    public TMP_Text victoryStats;
    public Button victoryAgainButton;
    public Button victoryMenuButton;

    [Header("Defeat")]
    public TMP_Text defeatCause;
    public TMP_Text defeatStats;
    public Button defeatAgainButton;
    public Button defeatMenuButton;

    CanvasGroup current;
    bool showingHowTo;

    void Awake()
    {
        Hook(startButton, OnStartPressed);
        Hook(howToPlayButton, () => showingHowTo = true);
        Hook(howToPlayBackButton, () => showingHowTo = false);
        Hook(quitButton, () => GameManager.Instance.QuitGame());
        Hook(resumeButton, () => GameManager.Instance.Resume());
        Hook(pauseRestartButton, () => GameManager.Instance.Restart());
        Hook(pauseMenuButton, () => GameManager.Instance.QuitToMenu());
        Hook(victoryAgainButton, () => GameManager.Instance.Restart());
        Hook(victoryMenuButton, () => GameManager.Instance.QuitToMenu());
        Hook(defeatAgainButton, () => GameManager.Instance.Restart());
        Hook(defeatMenuButton, () => GameManager.Instance.QuitToMenu());

#if UNITY_WEBGL
        if (quitButton) quitButton.gameObject.SetActive(false);
#endif
        foreach (var g in new[] { titleScreen, howToPlayScreen, pauseScreen, victoryScreen, defeatScreen })
            if (g) { g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false; }
    }

    void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    bool hasPlayedIntro = false;
    bool playingIntro = false;

    void Start()
    {
        if (controlsText)
        {
            controlsText.text = InputReader.IsTouchDevice
                ? "Left thumb: move  ·  Right side: look  ·  Push the stick fully to run\nJUMP  ·  CRAWL  ·  VIEW switches first / third person  ·  SWORD once you carry one\nTAKE / USE appear near objects  ·  PRAY appears once you hold the locket"
                : "WASD: move  ·  Mouse: look  ·  Shift: run  ·  Space: jump  ·  C: crawl  ·  V: first / third person\nE: interact  ·  Left click / F: sword  ·  P: pray  ·  Esc: pause";
        }

        if (!hasPlayedIntro)
        {
            hasPlayedIntro = true;
            playingIntro = true;
            StartCoroutine(IntroRoutine());
        }
    }

    System.Collections.IEnumerator IntroRoutine()
    {
        var am = Object.FindFirstObjectByType<AudioManager>();
        if (am) am.SuppressAmbience = true;
        
        AudioManager.Play(Sfx.Opening, 1.2f);
        
        // Short dramatic pause for the opening sound
        yield return new WaitForSeconds(4f);
        
        if (am) am.SuppressAmbience = false;
        playingIntro = false;
    }

    void OnStartPressed()
    {
        // Disable the button immediately so it cannot be pressed twice.
        if (startButton) startButton.interactable = false;
        StartCoroutine(StartSequence());
    }

    System.Collections.IEnumerator StartSequence()
    {
        // 1. Fade out menu music over 1.2 s.
        AudioManager.FadeMenuMusic(1.2f);

        // 2. Play the terror sweep ONCE (non-looping one-shot via the normal SFX path).
        AudioManager.Play(Sfx.Opening, 1f);

        // 3. Brief pause so the sweep can land before gameplay ambience kicks in.
        yield return new WaitForSeconds(1.0f);

        // 4. Hand off to gameplay.
        GameManager.Instance.StartGame();
    }

    static void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (!b) return;
        b.onClick.AddListener(() => AudioManager.Play(Sfx.UiClick));
        b.onClick.AddListener(action);
    }

    void OnStateChanged(GameState state)
    {
        showingHowTo = false;
        if (state == GameState.MainMenu && bestText)
        {
            var gm = GameManager.Instance;
            bestText.text = gm.BestScore > 0 ? "BEST SCORE  " + gm.BestScore.ToString("N0") + (gm.BestTime > 0f ? "   ·   FASTEST ESCAPE  " + GameManager.FormatTime(gm.BestTime) : "") : "";
        }
        if (state == GameState.Victory) FillVictory();
        if (state == GameState.Defeat) FillDefeat();
    }

    void FillVictory()
    {
        var r = GameManager.Instance.LastResult;
        if (victoryStats)
            victoryStats.text = "Escaped in " + GameManager.FormatTime(r.time) + "\nScore  " + r.score.ToString("N0") + (r.newBest ? "   ·   NEW BEST" : "");
    }

    void FillDefeat()
    {
        var gm = GameManager.Instance;
        var r = gm.LastResult;
        if (defeatCause) defeatCause.text = gm.DefeatCause;
        if (defeatStats)
            defeatStats.text = "Survived " + GameManager.FormatTime(r.time) + "   ·   Radio parts " + r.parts + "/5\nScore  " + r.score.ToString("N0") + (r.newBest ? "   ·   NEW BEST" : "");
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;

        CanvasGroup wanted = null;
        switch (gm.State)
        {
            case GameState.MainMenu: wanted = showingHowTo ? howToPlayScreen : titleScreen; break;
            case GameState.Paused: wanted = pauseScreen; break;
            case GameState.Victory: wanted = victoryScreen; break;
            case GameState.Defeat: wanted = defeatScreen; break;
        }
        current = wanted;

        float dt = Time.unscaledDeltaTime;
        foreach (var g in new[] { titleScreen, howToPlayScreen, pauseScreen, victoryScreen, defeatScreen })
        {
            if (!g) continue;
            bool on = g == current && !playingIntro;
            float speed = gm.State == GameState.Victory || gm.State == GameState.Defeat ? 0.8f : (playingIntro ? 0f : 4f);
            
            if (playingIntro) 
                g.alpha = 0f;
            else
                g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, dt * (on ? speed : 6f));
                
            g.blocksRaycasts = on;
            g.interactable = on && g.alpha > 0.5f;
        }
    }
}
