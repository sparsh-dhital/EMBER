using UnityEngine;

public class LanternLight : MonoBehaviour
{
    public LanternFuel fuel;
    public Light lanternLight;
    public float baseRange = 5f;
    public float bonusRange = 9f;
    public float baseIntensity = 5f;
    public float flickerAmount = 0.06f;

    void Reset()
    {
        lanternLight = GetComponent<Light>();
    }

    void OnEnable()
    {
        if (fuel) fuel.OnFuelChanged += HandleFuelChanged;
    }

    void OnDisable()
    {
        if (fuel) fuel.OnFuelChanged -= HandleFuelChanged;
    }

    void HandleFuelChanged(float pct)
    {
        if (!lanternLight) return;
        lanternLight.range = baseRange + pct * bonusRange;
        float flicker = 1f + (Random.value - 0.5f) * flickerAmount;
        lanternLight.intensity = baseIntensity * (0.6f + 0.4f * pct) * flicker;
    }
}
