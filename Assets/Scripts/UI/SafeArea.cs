using UnityEngine;

// Keeps its RectTransform inside the phone's safe area (notches, rounded corners, gesture bars).
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rect;
    Rect applied;
    Vector2Int appliedScreen;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != applied || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y) Apply();
    }

    void Apply()
    {
        applied = Screen.safeArea;
        appliedScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        // Clamp to the screen: some editors/simulators report a safe area larger than the view.
        Vector2 min = Vector2.Max(applied.position, Vector2.zero);
        Vector2 max = Vector2.Min(applied.position + applied.size, new Vector2(Screen.width, Screen.height));
        if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = new Vector2(Screen.width, Screen.height); }
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
