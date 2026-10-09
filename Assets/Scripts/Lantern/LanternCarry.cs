using UnityEngine;

// Raises the lantern arm to light the path ahead, on top of whatever the Animator is playing.
//
// The clips hold the lantern at the hip. While walking or standing the survivor now carries it
// forward at chest height, and lifts it higher and further out as the dark closes in: the lantern
// burning low, or a vampire near. Running, crawling, jumping, fighting and praying hand the arm
// back to the clips, which already author a swinging, tucked or kneeling lantern for those.
//
// Runs after the Animator and before LanternSway, so the pendulum swings from the raised hand.
[DefaultExecutionOrder(-20)]
public class LanternCarry : MonoBehaviour
{
    public PlayerController controller;
    public LanternFuel fuel;
    public PrayerSystem prayer;
    [Tooltip("Optional. Found at start when left empty.")]
    public VampireSpawner vampires;
    public Transform upperArm, forearm, hand;

    [Header("Carry pose (metres from the shoulder, in body space)")]
    // Held out to the side rather than across the chest: the flame stays out of the survivor's
    // eyes, and the torso, scarf and locket stay visible behind it.
    public Vector3 calmOffset = new Vector3(-0.1f, -0.32f, 0.32f);
    public Vector3 alertOffset = new Vector3(-0.12f, -0.08f, 0.46f);

    [Header("Weights")]
    [Range(0f, 1f)] public float calmWeight = 0.6f;
    [Tooltip("How much of the raise is kept while running.")]
    [Range(0f, 1f)] public float runWeight = 0.2f;
    [Tooltip("A vampire inside this distance raises the lantern fully.")]
    public float threatDistance = 10f;
    public float blendSpeed = 3f;

    float weight, alert;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<PlayerController>();
        if (!fuel) fuel = GetComponentInParent<LanternFuel>();
        if (!prayer) prayer = GetComponentInParent<PrayerSystem>();
        var root = controller ? controller.transform : transform;
        if (!upperArm) upperArm = Find(root, "UpperArmL");
        if (!forearm) forearm = Find(root, "ForearmL");
        if (!hand) hand = Find(root, "HandL");
    }

    void Start()
    {
        if (!vampires) vampires = FindFirstObjectByType<VampireSpawner>();
    }

    static Transform Find(Transform t, string name)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    void LateUpdate()
    {
        if (!controller || !upperArm || !forearm || !hand) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // How dark it is getting: nothing while the light is strong, everything once it is out.
        float danger = 0f;
        if (fuel) danger = Mathf.InverseLerp(0.55f, 0.1f, fuel.Fraction);
        if (vampires)
            danger = Mathf.Max(danger, Mathf.InverseLerp(threatDistance, threatDistance * 0.35f, vampires.NearestVampireDistance));
        alert = Mathf.MoveTowards(alert, danger, dt * 0.8f);

        var sword = SwordController.Instance;
        // A dead lantern lights nothing, so it drops to the hip - which also uncovers the locket
        // at the throat just as it starts pulsing for prayer.
        bool handedBack = (fuel && fuel.Depleted)
                          || controller.IsCrawling || controller.IsAirborne || controller.MovementLocked
                          || (prayer && prayer.IsActive)
                          || (sword && sword.HasSword && sword.CurrentStance != SwordController.Stance.Idle);
        float target = handedBack ? 0f
                     : Mathf.Lerp(calmWeight, 1f, alert) * (controller.IsRunning ? runWeight : 1f);
        weight = Mathf.MoveTowards(weight, target, blendSpeed * dt);
        if (weight <= 0.001f) return;

        Transform body = controller.transform;
        Vector3 offset = Vector3.Lerp(calmOffset, alertOffset, alert);
        Vector3 raised = upperArm.position + body.rotation * offset;
        TwoBoneIK.Solve(upperArm, forearm, hand, Vector3.Lerp(hand.position, raised, weight));
    }
}
