using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 60f;
    public float airControl = 0.35f;
    public float groundDamping = 9f;
    public float airDamping = 1.2f;
    public float gravity = -26f;
    public float jumpSpeed = 9.2f;
    public float coyoteTime = 0.12f;
    public float turnSpeed = 12f;

    [Header("Input")]
    public InputActionAsset inputActions;

    CharacterController controller;
    InputAction moveAction;
    InputAction jumpAction;

    Vector3 velocity;
    float timeSinceGrounded = 99f;
    bool jumpQueued;

    public bool IsGrounded { get; private set; }
    public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        var map = inputActions.FindActionMap("Player", true);
        moveAction = map.FindAction("Move", true);
        jumpAction = map.FindAction("Jump", true);
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        jumpAction.performed += OnJump;
    }

    void OnDisable()
    {
        jumpAction.performed -= OnJump;
        moveAction.Disable();
        jumpAction.Disable();
    }

    void OnJump(InputAction.CallbackContext ctx) => jumpQueued = true;

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Transform cam = Camera.main ? Camera.main.transform : transform;
        Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
        Vector3 desiredDir = camForward * input.y + camRight * input.x;
        if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

        IsGrounded = controller.isGrounded;
        timeSinceGrounded = IsGrounded ? 0f : timeSinceGrounded + Time.deltaTime;

        Vector3 desiredVel = desiredDir * moveSpeed;
        float control = IsGrounded ? 1f : airControl;
        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
        horizVel = Vector3.MoveTowards(horizVel, desiredVel, acceleration * control * Time.deltaTime);

        if (desiredDir.sqrMagnitude < 0.01f)
        {
            float damp = IsGrounded ? groundDamping : airDamping;
            horizVel *= Mathf.Max(0f, 1f - damp * Time.deltaTime);
        }

        velocity.x = horizVel.x;
        velocity.z = horizVel.z;

        if (IsGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        if (jumpQueued)
        {
            if (IsGrounded || timeSinceGrounded < coyoteTime)
            {
                velocity.y = jumpSpeed;
                timeSinceGrounded = 99f;
            }
            jumpQueued = false;
        }

        controller.Move(velocity * Time.deltaTime);

        if (desiredDir.sqrMagnitude > 0.01f)
        {
            float yaw = Mathf.Atan2(desiredDir.x, desiredDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), turnSpeed * Time.deltaTime);
        }
    }
}
