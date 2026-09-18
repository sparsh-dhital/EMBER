using UnityEngine;
using UnityEngine.EventSystems;

// Drag anywhere on the right side of the screen to turn the camera.
public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    int pointerId = int.MinValue;

    public void OnPointerDown(PointerEventData e)
    {
        if (pointerId == int.MinValue) pointerId = e.pointerId;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        // Normalise to a 1080p-tall screen so the feel is the same on every phone.
        float scale = 1080f / Mathf.Max(1, Screen.height);
        InputReader.Instance.AddTouchLook(e.delta * scale);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == pointerId) pointerId = int.MinValue;
    }

    void OnDisable() => pointerId = int.MinValue;
}
