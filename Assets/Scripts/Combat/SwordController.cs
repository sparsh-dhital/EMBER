using UnityEngine;
using System.Collections;

// The single sword the player can carry (filled in with the combat pass).
public class SwordController : MonoBehaviour
{
    static SwordController instance;
    public static bool Armed => instance && instance.HasSword;

    // Default to true for dual wield implementation
    public bool HasSword { get; private set; } = true;

    Animator animator;
    bool isAttacking;

    void Awake() 
    {
        instance = this;
        animator = GetComponentInChildren<Animator>();
    }
    
    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        if (!HasSword || !InputReader.Instance) return;

        if (InputReader.Instance.AttackPressed && !isAttacking)
        {
            Attack();
        }
    }

    void Attack()
    {
        isAttacking = true;
        if (animator) animator.SetTrigger("Attack");
        AudioManager.PlayAt(Sfx.SwordSwing, transform.position);
        StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        // Wait for the swing to reach apex (approx 0.25s)
        yield return new WaitForSeconds(0.25f);

        Vector3 hitCenter = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        Collider[] hits = Physics.OverlapSphere(hitCenter, 1.2f, EmberLayers.EnemyHitboxes | LayerMask.GetMask(EmberLayers.Enemy));
        
        bool hitSomething = false;
        foreach (var hit in hits)
        {
            VampireAI vampire = hit.GetComponentInParent<VampireAI>();
            if (vampire != null)
            {
                vampire.TakeDamage(10f);
                hitSomething = true;
            }
        }

        if (hitSomething)
        {
            AudioManager.PlayAt(Sfx.SwordHit, transform.position + transform.forward);
        }

        // Wait for the rest of the attack animation (0.75s total length)
        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }
}
