using System;
using UnityEngine;

// Physical third/first-person locomotion on a CharacterController:
// walk / jog / run with human acceleration and braking, a real jump arc, landing, ground snapping,
// slope handling and a crawl whose stand-up is protected by an overhead clearance check.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum Gait { Idle, Walk, Jog, Run, Crawl }

    [Header("Speeds (m/s)")]
    public float walkSpeed = 1.8f;
    public float jogSpeed = 3.6f;
    public float runSpeed = 5.8f;
    public float crawlSpeed = 1.1f;
    [Tooltip("Joystick deflection below this walks; above it the character jogs.")]
    [Range(0.2f, 0.9f)] public float walkInputThreshold = 0.55f;

    [Header("Acceleration")]
    [Tooltip("How quickly the character speeds up (m/s per second). Lower = heavier.")]
    public float acceleration = 11f;
    [Tooltip("How quickly the character slows down or brakes before a direction change.")]
    public float deceleration = 15f;
    [Tooltip("Degrees per second the body turns toward the movement direction.")]
    public float turnSpeed = 540f;
    [Range(0f, 1f)] public float airControl = 0.3f;

    [Header("Gravity & Jump")]
    [Tooltip("Multiplier on Physics.gravity while rising.")]
    public float gravityMultiplier = 2f;
    [Tooltip("Multiplier on Physics.gravity while falling. Higher than rising so jumps don't feel floaty.")]
    public float fallGravityMultiplier = 2.6f;
    public float jumpHeight = 1.1f;
    [Tooltip("Short crouch before take-off so the jump reads.")]
    public float jumpAnticipation = 0.08f;
    [Tooltip("Grace period to still jump just after stepping off an edge.")]
    public float coyoteTime = 0.12f;
    [Tooltip("A jump pressed slightly before landing still happens.")]
    public float jumpBuffer = 0.15f;
    public float maxFallSpeed = 30f;
    [Tooltip("Small downward speed while grounded so the character hugs slopes and steps.")]
    public float groundStickSpeed = 3f;
    [Tooltip("Walking over a small dip snaps back to the ground instead of going airborne.")]
    public float groundSnapDistance = 0.35f;

    [Header("Landing")]
    [Tooltip("Falling faster than this (m/s) is a hard landing: brief stagger and camera impact.")]
    public float hardLandingSpeed = 8f;
    public float hardLandingRecovery = 0.25f;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public float groundCheckDistance = 0.2f;

    [Header("Slopes")]
    [Tooltip("Speed the character slides down slopes steeper than the CharacterController's Slope Limit.")]
    public float steepSlideSpeed = 5f;
    [Tooltip("Fraction of speed lost walking straight up a slope at the slope limit.")]
    [Range(0f, 0.8f)] public float uphillSlowdown = 0.35f;

    [Header("Crawl")]
    public float crawlHeight = 0.9f;
    [Tooltip("How fast the collider shrinks/grows (m per second).")]
    public float heightChangeSpeed = 4f;

    public bool ControlEnabled { get; set; } = true;
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalSpeed;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public float VerticalSpeed => verticalSpeed;
    public bool IsGrounded { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCrawling { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsAirborne => !IsGrounded;
    public float SlopeAngle { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float TurnRate { get; private set; }
    public float CurrentHeight => controller.height;
    public float StandHeight => standHeight;
    // 0 = standing, 1 = fully crawling. Used to lower the camera and interaction point smoothly.
    public float CrawlBlend => Mathf.InverseLerp(standHeight, crawlHeight, controller.height);
    public Gait CurrentGait { get; private set; }
    [HideInInspector] public float speedMultiplier = 1f;
    // Set by the first-person camera: the body faces this yaw instead of the movement direction.
    [HideInInspector] public float? facingYawOverride;

    public event Action JumpStarted;
    public event Action<float> Landed;
    public event Action<bool> CrawlChanged;
    public event Action StandBlocked;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalSpeed;
    Vector3 impulse;
    float lockTimer;
    Vector3? forcedFacing;
    float standHeight, standCenterY;
    float timeSinceGrounded, jumpBufferTimer, anticipationTimer, airTime, peakFallSpeed;
    bool wasGrounded = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standHeight = controller.height;
        standCenterY = controller.center.y;
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
        bool canAct = !MovementLocked;

        HandleCrawlInput(input, canAct);
        UpdateHeight(dt);

        // ---- desired horizontal velocity, relative to where the camera looks
        Vector2 move = canAct ? input.Move : Vector2.zero;
        Transform cam = Camera.main ? Camera.main.transform : transform;
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 desiredDir = camForward * move.y + camRight * move.x;
        float inputAmount = Mathf.Clamp01(desiredDir.magnitude);
        if (inputAmount > 0.001f) desiredDir /= desiredDir.magnitude;

        IsRunning = input.Sprint && inputAmount > 0.1f && canAct && !IsCrawling;
        float targetSpeed = TargetSpeed(inputAmount, IsRunning) * speedMultiplier;

        CheckGround();

        // Walking straight up a slope costs a little speed.
        if (IsGrounded && SlopeAngle > 2f && inputAmount > 0.01f)
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
            downhill.y = 0f;
            float uphill = downhill.sqrMagnitude > 0.0001f ? Mathf.Clamp01(-Vector3.Dot(desiredDir, downhill.normalized)) : 0f;
            targetSpeed *= 1f - uphillSlowdown * uphill * Mathf.Clamp01(SlopeAngle / controller.slopeLimit);
        }

        Vector3 desiredVelocity = desiredDir * targetSpeed;
        // Speeding up uses acceleration; slowing down or turning against current motion brakes first.
        bool braking = desiredVelocity.sqrMagnitude < horizontalVelocity.sqrMagnitude || Vector3.Dot(desiredVelocity, horizontalVelocity) < 0f;
        float rate = braking ? deceleration : acceleration;
        if (!IsGrounded) rate *= airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * dt);

        // ---- slopes
        Vector3 planarMove = horizontalVelocity;
        bool tooSteep = IsGrounded && SlopeAngle > controller.slopeLimit + 1f;
        if (IsGrounded && !tooSteep && !IsJumping && planarMove.sqrMagnitude > 0.0001f)
            planarMove = Vector3.ProjectOnPlane(planarMove, GroundNormal).normalized * planarMove.magnitude;
        else if (tooSteep)
            planarMove += Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized * steepSlideSpeed;

        // ---- jumping and gravity
        timeSinceGrounded = IsGrounded ? 0f : timeSinceGrounded + dt;
        jumpBufferTimer = input.JumpPressed && canAct ? jumpBuffer : Mathf.Max(0f, jumpBufferTimer - dt);
        if (IsCrawling && input.JumpPressed) TryStand();
        HandleJump(dt, tooSteep);

        float gravity = Physics.gravity.y * (verticalSpeed > 0f ? gravityMultiplier : fallGravityMultiplier);
        if (IsGrounded && !tooSteep && !IsJumping && verticalSpeed <= 0f) verticalSpeed = -groundStickSpeed;
        else verticalSpeed = Mathf.Max(verticalSpeed + gravity * dt, -maxFallSpeed);

        impulse = Vector3.MoveTowards(impulse, Vector3.zero, 14f * dt);

        var flags = controller.Move((planarMove + impulse + Vector3.up * verticalSpeed) * dt);
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;

        SnapToGround();
        TrackLanding(dt);
        UpdateFacing(dt);
        UpdateGait();
    }

    float TargetSpeed(float inputAmount, bool running)
    {
        if (inputAmount <= 0.001f) return 0f;
        if (IsCrawling) return crawlSpeed * inputAmount;
        if (running) return runSpeed * inputAmount;
        if (inputAmount <= walkInputThreshold) return walkSpeed * inputAmount / walkInputThreshold;
        return Mathf.Lerp(walkSpeed, jogSpeed, (inputAmount - walkInputThreshold) / (1f - walkInputThreshold));
    }

    // ---------------------------------------------------------------- jump / land

    void HandleJump(float dt, bool tooSteep)
    {
        if (anticipationTimer > 0f)
        {
            anticipationTimer -= dt;
            if (anticipationTimer <= 0f)
            {
                verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * gravityMultiplier * jumpHeight);
                IsJumping = true;
                IsGrounded = false;
            }
            return;
        }

        bool canJump = (IsGrounded || timeSinceGrounded < coyoteTime) && !IsJumping && !IsCrawling && !tooSteep && !MovementLocked;
        if (jumpBufferTimer > 0f && canJump)
        {
            jumpBufferTimer = 0f;
            anticipationTimer = jumpAnticipation;
            timeSinceGrounded = coyoteTime;
            JumpStarted?.Invoke();
        }
    }

    void TrackLanding(float dt)
    {
        CheckGround();
        if (!IsGrounded)
        {
            airTime += dt;
            peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalSpeed);
        }
        else if (!wasGrounded)
        {
            if (airTime > 0.12f)
            {
                float impact = peakFallSpeed;
                if (impact > hardLandingSpeed)
                {
                    LockMovement(hardLandingRecovery);
                    horizontalVelocity *= 0.4f;
                }
                Landed?.Invoke(impact);
            }
            IsJumping = false;
            airTime = 0f;
            peakFallSpeed = 0f;
        }
        if (IsGrounded && verticalSpeed <= 0f) IsJumping = false;
        wasGrounded = IsGrounded;
    }

    // Keeps the feet on the ground when walking over small dips and down slopes (no "airborne" hops).
    void SnapToGround()
    {
        if (IsJumping || anticipationTimer > 0f || verticalSpeed > 0f || !wasGrounded || controller.isGrounded) return;
        Vector3 feet = transform.position + controller.center + Vector3.down * (controller.height * 0.5f);
        if (Physics.Raycast(feet + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, groundSnapDistance + 0.1f, groundLayers, QueryTriggerInteraction.Ignore)
            && Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
        {
            controller.Move(Vector3.down * (hit.distance - 0.1f + controller.skinWidth));
        }
    }

    // ---------------------------------------------------------------- crawl

    void HandleCrawlInput(InputReader input, bool canAct)
    {
        if (!input.CrawlPressed || !canAct) return;
        if (IsCrawling) TryStand();
        else if (IsGrounded && !IsJumping)
        {
            IsCrawling = true;
            CrawlChanged?.Invoke(true);
        }
    }

    // Standing up is only allowed with room overhead, so the player never clips through a low ceiling.
    public bool TryStand()
    {
        if (!IsCrawling) return true;
        if (!HasHeadroom(standHeight))
        {
            StandBlocked?.Invoke();
            return false;
        }
        IsCrawling = false;
        CrawlChanged?.Invoke(false);
        return true;
    }

    public bool HasHeadroom(float height)
    {
        float r = controller.radius * 0.9f;
        Vector3 bottom = transform.position + Vector3.up * (controller.center.y - controller.height * 0.5f + controller.radius);
        Vector3 top = transform.position + Vector3.up * (height - controller.radius + 0.02f);
        return !Physics.CheckCapsule(bottom + Vector3.up * 0.05f, top, r, groundLayers, QueryTriggerInteraction.Ignore);
    }

    void UpdateHeight(float dt)
    {
        float target = IsCrawling ? crawlHeight : standHeight;
        if (Mathf.Approximately(controller.height, target)) return;
        float h = Mathf.MoveTowards(controller.height, target, heightChangeSpeed * dt);
        // Grow only as far as the space overhead allows.
        if (h > controller.height && !HasHeadroom(h)) return;
        controller.height = h;
        controller.center = new Vector3(0f, standCenterY - (standHeight - h) * 0.5f, 0f);
    }

    // ---------------------------------------------------------------- ground

    void CheckGround()
    {
        // The sphere answers "is there ground under my feet?" (it also catches step edges).
        float radius = controller.radius * 0.95f;
        Vector3 origin = transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
        bool sphereHit = Physics.SphereCast(origin + Vector3.up * 0.05f, radius, Vector3.down, out _,
            groundCheckDistance + controller.skinWidth + 0.05f, groundLayers, QueryTriggerInteraction.Ignore);
        IsGrounded = (controller.isGrounded || sphereHit) && !(IsJumping && verticalSpeed > 0f);

        // The slope angle comes from a ray straight down the middle: a sphere touching a step's edge reports an
        // almost vertical normal, which made stairs look like an unclimbable cliff.
        float rayLength = controller.height * 0.5f + groundCheckDistance + controller.stepOffset;
        bool rayHit = Physics.Raycast(transform.position + controller.center, Vector3.down, out RaycastHit ray,
            rayLength, groundLayers, QueryTriggerInteraction.Ignore);
        GroundNormal = IsGrounded && rayHit ? ray.normal : Vector3.up;
        SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
    }

    void UpdateFacing(float dt)
    {
        float before = transform.eulerAngles.y;

        if (facingYawOverride.HasValue && !forcedFacing.HasValue)
        {
            var target = Quaternion.Euler(0f, facingYawOverride.Value, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * 1.5f * dt);
        }
        else
        {
            Vector3 faceDir = forcedFacing ?? horizontalVelocity;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.04f || (forcedFacing.HasValue && faceDir.sqrMagnitude > 0.0001f))
            {
                Quaternion target = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
                if (forcedFacing.HasValue && Quaternion.Angle(transform.rotation, target) < 2f) forcedFacing = null;
            }
        }

        TurnRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / dt;
    }

    void UpdateGait()
    {
        float s = HorizontalSpeed;
        CurrentGait = IsCrawling ? Gait.Crawl
            : s < 0.15f ? Gait.Idle
            : s < (walkSpeed + jogSpeed) * 0.5f ? Gait.Walk
            : s < (jogSpeed + runSpeed) * 0.5f ? Gait.Jog
            : Gait.Run;
    }
}
