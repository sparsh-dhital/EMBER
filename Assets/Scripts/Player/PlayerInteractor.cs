using UnityEngine;

// Anything the player can use with the Interact button (radio parts, the locket, the radio).
public interface IInteractable
{
    Transform transform { get; }
    // Text for the prompt, e.g. "Take radio part". Return null to hide the prompt.
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact(PlayerInteractor player);
}

// Finds the nearest interactable around the player and uses it when Interact is pressed.
public class PlayerInteractor : MonoBehaviour
{
    public float radius = 2.2f;
    public LayerMask interactableLayers;
    [Tooltip("Seconds between searches. Searching every frame is unnecessary.")]
    public float scanInterval = 0.1f;

    public IInteractable Current { get; private set; }
    public PlayerController Controller { get; private set; }
    public PlayerAnimator Animator { get; private set; }

    readonly Collider[] hits = new Collider[8];
    float nextScan;

    void Awake()
    {
        Controller = GetComponent<PlayerController>();
        Animator = GetComponentInChildren<PlayerAnimator>();
    }

    void Update()
    {
        bool active = GameManager.Instance == null || GameManager.Instance.IsGameplayActive;
        if (!active || (Controller && !Controller.ControlEnabled)) { Current = null; return; }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            Current = FindNearest();
        }

        if (Current != null && InputReader.Instance.InteractPressed)
        {
            if (Current.CanInteract)
            {
                AudioManager.Play(Sfx.Interact);
                Current.Interact(this);
                nextScan = 0f;
            }
            else
            {
                AudioManager.Play(Sfx.Denied);
            }
        }
    }

    IInteractable FindNearest()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, hits, interactableLayers, QueryTriggerInteraction.Collide);
        IInteractable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var it = hits[i].GetComponentInParent<IInteractable>();
            if (it == null || it.Prompt == null) continue;
            float d = (hits[i].transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }
}
