using System;
using UnityEngine;

public enum Sfx
{
    Footstep, FuelPickup, RadioPickup, LocketPickup, Interact, Denied,
    LowFuelWarning, LanternOut, LanternRelight,
    VampireHiss, VampireScreech, VampireAttack,
    PlayerHurt, PlayerDeath,
    PrayerStart, PrayerTick, PrayerEnd,
    RadioBeep, RadioSignal, RadioVoice, Flare,
    Victory, Defeat, UiClick, Heartbeat,
    // Added in the refinement pass. Always append: the values are saved in the scene.
    FootstepDirt, FootstepLeaves, FootstepWood, FootstepStone, CrawlRustle, Jump, Land,
    SwordPickup, SwordSwing, SwordHit, SwordBreak, VampireStagger, VampireDeath, LethalStrike,
    AmbientDistant, UiHover, Opening,
    // Repair-puzzle interface.
    PuzzleOpen, PuzzleClose, PuzzleClick, PuzzleTone, PuzzleSolved, PuzzleFail, PuzzleHint,
    // Boat and water.
    BoatBoard, BoatDock, BoatRow, WaterLap, Seagull
}

[Serializable]
public class SfxEntry
{
    public Sfx id;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.3f)] public float pitchVariance = 0.05f;
}

// Plays every sound in the game. One-shots are called with AudioManager.Play / PlayAt,
// and the ambience (wind, insects, heartbeat, tension drone, prayer choir) reacts to the lantern by itself.
public class AudioManager : MonoBehaviour
{
    static AudioManager instance;

    [Header("Sound Library")]
    public SfxEntry[] sounds;

    [Header("Ambience Loops")]
    public AudioClip windLoop;
    public AudioClip insectsLoop;
    public AudioClip tensionDroneLoop;
    public AudioClip prayerChoirLoop;
    public AudioClip lanternCrackleLoop;
    [Tooltip("Surf. Swells as the player nears the shore and while aboard the boat.")]
    public AudioClip shoreLoop;

    [Header("Menu Music")]
    [Tooltip("Looping horror music played only on the main menu/dashboard.")]
    public AudioClip menuMusicClip;
    [Range(0f, 1f)] public float menuMusicVolume = 0.28f;

    [Header("References")]
    public LanternFuel fuel;
    [Tooltip("Used to judge how close the player is to water.")]
    public Transform player;
    public PlayerHealth health;
    public VampireSpawner vampires;

    [Header("Mixer Groups")]
    public UnityEngine.Audio.AudioMixerGroup masterGroup;
    public UnityEngine.Audio.AudioMixerGroup sfxGroup;
    public UnityEngine.Audio.AudioMixerGroup ambienceGroup;

    [Header("Mix")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float windVolume = 0.35f;
    [Tooltip("Kept low: this is a constant bed, and anything prominent here grates within a minute.")]
    [Range(0f, 1f)] public float insectsVolume = 0.11f;
    [Range(0f, 1f)] public float droneVolume = 0.45f;
    [Range(0f, 1f)] public float choirVolume = 0.55f;
    [Range(0f, 1f)] public float crackleVolume = 0.18f;
    [Range(0f, 1f)] public float shoreVolume = 0.4f;
    [Tooltip("Distance from the waterline at which surf is no longer audible.")]
    public float shoreAudibleDistance = 26f;
    [Range(0f, 1f)] public float heartbeatVolume = 0.7f;

    [Header("Heartbeat (beats per minute)")]
    public float calmBpm = 58f;
    public float lowFuelBpm = 84f;
    public float criticalBpm = 112f;
    public float darkBpm = 132f;
    public float prayerBpm = 66f;

    [Header("3D Sounds")]
    public int pooledSources = 10;
    public float maxHearingDistance = 28f;

    AudioSource uiSource, oneShot2D;
    AudioSource[] pool;
    int poolIndex;
    AudioSource wind, insects, drone, choir, crackle, shore;
    AudioSource menuMusic;
    float menuMusicFadeTarget;
    float nextBeat;
    bool praying, ending;

    void Awake()
    {
        instance = this;
        uiSource = MakeSource("UI", false, sfxGroup);
        uiSource.ignoreListenerPause = true;
        oneShot2D = MakeSource("OneShots", false, sfxGroup);

        pool = new AudioSource[pooledSources];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = MakeSource("3D_" + i, false, sfxGroup);
            pool[i].spatialBlend = 1f;
            pool[i].rolloffMode = AudioRolloffMode.Linear;
            pool[i].minDistance = 2f;
            pool[i].maxDistance = maxHearingDistance;
            pool[i].dopplerLevel = 0f;
        }

        wind = MakeLoop("Wind", windLoop, ambienceGroup);
        insects = MakeLoop("Insects", insectsLoop, ambienceGroup);
        drone = MakeLoop("Drone", tensionDroneLoop, ambienceGroup);
        choir = MakeLoop("Choir", prayerChoirLoop, ambienceGroup);
        crackle = MakeLoop("Crackle", lanternCrackleLoop, ambienceGroup);
        shore = MakeLoop("Shore", shoreLoop, ambienceGroup);

        // Menu music: starts silent; OnStateChanged will bring it up when the menu is shown.
        menuMusic = MakeLoop("MenuMusic", menuMusicClip, ambienceGroup);
        menuMusicFadeTarget = 0f;
    }

