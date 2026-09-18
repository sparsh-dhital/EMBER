using System;
using UnityEngine;

// The heart of the game: lantern fuel drains over time, and the lantern's "light band"
// (Strong / Medium / Critical / Out) decides how brave the vampires are.
public class LanternFuel : MonoBehaviour
{
    [Header("Fuel")]
    public float maxFuel = 100f;
    [Tooltip("Fuel lost per second. 0.8 = a full lantern lasts about two minutes.")]
    public float drainPerSecond = 0.8f;
    [Tooltip("Turned on by the GameManager once a run starts.")]
    public bool Draining = true;

    [Header("Light Bands (fraction of max fuel)")]
    [Tooltip("Above this the light is Strong: vampires flee.")]
    [Range(0f, 1f)] public float strongAbove = 0.55f;
    [Tooltip("Below this the light is Critical: vampires attack.")]
    [Range(0f, 1f)] public float criticalBelow = 0.25f;
    [Tooltip("How far past a threshold fuel must go before the band changes back. Stops vampires flip-flopping.")]
    [Range(0f, 0.1f)] public float hysteresis = 0.03f;

    [Header("Warnings")]
    [Range(0f, 1f)] public float lowWarningAt = 0.25f;
    [Range(0f, 1f)] public float criticalWarningAt = 0.1f;

    public float CurrentFuel { get; private set; }
    public float Fraction => maxFuel > 0f ? CurrentFuel / maxFuel : 0f;
    public bool Depleted { get; private set; }
    public LightBand Band { get; private set; } = LightBand.Strong;

    public event Action<float> OnFuelChanged;
    public event Action OnFuelDepleted;
    public event Action<float> OnFuelAdded;
    public event Action OnRelit;
    public event Action<LightBand> OnBandChanged;
    public event Action<float> OnWarning;

    bool lowWarned, criticalWarned, outRaised;

    void Awake()
    {
        CurrentFuel = maxFuel;
    }

    void Start()
    {
        Notify();
    }

    void Update()
    {
        if (Depleted || !Draining) return;

        SetFuel(CurrentFuel - drainPerSecond * Time.deltaTime);
    }

    public void AddFuel(float amount)
    {
        if (amount <= 0f) return;
        bool wasOut = Depleted;
        SetFuel(CurrentFuel + amount);
        OnFuelAdded?.Invoke(amount);
        GameEvents.RaiseFuelCollected(amount);

        if (wasOut)
        {
            OnRelit?.Invoke();
            GameEvents.RaiseLanternRelit();
        }
    }

    // Fuel is always clamped, so the HUD can never show a negative or >100% value.
    void SetFuel(float value)
    {
        CurrentFuel = Mathf.Clamp(value, 0f, maxFuel);
        Depleted = CurrentFuel <= 0f;
        UpdateBand();
        UpdateWarnings();
        Notify();

        if (Depleted && Band == LightBand.Out && !outRaised)
        {
            outRaised = true;
            OnFuelDepleted?.Invoke();
            GameEvents.RaiseFuelEmpty();
        }
        if (!Depleted) outRaised = false;
    }

    void Notify()
    {
        OnFuelChanged?.Invoke(Fraction);
        GameEvents.RaiseFuelChanged(Fraction);
    }

    void UpdateBand()
    {
        float f = Fraction;
        LightBand next = Band;

        if (Depleted) next = LightBand.Out;
        else if (Band == LightBand.Out) next = f > strongAbove ? LightBand.Strong : f > criticalBelow ? LightBand.Medium : LightBand.Critical;
        else if (Band == LightBand.Strong && f < strongAbove - hysteresis) next = f < criticalBelow ? LightBand.Critical : LightBand.Medium;
        else if (Band == LightBand.Medium)
        {
            if (f > strongAbove + hysteresis) next = LightBand.Strong;
            else if (f < criticalBelow - hysteresis) next = LightBand.Critical;
        }
        else if (Band == LightBand.Critical && f > criticalBelow + hysteresis) next = f > strongAbove ? LightBand.Strong : LightBand.Medium;

        if (next == Band) return;
        Band = next;
        OnBandChanged?.Invoke(Band);
        GameEvents.RaiseLightBandChanged(Band);
    }

    void UpdateWarnings()
    {
        float f = Fraction;
        if (f > lowWarningAt + 0.05f) lowWarned = false;
        if (f > criticalWarningAt + 0.05f) criticalWarned = false;

        if (!Depleted && !criticalWarned && f <= criticalWarningAt)
        {
            criticalWarned = lowWarned = true;
            OnWarning?.Invoke(criticalWarningAt);
        }
        else if (!Depleted && !lowWarned && f <= lowWarningAt)
        {
            lowWarned = true;
            OnWarning?.Invoke(lowWarningAt);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Set Fuel 50%")] void DebugHalf() => SetFuel(maxFuel * 0.5f);
    [ContextMenu("Debug/Set Fuel 20%")] void DebugLow() => SetFuel(maxFuel * 0.2f);
    [ContextMenu("Debug/Set Fuel 1%")] void DebugEmpty() => SetFuel(maxFuel * 0.01f);
#endif
    public void DebugSetFraction(float fraction) => SetFuel(maxFuel * fraction);
}
