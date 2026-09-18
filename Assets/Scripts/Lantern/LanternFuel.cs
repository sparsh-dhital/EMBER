using System;
using UnityEngine;

public class LanternFuel : MonoBehaviour
{
    public float maxFuel = 100f;
    public float drainPerSecond = 100f / 110f;

    public float CurrentFuel { get; private set; }
    public float Fraction => CurrentFuel / maxFuel;
    public bool Depleted { get; private set; }

    public event Action<float> OnFuelChanged;
    public event Action OnFuelDepleted;

    void Awake()
    {
        CurrentFuel = maxFuel;
    }

    void Start()
    {
        OnFuelChanged?.Invoke(Fraction);
    }

    void Update()
    {
        if (Depleted) return;

        CurrentFuel = Mathf.Max(0f, CurrentFuel - drainPerSecond * Time.deltaTime);
        OnFuelChanged?.Invoke(Fraction);

        if (CurrentFuel <= 0f)
        {
            Depleted = true;
            OnFuelDepleted?.Invoke();
        }
    }

    public void AddFuel(float amount)
    {
        if (Depleted) return;
        CurrentFuel = Mathf.Min(maxFuel, CurrentFuel + amount);
        OnFuelChanged?.Invoke(Fraction);
    }
}
