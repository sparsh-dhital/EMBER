using UnityEngine;

/// <summary>
/// Forwards interaction from a trigger volume to something that lives elsewhere.
///
/// The boat is the case this exists for: its interaction volume has to be a generous box on
/// the Interactable layer, but the behaviour lives on the boat root alongside the hull and
/// the seat. Rather than teach PlayerInteractor about that arrangement, the trigger carries
/// this and points at the real target.
/// </summary>
public class InteractableProxy : MonoBehaviour, IInteractable
{
    [Tooltip("The component that actually handles the interaction. Must be an IInteractable.")]
    public MonoBehaviour target;

    IInteractable Target => target as IInteractable;

    public string Prompt => Target?.Prompt;
    public bool CanInteract => Target != null && Target.CanInteract;

    public void Interact(PlayerInteractor who) => Target?.Interact(who);

    void OnValidate()
    {
        // Catch a wrong drag in the Inspector at author time rather than as a silent no-op.
        if (target != null && target is not IInteractable)
        {
            Debug.LogWarning($"{name}: '{target.GetType().Name}' is not an IInteractable.", this);
            target = null;
        }
    }
}
