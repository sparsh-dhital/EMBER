using UnityEngine;

// The sacred locket worn at the survivor's throat once it has been found.
//
// Hidden until the locket is picked up - the Om must never show before then. After that the
// glyph (EMBER/Glyph shader) reads the prayer state at a glance:
//   lantern burning      - a dim, slow breath: the locket is carried, nothing more
//   flame out, can pray  - bright and quick: pray now
//   prayer answered      - a steady blaze
//   recharging           - near dark, warming back up as the cooldown runs down
public class LocketPendant : MonoBehaviour
{
    public PrayerSystem prayer;
    public Renderer glyph;
    [Tooltip("Everything shown once the locket is collected: the disc, the cord and the glyph.")]
    public GameObject visuals;

    MaterialPropertyBlock mpb;
    float intensity, rate, depth;
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int RateId = Shader.PropertyToID("_PulseRate");
    static readonly int DepthId = Shader.PropertyToID("_PulseDepth");

    void Awake()
    {
        if (!prayer) prayer = GetComponentInParent<PrayerSystem>();
        mpb = new MaterialPropertyBlock();
        if (visuals) visuals.SetActive(false);
    }

    void Update()
    {
        bool worn = prayer && prayer.HasLocket;
        if (visuals && visuals.activeSelf != worn) visuals.SetActive(worn);
        if (!worn || !glyph) return;

        float ti, tr, td;
        if (prayer.IsActive)            { ti = 2.4f; tr = 0.2f; td = 0.1f; }
        else if (prayer.CanPray)        { ti = 1.8f; tr = 1.4f; td = 0.7f; }
        else if (prayer.OnCooldown)     { ti = Mathf.Lerp(0.05f, 0.4f, prayer.CooldownFraction); tr = 0.25f; td = 0.3f; }
        else                            { ti = 0.45f; tr = 0.3f; td = 0.5f; }

        float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
        intensity = Mathf.Lerp(intensity, ti, k);
        // The rate is not eased: the shader computes cos(rate * time), and sliding the rate while
        // time is large sweeps the phase through dozens of cycles - a strobe on every state change.
        rate = tr;
        depth = Mathf.Lerp(depth, td, k);

        glyph.GetPropertyBlock(mpb);
        mpb.SetFloat(IntensityId, intensity);
        mpb.SetFloat(RateId, rate);
        mpb.SetFloat(DepthId, depth);
        glyph.SetPropertyBlock(mpb);
    }
}
