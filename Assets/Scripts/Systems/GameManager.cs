using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent game state: survival time, score, high score, and Playing / Paused / GameOver.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Paused, GameOver }

    private const string HighScoreKey = "EMBER_HighScore";

    public static GameManager Instance { get; private set; }

    [Tooltip("Score earned per second survived.")]
    [SerializeField, Min(0f)] private float pointsPerSecond = 10f;
    [Tooltip("Set Time.timeScale to 0 when the player dies so everything stops moving.")]
    [SerializeField] private bool freezeTimeOnGameOver = true;

    private int bonusScore;

    public GameState State { get; private set; } = GameState.Playing;
    public float SurvivalTime { get; private set; }
    public int Score => Mathf.FloorToInt(SurvivalTime * pointsPerSecond) + bonusScore;
    public int HighScore { get; private set; }
    public bool IsNewHighScore { get; private set; }

    /// <summary>Fired whenever State changes. Subscribe in OnEnable and unsubscribe in OnDisable.</summary>
    public event Action<GameState> StateChanged;

    // Domain reload is disabled in this project's Enter Play Mode settings,
    // so statics survive between Play sessions unless they are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (State == GameState.Playing)
            SurvivalTime += Time.deltaTime;
    }

    /// <summary>Adds bonus points on top of the survival-time score (pickups, kills, etc.).</summary>
    public void AddScore(int amount)
    {
        if (State == GameState.Playing)
            bonusScore += amount;
    }

    /// <summary>Call when the player dies (lantern fuel reaches zero). Safe to call more than once.</summary>
    public void OnPlayerDeath()
    {
        if (State == GameState.GameOver)
            return;

        int finalScore = Score;
        IsNewHighScore = finalScore > HighScore;
        if (IsNewHighScore)
        {
            HighScore = finalScore;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }

        if (freezeTimeOnGameOver)
            Time.timeScale = 0f;

        SetState(GameState.GameOver);
    }

    public void Pause()
    {
        if (State != GameState.Playing)
            return;

        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused)
            return;

        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
            Pause();
        else
            Resume();
    }

    /// <summary>Resets the run and reloads the current scene. Hook this up to a Retry button.</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SurvivalTime = 0f;
        bonusScore = 0;
        IsNewHighScore = false;
        SetState(GameState.Playing);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SetState(GameState newState)
    {
        if (State == newState)
            return;

        State = newState;
        StateChanged?.Invoke(newState);
    }
}
