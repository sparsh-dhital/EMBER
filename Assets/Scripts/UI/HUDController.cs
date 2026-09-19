using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The in-game HUD. Deliberately small: fuel (the main metaphor), radio parts, prayer status,
// one line of objective, a subtle signal meter / direction hint, and big moments as centred messages.
public class HUDController : MonoBehaviour
{
    [Header("Systems")]
    public LanternFuel fuel;
    public RadioMissionSystem mission;
    public PrayerSystem prayer;
    public PlayerInteractor interactor;
    public PlayerHealth health;
    public RadioCentre radioCentre;
    public Transform player;

    [Header("Root")]
    public CanvasGroup hudGroup;

    [Header("Fuel")]
    public TMP_Text fuelValue;
    public Image fuelBar;
    public Image fuelIcon;
    public TMP_Text fuelFloater;

    [Header("Mission")]
    public TMP_Text partsText;
    public TMP_Text prayerText;
    public TMP_Text objectiveText;
    public CanvasGroup signalGroup;
    public Image[] signalBars;

    [Header("Direction To Radio")]
    public CanvasGroup directionGroup;
    public RectTransform directionArrow;
    public TMP_Text directionText;

    [Header("Messages")]
    public CanvasGroup messageGroup;
    public TMP_Text messageTitle;
    public TMP_Text messageSubtitle;

    [Header("Prayer Countdown")]
    public CanvasGroup countdownGroup;
    public TMP_Text countdownText;
    public Image countdownRing;

    [Header("Interaction Prompt")]
    public CanvasGroup promptGroup;
    public TMP_Text promptText;
    public GameObject promptKey;

    [Header("Radio Call")]
    public CanvasGroup callGroup;
    public Image callBar;
    public TMP_Text callLabel;
    public TMP_Text subtitleText;

    [Header("Screen Effects")]
    public Image damageFlash;

    [Header("Colours")]
    public Color warm = new Color(0.97f, 0.8f, 0.52f);
    public Color dim = new Color(0.62f, 0.64f, 0.68f);
    public Color danger = new Color(0.96f, 0.36f, 0.24f);
    public Color holy = new Color(1f, 0.92f, 0.7f);
    public Color signal = new Color(0.55f, 0.95f, 0.78f);

    float messageTimer;
    float fuelPunch, partsPunch, floaterTimer, hitFlash;
    string lastObjective;
    float objectiveFade = 1f;

    void OnEnable()
    {
        GameEvents.MessageRequested += ShowMessage;
        GameEvents.FuelCollected += OnFuelCollected;
        GameEvents.FuelEmpty += OnFuelEmpty;
        GameEvents.LanternRelit += OnRelit;
        GameEvents.PlayerDamaged += OnDamaged;
        GameEvents.RadioPartCollected += OnPartCollected;
        GameEvents.PrayerStarted += OnPrayerStarted;
        if (fuel) fuel.OnWarning += OnFuelWarning;
    }

    void OnDisable()
    {
        GameEvents.MessageRequested -= ShowMessage;
        GameEvents.FuelCollected -= OnFuelCollected;
        GameEvents.FuelEmpty -= OnFuelEmpty;
        GameEvents.LanternRelit -= OnRelit;
        GameEvents.PlayerDamaged -= OnDamaged;
        GameEvents.RadioPartCollected -= OnPartCollected;
        GameEvents.PrayerStarted -= OnPrayerStarted;
        if (fuel) fuel.OnWarning -= OnFuelWarning;
    }

    Vector2 floaterBase;

    void Start()
    {
        if (messageGroup) messageGroup.alpha = 0f;
        if (fuelFloater) { fuelFloater.alpha = 0f; floaterBase = fuelFloater.rectTransform.anchoredPosition; }
    }

    // ---------------------------------------------------------------- events

    void ShowMessage(string title, string subtitle, float seconds)
    {
        if (messageTitle) messageTitle.text = title;
        if (messageSubtitle) messageSubtitle.text = subtitle;
        messageTimer = seconds;
    }

    void OnFuelCollected(float amount)
    {
        fuelPunch = 1f;
        floaterTimer = 1.2f;
        if (fuelFloater) fuelFloater.text = "+" + Mathf.RoundToInt(amount);
    }

    void OnFuelEmpty()
    {
        string sub = prayer && prayer.HasLocket && !prayer.Used
            ? (InputReader.IsTouchDevice ? "Tap PRAY" : "Press P or Space to pray")
            : "Find fuel — or run for the radio";
        ShowMessage("THE FLAME IS OUT", sub, 4.5f);
    }

    void OnRelit() => ShowMessage("THE FLAME RETURNS", "", 2f);
    void OnDamaged(float amount) => hitFlash = 1f;
    void OnPartCollected(int collected, int required) => partsPunch = 1f;
    // The cross and the countdown say it all; clear any lingering "press P to pray" message.
    void OnPrayerStarted() => messageTimer = 0f;

