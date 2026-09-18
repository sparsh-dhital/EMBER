using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Screen mood that follows the lantern: the vignette closes in and colour drains as fuel runs out,
// red edges when hurt, and a warm lift during prayer.
public class PostFXController : MonoBehaviour
{
    public Volume volume;
    public LanternFuel fuel;
    public PlayerHealth health;
    public PrayerSystem prayer;

    [Header("Vignette")]
    public float baseVignette = 0.3f;
    public float lowFuelVignette = 0.5f;
    public float darkVignette = 0.58f;
    public Color hurtColor = new Color(0.45f, 0.02f, 0.02f);
    public Color prayerColor = new Color(0.35f, 0.26f, 0.1f);

    [Header("Colour")]
    public float baseSaturation = -12f;
    public float darkSaturation = -45f;
    public float prayerExposure = 0.45f;

    Vignette vignette;
    ColorAdjustments colour;
    ChromaticAberration aberration;
    float hit;
    float prayerBlend;
    float baseExposure;

    void Start()
    {
        if (!volume) volume = GetComponent<Volume>();
        if (!volume) return;
        var profile = volume.profile; // runtime copy, the asset on disk is untouched
        profile.TryGet(out vignette);
        profile.TryGet(out colour);
        profile.TryGet(out aberration);
        if (colour) baseExposure = colour.postExposure.value;
    }

    void OnEnable() => GameEvents.PlayerDamaged += OnHit;
    void OnDisable() => GameEvents.PlayerDamaged -= OnHit;
    void OnHit(float amount) => hit = 1f;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float f = fuel ? fuel.Fraction : 1f;
        bool dark = fuel && fuel.Depleted;
        float low = Mathf.Clamp01(1f - f / 0.3f);
        float hurt = health ? 1f - health.Fraction : 0f;
        hit = Mathf.MoveTowards(hit, 0f, dt * 2f);
        prayerBlend = Mathf.MoveTowards(prayerBlend, prayer && prayer.IsActive ? 1f : 0f, dt * 0.8f);

        if (vignette)
        {
            float v = dark ? darkVignette : Mathf.Lerp(baseVignette, lowFuelVignette, low);
            v = Mathf.Max(v, baseVignette + hurt * 0.25f + hit * 0.2f);
            v = Mathf.Lerp(v, 0.38f, prayerBlend);
            vignette.intensity.Override(v);
            Color c = Color.Lerp(Color.black, hurtColor, Mathf.Max(hurt * 0.8f, hit));
            vignette.color.Override(Color.Lerp(c, prayerColor, prayerBlend));
        }

        if (colour)
        {
            float sat = dark ? darkSaturation : Mathf.Lerp(baseSaturation, darkSaturation * 0.6f, low);
            colour.saturation.Override(Mathf.Lerp(sat, 0f, prayerBlend));
            colour.postExposure.Override(baseExposure + prayerBlend * prayerExposure);
        }

        if (aberration) aberration.intensity.Override(hit * 0.6f + (dark ? 0.12f : 0f));
    }
}
