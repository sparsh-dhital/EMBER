using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns the overall flow of a run: menu -> playing -> (prayer emergency) -> victory/defeat.
// Also keeps the run score and the best score saved on this device.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    public PlayerController player;
    public LanternFuel fuel;
    public PlayerHealth health;
    public RadioMissionSystem mission;
    public PrayerSystem prayer;

    [Header("Cameras")]
    public CinemachineCamera menuCamera;
    public CinemachineCamera gameplayCamera;

    [Header("Flow")]
    [Tooltip("Seconds between the killing blow and the defeat screen.")]
    public float defeatScreenDelay = 2.8f;

    [Header("Score")]
    public int pointsPerSecond = 2;
    public int pointsPerRadioPart = 150;
    public int locketBonus = 100;
    public int victoryBonus = 1000;
    [Tooltip("Bonus per percent of fuel left when the rescue call goes out.")]
    public int pointsPerFuelPercent = 4;

    public GameState State { get; private set; } = GameState.MainMenu;
    public float SurvivalTime { get; private set; }

    /// <summary>
    /// Randomises the generated content of a run (currently the repair-puzzle layouts) so a
    /// second playthrough is not a memory test, while keeping every board stable within a run.
    /// </summary>
    public int RunSeed { get; private set; } = 1;
    public bool IsGameplayActive => State == GameState.Playing || State == GameState.PrayerEmergency;
    public RunResult LastResult { get; private set; }
    public string DefeatCause { get; set; } = "The dark took you.";

    public int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
    public float BestTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

    const string BestScoreKey = "ember_best_score";
    const string BestTimeKey = "ember_best_escape_time";

    // Survives a scene reload so "Restart" skips the title screen.
    static bool skipMenuOnLoad;
    GameState stateBeforePause;

    public struct RunResult
    {
        public bool won;
        public float time;
        public int parts;
        public int score;
        public bool newBest;
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void OnEnable()
    {
        GameEvents.FuelEmpty += HandleFuelEmpty;
        GameEvents.LanternRelit += HandleRelit;
        GameEvents.PlayerDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        GameEvents.FuelEmpty -= HandleFuelEmpty;
        GameEvents.LanternRelit -= HandleRelit;
        GameEvents.PlayerDied -= HandlePlayerDied;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (skipMenuOnLoad)
        {
            skipMenuOnLoad = false;
            StartGame();
        }
        else
        {
            EnterMenu();
        }
    }

    void Update()
    {
        Haptics.Tick();
        var input = InputReader.Instance;
        if (input.PausePressed)
        {
            if (IsGameplayActive) Pause();
            else if (State == GameState.Paused) Resume();
        }

        if (IsGameplayActive)
        {
            SurvivalTime += Time.deltaTime;
            if (Cursor.lockState != CursorLockMode.Locked && UnityEngine.InputSystem.Mouse.current != null
                && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame
                && !(UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                InputReader.SetCursorLocked(true);
            }
        }
    }

    // ---------------------------------------------------------------- flow

    void EnterMenu()
    {
        SetCameras(menu: true);
        if (player) player.ControlEnabled = false;
        if (fuel) fuel.Draining = false;
        InputReader.SetCursorLocked(false);
        SetState(GameState.MainMenu);
    }

    public void StartGame()
    {
        SurvivalTime = 0f;
        RunSeed = UnityEngine.Random.Range(1, int.MaxValue);
        SetCameras(menu: false);
        if (player) player.ControlEnabled = true;
        if (fuel) fuel.Draining = true;
        InputReader.SetCursorLocked(true);
        SetState(GameState.Playing);
        GameEvents.ShowMessage("KEEP THE LIGHT ALIVE", "Find the five radio parts", 4f);
    }

    public void Pause()
    {
        if (!IsGameplayActive) return;
        stateBeforePause = State;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        InputReader.SetCursorLocked(false);
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        InputReader.SetCursorLocked(true);
        SetState(stateBeforePause);
    }

    public void Restart()
    {
        skipMenuOnLoad = true;
        ReloadScene();
    }

    public void QuitToMenu()
    {
        skipMenuOnLoad = false;
        ReloadScene();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ReloadScene()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Called by the radio centre once the rescue call and ending shot have finished.
    public void WinGame()
    {
        if (State == GameState.Victory || State == GameState.Defeat) return;
        if (player) player.ControlEnabled = false;
        if (fuel) fuel.Draining = false;
        InputReader.SetCursorLocked(false);
        LastResult = BuildResult(won: true);
        SetState(GameState.Victory);
        GameEvents.RaisePlayerWon();
    }

    // Called when the rescue call succeeds: the ending shot plays out with the player safe and still.
    public void BeginEnding()
    {
        if (fuel) fuel.Draining = false;
        if (player) player.ControlEnabled = false;
    }

    void HandleFuelEmpty()
    {
        if (State == GameState.Playing) SetState(GameState.PrayerEmergency);
    }

    void HandleRelit()
    {
        if (State == GameState.PrayerEmergency) SetState(GameState.Playing);
    }

    void HandlePlayerDied()
    {
        if (State == GameState.Defeat || State == GameState.Victory) return;
        StartCoroutine(DefeatRoutine());
    }

    IEnumerator DefeatRoutine()
    {
        if (player) player.ControlEnabled = false;
        if (fuel) fuel.Draining = false;
        yield return new WaitForSeconds(defeatScreenDelay);
        InputReader.SetCursorLocked(false);
        LastResult = BuildResult(won: false);
        SetState(GameState.Defeat);
    }

    void SetState(GameState next)
    {
        State = next;
        GameEvents.RaiseStateChanged(next);
    }

    void SetCameras(bool menu)
    {
        if (menuCamera) { menuCamera.Priority.Enabled = true; menuCamera.Priority.Value = menu ? 20 : 0; }
        if (gameplayCamera) { gameplayCamera.Priority.Enabled = true; gameplayCamera.Priority.Value = 10; }
    }

    // ---------------------------------------------------------------- score

    RunResult BuildResult(bool won)
    {
        int parts = mission ? mission.Collected : 0;
        int score = Mathf.FloorToInt(SurvivalTime) * pointsPerSecond + parts * pointsPerRadioPart;
        if (prayer && prayer.HasLocket) score += locketBonus;
        if (won)
        {
            score += victoryBonus;
            if (fuel) score += Mathf.RoundToInt(fuel.Fraction * 100f) * pointsPerFuelPercent;
        }

        bool newBest = score > BestScore;
        if (newBest) PlayerPrefs.SetInt(BestScoreKey, score);
        if (won && (BestTime <= 0f || SurvivalTime < BestTime)) PlayerPrefs.SetFloat(BestTimeKey, SurvivalTime);
        PlayerPrefs.Save();

        return new RunResult { won = won, time = SurvivalTime, parts = parts, score = score, newBest = newBest };
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
