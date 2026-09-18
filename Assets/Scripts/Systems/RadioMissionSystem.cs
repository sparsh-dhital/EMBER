using UnityEngine;

// Tracks the radio parts: "RADIO PARTS 3/5", the current objective, and which part is closest.
public class RadioMissionSystem : MonoBehaviour
{
    [Tooltip("Leave empty to use every RadioPart in the scene.")]
    public RadioPart[] parts;

    public int Required => parts.Length;
    public int Collected { get; private set; }
    public bool AllCollected => Collected >= Required;

    void Awake()
    {
        if (parts == null || parts.Length == 0) parts = FindObjectsByType<RadioPart>(FindObjectsInactive.Include);
        foreach (var p in parts) p.mission = this;
    }

    public void Collect(RadioPart part)
    {
        Collected = Mathf.Min(Collected + 1, Required);
        GameEvents.RaiseRadioPartCollected(Collected, Required);

        if (AllCollected)
        {
            GameEvents.ShowMessage("ALL PARTS FOUND", "Return to the radio centre", 4f);
            GameEvents.RaiseAllRadioPartsCollected();
        }
        else
        {
            GameEvents.ShowMessage("RADIO PART " + Collected + "/" + Required, part.partName, 2.5f);
        }
    }

    // Used by the HUD's signal meter: how close is the nearest part you still need?
    public RadioPart NearestRemaining(Vector3 position, out float distance)
    {
        RadioPart best = null;
        distance = float.MaxValue;
        foreach (var p in parts)
        {
            if (!p || p.Collected) continue;
            float d = Vector3.Distance(position, p.transform.position);
            if (d < distance) { distance = d; best = p; }
        }
        return best;
    }
}
