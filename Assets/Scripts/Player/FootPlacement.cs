using UnityEngine;

// Terrain-aware foot placement for the generic (non-Humanoid) rig, applied after the Animator each frame:
// each foot follows the ground under it, the hips drop for the lower foot, and feet tilt with the slope.
// It fades out while running, crawling or airborne so it never fights big motions.
[DefaultExecutionOrder(-10)]
public class FootPlacement : MonoBehaviour
{
    [Header("References")]
    public PlayerController controller;
    public Transform hips;
    public Transform thighL, shinL, footL, thighR, shinR, footR;

    [Header("Tuning")]
    public LayerMask groundLayers = ~0;
    [Tooltip("Largest distance the hips may lower to let a foot reach lower ground.")]
    public float maxHipDrop = 0.35f;
    [Tooltip("Largest distance a foot may be raised onto higher ground.")]
    public float maxFootLift = 0.4f;
    [Tooltip("IK fades out between these speeds (m/s).")]
    public float fadeStartSpeed = 2f, fadeEndSpeed = 4.5f;
    public float smoothing = 14f;
    [Range(0f, 1f)] public float footAlignment = 0.8f;

    float weight, hipOffset, liftL, liftR;
    Vector3 normalL = Vector3.up, normalR = Vector3.up;

    void LateUpdate()
    {
        if (!controller || !hips || !footL || !footR) return;
        float dt = Time.deltaTime;

        bool planted = controller.IsGrounded && !controller.IsCrawling && controller.ControlEnabled;
        float target = planted ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStartSpeed, fadeEndSpeed, controller.HorizontalSpeed)) : 0f;
        weight = Mathf.MoveTowards(weight, target, dt * 4f);
        float k = 1f - Mathf.Exp(-smoothing * dt);

        float rootY = controller.transform.position.y;
        float groundL = GroundUnder(footL.position, ref normalL, rootY);
        float groundR = GroundUnder(footR.position, ref normalR, rootY);
        liftL = Mathf.Lerp(liftL, Mathf.Clamp(groundL - rootY, -maxHipDrop, maxFootLift) * weight, k);
        liftR = Mathf.Lerp(liftR, Mathf.Clamp(groundR - rootY, -maxHipDrop, maxFootLift) * weight, k);
        hipOffset = Mathf.Lerp(hipOffset, Mathf.Min(liftL, liftR, 0f), k);
        if (weight < 0.001f && Mathf.Abs(hipOffset) < 0.001f) return;

        Vector3 targetL = footL.position + Vector3.up * liftL;
        Vector3 targetR = footR.position + Vector3.up * liftR;
        hips.position += Vector3.up * hipOffset;

        TwoBoneIK.Solve(thighL, shinL, footL, targetL);
        TwoBoneIK.Solve(thighR, shinR, footR, targetR);
        AlignFoot(footL, normalL);
        AlignFoot(footR, normalR);
    }

    float GroundUnder(Vector3 foot, ref Vector3 normal, float fallback)
    {
        if (Physics.Raycast(foot + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.3f, groundLayers, QueryTriggerInteraction.Ignore)
            && Vector3.Angle(hit.normal, Vector3.up) < 50f)
        {
            normal = hit.normal;
            return hit.point.y;
        }
        normal = Vector3.up;
        return fallback;
    }

    void AlignFoot(Transform foot, Vector3 normal)
    {
        Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
        foot.rotation = Quaternion.Slerp(foot.rotation, tilt * foot.rotation, footAlignment * weight);
    }
}
