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
    AmbientDistant, UiHover
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

    [Header("References")]
    public LanternFuel fuel;
    public PlayerHealth health;
    public VampireSpawner vampires;

    [Header("Mix")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float windVolume = 0.35f;
    [Range(0f, 1f)] public float insectsVolume = 0.3f;
    [Range(0f, 1f)] public float droneVolume = 0.45f;
    [Range(0f, 1f)] public float choirVolume = 0.55f;
    [Range(0f, 1f)] public float crackleVolume = 0.18f;
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
    AudioSource wind, insects, drone, choir, crackle;
    float nextBeat;
    bool praying, ending;

    void Awake()
    {
        instance = this;
        uiSource = MakeSource("UI", false);
        uiSource.ignoreListenerPause = true;
        oneShot2D = MakeSource("OneShots", false);

        pool = new AudioSource[pooledSources];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = MakeSource("3D_" + i, false);
            pool[i].spatialBlend = 1f;
            pool[i].rolloffMode = AudioRolloffMode.Linear;
            pool[i].minDistance = 2f;
            pool[i].maxDistance = maxHearingDistance;
            pool[i].dopplerLevel = 0f;
        }

        wind = MakeLoop("Wind", windLoop);
        insects = MakeLoop("Insects", insectsLoop);
        drone = MakeLoop("Drone", tensionDroneLoop);
        choir = MakeLoop("Choir", prayerChoirLoop);
        crackle = MakeLoop("Crackle", lanternCrackleLoop);
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

    AudioSource MakeSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        return src;
    }

    AudioSource MakeLoop(string name, AudioClip clip)
    {
        var src = MakeSource(name, true);
        src.clip = clip;
        src.volume = 0f;
        if (clip) src.Play();
        return src;
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
    }

    // ---------------------------------------------------------------- adaptive ambience

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        var gm = GameManager.Instance;
        bool inMenu = gm && gm.State == GameState.MainMenu;
        float fuel01 = fuel ? fuel.Fraction : 1f;
        bool dark = fuel && fuel.Depleted;

        // Insects are loud when you are safe and fall silent as the light fails: silence is tension.
        float insectsTarget = ending ? insectsVolume : insectsVolume * Mathf.Clamp01((fuel01 - 0.2f) / 0.5f);
        float fear = dark ? 1f : Mathf.Clamp01(1f - fuel01 / 0.35f);
        float proximity = vampires ? Mathf.Clamp01(1f - (vampires.NearestVampireDistance - 3f) / 12f) : 0f;
        float droneTarget = ending || inMenu ? 0f : droneVolume * Mathf.Max(fear, proximity * 0.8f);
        if (praying) droneTarget *= 0.2f;

        Fade(wind, windVolume * (ending ? 0.5f : 1f), dt, 0.5f);
        Fade(insects, insectsTarget, dt, 0.3f);
        Fade(drone, droneTarget, dt, 0.4f);
        Fade(choir, praying ? choirVolume : 0f, dt, praying ? 0.6f : 0.25f);
        Fade(crackle, dark || inMenu || ending ? 0f : crackleVolume * (0.4f + 0.6f * fuel01), dt, 1f);

        UpdateHeartbeat(inMenu, fuel01, dark);
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