    void OnEnable()
    {
        GameEvents.PrayerStarted += OnPrayerStarted;
        GameEvents.PrayerEnded += OnPrayerEnded;
        GameEvents.FuelEmpty += OnFuelEmpty;
        GameEvents.LanternRelit += OnRelit;
        GameEvents.FuelCollected += OnFuelCollected;
        GameEvents.PlayerWon += OnWon;
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.RadioCallStarted += OnCallStarted;
        if (fuel) fuel.OnWarning += OnFuelWarning;
    }

    void OnDisable()
    {
        GameEvents.PrayerStarted -= OnPrayerStarted;
        GameEvents.PrayerEnded -= OnPrayerEnded;
        GameEvents.FuelEmpty -= OnFuelEmpty;
        GameEvents.LanternRelit -= OnRelit;
        GameEvents.FuelCollected -= OnFuelCollected;
        GameEvents.PlayerWon -= OnWon;
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.RadioCallStarted -= OnCallStarted;
        if (fuel) fuel.OnWarning -= OnFuelWarning;
    }

    void Start()
    {
        // GameState.MainMenu is the GameManager's initial field value, so SetState is never
        // called for it and StateChanged never fires. Without this the menu music only ever
        // began after returning to the menu from a run, never on a cold start.
        var gm = GameManager.Instance;
        if (gm == null || gm.State == GameState.MainMenu) StartMenuMusic();
    }

    void StartMenuMusic()
    {
        if (!menuMusic || !menuMusicClip) return;
        menuMusicFadeTarget = menuMusicVolume;
        if (menuMusic.clip != menuMusicClip) menuMusic.clip = menuMusicClip;
        if (!menuMusic.isPlaying) menuMusic.Play();
    }

    AudioSource MakeSource(string name, bool loop, UnityEngine.Audio.AudioMixerGroup group = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        if (group != null) src.outputAudioMixerGroup = group;
        else if (masterGroup != null) src.outputAudioMixerGroup = masterGroup;
        return src;
    }

    AudioSource MakeLoop(string name, AudioClip clip, UnityEngine.Audio.AudioMixerGroup group = null)
    {
        var src = MakeSource(name, true, group);
        src.clip = clip;
        src.volume = 0f;
        if (clip) src.Play();
        return src;
    }

    // ---------------------------------------------------------------- menu music API

    /// <summary>Smoothly fades out the menu music over <paramref name="duration"/> seconds.</summary>
    public static void FadeMenuMusic(float duration = 1.5f)
    {
        if (instance) instance.StartCoroutine(instance.FadeMenuMusicRoutine(duration));
    }

