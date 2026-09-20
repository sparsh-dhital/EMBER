using UnityEngine;
using UnityEngine.EventSystems;

// An on-screen button that fires the moment it's touched (not on release), for responsive mobile play.
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // Block is the odd one out: it is held rather than tapped, so it pushes a state
    // on press and clears it on release instead of firing a one-shot.
    public enum Action { Interact, Pray, Pause, Jump, Crawl, ToggleCamera, Attack, Block }
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
            case Action.Jump: input.PressTouchJump(); break;
            case Action.Crawl: input.PressTouchCrawl(); break;
            case Action.ToggleCamera: input.PressTouchToggleCamera(); break;
            case Action.Attack: input.PressTouchAttack(); break;
            case Action.Block: input.HoldTouchBlock(true); break;
        }
        if (pressVisual) pressVisual.localScale = Vector3.one * 0.92f;
        Haptics.Pulse(0.15f, 0.03f);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (action == Action.Block) InputReader.Instance.HoldTouchBlock(false);
        if (pressVisual) pressVisual.localScale = Vector3.one;
    }

    // A finger sliding off the button, or the UI being hidden mid-press, must not
    // leave the guard stuck up.
    void OnDisable()
    {
        if (action == Action.Block && InputReader.Exists) InputReader.Instance.HoldTouchBlock(false);
        if (pressVisual) pressVisual.localScale = Vector3.one;
    }
}
