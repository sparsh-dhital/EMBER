using UnityEngine;

// Lets the lantern hang from the hand like a real pendulum.
// Put this on the lantern object whose pivot is at the top of its handle, parented to the hand.
public class LanternSway : MonoBehaviour
{
    [Tooltip("Usually the player root: the lantern turns with the body but keeps hanging straight down.")]
    public Transform yawReference;
    public float stiffness = 55f;
    public float damping = 6f;
    [Tooltip("How strongly hand acceleration swings the lantern.")]
    public float inertia = 1f;
    public float maxAngle = 40f;

    Vector3 lastPos, lastVel;
    Vector2 angle, angularVelocity;

    void OnEnable()
    {
        lastPos = transform.position;
        lastVel = Vector3.zero;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.parent ? transform.parent.position : transform.position;
        Vector3 vel = (pos - lastPos) / dt;
        Vector3 accel = Vector3.ClampMagnitude((vel - lastVel) / dt, 40f);
        lastPos = pos;
        lastVel = vel;

        float g = Mathf.Abs(Physics.gravity.y);
        Vector2 target = new Vector2(
            Mathf.Atan2(accel.z, g) * Mathf.Rad2Deg,
            -Mathf.Atan2(accel.x, g) * Mathf.Rad2Deg) * inertia;

        angularVelocity += (target - angle) * stiffness * dt;
        angularVelocity *= Mathf.Exp(-damping * dt);
        angle += angularVelocity * dt;
        angle = Vector2.ClampMagnitude(angle, maxAngle);

        float yaw = yawReference ? yawReference.eulerAngles.y : 0f;
        transform.rotation = Quaternion.AngleAxis(angle.x, Vector3.right)
                           * Quaternion.AngleAxis(angle.y, Vector3.forward)
                           * Quaternion.Euler(0f, yaw, 0f);
    }
}
