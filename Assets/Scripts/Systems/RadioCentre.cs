using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// The emergency radio at the centre of the map. Dead until every part is found,
// then the player must physically return, repair it, and hold out while the call goes through.
public class RadioCentre : MonoBehaviour, IInteractable
{
    public enum Phase { Inactive, Ready, Repairing, Calling, Complete }

    [Header("References")]
    public RadioMissionSystem mission;
    public VampireSpawner spawner;
    public PlayerHealth playerHealth;
    public Transform radioSet;

    [Header("Radio Feedback")]
    public Renderer statusLight;
    [ColorUsage(false, true)] public Color inactiveColor = new Color(1.2f, 0.05f, 0.03f);
    [ColorUsage(false, true)] public Color readyColor = new Color(2.5f, 1.4f, 0.2f);
    [ColorUsage(false, true)] public Color callingColor = new Color(0.3f, 2.5f, 0.9f);
    public Renderer antennaBeacon;
    [ColorUsage(false, true)] public Color beaconColor = new Color(4f, 0.2f, 0.1f);
    public AudioSource staticLoop;
    public ParticleSystem signalWaves;

    [Header("Call Sequence")]
    public float repairSeconds = 3f;
    public float callSeconds = 20f;
    [Tooltip("The call only progresses while the player stays this close to the radio.")]
    public float callRadius = 8f;

    [Header("Ending")]
    public ParticleSystem flare;
    public Light flareLight;
    public Light moonLight;
    public Color dawnLightColor = new Color(1f, 0.72f, 0.5f);
    public float dawnLightIntensity = 1.1f;
    public Color dawnAmbient = new Color(0.32f, 0.26f, 0.28f);
    public Color dawnFog = new Color(0.42f, 0.33f, 0.33f);
    public float dawnFogDensity = 0.018f;
    public float dawnSeconds = 7f;
    public CinemachineCamera endingCamera;
    public float victoryScreenDelay = 7.5f;

    public Phase CurrentPhase { get; private set; } = Phase.Inactive;
    public float Progress { get; private set; }
    public bool PlayerInRange { get; private set; } = true;
    public string Subtitle { get; private set; } = "";

    public string Prompt
    {
        get
        {
            switch (CurrentPhase)
            {
                case Phase.Inactive:
                    int missing = mission ? mission.Required - mission.Collected : 0;
                    return "Radio is dead — " + missing + (missing == 1 ? " part" : " parts") + " missing";
                case Phase.Ready: return "Repair the radio and call for help";
                default: return null;
            }
        }
    }

    public bool CanInteract => CurrentPhase == Phase.Ready;

    MaterialPropertyBlock mpb;
    PlayerInteractor caller;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake() => mpb = new MaterialPropertyBlock();

    void OnEnable() => GameEvents.AllRadioPartsCollected += HandleAllParts;
    void OnDisable() => GameEvents.AllRadioPartsCollected -= HandleAllParts;

    void HandleAllParts()
    {
        if (CurrentPhase != Phase.Inactive) return;
        CurrentPhase = Phase.Ready;
        if (staticLoop) { staticLoop.volume = 0.12f; staticLoop.Play(); }
    }

    public void Interact(PlayerInteractor player)
    {
        if (CurrentPhase == Phase.Inactive)
        {
            GameEvents.ShowMessage("THE RADIO IS DEAD", "Find the missing parts", 2.5f);
            return;
        }
        if (CurrentPhase != Phase.Ready) return;
        caller = player;
        StartCoroutine(CallSequence());
    }

