using UnityEngine;

/// <summary>
/// The rowing boat that links the three islands.
///
/// It is a real vehicle rather than a teleport: you board it at a jetty, steer it across
/// open water under your own power, and run it aground on the far beach. The lantern comes
/// with you and keeps burning, so a crossing in the dark with a low flame is exactly as
/// tense as it should be - but vampires cannot follow you onto the water, which makes the
/// boat a genuine tactical retreat as well as transport.
///
/// Movement is deliberately simple: thrust and turn, with drag and a little inertia. No
/// buoyancy simulation - the hull is pinned to the waterline and bobs, which reads as
/// convincingly afloat without any of the instability a real rigidbody on water brings.
/// </summary>
public class BoatController : MonoBehaviour, IInteractable
{
    [Header("References")]
    public Transform hull;
    [Tooltip("Where the player is parented while aboard.")]
    public Transform seat;
    public Transform bowLight;
    public ParticleSystem wake;
    public ParticleSystem splash;

    [Header("Handling")]
    [Tooltip("Metres per second at full throttle.")]
    public float topSpeed = 5.2f;
    public float acceleration = 3.2f;
    [Tooltip("How quickly speed bleeds off when you stop rowing.")]
    public float drag = 1.1f;
    public float turnSpeed = 62f;
    [Tooltip("Turning is sluggish at a standstill and sharpens as you gather way.")]
    [Range(0f, 1f)] public float turnAtRest = 0.35f;

    [Header("Feel")]
    [Tooltip("How far the hull rocks, in degrees.")]
    public float rockAmplitude = 2.6f;
    public float rockSpeed = 1.5f;
    [Tooltip("Vertical bob, in metres.")]
    public float bobAmplitude = 0.07f;
    [Tooltip("How far the boat heels into a turn.")]
    public float leanPerTurn = 0.16f;

    [Header("Docking")]
    [Tooltip("How close the player must be to board.")]
    public float boardRange = 3.2f;
    [Tooltip("Water depth below which the hull grounds out on a beach.")]
    public float groundingDepth = 0.55f;

    public bool Occupied { get; private set; }
    public float Speed01 => Mathf.Abs(speed) / Mathf.Max(0.01f, topSpeed);

    // ---- IInteractable: the boat uses the same prompt and E-key path as everything else.
    public string Prompt => Occupied ? null : "Board the boat";
    public bool CanInteract => !Occupied && Time.time - lastDisembarkTime >= 0.6f;

    public void Interact(PlayerInteractor who)
    {
        if (Occupied || who == null) return;
        var controller = who.GetComponent<PlayerController>() ?? who.GetComponentInParent<PlayerController>();
        if (controller) Board(controller);
    }

    PlayerController player;
    Transform playerTransform;
    CharacterController playerCollider;
    float speed;
    float yaw;
    float bobPhase;
    float lean;
    float lastDisembarkTime = -99f;

    void Awake()
    {
        if (!hull) hull = transform.childCount > 0 ? transform.GetChild(0) : transform;
        yaw = transform.eulerAngles.y;
        bobPhase = Random.value * 10f;
    }

    // ---------------------------------------------------------------- boarding

    /// <summary>Can this player step aboard right now?</summary>
    public bool CanBoard(Transform who)
    {
        if (Occupied || who == null) return false;
        if (Time.time - lastDisembarkTime < 0.6f) return false;   // no instant re-board
        return Flat(who.position - BoardPoint()).magnitude <= boardRange;
    }

    Vector3 BoardPoint() => transform.position;

    public void Board(PlayerController who)
    {
        if (Occupied || who == null) return;

        player = who;
        playerTransform = who.transform;
        playerCollider = who.GetComponent<CharacterController>();
        Occupied = true;

        // Hand control to the boat completely. Clearing ControlEnabled only stops the player
        // reading input - the controller still runs gravity and ground-snapping every frame,
        // which drags the passenger out of the seat. The component itself has to stop.
        player.ControlEnabled = false;
        player.enabled = false;
        if (playerCollider) playerCollider.enabled = false;

        playerTransform.SetParent(seat ? seat : transform, false);
        playerTransform.localPosition = Vector3.zero;
        playerTransform.localRotation = Quaternion.identity;

        if (wake) wake.Play();
        AudioManager.PlayAt(Sfx.BoatBoard, transform.position);
        GameEvents.ShowMessage("ABOARD", "Steer with the movement controls — press E to land", 3.5f);
    }

