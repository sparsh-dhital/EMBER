using UnityEngine;
using UnityEngine.EventSystems;

// An on-screen button that fires the moment it's touched (not on release), for responsive mobile play.
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum Action { Interact, Pray, Pause }
    public Action action;
    [Tooltip("Scaled down slightly while held.")]
    public RectTransform pressVisual;

    public void OnPointerDown(PointerEventData e)
    {
        var input = InputReader.Instance;
        switch (action)
        {
            case Action.Interact: input.PressTouchInteract(); break;
            case Action.Pray: input.PressTouchPray(); break;
            case Action.Pause: input.PressTouchPause(); break;
        }
        if (pressVisual) pressVisual.localScale = Vector3.one * 0.92f;
        Haptics.Pulse(0.15f, 0.03f);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (pressVisual) pressVisual.localScale = Vector3.one;
    }
}
