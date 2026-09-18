using UnityEngine;

// Procedural body language for the vampire, so its state is readable at a glance:
// cowering from the light, creeping low while stalking, reaching while it charges,
// and an obvious arms-up telegraph before it strikes.
public class VampireAnimator : MonoBehaviour
{
    [Header("Bones")]
    public Transform model;
    public Transform hips;
    public Transform torso;
    public Transform head;
    public Transform upperArmL, upperArmR, forearmL, forearmR;
    public Transform thighL, thighR, shinL, shinR;
    public Transform capeL, capeR;

    [Header("Eyes")]
    public Renderer[] eyes;
    [ColorUsage(false, true)] public Color eyeColor = new Color(6f, 0.25f, 0.12f);

    [Header("Motion")]
    public float strideFrequency = 1.6f;
    public float poseSharpness = 9f;

    float phase;
    float windUp, strike;
    float fade = 1f;
    Vector3 hipsBase;
    MaterialPropertyBlock mpb;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    // Current (smoothed) pose angles.
    float torsoX, headX, armX, armZ, foreX, legAmp;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (hips) hipsBase = hips.localPosition;
    }

    public void ResetVisual()
    {
        fade = 1f;
        windUp = strike = 0f;
        if (model) { model.localScale = Vector3.one; model.localPosition = Vector3.zero; }
    }

    public void BeginWindUp() { windUp = 1f; strike = 0f; }
    public void Strike() { strike = 1f; windUp = 0f; }
    public void SetFade(float amount) => fade = amount;

    public void Animate(VampireAI.State state, float speed01, float aggression)
    {
        float dt = Time.deltaTime;
        speed01 = Mathf.Clamp01(speed01);
        phase += dt * strideFrequency * Mathf.Lerp(0.6f, 2.4f, speed01) * Mathf.PI * 2f;

        // Target pose per state.
        float tTorso = 25f, tHead = -10f, tArm = -15f, tArmZ = 8f, tFore = -20f, tLeg = 10f + 35f * speed01;
        switch (state)
        {
            case VampireAI.State.Flee:
                tTorso = 35f; tHead = 15f; tArm = -140f; tArmZ = 20f; tFore = -70f; break;
            case VampireAI.State.Stalk:
                tTorso = 42f; tHead = -32f; tArm = -55f; tArmZ = 14f; tFore = -35f; tLeg = 8f + 25f * speed01; break;
            case VampireAI.State.Aggressive:
            case VampireAI.State.Attack:
                tTorso = 30f; tHead = -22f; tArm = -80f; tArmZ = 10f; tFore = -12f; break;
            case VampireAI.State.Banished:
                tTorso = 40f; tHead = 20f; tArm = -130f; tArmZ = 25f; tFore = -80f; break;
        }

        // Wind-up: arms thrown high and wide. Strike: slash down and lunge.
        windUp = Mathf.MoveTowards(windUp, 0f, dt * 1.2f);
        strike = Mathf.MoveTowards(strike, 0f, dt * 3.5f);
        if (windUp > 0f) { tTorso = -8f; tHead = -15f; tArm = -165f; tArmZ = 35f; tFore = -25f; }
        if (strike > 0f) { tTorso = Mathf.Lerp(tTorso, 50f, strike); tArm = Mathf.Lerp(tArm, -45f, strike); tArmZ = 5f; tFore = -5f; }

        float k = 1f - Mathf.Exp(-poseSharpness * dt);
        torsoX = Mathf.Lerp(torsoX, tTorso, k);
        headX = Mathf.Lerp(headX, tHead, k);
        armX = Mathf.Lerp(armX, tArm, k);
        armZ = Mathf.Lerp(armZ, tArmZ, k);
        foreX = Mathf.Lerp(foreX, tFore, k);
        legAmp = Mathf.Lerp(legAmp, tLeg, k);

        float s = Mathf.Sin(phase);
        float breathe = Mathf.Sin(Time.time * 1.7f) * 2f;

        if (torso) torso.localRotation = Quaternion.Euler(torsoX + breathe, s * 6f * speed01, 0f);
        if (head) head.localRotation = Quaternion.Euler(headX, Mathf.Sin(Time.time * 0.9f) * 12f * (1f - speed01), 0f);
        if (upperArmL) upperArmL.localRotation = Quaternion.Euler(armX + s * 18f * speed01, 0f, -armZ);
        if (upperArmR) upperArmR.localRotation = Quaternion.Euler(armX - s * 18f * speed01, 0f, armZ);
        if (forearmL) forearmL.localRotation = Quaternion.Euler(foreX, 0f, 0f);
        if (forearmR) forearmR.localRotation = Quaternion.Euler(foreX, 0f, 0f);
        if (thighL) thighL.localRotation = Quaternion.Euler(-s * legAmp - 10f, 0f, 0f);
        if (thighR) thighR.localRotation = Quaternion.Euler(s * legAmp - 10f, 0f, 0f);
        if (shinL) shinL.localRotation = Quaternion.Euler(20f + Mathf.Max(0f, s) * legAmp * 1.4f, 0f, 0f);
        if (shinR) shinR.localRotation = Quaternion.Euler(20f + Mathf.Max(0f, -s) * legAmp * 1.4f, 0f, 0f);
        if (hips) hips.localPosition = hipsBase + Vector3.up * (Mathf.Abs(s) * 0.06f * speed01 - 0.08f + Mathf.Sin(Time.time * 1.7f) * 0.01f)
                                     + Vector3.forward * strike * 0.35f;

        float capeFlap = 12f + 45f * speed01 + Mathf.Sin(Time.time * 9f) * 5f * (0.3f + speed01);
        if (capeL) capeL.localRotation = Quaternion.Euler(capeFlap, 0f, -6f);
        if (capeR) capeR.localRotation = Quaternion.Euler(capeFlap * 0.92f, 0f, 6f);

        if (model)
        {
            model.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, fade);
            model.localPosition = Vector3.down * (1f - fade) * 0.8f;
        }

        if (eyes != null)
        {
            float glow = Mathf.Lerp(0.5f, 1.6f, aggression) * fade * (windUp > 0f ? 1.8f : 1f);
            foreach (var r in eyes)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, eyeColor * glow);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
