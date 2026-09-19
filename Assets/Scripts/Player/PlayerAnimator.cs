using UnityEngine;

// Feeds the Animator from the physical controller (real speed, air state, crawl, jump/land),
// leans the body into turns, and times footsteps from the locomotion cycle with surface-aware sounds.
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PlayerController controller;
    [Tooltip("The model root that leans into turns (a child of the player, parent of the Hips).")]
    public Transform leanRoot;

    [Header("Animation Speed")]
    [Tooltip("Native speed (m/s) of each locomotion clip. Must match the blend tree thresholds.")]
    public float walkClipSpeed = 1.8f;
    public float crawlClipSpeed = 1.1f;

    [Header("Lean")]
    public float maxLean = 8f;
    [Tooltip("Degrees of lean per degree/second of turning.")]
    public float leanPerTurnRate = 0.025f;
    public float leanSmoothing = 8f;

    [Header("Footsteps")]
    public float footstepVolume = 0.55f;
    [Tooltip("Where in the locomotion cycle each foot lands (0-1). Clips are authored with contacts at 0.25 and 0.75.")]
    public float leftContact = 0.25f, rightContact = 0.75f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int MoveMulId = Animator.StringToHash("MoveMul");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    static readonly int JumpId = Animator.StringToHash("Jump");
    static readonly int LandId = Animator.StringToHash("Land");
    static readonly int CrawlId = Animator.StringToHash("Crawl");
    static readonly int HitId = Animator.StringToHash("Hit");
    static readonly int DeadId = Animator.StringToHash("Dead");
    static readonly int PrayId = Animator.StringToHash("Pray");
    static readonly int WorkId = Animator.StringToHash("Work");
    static readonly int AttackId = Animator.StringToHash("Attack");
    static readonly int LocomotionId = Animator.StringToHash("Locomotion");
    static readonly int CrawlStateId = Animator.StringToHash("CrawlMove");

    float roll, lastPhase;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponentInParent<PlayerController>();
    }

    void OnEnable()
    {
        if (!controller) return;
        controller.JumpStarted += OnJump;
        controller.Landed += OnLanded;
    }

    void OnDisable()
    {
        if (!controller) return;
        controller.JumpStarted -= OnJump;
        controller.Landed -= OnLanded;
    }

    public void PlayHit() { if (animator) animator.SetTrigger(HitId); }
    public void SetDead() { if (animator) animator.SetBool(DeadId, true); }
    public void SetPraying(bool on) { if (animator) animator.SetBool(PrayId, on); }
    public void SetWorking(bool on) { if (animator) animator.SetBool(WorkId, on); }
    public void PlayAttack() { if (animator) animator.SetTrigger(AttackId); }

    void OnJump()
    {
        if (animator) animator.SetTrigger(JumpId);
        AudioManager.PlayAt(Sfx.Jump, controller.transform.position, 0.6f);
    }

    void OnLanded(float impact)
    {
        if (animator) animator.SetTrigger(LandId);
        float strength = Mathf.InverseLerp(2f, 12f, impact);
        AudioManager.PlayAt(Sfx.Land, controller.transform.position, 0.4f + 0.6f * strength);
        CameraController.Shake(0.2f + 0.6f * strength);
        if (strength > 0.5f) Haptics.Pulse(0.3f * strength, 0.08f);
    }

    void Update()
    {
        if (!controller) return;
        float dt = Time.deltaTime;
        float speed = controller.HorizontalSpeed;

        if (animator)
        {
            // Clips are authored at native speeds; between them the blend tree interpolates and the playback
            // rate is scaled so feet don't slide at slow speeds.
            float nativeSpeed = controller.IsCrawling ? crawlClipSpeed : walkClipSpeed;
            float mul = speed < nativeSpeed ? Mathf.Lerp(0.55f, 1f, speed / nativeSpeed) : 1f;
            animator.SetFloat(SpeedId, speed, 0.06f, dt);
            animator.SetFloat(MoveMulId, mul);
            animator.SetBool(GroundedId, controller.IsGrounded);
            animator.SetFloat(VerticalSpeedId, controller.VerticalSpeed);
            animator.SetBool(CrawlId, controller.IsCrawling);
        }

        if (leanRoot)
        {
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, controller.runSpeed));
            float targetRoll = controller.IsGrounded ? Mathf.Clamp(-controller.TurnRate * leanPerTurnRate * speed01, -maxLean, maxLean) : 0f;
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-leanSmoothing * dt));
            leanRoot.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        UpdateFootsteps();
    }

    // Footsteps land on the animation's own contact frames, so they always match what the legs are doing.
    void UpdateFootsteps()
    {
        if (!animator || !controller.IsGrounded || controller.HorizontalSpeed < 0.3f) { lastPhase = -1f; return; }
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != LocomotionId && state.shortNameHash != CrawlStateId) { lastPhase = -1f; return; }

        float phase = Mathf.Repeat(state.normalizedTime, 1f);
        if (lastPhase >= 0f && (Crossed(lastPhase, phase, leftContact) || Crossed(lastPhase, phase, rightContact))) Step();
        lastPhase = phase;
    }

    static bool Crossed(float from, float to, float mark)
    {
        return from <= to ? from < mark && to >= mark : from < mark || to >= mark;
    }

    void Step()
    {
        Vector3 pos = controller.transform.position;
        if (controller.IsCrawling)
        {
            AudioManager.PlayAt(Sfx.CrawlRustle, pos, 0.5f, Random.Range(0.9f, 1.1f));
            return;
        }
        float loud = controller.CurrentGait == PlayerController.Gait.Run ? 1f : controller.CurrentGait == PlayerController.Gait.Jog ? 0.8f : 0.6f;
        float pitch = controller.CurrentGait == PlayerController.Gait.Run ? 1.05f : 1f;
        AudioManager.PlayAt(FootstepSurface.SoundAt(pos), pos, footstepVolume * loud, pitch * Random.Range(0.93f, 1.07f));
    }
}

// Works out what the player is standing on for footstep sounds: terrain layers (grass / dirt / leaves)
// or the material name of props (wood, stone, metal).
public static class FootstepSurface
{
    static int groundMask;

    public static Sfx SoundAt(Vector3 position)
    {
        if (groundMask == 0) groundMask = EmberLayers.World;
        if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, groundMask, QueryTriggerInteraction.Ignore))
            return Sfx.Footstep;

        if (hit.collider is TerrainCollider tc)
        {
            var data = tc.terrainData;
            Vector3 local = hit.point - tc.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] w = data.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int i = 1; i < w.GetLength(2); i++) if (w[0, 0, i] > w[0, 0, best]) best = i;
            return best == 1 ? Sfx.FootstepDirt : best == 2 ? Sfx.FootstepLeaves : Sfx.Footstep;
        }

        var r = hit.collider.GetComponent<Renderer>();
        string mat = r && r.sharedMaterial ? r.sharedMaterial.name : "";
        if (mat.Contains("Wood") || mat.Contains("Roof")) return Sfx.FootstepWood;
        if (mat.Contains("Stone") || mat.Contains("Rock") || mat.Contains("Metal")) return Sfx.FootstepStone;
        return Sfx.FootstepDirt;
    }
}
