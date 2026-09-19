using Unity.Cinemachine;
using UnityEngine;

// Switches between the third-person orbit camera and an eye-level first-person camera (V / the mobile VIEW button).
// First person gets restrained head motion (bob, breathing, landing dip, turn roll), hides the head so it never
// fills the view (its shadow stays), and raises the lantern arm so the light source is visible in front of you.
[DefaultExecutionOrder(-50)]
public class CameraModeController : MonoBehaviour
{
    public enum Mode { ThirdPerson, FirstPerson }

    static CameraModeController instance;
    public static Mode Current => instance ? instance.mode : Mode.ThirdPerson;
    public static Vector3 EyePosition => instance && instance.firstPerson ? instance.firstPerson.transform.position : Vector3.zero;
    public static Vector3 EyeForward => instance && instance.firstPerson ? instance.firstPerson.transform.forward : Vector3.forward;

    [Header("References")]
    public CinemachineCamera thirdPerson;
    public CinemachineCamera firstPerson;
    public CameraController thirdPersonController;
    public PlayerController player;
    [Tooltip("Hidden in first person (still cast shadows): head, hair, neck, scarf.")]
    public Renderer[] hideInFirstPerson;
    public Transform upperArmR, forearmR, handR;

    [Header("First Person View")]
    [Tooltip("Eye height below the top of the collider.")]
    public float eyeBelowTop = 0.17f;
    [Tooltip("Eyes sit slightly in front of the body's centre.")]
    public float eyeForward = 0.14f;
    [Tooltip("While crawling the head is well forward of the body.")]
    public float crawlEyeForward = 0.5f;
    [Tooltip("Looking down leans the head forward over the body, so you see the ground and your boots, not the inside of the torso.")]
    public float lookDownLean = 0.2f;
    public float minPitch = -75f;
    public float maxPitch = 70f;
    public float lookSharpness = 24f;

    [Header("Head Motion")]
    public float walkBob = 0.025f;
    public float runBob = 0.05f;
    public float bobSway = 0.012f;
    public float breathing = 0.005f;
    public float landingDip = 0.12f;
    public float turnRoll = 2f;

    [Header("First Person Lantern")]
    [Tooltip("Where the right hand holds the lantern, relative to the eyes (right, up, forward), so it hangs in the lower right of the view.")]
    public Vector3 lanternHold = new Vector3(0.24f, -0.1f, 0.46f);
    public Transform lantern;
    [Tooltip("The lantern is modelled a little oversized for third person; at arm's length in first person it is shown at true size.")]
    public float firstPersonLanternScale = 1f;
    [Tooltip("How much the held lantern follows looking up/down.")]
    public float holdPitchFollow = 0.55f;
    [Tooltip("Blocks the camera from pushing into walls when the eyes sit forward of the body (crawling).")]
    public LayerMask eyeBlockers = 1;

    [Header("Priorities")]
    public int thirdPersonPriority = 10;
    public int firstPersonPriority = 15;

    public const string PrefKey = "ember_camera_mode";

    Mode mode;
    float yaw, pitch, fpWeight, armWeight, bobPhase, dip, dipVelocity, roll;
    Vector2 smoothedLook;
    bool meshesHidden;
    Vector3 lanternScale = Vector3.one;

    void Awake()
    {
        instance = this;
        if (lantern) lanternScale = lantern.localScale;
    }

    void OnEnable()
    {
        if (player) player.Landed += OnLanded;
    }

    void OnDisable()
    {
        if (player) player.Landed -= OnLanded;
        if (instance == this) instance = null;
    }

    void Start()
    {
        if (player) yaw = player.transform.eulerAngles.y;
        SetMode(PlayerPrefs.GetInt(PrefKey, 0) == 1 ? Mode.FirstPerson : Mode.ThirdPerson, true);
    }

    void OnLanded(float impact) => dipVelocity -= Mathf.Clamp(impact, 0f, 14f) * landingDip;

    public static void Toggle()
    {
        if (instance) instance.SetMode(instance.mode == Mode.ThirdPerson ? Mode.FirstPerson : Mode.ThirdPerson, false);
    }

