using UnityEngine;
using UnityEngine.UI;

public class FuelHUD : MonoBehaviour
{
    public LanternFuel fuel;
    public Image fillImage;
    public Text label;

    void OnEnable()
    {
        if (fuel) fuel.OnFuelChanged += HandleChanged;
    }

    void OnDisable()
    {
        if (fuel) fuel.OnFuelChanged -= HandleChanged;
    }

    void HandleChanged(float pct)
    {
        if (fillImage) fillImage.fillAmount = pct;
        if (label) label.text = "FUEL " + Mathf.RoundToInt(pct * 100f) + "%";
    }
}
