using UnityEngine;

// Feeds the Animator Controller from gameplay (speed, hit, death, prayer, radio work),
// adds a small procedural lean into turns, and plays footsteps.
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PlayerController controller;
    [Tooltip("The model root that leans into turns (a child of the player, parent of the Hips).")]
    public Transform leanRoot;

    [Header("Lean")]
    public float maxLean = 9f;
    [Tooltip("Degrees of lean per degree/second of turning.")]
    public float leanPerTurnRate = 0.03f;
    public float forwardLeanWhenRunning = 3f;
    public float leanSmoothing = 8f;

    [Header("Footsteps")]
    public float walkStepLength = 0.85f;
    public float runStepLength = 1.25f;
    public float footstepVolume = 0.55f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int HitId = Animator.StringToHash("Hit");
    static readonly int DeadId = Animator.StringToHash("Dead");
    static readonly int PrayId = Animator.StringToHash("Pray");
    static readonly int WorkId = Animator.StringToHash("Work");

    float roll, pitch, stepDistance;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponentInParent<PlayerController>();
    }

    public void PlayHit() { if (animator) animator.SetTrigger(HitId); }
    public void SetDead() { if (animator) animator.SetBool(DeadId, true); }
    public void SetPraying(bool on) { if (animator) animator.SetBool(PrayId, on); }
    public void SetWorking(bool on) { if (animator) animator.SetBool(WorkId, on); }

    void Update()
    {
        if (!controller) return;
        float dt = Time.deltaTime;

        if (animator) animator.SetFloat(SpeedId, controller.NormalizedSpeed, 0.08f, dt);

        if (leanRoot)
        {
            float speed01 = Mathf.Clamp01(controller.HorizontalSpeed / Mathf.Max(0.1f, controller.runSpeed));
            float targetRoll = Mathf.Clamp(-controller.TurnRate * leanPerTurnRate * speed01, -maxLean, maxLean);
            float targetPitch = controller.IsRunning ? forwardLeanWhenRunning * speed01 : 0f;
            float k = 1f - Mathf.Exp(-leanSmoothing * dt);
            roll = Mathf.Lerp(roll, targetRoll, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            leanRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        UpdateFootsteps(dt);
    }

    void UpdateFootsteps(float dt)
    {
        if (!controller.IsGrounded || controller.HorizontalSpeed < 0.6f) { stepDistance = 0.4f; return; }

        stepDistance += controller.HorizontalSpeed * dt;
        float stride = controller.IsRunning ? runStepLength : walkStepLength;
        if (stepDistance < stride) return;

        stepDistance = 0f;
        float vol = footstepVolume * (controller.IsRunning ? 1f : 0.75f);
        AudioManager.PlayAt(Sfx.Footstep, controller.transform.position, vol, Random.Range(0.9f, 1.1f));
    }
}
