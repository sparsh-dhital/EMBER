using System.Collections;
using UnityEngine;

// The survivor's body goes limp and falls under real physics when he dies, instead of
// playing a canned death. The ragdoll (rigidbodies, colliders and joints on the humanoid
// bones) is built in the editor by RealisticSurvivor; here it stays kinematic with its
// colliders off while he is alive, so it never fights the CharacterController.
public class RagdollDeath : MonoBehaviour
{
    public Animator animator;
    public PlayerHealth health;
    public CharacterController controller;
    public Rigidbody[] bodies;
    public Collider[] colliders;
    [Tooltip("A small shove at the moment of death, along the body's backward direction.")]
    public float shove = 1.4f;
    public float delay = 0.08f;

    public bool IsRagdoll { get; private set; }

    void Awake() => SetRagdoll(false);

    void OnEnable()
    {
        if (health) health.OnDied += OnDied;
    }

    void OnDisable()
    {
        if (health) health.OnDied -= OnDied;
    }

    void OnDied() => StartCoroutine(Fall());

    IEnumerator Fall()
    {
        yield return new WaitForSeconds(delay);
        if (controller)
            foreach (var c in colliders)
                if (c) Physics.IgnoreCollision(controller, c, true);
        var hips = bodies.Length > 0 ? bodies[0] : null;
        // Carry the motion he had into the fall, so a running death tumbles forward.
        Vector3 carry = controller ? controller.velocity : Vector3.zero;
        if (animator) animator.enabled = false;
        SetRagdoll(true);
        foreach (var b in bodies)
            if (b) b.linearVelocity = carry;
        if (hips) hips.AddForce(-transform.forward * shove + Vector3.down * 0.5f, ForceMode.VelocityChange);
    }

    void SetRagdoll(bool on)
    {
        IsRagdoll = on;
        if (bodies != null)
            foreach (var b in bodies)
            {
                if (!b) continue;
                b.isKinematic = !on;
                b.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
        if (colliders != null)
            foreach (var c in colliders)
                if (c) c.enabled = on;
    }
}
