using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The one place gameplay reads input from.
// Keyboard, mouse and gamepad come from the Input System actions asset.
// On-screen touch controls (joystick, look area, buttons) push their values in here,
// so the player, camera and prayer code never care which device is being used.
[DefaultExecutionOrder(-100)]
public class InputReader : MonoBehaviour
{
    static InputReader instance;
    public static bool Exists => instance;
    public static InputReader Instance
    {
        get
        {
            if (!instance)
            {
                instance = FindAnyObjectByType<InputReader>();
                if (!instance) instance = new GameObject("InputReader").AddComponent<InputReader>();
            }
            return instance;
        }
    }

    [Header("Actions")]
    [Tooltip("Leave empty to use the project-wide actions (Edit > Project Settings > Input System Package).")]
    public InputActionAsset actions;

    [Header("Look Sensitivity")]
    [Tooltip("Degrees of camera rotation per pixel of mouse movement.")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("Degrees of camera rotation per pixel of touch drag.")]
    public float touchSensitivity = 0.2f;
    [Tooltip("Degrees per second at full gamepad stick deflection.")]
    public float gamepadSensitivity = 150f;
    public bool invertY;

    [Header("Touch")]
    [Tooltip("Touch joystick deflection above this makes the character run.")]
    [Range(0.5f, 1f)] public float touchRunThreshold = 0.85f;

    InputAction moveAction, interactAction, prayAction, pauseAction, sprintAction;

    Vector2 touchMove;
    Vector2 touchLookPixels;
    bool touchInteractQueued, touchPrayQueued, touchPauseQueued;

    public Vector2 Move { get; private set; }
    public Vector2 LookDegrees { get; private set; }
    public bool Sprint { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool PrayPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool HasLookInput => LookDegrees.sqrMagnitude > 0.0001f;

    public bool GameplayLookEnabled { get; set; } = true;

    void Awake()
    {
        if (instance && instance != this) { Destroy(this); return; }
        instance = this;

        if (!actions) actions = InputSystem.actions;
        var map = actions.FindActionMap("Player", true);
        moveAction = map.FindAction("Move", true);
        interactAction = map.FindAction("Interact", true);
        prayAction = map.FindAction("Pray", true);
        pauseAction = map.FindAction("Pause", true);
        sprintAction = map.FindAction("Sprint", true);
    }

    void OnEnable() => actions?.FindActionMap("Player")?.Enable();
    void OnDisable() => actions?.FindActionMap("Player")?.Disable();

    // ----- called by the on-screen touch controls -----
    public void SetTouchMove(Vector2 value) => touchMove = Vector2.ClampMagnitude(value, 1f);
    public void AddTouchLook(Vector2 pixels) => touchLookPixels += pixels;
    public void PressTouchInteract() => touchInteractQueued = true;
    public void PressTouchPray() => touchPrayQueued = true;
    public void PressTouchPause() => touchPauseQueued = true;

    void Update()
    {
        Vector2 keyMove = moveAction.ReadValue<Vector2>();
        Move = touchMove.sqrMagnitude > keyMove.sqrMagnitude ? touchMove : Vector2.ClampMagnitude(keyMove, 1f);
        Sprint = sprintAction.IsPressed() || touchMove.magnitude > touchRunThreshold;

        InteractPressed = interactAction.WasPressedThisFrame() || touchInteractQueued;
        PrayPressed = prayAction.WasPressedThisFrame() || touchPrayQueued;
        PausePressed = pauseAction.WasPressedThisFrame() || touchPauseQueued;
        touchInteractQueued = touchPrayQueued = touchPauseQueued = false;

        LookDegrees = GameplayLookEnabled ? ReadLook() : Vector2.zero;
        touchLookPixels = Vector2.zero;
    }

    Vector2 ReadLook()
    {
        Vector2 look = touchLookPixels * touchSensitivity;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool dragging = (mouse.rightButton.isPressed || mouse.leftButton.isPressed) && !PointerOverUI();
            if (locked || dragging) look += mouse.delta.ReadValue() * mouseSensitivity;
        }

        var pad = Gamepad.current;
        if (pad != null) look += pad.rightStick.ReadValue() * gamepadSensitivity * Time.unscaledDeltaTime;

        if (invertY) look.y = -look.y;
        return look;
    }

    static bool PointerOverUI()
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }

    public static bool IsTouchDevice => Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);

    public static void SetCursorLocked(bool locked)
    {
        if (IsTouchDevice) return;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