    void SetMode(Mode next, bool instant)
    {
        var cam = Camera.main;
        if (next == Mode.FirstPerson && cam && !instant)
        {
            // Keep looking the way the third-person camera was looking.
            Vector3 f = cam.transform.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg * 0.5f, minPitch, maxPitch);
        }
        if (next == Mode.ThirdPerson && thirdPersonController && thirdPersonController.orbit)
        {
            thirdPersonController.orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            thirdPersonController.orbit.VerticalAxis.Value = 15f;
        }
        mode = next;
        PlayerPrefs.SetInt(PrefKey, next == Mode.FirstPerson ? 1 : 0);
        if (firstPerson) { firstPerson.Priority.Enabled = true; firstPerson.Priority.Value = next == Mode.FirstPerson ? firstPersonPriority : 0; }
        if (thirdPerson) { thirdPerson.Priority.Enabled = true; thirdPerson.Priority.Value = thirdPersonPriority; }
        if (instant) fpWeight = next == Mode.FirstPerson ? 1f : 0f;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.IsGameplayActive;
        if (playing && InputReader.Instance.ToggleCameraPressed)
        {
            Toggle();
            AudioManager.Play(Sfx.UiClick, 0.6f);
        }
    }

    void LateUpdate()
    {
        if (!player || !firstPerson) return;
        float dt = Time.deltaTime;
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.IsGameplayActive;
        bool fp = mode == Mode.FirstPerson;

        if (thirdPersonController) thirdPersonController.inputEnabled = !fp;

        // ---- look
        Vector2 look = fp && playing ? InputReader.Instance.LookDegrees : Vector2.zero;
        smoothedLook = Vector2.Lerp(smoothedLook, look, 1f - Mathf.Exp(-lookSharpness * Time.unscaledDeltaTime));
        yaw += smoothedLook.x;
        pitch = Mathf.Clamp(pitch - smoothedLook.y, minPitch, maxPitch);
        if (!fp && Camera.main)
        {
            Vector3 f = Camera.main.transform.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
        player.facingYawOverride = fp && player.ControlEnabled ? yaw : (float?)null;

        // ---- head motion
        float speed = player.HorizontalSpeed;
        float bobAmount = player.IsGrounded ? Mathf.Lerp(0f, speed > player.jogSpeed ? runBob : walkBob, Mathf.Clamp01(speed / player.walkSpeed)) : 0f;
        bobPhase += dt * Mathf.Lerp(1.6f, 3.4f, Mathf.InverseLerp(0f, player.runSpeed, speed)) * Mathf.PI * 2f * (speed > 0.2f ? 1f : 0f);
        float breathe = Mathf.Sin(Time.time * 1.6f) * breathing;
        dipVelocity += (-dip * 90f - dipVelocity * 14f) * dt;
        dip += dipVelocity * dt;
        roll = Mathf.Lerp(roll, Mathf.Clamp(-player.TurnRate * 0.01f, -1f, 1f) * turnRoll, 1f - Mathf.Exp(-6f * dt));

        float crawl = player.CrawlBlend;
        Quaternion bodyYaw = Quaternion.Euler(0f, yaw, 0f);
        Vector3 headBase = player.transform.position + Vector3.up * (player.CurrentHeight - eyeBelowTop);
        float forward = Mathf.Lerp(eyeForward, crawlEyeForward, crawl) + Mathf.Clamp01(pitch / maxPitch) * lookDownLean * (1f - crawl);
        Vector3 fwd = bodyYaw * Vector3.forward;
        // Never push the eyes through a wall or rock in front of the body.
        if (Physics.SphereCast(headBase, 0.12f, fwd, out var hit, forward, eyeBlockers, QueryTriggerInteraction.Ignore))
            forward = Mathf.Max(0f, hit.distance - 0.02f);
        Vector3 motion = Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmount + breathe + dip)
                         + bodyYaw * Vector3.right * (Mathf.Sin(bobPhase * 0.5f) * bobSway * Mathf.Clamp01(speed / player.walkSpeed));
        Vector3 eye = headBase + fwd * forward + motion;
        firstPerson.transform.SetPositionAndRotation(eye, Quaternion.Euler(pitch, yaw, roll));

        // ---- body presentation
        fpWeight = Mathf.MoveTowards(fpWeight, fp ? 1f : 0f, dt / 0.35f);
        bool hide = fpWeight > 0.5f;
        if (hide != meshesHidden) SetHeadHidden(hide);

        // Hold the lantern out in view; let the animation have the arm back while crawling, praying or working.
        bool holding = player.ControlEnabled && !player.MovementLocked && !player.IsCrawling;
        armWeight = Mathf.MoveTowards(armWeight, holding ? 1f : 0f, dt / 0.3f);
        float w = Mathf.SmoothStep(0f, 1f, fpWeight) * Mathf.SmoothStep(0f, 1f, armWeight);
        if (lantern) lantern.localScale = lanternScale * Mathf.Lerp(1f, firstPersonLanternScale / Mathf.Max(0.01f, lanternScale.x), Mathf.SmoothStep(0f, 1f, fpWeight));
        if (w > 0.001f && upperArmR && forearmR && handR)
        {
            Quaternion holdFrame = Quaternion.Euler(Mathf.Clamp(pitch * holdPitchFollow, -25f, 35f), yaw, 0f);
            // The hand rides the walk a little more than the head does, so the lantern swings.
            Vector3 target = headBase + fwd * Mathf.Min(forward, eyeForward) + holdFrame * lanternHold + motion * 1.6f;
            Quaternion u0 = upperArmR.localRotation, f0 = forearmR.localRotation;
            TwoBoneIK.Solve(upperArmR, forearmR, handR, target);
            upperArmR.localRotation = Quaternion.Slerp(u0, upperArmR.localRotation, w);
            forearmR.localRotation = Quaternion.Slerp(f0, forearmR.localRotation, w);
        }
    }

    void SetHeadHidden(bool hidden)
    {
        meshesHidden = hidden;
        if (hideInFirstPerson == null) return;
        foreach (var r in hideInFirstPerson)
            if (r) r.shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
    }
}
