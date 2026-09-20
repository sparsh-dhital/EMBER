using System.Collections;
using UnityEngine;

// The one-time emergency prayer.
// Needs the locket, only works once the flame is out, and protects you for about a minute.
// It does NOT refill the lantern: it only buys time to reach the radio.
public class PrayerSystem : MonoBehaviour
{
    [Header("References")]
    public LanternFuel fuel;
    public PlayerHealth health;
    public PlayerController controller;
    public PlayerAnimator playerAnimator;

    [Header("Holy Visuals")]
    [Tooltip("Root of the luminous Om that appears above the player.")]
    public Transform crossRoot;
    public Renderer[] crossRenderers;
    [Tooltip("Emission driven onto the Om plate. Orange flame, not white holy light.")]
    [ColorUsage(false, true)] public Color crossGlow = new Color(6.5f, 2.6f, 0.7f);
    public Light holyLight;
    public float holyLightIntensity = 16f;
    public float holyLightRange = 17f;
    [Tooltip("Height of the cross above the player's feet.")]
    public float crossHeight = 2.55f;
    public ParticleSystem risingMotes;
    public ParticleSystem activationRing;

    [Header("Tuning")]
    [Tooltip("Seconds the player kneels (cannot move) when the prayer begins.")]
    public float kneelSeconds = 2.4f;
    [Tooltip("Vampires inside this distance flee in terror.")]
    public float fearRadius = 18f;
    public float fadeInSeconds = 1f;
    public float fadeOutSeconds = 1.8f;

    // Duration, cooldown and the warning threshold all live in GameConfig so there is exactly
    // one place to change them. Prayer is repeatable: it holds the dark back for a while, then
    // has to recharge, which makes it a tool to spend rather than a single get-out-of-jail card.
    float Duration => GameConfig.PrayerDurationSeconds;
    float Cooldown => GameConfig.PrayerCooldownSeconds;
    float WarningAt => GameConfig.PrayerWarningSeconds;

