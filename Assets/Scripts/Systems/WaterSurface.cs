using UnityEngine;

/// <summary>
/// Makes the sea look alive without simulating it.
///
/// The water is one flat quad. Two scrolling normal-map layers moving at different speeds
/// and angles give it moving swell, and a slow vertical breathe suggests tide. At night,
/// through fog, that reads as convincingly as a displaced mesh would, and costs a couple of
/// material property writes a frame instead of a vertex buffer upload.
/// </summary>
[DefaultExecutionOrder(-80)]
public class WaterSurface : MonoBehaviour
{
    [Header("Placement")]
    public float seaLevel = 0.35f;

    [Header("Swell")]
    [Tooltip("How fast the primary normal layer drifts, in UV units per second.")]
    public Vector2 primaryDrift = new Vector2(0.013f, 0.009f);
    [Tooltip("The second layer moves across the first, so the pattern never repeats visibly.")]
    public Vector2 secondaryDrift = new Vector2(-0.008f, 0.017f);

    [Header("Tide")]
    [Tooltip("How far the surface rises and falls, in metres.")]
    public float breatheAmplitude = 0.035f;
    public float breathePeriod = 11f;

    Renderer surface;
    MaterialPropertyBlock mpbCache;
    MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap_ST");
    static readonly int BumpMapId = Shader.PropertyToID("_BumpMap_ST");

    Vector2 primaryOffset, secondaryOffset;
    float baseY;

    void Awake()
    {
        surface = GetComponent<Renderer>();
        baseY = transform.position.y;
    }

    void Update()
    {
        // Unscaled: the sea keeps moving behind a paused menu or an open puzzle board,
        // which stops the world looking frozen while the player is reading.
        float dt = Time.unscaledDeltaTime;

        primaryOffset += primaryDrift * dt;
        secondaryOffset += secondaryDrift * dt;
        primaryOffset = Wrap(primaryOffset);
        secondaryOffset = Wrap(secondaryOffset);

        if (surface)
        {
            surface.GetPropertyBlock(mpb);
            // Tiling is baked into the material; only the offset moves.
            mpb.SetVector(BaseMapId, new Vector4(14f, 14f, primaryOffset.x, primaryOffset.y));
            mpb.SetVector(BumpMapId, new Vector4(9f, 9f, secondaryOffset.x, secondaryOffset.y));
            surface.SetPropertyBlock(mpb);
        }

        float breathe = Mathf.Sin(Time.unscaledTime / Mathf.Max(0.01f, breathePeriod) * Mathf.PI * 2f);
        var pos = transform.position;
        pos.y = baseY + breathe * breatheAmplitude;
        transform.position = pos;
    }

    // Keeps the offsets small so they never lose floating-point precision over a long run.
    static Vector2 Wrap(Vector2 v) => new Vector2(v.x % 1f, v.y % 1f);
}
