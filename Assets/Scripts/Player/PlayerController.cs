using UnityEngine;

// Physical third-person movement on a CharacterController:
// camera-relative input, acceleration/deceleration, Unity gravity, ground checks and slope handling.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3.4f;
    public float runSpeed = 5.8f;
    [Tooltip("How fast the character reaches full speed (m/s per second).")]
    public float acceleration = 24f;
    [Tooltip("How fast the character stops when you let go (m/s per second).")]
    public float deceleration = 32f;
    [Tooltip("Degrees per second the body turns toward the movement direction.")]
    public float turnSpeed = 720f;

    [Header("Gravity")]
    [Tooltip("Multiplier on Physics.gravity. Slightly above 1 feels less floaty for a game character.")]
    public float gravityMultiplier = 2f;
    [Tooltip("Small downward speed while grounded so the character hugs slopes and steps.")]
    public float groundStickSpeed = 3f;
    public float maxFallSpeed = 30f;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public float groundCheckDistance = 0.2f;

    [Header("Slopes")]
    [Tooltip("Speed the character slides down slopes steeper than the CharacterController's Slope Limit.")]
    public float steepSlideSpeed = 5f;

    public bool ControlEnabled { get; set; } = true;
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalSpeed;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public bool IsGrounded { get; private set; }
    public bool IsRunning { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float TurnRate { get; private set; }
    // 0 = idle, 0.5 = walking, 1 = running. Used by the animator.
    public float NormalizedSpeed => HorizontalSpeed <= walkSpeed
        ? 0.5f * HorizontalSpeed / walkSpeed
        : 0.5f + 0.5f * Mathf.InverseLerp(walkSpeed, runSpeed, HorizontalSpeed);
    [HideInInspector] public float speedMultiplier = 1f;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalSpeed;
    Vector3 impulse;
    float lockTimer;
    Vector3? forcedFacing;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Stops movement input for a moment (prayer, radio repair, being hit).
    public void LockMovement(float seconds) => lockTimer = Mathf.Max(lockTimer, seconds);
    public void UnlockMovement() => lockTimer = 0f;
    public bool MovementLocked => lockTimer > 0f || !ControlEnabled;

    public void AddImpulse(Vector3 velocityChange)
    {
        velocityChange.y = 0f;
        impulse += velocityChange;
    }

    public void FaceTowards(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) forcedFacing = d.normalized;
    }

    // Moves the character instantly (used by restart/teleport helpers).
    public void Teleport(Vector3 position, float yaw)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        horizontalVelocity = impulse = Vector3.zero;
        verticalSpeed = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        lockTimer = Mathf.Max(0f, lockTimer - dt);

        var input = InputReader.Instance;
        Vector2 move = MovementLocked ? Vector2.zero : input.Move;

        // Input is relative to where the camera looks, flattened onto the ground.
        Transform cam = Camera.main ? Camera.main.transform : transform;
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 desiredDir = camForward * move.y + camRight * move.x;
        float inputAmount = Mathf.Clamp01(desiredDir.magnitude);
        if (inputAmount > 0.001f) desiredDir /= desiredDir.magnitude;

        IsRunning = input.Sprint && inputAmount > 0.1f && !MovementLocked;
        float targetSpeed = (IsRunning ? runSpeed : walkSpeed) * inputAmount * speedMultiplier;
        Vector3 desiredVelocity = desiredDir * targetSpeed;

        float rate = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * dt);

        CheckGround();

        // Slopes: follow walkable slopes smoothly, slide off ones that are too steep.
        Vector3 planarMove = horizontalVelocity;
        float slopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
        bool tooSteep = IsGrounded && slopeAngle > controller.slopeLimit + 1f;
        if (IsGrounded && !tooSteep && planarMove.sqrMagnitude > 0.0001f)
        {
            planarMove = Vector3.ProjectOnPlane(planarMove, GroundNormal).normalized * planarMove.magnitude;
        }
        else if (tooSteep)
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
            planarMove += downhill * steepSlideSpeed;
        }

        // Gravity comes from the physics settings, not from moving the transform by hand.
        if (IsGrounded && !tooSteep && verticalSpeed <= 0f) verticalSpeed = -groundStickSpeed;
        else verticalSpeed = Mathf.Max(verticalSpeed + Physics.gravity.y * gravityMultiplier * dt, -maxFallSpeed);

        impulse = Vector3.MoveTowards(impulse, Vector3.zero, 14f * dt);

        controller.Move((planarMove + impulse + Vector3.up * verticalSpeed) * dt);

        UpdateFacing(dt);
    }

    void CheckGround()
    {
        float radius = controller.radius * 0.95f;
        Vector3 origin = transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
        bool hit = Physics.SphereCast(origin + Vector3.up * 0.05f, radius, Vector3.down, out RaycastHit info,
            groundCheckDistance + controller.skinWidth + 0.05f, groundLayers, QueryTriggerInteraction.Ignore);

        IsGrounded = controller.isGrounded || hit;
        GroundNormal = hit ? info.normal : Vector3.up;
    }

    void UpdateFacing(float dt)
    {
        Vector3 faceDir = forcedFacing ?? horizontalVelocity;
        faceDir.y = 0f;
        float before = transform.eulerAngles.y;

        if (faceDir.sqrMagnitude > 0.04f || (forcedFacing.HasValue && faceDir.sqrMagnitude > 0.0001f))
        {
            Quaternion target = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            if (forcedFacing.HasValue && Quaternion.Angle(transform.rotation, target) < 2f) forcedFacing = null;
        }

        TurnRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / dt;
    }
}
