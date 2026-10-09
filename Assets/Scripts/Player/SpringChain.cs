using UnityEngine;

// Secondary motion for loose cloth on the procedural rig: scarf tails and the coat-skirt panels.
// Each bone in the chain chases its animated rest pose through a damped verlet particle at its
// tip, so the cloth lags behind sudden moves, swings with the stride and settles again.
//
// The bones are not animated by any clip, so the rest pose is captured once and restored every
// frame before simulating. Leg and torso spheres push the particles out, which is what makes a
// striding thigh kick the coat flap forward instead of passing straight through it.
[DefaultExecutionOrder(100)]   // after the Animator and the arm/foot IK have posed the body
public class SpringChain : MonoBehaviour
{
    [System.Serializable]
    public struct Sphere
    {
        public Transform centre;
        public float radius;
    }

    [Tooltip("Bones from the root outwards. Each one swings about its own pivot.")]
    public Transform[] bones;
    [Tooltip("Length of each bone, pivot to tip, in metres.")]
    public float[] lengths;

    [Range(0f, 1f)] public float stiffness = 0.12f;
    [Range(0f, 1f)] public float damping = 0.18f;
    [Tooltip("Fraction of world gravity applied on top of the rest pose.")]
    public float gravity = 0.6f;
    [Tooltip("Furthest any bone may swing from its rest direction, in degrees.")]
    public float maxAngle = 55f;
    public Sphere[] colliders;

    Quaternion[] restLocal;
    Vector3[] tip, prevTip;
    bool primed;

    void Awake()
    {
        int n = bones != null ? bones.Length : 0;
        restLocal = new Quaternion[n];
        tip = new Vector3[n];
        prevTip = new Vector3[n];
        for (int i = 0; i < n; i++) restLocal[i] = bones[i].localRotation;
    }

    // Teleports and scene reloads would otherwise fling the cloth across the map.
    void OnEnable() => primed = false;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || restLocal == null) return;
        Vector3 g = Physics.gravity * (gravity * dt * dt);

        for (int i = 0; i < bones.Length; i++)
        {
            var b = bones[i];
            b.localRotation = restLocal[i];
            Vector3 pivot = b.position;
            Vector3 restDir = b.rotation * Vector3.down;
            float len = lengths[i];
            Vector3 restTip = pivot + restDir * len;

            if (!primed) { tip[i] = prevTip[i] = restTip; }

            // Verlet step: carry momentum, fall, and ease back toward the animated pose.
            Vector3 p = tip[i];
            Vector3 v = (p - prevTip[i]) * (1f - damping);
            prevTip[i] = p;
            p += v + g;
            p += (restTip - p) * stiffness;

            if (colliders != null)
                foreach (var c in colliders)
                {
                    if (!c.centre) continue;
                    Vector3 d = p - c.centre.position;
                    float r = c.radius + 0.01f;
                    if (d.sqrMagnitude < r * r) p = c.centre.position + d.normalized * r;
                }

            // Keep the cloth its own length, and inside its swing limit.
            Vector3 dir = p - pivot;
            if (dir.sqrMagnitude < 1e-8f) dir = restDir;
            dir = Vector3.RotateTowards(restDir, dir.normalized,
                                        maxAngle * Mathf.Deg2Rad, 0f);
            p = pivot + dir * len;
            tip[i] = p;

            b.rotation = Quaternion.FromToRotation(restDir, dir) * b.rotation;
        }
        primed = true;
    }
}
