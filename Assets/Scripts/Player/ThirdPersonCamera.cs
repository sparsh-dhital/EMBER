using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 9.5f;
    public float height = 7.2f;
    public float followSharpness = 3.2f;
    public float lookSharpness = 6.5f;

    Vector3 lookCurrent;
    bool initialized;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 flatForward = target.forward; flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward; else flatForward.Normalize();

        Vector3 desiredPos = target.position - flatForward * distance + Vector3.up * height;
        Vector3 lookTarget = target.position + Vector3.up * 1.3f;

        if (!initialized)
        {
            transform.position = desiredPos;
            lookCurrent = lookTarget;
            initialized = true;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            lookCurrent = Vector3.Lerp(lookCurrent, lookTarget, 1f - Mathf.Exp(-lookSharpness * Time.deltaTime));
        }

        transform.LookAt(lookCurrent);
    }
}
