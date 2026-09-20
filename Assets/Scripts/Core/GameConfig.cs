using System;
using UnityEngine;

/// <summary>Which difficulty the player picked. Persisted between runs.</summary>
public enum Difficulty { Easy, Normal, Hard }

/// <summary>
/// Every gameplay number that difficulty is allowed to move, in one place.
/// Systems read these as multipliers over their own designer-tuned defaults, so a
/// designer can retune a script in the Inspector without fighting the difficulty curve.
/// </summary>
[Serializable]
public class DifficultyProfile
{
    public Difficulty level;
    public string displayName;
    public string tagline;

    [Header("Lantern")]
    [Tooltip("Scales LanternFuel.drainPerSecond. Higher means the light dies faster.")]
    public float fuelDrainMultiplier = 1f;
    [Tooltip("Scales how much fuel a can gives back.")]
    public float fuelPickupMultiplier = 1f;

    [Header("Vampires")]
    [Tooltip("Added on top of the spawner's own count curve.")]
    public int extraVampires;
    public float vampireDamageMultiplier = 1f;
    public float vampireSpeedMultiplier = 1f;
    [Tooltip("Scales the vampire's telegraph. Below 1 gives you less time to react.")]
    public float vampireWindUpMultiplier = 1f;

    [Header("Player")]
    public float swordDamageMultiplier = 1f;
    public float playerHealthMultiplier = 1f;

    [Header("Puzzles")]
    [Tooltip("Board size / node count tier for radio-part puzzles. 0 = smallest.")]
    public int puzzleComplexity = 1;
    [Tooltip("Free hints the player may spend per puzzle. 0 = none.")]
    public int puzzleHints = 1;
    [Tooltip("Seconds allowed per puzzle. 0 = untimed.")]
    public float puzzleTimeLimit;
}

/// <summary>
/// The single source of truth for tuning values that would otherwise be duplicated
/// across scripts, plus the selected difficulty and its profile.
///
/// This is a plain static class rather than a MonoBehaviour on purpose: difficulty is
/// chosen in the menu and has to survive the scene reload that starting a run performs,
/// and every system needs to read it without holding a reference to anything.
/// </summary>
public static class GameConfig
{
    // ---------------------------------------------------------------- prayer

    /// <summary>
    /// Seconds the player must wait after a prayer ends before another can be offered.
    /// Requirement: 24 s. Nothing else in the project may hard-code this.
    /// </summary>
    public const float PrayerCooldownSeconds = 24f;

    /// <summary>How long a single prayer holds the vampires back.</summary>
    public const float PrayerDurationSeconds = 24f;

    /// <summary>Seconds of prayer left at which the HUD starts warning the player.</summary>
    public const float PrayerWarningSeconds = 8f;

    // ---------------------------------------------------------------- mission

    /// <summary>Radio parts needed before the extraction call can be made.</summary>
    public const int RadioPartsRequired = 5;

    // ---------------------------------------------------------------- difficulty

    const string DifficultyKey = "ember.difficulty";
    const string DifficultyChosenKey = "ember.difficultyChosen";

    static readonly DifficultyProfile[] Profiles =
    {
        new DifficultyProfile
        {
            level = Difficulty.Easy,
            displayName = "KEEPER",
            tagline = "A steadier flame, and they are slower to grow bold.",
            fuelDrainMultiplier = 0.7f,
            fuelPickupMultiplier = 1.35f,
            extraVampires = -1,
            vampireDamageMultiplier = 0.7f,
            vampireSpeedMultiplier = 0.9f,
            vampireWindUpMultiplier = 1.3f,
            swordDamageMultiplier = 1.25f,
            playerHealthMultiplier = 1.25f,
            puzzleComplexity = 0,
            puzzleHints = 2,
            puzzleTimeLimit = 0f,
        },
        new DifficultyProfile
        {
            level = Difficulty.Normal,
            displayName = "SURVIVOR",
            tagline = "The island as it is. Mind the fuel.",
            fuelDrainMultiplier = 1f,
            fuelPickupMultiplier = 1f,
            extraVampires = 0,
            vampireDamageMultiplier = 1f,
            vampireSpeedMultiplier = 1f,
            vampireWindUpMultiplier = 1f,
            swordDamageMultiplier = 1f,
            playerHealthMultiplier = 1f,
            puzzleComplexity = 1,
            puzzleHints = 1,
            puzzleTimeLimit = 0f,
        },
        new DifficultyProfile
        {
            level = Difficulty.Hard,
            displayName = "PENITENT",
            tagline = "The dark is patient, and it is hungry. No hints.",
            fuelDrainMultiplier = 1.35f,
            fuelPickupMultiplier = 0.75f,
            extraVampires = 2,
            vampireDamageMultiplier = 1.4f,
            vampireSpeedMultiplier = 1.1f,
            vampireWindUpMultiplier = 0.75f,
            swordDamageMultiplier = 0.85f,
            playerHealthMultiplier = 0.85f,
            puzzleComplexity = 2,
            puzzleHints = 0,
            puzzleTimeLimit = 0f,
        },
    };

    static Difficulty? cached;

    /// <summary>Raised when the player picks a different difficulty, so menus can refresh.</summary>
    public static event Action<Difficulty> DifficultyChanged;

    /// <summary>True once the player has made a deliberate choice, rather than defaulting.</summary>
    public static bool DifficultyChosen => PlayerPrefs.GetInt(DifficultyChosenKey, 0) == 1;

    public static Difficulty Selected
    {
        get
        {
            if (cached.HasValue) return cached.Value;
            int stored = PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
            cached = (Difficulty)Mathf.Clamp(stored, 0, Profiles.Length - 1);
            return cached.Value;
        }
        set
        {
            if (cached.HasValue && cached.Value == value) return;
            cached = value;
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.SetInt(DifficultyChosenKey, 1);
            PlayerPrefs.Save();
            DifficultyChanged?.Invoke(value);
        }
    }

    /// <summary>The tuning profile for the current difficulty. Never null.</summary>
    public static DifficultyProfile Current => Profile(Selected);

    public static DifficultyProfile Profile(Difficulty level)
    {
        foreach (var p in Profiles)
            if (p.level == level) return p;
        return Profiles[(int)Difficulty.Normal];
    }

    public static DifficultyProfile[] All => Profiles;

    /// <summary>Steps to the next difficulty, for a single cycling menu button.</summary>
    public static Difficulty Cycle(Difficulty from, int direction = 1)
    {
        int count = Profiles.Length;
        int next = ((int)from + direction % count + count) % count;
        return Profiles[next].level;
    }

#if UNITY_EDITOR
    /// <summary>Lets editor tooling and the playtest harness force a difficulty without touching prefs ordering.</summary>
    public static void EditorOverride(Difficulty level) => cached = level;
#endif
}
