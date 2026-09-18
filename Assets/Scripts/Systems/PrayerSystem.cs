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
    [Tooltip("Root of the luminous cross that appears above the player.")]
    public Transform crossRoot;
    public Renderer[] crossRenderers;
    [ColorUsage(false, true)] public Color crossGlow = new Color(6f, 4.6f, 2.6f);
    public Light holyLight;
    public float holyLightIntensity = 16f;
    public float holyLightRange = 17f;
    public ParticleSystem risingMotes;
    public ParticleSystem activationRing;

    [Header("Tuning")]
    public float duration = 60f;
    [Tooltip("Seconds the player kneels (cannot move) when the prayer begins.")]
    public float kneelSeconds = 2.4f;
    [Tooltip("Vampires inside this distance flee in terror.")]
    public float fearRadius = 18f;
    [Tooltip("Countdown ticks and flicker start when this many seconds are left.")]
    public float warningAt = 10f;
    public float fadeInSeconds = 1f;
    public float fadeOutSeconds = 1.8f;

    public bool HasLocket { get; private set; }
    public bool Used { get; private set; }
    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }
    public bool CanPray => HasLocket && !Used && !IsActive && fuel && fuel.Depleted
                           && (GameManager.Instance == null || GameManager.Instance.IsGameplayActive)
                           && (!health || !health.IsDead);
    public float FearRadius => IsActive ? fearRadius * strength : 0f;

    float strength;
    int lastTickSecond = -1;
    MaterialPropertyBlock mpb;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
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
            crossRoot.localPosition = new Vector3(0f, 2.75f + Mathf.Sin(Time.time * 1.3f) * 0.06f, 0f);
        }
    }

    public void TryPray()
    {
        if (GameManager.Instance && !GameManager.Instance.IsGameplayActive) return;
        if (CanPray) { StartCoroutine(PrayerRoutine()); return; }
        if (IsActive) return;

        if (!HasLocket) GameEvents.ShowMessage("PRAYER LOCKED", "Find the sacred locket", 2.5f);
        else if (Used) GameEvents.ShowMessage("YOUR PRAYER IS SPENT", "Reach the radio", 2.5f);
        else GameEvents.ShowMessage("NOT YET", "Prayer answers only when the flame is out", 2.5f);
        AudioManager.Play(Sfx.Denied);
    }

    IEnumerator PrayerRoutine()
    {
        Used = true;
        IsActive = true;
        TimeLeft = duration;
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
            float waver = TimeLeft < warningAt ? 1f - 0.35f * Mathf.PerlinNoise(Time.time * 7f, 0.5f) * (1f - TimeLeft / warningAt) : 1f;
            strength = fadeIn * fadeOut;
            SetVisuals(strength * waver);

            int second = Mathf.CeilToInt(TimeLeft);
            if (TimeLeft <= warningAt && second != lastTickSecond && second > 0)
            {
                lastTickSecond = second;
                AudioManager.Play(Sfx.PrayerTick, 0.5f + 0.5f * (1f - TimeLeft / warningAt));
            }
            yield return null;
        }

        if (playerAnimator) playerAnimator.SetPraying(false);
        if (risingMotes) risingMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        IsActive = false;
        strength = 0f;
        SetVisuals(0f);
        if (health) health.RemoveProtection();
        GameEvents.RaisePrayerEnded();
        if (fuel && fuel.Depleted) GameEvents.ShowMessage("THE PRAYER FADES", "They are coming back", 3f);
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
