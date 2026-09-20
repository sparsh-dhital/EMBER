using UnityEngine;

// One of the radio components hidden around the level.
// Clues instead of giant markers: a blinking indicator light and a faint beep that gets louder as you approach.
public class RadioPart : MonoBehaviour, IInteractable
{
    public string partName = "Radio part";
    public RadioMissionSystem mission;

    [Header("Repair Puzzle")]
    [Tooltip("Which puzzle guards this part. Each part is assigned a different mechanic.")]
    public PuzzleKind puzzle = PuzzleKind.SignalCalibration;
    [Tooltip("Off only for parts that should be picked up outright (none by default).")]
    public bool requiresPuzzle = true;

    [Header("Clues")]
    public Renderer indicator;
    [ColorUsage(false, true)] public Color indicatorColor = new Color(0.4f, 2.2f, 1.1f);
    public float blinkInterval = 1.3f;
    public float beepInterval = 2.6f;
    [Range(0f, 1f)] public float beepVolume = 0.55f;
    [Tooltip("Beyond this distance the beep can't be heard.")]
    public float hearingDistance = 22f;

    [Header("Feedback")]
    public GameObject visual;
    public ParticleSystem pickupBurst;

    public bool Collected { get; private set; }
    public string Prompt => Collected ? null : (requiresPuzzle ? "Repair " : "Take ") + partName.ToLower();
    public bool CanInteract => !Collected;

    AudioSource beepSource;
    bool busy;   // a board is open for this part
    // Lazily created rather than built in Awake: a domain reload (recompiling while play
    // mode is running) clears non-serialized fields without calling Awake again, which
    // used to leave this null and throw once per renderer per frame.
    MaterialPropertyBlock mpbCache;
    MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();
    float phase;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        phase = Random.value * beepInterval;

        beepSource = gameObject.AddComponent<AudioSource>();
        beepSource.playOnAwake = false;
        beepSource.spatialBlend = 1f;
        beepSource.rolloffMode = AudioRolloffMode.Linear;
        beepSource.minDistance = 1.5f;
        beepSource.maxDistance = hearingDistance;
        beepSource.dopplerLevel = 0f;
    }

    void Update()
    {
        if (Collected) return;
        phase += Time.deltaTime;

        if (indicator)
        {
            float blink = Mathf.Repeat(phase, blinkInterval) < 0.12f ? 1f : 0.04f;
            indicator.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, indicatorColor * blink);
            indicator.SetPropertyBlock(mpb);
        }

        if (phase >= beepInterval && GameManager.Instance && GameManager.Instance.IsGameplayActive)
        {
            phase = 0f;
            var clip = AudioManager.GetClip(Sfx.RadioBeep);
            if (clip) beepSource.PlayOneShot(clip, beepVolume);
        }
    }

    public void Interact(PlayerInteractor player)
    {
        if (Collected || busy) return;

        // A part is salvaged, not picked up: the set has to be coaxed back to life first.
        // Only a solved board awards it, so backing out of the puzzle leaves the part in
        // the world to be attempted again.
        if (requiresPuzzle && PuzzlePanel.Instance != null)
        {
            busy = true;
            AudioManager.Play(Sfx.PuzzleOpen, 0.8f);
            var request = PuzzleRequest.ForPart(puzzle, partName, PuzzleSeed());
            PuzzlePanel.Instance.Open(request, outcome =>
            {
                busy = false;
                if (outcome == PuzzleOutcome.Solved) Award();
                else GameEvents.ShowMessage("REPAIR ABANDONED", partName + " is still on the bench", 2.2f);
            });
            return;
        }

        Award();
    }

    // A stable per-part seed: the same part always poses the same board within a run,
    // but a new run reshuffles them.
    int PuzzleSeed()
    {
        int runSalt = GameManager.Instance ? GameManager.Instance.RunSeed : 0;
        return partName.GetHashCode() ^ (runSalt * 397);
    }

    void Award()
    {
        if (Collected) return;
        Collected = true;

        AudioManager.Play(Sfx.RadioPickup);
        Haptics.Pulse(0.45f, 0.12f);
        if (pickupBurst) pickupBurst.Play();
        if (visual) visual.SetActive(false);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (mission) mission.Collect(this);
    }
}