    IEnumerator CallSequence()
    {
        var controller = caller.Controller;
        var anim = caller.Animator;

        // 1. Repair: the player kneels at the radio and fits the parts.
        CurrentPhase = Phase.Repairing;
        if (controller) { controller.LockMovement(repairSeconds); controller.FaceTowards(radioSet ? radioSet.position : transform.position); }
        if (anim) anim.SetWorking(true);
        GameEvents.ShowMessage("REPAIRING THE RADIO", "", repairSeconds);
        for (float t = 0f; t < repairSeconds; t += Time.deltaTime)
        {
            Progress = t / repairSeconds;
            yield return null;
        }
        if (anim) anim.SetWorking(false);

        // 2. The call: static, a building signal, and every vampire in the woods drawn to it.
        CurrentPhase = Phase.Calling;
        Progress = 0f;
        GameEvents.RaiseRadioCallStarted();
        GameEvents.ShowMessage("MAYDAY", "Stay near the radio until help answers", 3.5f);
        if (spawner) spawner.StartFinalWave();
        if (signalWaves) signalWaves.Play();

        string[] lines =
        {
            "\"…this is… anyone receiving…?\"",
            "\"…we hear you… where are you…?\"",
            "\"…got your signal… fire a flare… hold on…\""
        };
        int lineIndex = 0;

        while (Progress < 1f)
        {
            Vector3 flat = caller.transform.position - transform.position;
            flat.y = 0f;
            PlayerInRange = flat.magnitude <= callRadius;
            bool alive = !playerHealth || !playerHealth.IsDead;
            if (!alive) yield break;

            if (PlayerInRange) Progress = Mathf.Min(1f, Progress + Time.deltaTime / callSeconds);
            if (staticLoop) staticLoop.volume = Mathf.Lerp(0.15f, 0.55f, Progress) * (PlayerInRange ? 1f : 0.5f);

            if (lineIndex < lines.Length && Progress >= (lineIndex + 1) * 0.28f)
            {
                Subtitle = lines[lineIndex++];
                AudioManager.Play(Sfx.RadioVoice, 0.8f);
            }
            yield return null;
        }

        // 3. Rescue: the flare goes up, the dawn breaks, the vampires retreat.
        CurrentPhase = Phase.Complete;
        Subtitle = "\"…we see your flare! We're coming!\"";
        if (playerHealth) playerHealth.AddProtection();
        if (GameManager.Instance) GameManager.Instance.BeginEnding();
        GameEvents.RaiseMissionCompleted();
        if (spawner) spawner.BanishAll();
        if (signalWaves) signalWaves.Stop();

        if (flare) flare.Play();
        AudioManager.Play(Sfx.Flare);
        if (flareLight) StartCoroutine(FlareLightRoutine());
        GameEvents.ShowMessage("HELP IS COMING", "Dawn is breaking", 4f);

        if (endingCamera) { endingCamera.Priority.Enabled = true; endingCamera.Priority.Value = 40; }
        StartCoroutine(DawnRoutine());

        yield return new WaitForSeconds(victoryScreenDelay);
        Subtitle = "";
        if (GameManager.Instance) GameManager.Instance.WinGame();
    }

    IEnumerator FlareLightRoutine()
    {
        flareLight.enabled = true;
        float peak = flareLight.intensity;
        for (float t = 0f; t < 6f; t += Time.deltaTime)
        {
            float rise = Mathf.Clamp01(t / 0.6f);
            float fall = 1f - Mathf.Clamp01((t - 2.5f) / 3.5f);
            flareLight.intensity = peak * rise * fall * (0.85f + 0.15f * Mathf.PerlinNoise(t * 12f, 0f));
            flareLight.transform.position = transform.position + Vector3.up * Mathf.Lerp(3f, 26f, Mathf.Sqrt(Mathf.Clamp01(t / 2.2f)));
            yield return null;
        }
        flareLight.enabled = false;
    }

    IEnumerator DawnRoutine()
    {
        Color startLight = moonLight ? moonLight.color : Color.white;
        float startIntensity = moonLight ? moonLight.intensity : 0f;
        Quaternion startRot = moonLight ? moonLight.transform.rotation : Quaternion.identity;
        Quaternion dawnRot = Quaternion.Euler(12f, 70f, 0f);
        Color startAmbient = RenderSettings.ambientLight;
        Color startFog = RenderSettings.fogColor;
        float startDensity = RenderSettings.fogDensity;
        var cam = Camera.main;
        Color startBg = cam ? cam.backgroundColor : Color.black;

        for (float t = 0f; t < dawnSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dawnSeconds);
            if (moonLight)
            {
                moonLight.color = Color.Lerp(startLight, dawnLightColor, k);
                moonLight.intensity = Mathf.Lerp(startIntensity, dawnLightIntensity, k);
                moonLight.transform.rotation = Quaternion.Slerp(startRot, dawnRot, k);
            }
            RenderSettings.ambientLight = Color.Lerp(startAmbient, dawnAmbient, k);
            RenderSettings.fogColor = Color.Lerp(startFog, dawnFog, k);
            RenderSettings.fogDensity = Mathf.Lerp(startDensity, dawnFogDensity, k);
            if (cam) cam.backgroundColor = Color.Lerp(startBg, dawnFog, k);
            yield return null;
        }
    }

    void Update()
    {
        if (statusLight)
        {
            Color c = CurrentPhase == Phase.Inactive ? inactiveColor * (0.4f + 0.6f * Pulse(0.8f))
                    : CurrentPhase == Phase.Ready ? readyColor * (0.5f + 0.5f * Pulse(2.2f))
                    : callingColor * (0.6f + 0.4f * Pulse(6f));
            SetEmission(statusLight, c);
        }
        if (antennaBeacon)
        {
            float rate = CurrentPhase >= Phase.Calling ? 0.45f : 1.6f;
            SetEmission(antennaBeacon, beaconColor * (Mathf.Repeat(Time.time, rate) < 0.18f ? 1f : 0.05f));
        }
    }

    static float Pulse(float speed) => 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI);

    void SetEmission(Renderer r, Color c)
    {
        r.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionId, c);
        r.SetPropertyBlock(mpb);
    }
}
