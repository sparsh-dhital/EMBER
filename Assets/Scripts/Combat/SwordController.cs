using UnityEngine;

// The single sword the player can carry (filled in with the combat pass).
public class SwordController : MonoBehaviour
{
    static SwordController instance;
    public static bool Armed => instance && instance.HasSword;

    public bool HasSword { get; private set; }

    void Awake() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }
}
