using UnityEngine;

// Makes the lantern look alive: light range, brightness, flicker and flame size all follow the fuel.
// Every "feel" value is an AnimationCurve so it can be tuned in the Inspector (X = fuel 0..1).
public class LanternLight : MonoBehaviour
{
    [Header("References")]
    public LanternFuel fuel;
    public Light lanternLight;
    public Transform flame;
    public Renderer flameRenderer;
    public Renderer glassRenderer;
    public ParticleSystem embers;
    public ParticleSystem extinguishSmoke;
    public ParticleSystem pickupBurst;

    [Header("Light vs Fuel (X = fuel 0..1)")]
    public AnimationCurve rangeByFuel = new AnimationCurve(
        new Keyframe(0f, 2.2f), new Keyframe(0.1f, 3.8f), new Keyframe(0.3f, 7f), new Keyframe(0.6f, 10.5f), new Keyframe(1f, 13.5f));
    [Tooltip("URP point lights fall off with distance squared, so these values are higher than they look.")]
    public AnimationCurve intensityByFuel = new AnimationCurve(
        new Keyframe(0f, 1.5f), new Keyframe(0.1f, 3.5f), new Keyframe(0.3f, 7f), new Keyframe(0.6f, 11f), new Keyframe(1f, 14f));
    public Color fullColor = new Color(1f, 0.66f, 0.32f);
    public Color lowColor = new Color(1f, 0.42f, 0.16f);

    [Header("Flicker")]
    public AnimationCurve flickerByFuel = new AnimationCurve(
        new Keyframe(0f, 0.55f), new Keyframe(0.1f, 0.4f), new Keyframe(0.3f, 0.18f), new Keyframe(0.6f, 0.07f), new Keyframe(1f, 0.04f));
    [Tooltip("Flicker speed with a full lantern.")]
    public float flickerSpeed = 6f;
    [Tooltip("Flicker speed when the lantern is almost empty.")]
    public float lowFuelFlickerSpeed = 15f;

    [Header("Flame")]
    public AnimationCurve flameScaleByFuel = new AnimationCurve(
        new Keyframe(0f, 0.3f), new Keyframe(0.1f, 0.5f), new Keyframe(0.5f, 0.85f), new Keyframe(1f, 1f));
    [ColorUsage(false, true)] public Color flameColor = new Color(4f, 1.9f, 0.55f);
    [ColorUsage(false, true)] public Color glassGlow = new Color(1.1f, 0.5f, 0.16f);

    [Header("Pickup Flare")]
    [Tooltip("How much brighter/larger the light gets for a moment after collecting fuel.")]
    public float flareStrength = 0.6f;
    public float flareDuration = 0.8f;

    // Steady light radius (no flicker). Vampires and the spawner use this.
    public float LightRadius => fuel && fuel.Depleted ? 0f : rangeByFuel.Evaluate(fuel ? fuel.Fraction : 1f);

    Vector3 flameBaseScale = Vector3.one;
    float shownFraction = 1f;
    float flare;
    float disturb;
    bool isOut;
    float noiseSeed;
    // Lazily created rather than built in Awake: a domain reload (recompiling while play
    // mode is running) clears non-serialized fields without calling Awake again, which
    // used to leave this null and throw once per renderer per frame.
    MaterialPropertyBlock mpbCache;
    MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Reset()
    {
        lanternLight = GetComponentInChildren<Light>();
    }

    void Awake()
    {
        noiseSeed = Random.value * 100f;
        if (flame) flameBaseScale = flame.localScale;
        if (!fuel) fuel = GetComponentInParent<LanternFuel>();
    }

    void OnEnable()
    {
        if (!fuel) return;
        fuel.OnFuelAdded += HandleFuelAdded;
        fuel.OnFuelDepleted += HandleOut;
        fuel.OnRelit += HandleRelit;
    }

    void OnDisable()
    {
        if (!fuel) return;
        fuel.OnFuelAdded -= HandleFuelAdded;
        fuel.OnFuelDepleted -= HandleOut;
        fuel.OnRelit -= HandleRelit;
    }

    void Start()
    {
        shownFraction = fuel ? fuel.Fraction : 1f;
    }

    // A vampire hit or a gust makes the flame gutter for a moment.
    public void Disturb(float amount = 1f) => disturb = Mathf.Max(disturb, amount);

    void HandleFuelAdded(float amount)
    {
        flare = 1f;
        if (pickupBurst) pickupBurst.Play();
    }

    void HandleOut()
    {
        isOut = true;
        if (lanternLight) lanternLight.enabled = false;
        if (flame) flame.gameObject.SetActive(false);
        if (embers) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (extinguishSmoke) extinguishSmoke.Play();
        SetGlass(Color.black);
    }

    void HandleRelit()
    {
        isOut = false;
        if (lanternLight) lanternLight.enabled = true;
        if (flame) flame.gameObject.SetActive(true);
        if (embers) embers.Play();
        flare = 1f;
    }

    void Update()
    {
        if (!fuel || isOut) return;

        float target = fuel.Fraction;
        // Rises smoothly after a pickup (a visible "surge"), follows the slow drain directly.
        shownFraction = target > shownFraction ? Mathf.MoveTowards(shownFraction, target, 0.7f * Time.deltaTime) : target;

        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime / flareDuration);
        disturb = Mathf.MoveTowards(disturb, 0f, Time.deltaTime * 2.5f);

        float speed = Mathf.Lerp(lowFuelFlickerSpeed, flickerSpeed, shownFraction);
        float t = Time.time * speed + noiseSeed;
        float n = Mathf.PerlinNoise(t, 0.37f) * 0.7f + Mathf.PerlinNoise(t * 2.3f, 5.1f) * 0.3f;
        float flicker = 1f - flickerByFuel.Evaluate(shownFraction) * n;

        float flareBoost = Mathf.SmoothStep(0f, 1f, flare) * flareStrength;
        float gutter = 1f - disturb * 0.6f;

        if (lanternLight)
        {
            lanternLight.range = rangeByFuel.Evaluate(shownFraction) * (1f + flareBoost * 0.35f) * Mathf.Lerp(1f, flicker, 0.35f);
            lanternLight.intensity = intensityByFuel.Evaluate(shownFraction) * flicker * (1f + flareBoost) * gutter;
            lanternLight.color = Color.Lerp(lowColor, fullColor, shownFraction);
        }

        if (flame)
        {
            float s = flameScaleByFuel.Evaluate(shownFraction) * (0.88f + 0.24f * n) * (1f + flareBoost * 0.5f) * gutter;
            flame.localScale = new Vector3(flameBaseScale.x * s, flameBaseScale.y * s * (1f + 0.15f * (1f - n)), flameBaseScale.z * s);
        }

        if (flameRenderer)
        {
            flameRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, flameColor * (0.6f + 0.5f * flicker) * (1f + flareBoost));
            flameRenderer.SetPropertyBlock(mpb);
        }

        SetGlass(glassGlow * (0.25f + 0.75f * shownFraction) * flicker * (1f + flareBoost));
    }

    void SetGlass(Color emission)
    {
        if (!glassRenderer) return;
        glassRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionId, emission);
        glassRenderer.SetPropertyBlock(mpb);
    }
}
