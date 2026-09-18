using UnityEngine;

// Title-screen camera: a slow, gentle sweep across the front of the radio centre.
public class MenuCameraOrbit : MonoBehaviour
{
    public Transform centre;
    public float radius = 13f;
    public float height = 4.2f;
    public float lookHeight = 2f;
    [Tooltip("Direction the sweep is centred on (180 = looking at the shack's lit front from the north).")]
    public float centreAngle = 180f;
    [Tooltip("How far (degrees) the camera drifts either side.")]
    public float sweep = 30f;
    [Tooltip("Seconds for one full back-and-forth sweep.")]
    public float period = 60f;

    void LateUpdate()
    {
        if (!centre) return;
        float angle = centreAngle + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period) * sweep;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -radius);
        transform.position = centre.position + offset + Vector3.up * height;
        transform.LookAt(centre.position + Vector3.up * lookHeight);
    }
}