    System.Collections.IEnumerator FadeMenuMusicRoutine(float duration)
    {
        if (!menuMusic) yield break;
        float start = menuMusic.volume;
        menuMusicFadeTarget = 0f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            menuMusic.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        menuMusic.volume = 0f;
        menuMusic.Stop();
    }

    // ---------------------------------------------------------------- public API

    public static void Play(Sfx id, float volume = 1f, float pitch = 1f)
    {
        if (!instance) return;
        var e = instance.Find(id);
        if (e == null) return;
        var src = id == Sfx.UiClick ? instance.uiSource : instance.oneShot2D;
        src.pitch = pitch * (1f + UnityEngine.Random.Range(-e.pitchVariance, e.pitchVariance));
        src.PlayOneShot(e.clips[UnityEngine.Random.Range(0, e.clips.Length)], e.volume * volume * instance.masterVolume);
    }

    public static void PlayAt(Sfx id, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (!instance) return;
        var e = instance.Find(id);
        if (e == null) return;
        var src = instance.pool[instance.poolIndex];
        instance.poolIndex = (instance.poolIndex + 1) % instance.pool.Length;
        src.transform.position = position;
        src.pitch = pitch * (1f + UnityEngine.Random.Range(-e.pitchVariance, e.pitchVariance));
        src.volume = e.volume * volume * instance.masterVolume;
        src.clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        src.Play();
    }

    /// <summary>
    /// Plays one specific clip of a multi-variant entry rather than a random one. The tone
    /// puzzle needs key 3 to always sound like key 3, which Play's random pick cannot give.
    /// </summary>
    public static void PlayVariant(Sfx id, int variant, float volume = 1f, float pitch = 1f)
    {
        if (!instance) return;
        var e = instance.Find(id);
        if (e == null) return;
        var src = instance.oneShot2D;
        src.pitch = pitch;
        src.PlayOneShot(e.clips[Mathf.Clamp(variant, 0, e.clips.Length - 1)],
                        e.volume * volume * instance.masterVolume);
    }

    public static AudioClip GetClip(Sfx id)
    {
        var e = instance ? instance.Find(id) : null;
        return e != null ? e.clips[0] : null;
    }

    SfxEntry Find(Sfx id)
    {
        if (sounds == null) return null;
        foreach (var e in sounds)
            if (e != null && e.id == id && e.clips != null && e.clips.Length > 0) return e;
        return null;
    }

    // ---------------------------------------------------------------- events

    void OnPrayerStarted() { praying = true; Play(Sfx.PrayerStart); }
    void OnPrayerEnded() { praying = false; Play(Sfx.PrayerEnd); }
    void OnFuelEmpty() => Play(Sfx.LanternOut);
    void OnRelit() => Play(Sfx.LanternRelight);
    void OnFuelCollected(float amount) => Play(Sfx.FuelPickup);
    void OnFuelWarning(float level) => Play(Sfx.LowFuelWarning, level < 0.15f ? 1f : 0.7f);
    void OnCallStarted() => Play(Sfx.RadioSignal);
    void OnWon() { ending = true; Play(Sfx.Victory); }

    void OnStateChanged(GameState s)
    {
        if (s == GameState.Defeat) { ending = true; Play(Sfx.Defeat); }

        // Start menu music when the main menu appears; stop it as soon as gameplay begins.
        if (s == GameState.MainMenu)
        {
            ending = false;
            StartMenuMusic();
        }
        else if (s == GameState.Playing || s == GameState.PrayerEmergency)
        {
            menuMusicFadeTarget = 0f;
        }
    }

    // ---------------------------------------------------------------- adaptive ambience

