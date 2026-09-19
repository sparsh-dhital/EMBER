using Unity.Cinemachine;
using UnityEngine;

// Drives the Cinemachine third-person camera from InputReader (mouse, touch drag or gamepad),
// and adds the game-feel touches: gentle auto-recenter, run FOV, prayer push-in and camera shake.
public class CameraController : MonoBehaviour
{
    static CameraController instance;

    [Header("References")]
    public CinemachineCamera cinemachineCamera;
    public CinemachineOrbitalFollow orbit;
    public CinemachineBasicMultiChannelPerlin noise;
    public PlayerController player;
    public LanternFuel fuel;

    [Header("Orbit")]
    public float minPitch = -8f;
    public float maxPitch = 60f;
    public float startPitch = 17f;
    [Tooltip("Smooths raw look input. Higher = snappier.")]
    public float lookSharpness = 22f;

    [Header("Auto Recenter")]
    [Tooltip("Seconds without look input before the camera slowly swings behind the moving player.")]
    public float recenterDelay = 3f;
    public float recenterSpeed = 45f;

    [Header("Lens")]
    public float baseFov = 50f;
    public float runFov = 55f;
    public float pushInFov = 41f;
    public float fovSharpness = 3f;

    [Header("Shake / Handheld")]
    public float idleNoise = 0.25f;
    [Tooltip("Extra handheld sway as the lantern dies — nerves.")]
    public float lowFuelNoise = 0.7f;
    public float shakeDecay = 2.5f;

    Vector2 smoothedLook;
    float lastLookTime;
    float shake;
    float pushInTimer, pushInDuration;

    void Awake()
    {
        instance = this;
        if (!cinemachineCamera) cinemachineCamera = GetComponent<CinemachineCamera>();
        if (!orbit) orbit = GetComponent<CinemachineOrbitalFollow>();
        if (!noise) noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    void Start()
    {
        if (orbit)
        {
            orbit.VerticalAxis.Value = startPitch;
            if (player) orbit.HorizontalAxis.Value = player.transform.eulerAngles.y;
        }
    }

    public static void Shake(float amount)
    {
        if (instance) instance.shake = Mathf.Max(instance.shake, amount);
    }

    // Slow cinematic push-in, used when prayer starts.
    public static void PushIn(float seconds)
    {
        if (!instance) return;
        instance.pushInDuration = seconds;
        instance.pushInTimer = seconds;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        bool active = GameManager.Instance == null || GameManager.Instance.IsGameplayActive;

        Vector2 look = active ? InputReader.Instance.LookDegrees : Vector2.zero;
        if (look.sqrMagnitude > 0.0001f) lastLookTime = Time.unscaledTime;
        smoothedLook = Vector2.Lerp(smoothedLook, look, 1f - Mathf.Exp(-lookSharpness * dt));

        if (orbit && active)
        {
            orbit.HorizontalAxis.Value = Mathf.Repeat(orbit.HorizontalAxis.Value + smoothedLook.x + 180f, 360f) - 180f;
            orbit.VerticalAxis.Value = Mathf.Clamp(orbit.VerticalAxis.Value - smoothedLook.y, minPitch, maxPitch);
            Recenter(Time.deltaTime);
        }

        UpdateLens(dt);
        UpdateNoise(dt);
    }

    void Recenter(float dt)
    {
        if (!player || Time.unscaledTime - lastLookTime < recenterDelay) return;
        if (player.HorizontalSpeed < 1f) return;

        // Compare against where the camera actually looks (the framing offset turns it a few degrees away from the
        // orbit angle). Comparing against the orbit angle made the camera chase its own offset and slowly spin.
        var cam = Camera.main;
        if (!cam) return;
        Vector3 view = cam.transform.forward;
        float cameraYaw = Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg;
        float diff = Mathf.DeltaAngle(cameraYaw, player.transform.eulerAngles.y);
        // Only ease in behind a player walking roughly away from the camera; strafing or walking toward it never spins it.
        if (Mathf.Abs(diff) > 60f) return;
        orbit.HorizontalAxis.Value += Mathf.Clamp(diff, -recenterSpeed * dt, recenterSpeed * dt);
    }

    void UpdateLens(float dt)
    {
        if (!cinemachineCamera) return;

        float target = baseFov;
        if (player && player.IsRunning && player.HorizontalSpeed > player.walkSpeed) target = runFov;
        if (pushInTimer > 0f)
        {
            pushInTimer -= Time.deltaTime;
            float t = 1f - pushInTimer / Mathf.Max(0.01f, pushInDuration);
            target = Mathf.Lerp(pushInFov, baseFov, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f)));
        }

        var lens = cinemachineCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, target, 1f - Mathf.Exp(-fovSharpness * dt));
        cinemachineCamera.Lens = lens;
    }

    void UpdateNoise(float dt)
    {
        if (!noise) return;
        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * dt);
        float fear = fuel ? Mathf.Clamp01(1f - fuel.Fraction / 0.3f) : 0f;
        noise.AmplitudeGain = idleNoise + fear * lowFuelNoise + shake * 2.5f;
        noise.FrequencyGain = 1f + shake * 2f + fear * 0.5f;
    }
}
