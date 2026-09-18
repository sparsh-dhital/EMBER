using UnityEngine;
using UnityEngine.EventSystems;

// Floating virtual joystick: touch anywhere in its zone (bottom-left) and the stick appears under your thumb.
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("The ring that appears where you touch.")]
    public RectTransform stickBase;
    public RectTransform knob;
    [Tooltip("How far (in canvas units) the knob can travel from the centre.")]
    public float radius = 90f;
    [Range(0f, 0.5f)] public float deadZone = 0.08f;
    public CanvasGroup visuals;
    [Range(0f, 1f)] public float idleAlpha = 0.35f;

    RectTransform zone;
    Vector2 restPosition;
    int pointerId = int.MinValue;

    void Awake()
    {
        zone = (RectTransform)transform;
        if (stickBase) restPosition = stickBase.anchoredPosition;
        if (visuals) visuals.alpha = idleAlpha;
    }

    void OnDisable() => Release();

    public void OnPointerDown(PointerEventData e)
    {
        if (pointerId != int.MinValue) return;
        pointerId = e.pointerId;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out Vector2 local))
        {
            stickBase.anchoredPosition = ClampInsideZone(local);
        }
        if (visuals) visuals.alpha = 1f;
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, e.position, e.pressEventCamera, out Vector2 local)) return;

        Vector2 offset = Vector2.ClampMagnitude(local, radius);
        knob.anchoredPosition = offset;

        Vector2 value = offset / radius;
        float mag = value.magnitude;
        value = mag < deadZone ? Vector2.zero : value.normalized * Mathf.InverseLerp(deadZone, 1f, mag);
        InputReader.Instance.SetTouchMove(value);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == pointerId) Release();
    }

    void Release()
    {
        pointerId = int.MinValue;
        if (knob) knob.anchoredPosition = Vector2.zero;
        if (stickBase) stickBase.anchoredPosition = restPosition;
        if (visuals) visuals.alpha = idleAlpha;
        if (InputReader.Exists) InputReader.Instance.SetTouchMove(Vector2.zero);
    }

    Vector2 ClampInsideZone(Vector2 local)
    {
        Rect r = zone.rect;
        float m = radius * 0.8f;
        return new Vector2(Mathf.Clamp(local.x, r.xMin + m, r.xMax - m), Mathf.Clamp(local.y, r.yMin + m, r.yMax - m));
    }
}