    public bool SuppressAmbience { get; set; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        var gm = GameManager.Instance;
        bool inMenu = gm && gm.State == GameState.MainMenu;
        float fuel01 = fuel ? fuel.Fraction : 1f;
        bool dark = fuel && fuel.Depleted;

        // Insects are loud when you are safe and fall silent as the light fails: silence is tension.
        float insectsTarget = ending || SuppressAmbience ? 0f : insectsVolume * Mathf.Clamp01((fuel01 - 0.2f) / 0.5f);
        float fear = dark ? 1f : Mathf.Clamp01(1f - fuel01 / 0.35f);
        float proximity = vampires ? Mathf.Clamp01(1f - (vampires.NearestVampireDistance - 3f) / 12f) : 0f;
        float droneTarget = ending || inMenu || SuppressAmbience ? 0f : droneVolume * Mathf.Max(fear, proximity * 0.8f);
        if (praying) droneTarget *= 0.2f;

        Fade(wind, SuppressAmbience ? 0f : windVolume * (ending ? 0.5f : 1f), dt, 0.5f);
        Fade(insects, insectsTarget, dt, 0.3f);
        Fade(drone, droneTarget, dt, 0.4f);
        Fade(choir, praying && !SuppressAmbience ? choirVolume : 0f, dt, praying ? 0.6f : 0.25f);
        Fade(crackle, dark || inMenu || ending || SuppressAmbience ? 0f : crackleVolume * (0.4f + 0.6f * fuel01), dt, 1f);
        Fade(shore, ShoreTarget(inMenu), dt, 0.35f);

        // While the menu is up, keep insisting that the music plays. A Play() issued before the
        // clip has finished loading in the background - or, in a browser build, before the audio
        // context has been resumed by the first user gesture - is dropped silently, and the track
        // then never starts at all. Retrying costs nothing once it is running.
        if (menuMusic && inMenu && menuMusicFadeTarget > 0f && !menuMusic.isPlaying) StartMenuMusic();

        // Menu music fades driven by menuMusicFadeTarget (set in OnStateChanged / FadeMenuMusicRoutine).
        if (menuMusic && menuMusic.isPlaying)
            menuMusic.volume = Mathf.MoveTowards(menuMusic.volume, menuMusicFadeTarget * masterVolume, 0.6f * dt);

        UpdateHeartbeat(inMenu, fuel01, dark);
    }

    // Surf rises as you approach the waterline and is loudest aboard the boat, which
    // gives the crossings their own soundscape without a separate music cue.
    float ShoreTarget(bool inMenu)
    {
        if (inMenu || SuppressAmbience || !shoreLoop || !player) return 0f;

        float height = player.position.y - EmberIslands.SeaLevel;
        // Aboard the boat the player sits essentially at the waterline.
        if (height < 0.9f) return shoreVolume;

        // Otherwise fall off with height above the sea, which stands in for distance
        // inland far more cheaply than a nearest-shore search every frame.
        float t = Mathf.Clamp01(1f - height / Mathf.Max(1f, shoreAudibleDistance * 0.22f));
        return shoreVolume * t;
    }

    void UpdateHeartbeat(bool inMenu, float fuel01, bool dark)
    {
        if (inMenu || ending || Time.timeScale == 0f) return;

        float bpm, vol;
        if (praying) { bpm = prayerBpm; vol = 0.25f; }
        else if (dark) { bpm = darkBpm; vol = 1f; }
        else if (fuel01 < 0.1f) { bpm = criticalBpm; vol = 0.85f; }
        else if (fuel01 < 0.3f) { bpm = Mathf.Lerp(criticalBpm, lowFuelBpm, (fuel01 - 0.1f) / 0.2f); vol = 0.6f; }
        else if (fuel01 < 0.6f) { bpm = Mathf.Lerp(lowFuelBpm, calmBpm, (fuel01 - 0.3f) / 0.3f); vol = 0.3f; }
        else { bpm = calmBpm; vol = 0.08f; }

        // Being hurt makes the heart race too.
        if (health) { float hurt = 1f - health.Fraction; bpm += hurt * 30f; vol = Mathf.Max(vol, hurt * 0.9f); }

        if (Time.time < nextBeat) return;
        nextBeat = Time.time + 60f / bpm;
        Play(Sfx.Heartbeat, vol * heartbeatVolume);
    }

    void Fade(AudioSource src, float target, float dt, float speed)
    {
        if (!src || !src.clip) return;
        src.volume = Mathf.MoveTowards(src.volume, target * masterVolume, speed * dt);
    }
}
