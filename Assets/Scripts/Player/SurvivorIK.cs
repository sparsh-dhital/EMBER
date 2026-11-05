using UnityEngine;

// Humanoid IK touches for the realistic survivor. While he kneels to pray, both hands are
// drawn together in front of the chest; the kneeling capture underneath was recorded
// reaching for someone, and its hands would otherwise wander.
// Needs the IK pass enabled on the base layer of the controller.
public class SurvivorIK : MonoBehaviour
{
    public Animator animator;
    public float blendSpeed = 3f;
    [Tooltip("Where the hands meet, from the upper chest: forward, up.")]
    public Vector2 clasp = new Vector2(0.27f, -0.02f);

    static readonly int PrayId = Animator.StringToHash("Pray");
    float pray;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layer)
    {
        if (layer != 0 || !animator || !animator.isHuman) return;
        pray = Mathf.MoveTowards(pray, animator.GetBool(PrayId) ? 1f : 0f, blendSpeed * Time.deltaTime);
        if (pray <= 0.001f)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        if (!chest) return;
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Vector3 at = chest.position + fwd * clasp.x + Vector3.up * clasp.y;
        float w = Mathf.SmoothStep(0f, 1f, pray);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, at - side * 0.02f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, at + side * 0.02f);
    }
}