    void OnFuelWarning(float level)
    {
        if (level < 0.15f) ShowMessage("THE FLAME IS DYING", "Find fuel now", 3f);
        else ShowMessage("YOUR LANTERN IS FADING", "Find fuel", 3f);
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.IsGameplayActive;
        float dt = Time.unscaledDeltaTime;

        if (hudGroup) hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, playing ? 1f : 0f, dt * 3f);
        if (!playing && hudGroup && hudGroup.alpha <= 0f) return;

        UpdateFuel(dt);
        UpdateMission(dt);
        UpdateObjective(dt);
        UpdateMessage(dt);
        UpdatePrayerCountdown(dt);
        UpdatePrompt(dt);
        UpdateCall(dt);
        UpdateDamage(dt);
    }

    void UpdateFuel(float dt)
    {
        if (!fuel) return;
        float f = fuel.Fraction;
        bool pulse = f < 0.1f && !fuel.Depleted;
        Color c = fuel.Depleted ? danger * 0.8f : f < 0.1f ? danger : f < 0.3f ? Color.Lerp(danger, warm, (f - 0.1f) / 0.2f) : warm;
        if (pulse) c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));

        if (fuelValue)
        {
            fuelValue.text = fuel.Depleted ? "OUT" : Mathf.CeilToInt(f * 100f) + "%";
            fuelValue.color = c;
            fuelPunch = Mathf.MoveTowards(fuelPunch, 0f, dt * 3f);
            fuelValue.rectTransform.localScale = Vector3.one * (1f + 0.3f * EaseOut(fuelPunch));
        }
        if (fuelBar) { fuelBar.fillAmount = Mathf.Lerp(fuelBar.fillAmount, f, 1f - Mathf.Exp(-8f * dt)); fuelBar.color = c; }
        if (fuelIcon) fuelIcon.color = fuel.Depleted ? dim * 0.6f : c;

        if (fuelFloater)
        {
            floaterTimer = Mathf.Max(0f, floaterTimer - dt);
            fuelFloater.alpha = Mathf.Clamp01(floaterTimer / 0.5f);
            fuelFloater.rectTransform.anchoredPosition = floaterBase + Vector2.up * (1.2f - floaterTimer) * 30f;
        }
    }

    void UpdateMission(float dt)
    {
        if (mission && partsText)
        {
            partsText.text = "RADIO PARTS  " + mission.Collected + "/" + mission.Required;
            partsText.color = mission.AllCollected ? signal : warm;
            partsPunch = Mathf.MoveTowards(partsPunch, 0f, dt * 2.5f);
            partsText.rectTransform.localScale = Vector3.one * (1f + 0.2f * EaseOut(partsPunch));
        }

        if (prayer && prayerText)
        {
            if (prayer.IsActive) { prayerText.text = "PRAYER ANSWERED"; prayerText.color = holy; }
            else if (prayer.CanPray)
            {
                prayerText.text = InputReader.IsTouchDevice ? "PRAY" : "PRAY  [P]";
                Color h = holy; h.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                prayerText.color = h;
            }
            else if (prayer.Used) { prayerText.text = "PRAYER SPENT"; prayerText.color = dim * 0.8f; }
            else if (prayer.HasLocket) { prayerText.text = "PRAYER READY"; prayerText.color = holy * 0.9f; }
            else { prayerText.text = "PRAYER LOCKED"; prayerText.color = dim * 0.8f; }
        }

        // Signal meter: a faint "detector" that grows as you near an uncollected part.
        bool searching = mission && !mission.AllCollected && player;
        if (signalGroup) signalGroup.alpha = Mathf.MoveTowards(signalGroup.alpha, searching ? 1f : 0f, dt * 2f);
        if (searching && signalBars != null)
        {
            mission.NearestRemaining(player.position, out float d);
            int lit = d < 7f ? 4 : d < 13f ? 3 : d < 20f ? 2 : d < 30f ? 1 : 0;
            for (int i = 0; i < signalBars.Length; i++)
            {
                if (!signalBars[i]) continue;
                bool on = i < lit;
                Color col = on ? signal : new Color(1f, 1f, 1f, 0.14f);
                if (on && lit == 4) col.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f);
                signalBars[i].color = col;
            }
        }

        // Once every part is found, a small arrow points home.
        bool returning = mission && mission.AllCollected && radioCentre && radioCentre.CurrentPhase == RadioCentre.Phase.Ready && player;
        if (directionGroup) directionGroup.alpha = Mathf.MoveTowards(directionGroup.alpha, returning ? 1f : 0f, dt * 2f);
        if (returning && directionArrow && Camera.main)
        {
            Vector3 toTarget = radioCentre.transform.position - player.position;
            toTarget.y = 0f;
            Vector3 camFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
            float angle = Vector3.SignedAngle(camFwd, toTarget, Vector3.up);
            directionArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (directionText) directionText.text = Mathf.RoundToInt(toTarget.magnitude) + " m";
        }
    }

    void UpdateObjective(float dt)
    {
        if (!objectiveText) return;
        string next = CurrentObjective();
        if (next != lastObjective)
        {
            objectiveFade = Mathf.MoveTowards(objectiveFade, 0f, dt * 4f);
            if (objectiveFade <= 0f) { lastObjective = next; objectiveText.text = next; }
        }
        else objectiveFade = Mathf.MoveTowards(objectiveFade, 1f, dt * 2f);
        objectiveText.alpha = objectiveFade;
    }

    string CurrentObjective()
    {
        if (radioCentre)
        {
            if (radioCentre.CurrentPhase == RadioCentre.Phase.Repairing) return "Repairing the radio…";
            if (radioCentre.CurrentPhase == RadioCentre.Phase.Calling) return "Hold out until help answers";
            if (radioCentre.CurrentPhase == RadioCentre.Phase.Complete) return "";
        }
        bool all = mission && mission.AllCollected;
        if (fuel && fuel.Depleted)
        {
            if (prayer && prayer.CanPray) return InputReader.IsTouchDevice ? "The flame is out — PRAY" : "The flame is out — press P to pray";
            if (prayer && prayer.IsActive) return all ? "RETURN TO THE RADIO CENTRE" : "Find the last parts — quickly";
            return all ? "RUN FOR THE RADIO CENTRE" : "Find fuel — the dark is closing in";
        }
        if (all) return "RETURN TO THE RADIO CENTRE";
        if (fuel && fuel.Fraction < 0.25f) return "Find fuel";
        return "Find the radio parts";
    }

    void UpdateMessage(float dt)
    {
        if (!messageGroup) return;
        messageTimer -= dt;
        float target = messageTimer > 0f ? 1f : 0f;
        messageGroup.alpha = Mathf.MoveTowards(messageGroup.alpha, target, dt * (target > 0f ? 4f : 1.6f));
        float s = 1f + 0.04f * (1f - messageGroup.alpha);
        messageGroup.transform.localScale = new Vector3(s, s, 1f);
    }

    void UpdatePrayerCountdown(float dt)
    {
        if (!countdownGroup || !prayer) return;
        countdownGroup.alpha = Mathf.MoveTowards(countdownGroup.alpha, prayer.IsActive ? 1f : 0f, dt * 2f);
        if (!prayer.IsActive) return;
        if (countdownText)
        {
            countdownText.text = Mathf.CeilToInt(prayer.TimeLeft).ToString();
            countdownText.color = prayer.TimeLeft < prayer.warningAt ? Color.Lerp(danger, holy, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f))) : holy;
        }
        if (countdownRing) countdownRing.fillAmount = prayer.TimeLeft / prayer.duration;
    }

    void UpdatePrompt(float dt)
    {
        if (!promptGroup) return;
        var it = interactor ? interactor.Current : null;
        string text = it != null ? it.Prompt : null;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, text != null ? 1f : 0f, dt * 6f);
        if (text == null) return;
        if (promptText) { promptText.text = text; promptText.color = it.CanInteract ? warm : dim; }
        if (promptKey) promptKey.SetActive(!InputReader.IsTouchDevice && it.CanInteract);
    }

    void UpdateCall(float dt)
    {
        bool calling = radioCentre && (radioCentre.CurrentPhase == RadioCentre.Phase.Repairing || radioCentre.CurrentPhase == RadioCentre.Phase.Calling);
        if (callGroup) callGroup.alpha = Mathf.MoveTowards(callGroup.alpha, calling ? 1f : 0f, dt * 3f);
        if (calling)
        {
            bool repairing = radioCentre.CurrentPhase == RadioCentre.Phase.Repairing;
            if (callBar) { callBar.fillAmount = radioCentre.Progress; callBar.color = radioCentre.PlayerInRange ? signal : danger; }
            if (callLabel)
            {
                callLabel.text = repairing ? "REPAIRING" : radioCentre.PlayerInRange ? "SIGNAL  " + Mathf.RoundToInt(radioCentre.Progress * 100f) + "%" : "STAY NEAR THE RADIO";
                callLabel.color = radioCentre.PlayerInRange ? warm : danger;
            }
        }
        if (subtitleText) subtitleText.text = radioCentre ? radioCentre.Subtitle : "";
    }

    void UpdateDamage(float dt)
    {
        if (!damageFlash) return;
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 1.8f);
        float injury = health ? (1f - health.Fraction) * 0.35f : 0f;
        if (prayer && prayer.IsActive) injury *= 0.25f;
        Color c = damageFlash.color;
        c.a = Mathf.Max(hitFlash * 0.55f, injury);
        damageFlash.color = c;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