    public bool HasLocket { get; private set; }
    /// <summary>True once at least one prayer has been offered. Kept for scoring and the HUD.</summary>
    public bool Used { get; private set; }
    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }
    public int TimesPrayed { get; private set; }

    /// <summary>Seconds until prayer is available again. Zero when it is ready.</summary>
    public float CooldownLeft => Mathf.Max(0f, cooldownUntil - Time.time);
    public bool OnCooldown => CooldownLeft > 0f;
    /// <summary>0..1 recharge progress, for the HUD ring.</summary>
    public float CooldownFraction => Cooldown <= 0f ? 1f : 1f - CooldownLeft / Cooldown;

    public bool CanPray => HasLocket && !IsActive && !OnCooldown && fuel && fuel.Depleted
                           && (GameManager.Instance == null || GameManager.Instance.IsGameplayActive)
                           && (!health || !health.IsDead);
    public float FearRadius => IsActive ? fearRadius * strength : 0f;

    float strength;
    float cooldownUntil;
    int lastTickSecond = -1;
    // Lazily created rather than built in Awake: a domain reload (recompiling while play
    // mode is running) clears non-serialized fields without calling Awake again, which
    // used to leave this null and throw once per renderer per frame.
    MaterialPropertyBlock mpbCache;
    MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        SetVisuals(0f);
    }

    public void Unlock()
    {
        if (HasLocket) return;
        HasLocket = true;
        GameEvents.RaiseLocketCollected();
        GameEvents.ShowMessage("THE SACRED LOCKET", "When the flame dies, pray", 4f);
    }

    void Update()
    {
        if (InputReader.Instance.PrayPressed) TryPray();

        // The Om only exists while a prayer is being answered. It is hidden in Awake, but
        // enforce it every frame as well: the symbol must never be visible before the locket
        // is found and the flame has died, whatever state the scene was authored in.
        if (crossRoot && strength <= 0f && crossRoot.gameObject.activeSelf)
            crossRoot.gameObject.SetActive(false);

        if (crossRoot && strength > 0f)
        {
            // The cross always faces the camera so its shape reads clearly, and floats gently.
            var cam = Camera.main;
            if (cam)
            {
                Vector3 toCam = cam.transform.position - crossRoot.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f) crossRoot.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
            crossRoot.localPosition = new Vector3(0f, crossHeight + Mathf.Sin(Time.time * 1.3f) * 0.06f, 0f);
        }
    }

    public void TryPray()
    {
        if (GameManager.Instance && !GameManager.Instance.IsGameplayActive) return;
        if (CanPray) { StartCoroutine(PrayerRoutine()); return; }
        if (IsActive) return;

        if (!HasLocket) GameEvents.ShowMessage("PRAYER LOCKED", "Find the sacred locket", 2.5f);
        else if (OnCooldown) GameEvents.ShowMessage("THE OM IS STILL COOLING",
            Mathf.CeilToInt(CooldownLeft) + "s until you may pray again", 2.5f);
        else GameEvents.ShowMessage("NOT YET", "Prayer answers only when the flame is out", 2.5f);
        AudioManager.Play(Sfx.Denied);
    }

    IEnumerator PrayerRoutine()
    {
        Used = true;
        TimesPrayed++;
        IsActive = true;
        TimeLeft = Duration;
        lastTickSecond = -1;
        if (health) health.AddProtection();

        if (controller) controller.LockMovement(kneelSeconds);
        if (playerAnimator) playerAnimator.SetPraying(true);
        if (risingMotes) risingMotes.Play();
        if (activationRing) activationRing.Play();
        CameraController.PushIn(kneelSeconds + 1.5f);
        Haptics.Pulse(0.6f, 0.4f);
        GameEvents.RaisePrayerStarted();

        float t = 0f;
        while (TimeLeft > 0f)
        {
            float dt = Time.deltaTime;
            t += dt;
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);

            if (playerAnimator && t >= kneelSeconds) playerAnimator.SetPraying(false);

            float fadeIn = Mathf.SmoothStep(0f, 1f, t / fadeInSeconds);
            float fadeOut = Mathf.Clamp01(TimeLeft / fadeOutSeconds);
            // In the last seconds the holy light starts to waver, warning that protection is ending.
            float waver = TimeLeft < WarningAt ? 1f - 0.35f * Mathf.PerlinNoise(Time.time * 7f, 0.5f) * (1f - TimeLeft / WarningAt) : 1f;
            strength = fadeIn * fadeOut;
            SetVisuals(strength * waver);

            int second = Mathf.CeilToInt(TimeLeft);
            if (TimeLeft <= WarningAt && second != lastTickSecond && second > 0)
            {
                lastTickSecond = second;
                AudioManager.Play(Sfx.PrayerTick, 0.5f + 0.5f * (1f - TimeLeft / WarningAt));
            }
            yield return null;
        }

        if (playerAnimator) playerAnimator.SetPraying(false);
        if (risingMotes) risingMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        IsActive = false;
        strength = 0f;
        SetVisuals(0f);
        if (health) health.RemoveProtection();
        cooldownUntil = Time.time + Cooldown;
        GameEvents.RaisePrayerEnded();
        if (fuel && fuel.Depleted)
            GameEvents.ShowMessage("THE PRAYER FADES",
                "They are coming back \u2014 " + Mathf.RoundToInt(Cooldown) + "s to pray again", 3f);
    }

    void SetVisuals(float amount)
    {
        if (crossRoot)
        {
            crossRoot.gameObject.SetActive(amount > 0.001f);
            crossRoot.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, amount);
        }
        if (crossRenderers != null)
        {
            foreach (var r in crossRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, crossGlow * amount);
                mpb.SetColor(BaseColorId, new Color(1f, 0.93f, 0.75f, amount));
                r.SetPropertyBlock(mpb);
            }
        }
        if (holyLight)
        {
            holyLight.enabled = amount > 0.001f;
            holyLight.intensity = holyLightIntensity * amount;
            holyLight.range = holyLightRange * Mathf.Lerp(0.4f, 1f, amount);
        }
    }
}
