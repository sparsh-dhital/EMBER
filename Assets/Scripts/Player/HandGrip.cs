using UnityEngine;

// Closes the realistic survivor's fingers around what he carries: the lantern bail in the
// left hand and the sword grip in the right. The motion capture has no finger data, so
// without this the hands hang open around the handles.
//
// Runs after the Animator. Each finger joint is turned about the hand's own knuckle axis,
// worked out once from the bind pose, so it holds whatever the arm is doing.
[DefaultExecutionOrder(50)]
public class HandGrip : MonoBehaviour
{
    public Animator animator;
    [Range(0f, 1f)] public float leftGrip = 0.85f;
    [Range(0f, 1f)] public float rightGrip = 0.95f;
    [Tooltip("Curl, in degrees at full grip, for the proximal, middle and distal joints.")]
    public Vector3 curl = new Vector3(55f, 70f, 40f);
    public float thumbCurl = 30f;

    struct Hand
    {
        public Transform hand;
        public Transform[] joints;      // index/middle/ring/little x proximal/intermediate/distal
        public Transform[] thumb;
        public Vector3 knuckleAxisLocal; // in hand space: rotating about it closes the fingers
    }

    Hand left, right;
    bool ready;

    static readonly HumanBodyBones[] LeftFingers =
    {
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
    };
    static readonly HumanBodyBones[] RightFingers =
    {
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
    };

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator || !animator.isHuman) return;
        // Awake runs before the first animator update, so the bones are still in the bind pose:
        // a T-pose with the palms facing down.
        left = Build(HumanBodyBones.LeftHand, LeftFingers,
                     new[] { HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal });
        right = Build(HumanBodyBones.RightHand, RightFingers,
                      new[] { HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal });
        ready = left.hand && right.hand;
    }

    Hand Build(HumanBodyBones handBone, HumanBodyBones[] fingers, HumanBodyBones[] thumb)
    {
        var h = new Hand { hand = animator.GetBoneTransform(handBone) };
        if (!h.hand) return h;
        h.joints = new Transform[fingers.Length];
        for (int i = 0; i < fingers.Length; i++) h.joints[i] = animator.GetBoneTransform(fingers[i]);
        h.thumb = new Transform[thumb.Length];
        for (int i = 0; i < thumb.Length; i++) h.thumb[i] = animator.GetBoneTransform(thumb[i]);

        var middle = animator.GetBoneTransform(handBone == HumanBodyBones.LeftHand
            ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        Vector3 fingerDir = middle ? (middle.position - h.hand.position).normalized : h.hand.up;
        Vector3 palm = Vector3.down;
        Vector3 axis = Vector3.Cross(fingerDir, palm).normalized;
        // Pick the sign that actually swings the fingertips toward the palm.
        if (Vector3.Dot(Quaternion.AngleAxis(30f, axis) * fingerDir, palm) < 0f) axis = -axis;
        h.knuckleAxisLocal = h.hand.InverseTransformDirection(axis);
        return h;
    }

    void LateUpdate()
    {
        if (!ready) return;
        Close(left, leftGrip);
        Close(right, rightGrip);
    }

    void Close(Hand h, float grip)
    {
        if (grip <= 0.001f) return;
        Vector3 axis = h.hand.TransformDirection(h.knuckleAxisLocal);
        for (int i = 0; i < h.joints.Length; i++)
        {
            var j = h.joints[i];
            if (!j) continue;
            float deg = (i % 3 == 0 ? curl.x : i % 3 == 1 ? curl.y : curl.z) * grip;
            j.rotation = Quaternion.AngleAxis(deg, axis) * j.rotation;
        }
        foreach (var t in h.thumb)
            if (t) t.rotation = Quaternion.AngleAxis(thumbCurl * grip, axis) * t.rotation;
    }
}