    public void Disembark()
    {
        if (!Occupied) return;

        // Step out onto whatever dry land is nearest, so you never end up swimming.
        Vector3 landing = FindLanding();

        playerTransform.SetParent(null, false);
        playerTransform.position = landing;
        playerTransform.rotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y, 0f);
        if (playerCollider) playerCollider.enabled = true;
        player.enabled = true;
        player.ControlEnabled = true;

        Occupied = false;
        lastDisembarkTime = Time.time;
        speed = 0f;

        if (wake) wake.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (splash) splash.Play();
        AudioManager.PlayAt(Sfx.BoatDock, transform.position);

        int isle = EmberIslands.IslandAt(new Vector2(landing.x, landing.z));
        if (isle >= 0)
            GameEvents.ShowMessage(EmberIslands.Name(isle).ToUpperInvariant(), "", 2.5f);

        player = null;
        playerTransform = null;
        playerCollider = null;
    }

    // Looks for dry ground around the boat, preferring straight ahead so landing feels
    // like beaching rather than being teleported sideways.
    Vector3 FindLanding()
    {
        Vector3 origin = transform.position;
        float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };

        foreach (float a in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Flat(transform.forward).normalized;
            for (float dist = 1.6f; dist <= 5.5f; dist += 0.7f)
            {
                Vector3 probe = origin + dir * dist;
                float ground = EmberIslands.SeabedHeight(probe);
                if (ground <= EmberIslands.SeaLevel + 0.2f) continue;   // still water
                return new Vector3(probe.x, ground + 0.15f, probe.z);
            }
        }

        // Nothing dry within reach: stay put rather than dropping the player in the sea.
        return origin + Vector3.up * 0.4f;
    }

    // ---------------------------------------------------------------- driving

    void Update()
    {
        float dt = Time.deltaTime;
        bobPhase += dt * rockSpeed;

        if (Occupied) Drive(dt);
        else speed = Mathf.MoveTowards(speed, 0f, drag * 2f * dt);

        Settle(dt);
    }

    void Drive(float dt)
    {
        var gm = GameManager.Instance;
        if (gm && !gm.IsGameplayActive) return;
        if (!InputReader.Exists) return;
        var input = InputReader.Instance;

        // Step ashore.
        if (input.InteractPressed) { Disembark(); return; }

        Vector2 move = input.Move;

        // Forward/back on the vertical axis, steering on the horizontal - the same stick
        // layout as on foot, so nothing has to be relearned.
        float throttle = move.y;
        speed = throttle != 0f
            ? Mathf.MoveTowards(speed, topSpeed * throttle, acceleration * dt)
            : Mathf.MoveTowards(speed, 0f, drag * dt);

        // A boat with no way on answers the rudder poorly.
        float authority = Mathf.Lerp(turnAtRest, 1f, Speed01);
        float turn = move.x * turnSpeed * authority * dt;
        // Reverse steers the way a boat actually does.
        if (speed < 0f) turn = -turn;
        yaw += turn;
        lean = Mathf.Lerp(lean, -turn / Mathf.Max(0.0001f, dt) * leanPerTurn * 0.01f, dt * 4f);

        Vector3 delta = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * dt;
        Vector3 next = transform.position + delta;

        // Run aground rather than sailing up the beach. This asks the terrain directly:
        // a raycast would hit the jetty planking the boat is moored under.
        if (EmberIslands.DepthAt(next) < groundingDepth)
        {
            if (speed > 0.6f)
            {
                if (splash) splash.Play();
                AudioManager.PlayAt(Sfx.BoatDock, transform.position, 0.7f);
                CameraController.Shake(0.35f);
            }
            speed = Mathf.Min(speed, 0f);      // you can still back off
            return;
        }

        transform.position = next;

        if (wake)
        {
            var emission = wake.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(2f, 26f, Speed01);
        }
    }

    // Pins the hull to the waterline and gives it a little life.
    void Settle(float dt)
    {
        Vector3 pos = transform.position;
        pos.y = EmberIslands.SeaLevel + Mathf.Sin(bobPhase * Mathf.PI * 2f) * bobAmplitude;
        transform.position = pos;

        float rock = Mathf.Sin(bobPhase * Mathf.PI * 2f * 0.85f) * rockAmplitude * Mathf.Lerp(1f, 0.35f, Speed01);
        float pitch = -Speed01 * 3.2f;           // bow lifts as she gathers way
        transform.rotation = Quaternion.Euler(pitch, yaw, rock + lean);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, boardRange);
    }
#endif
}
