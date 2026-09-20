using UnityEngine;

// Everything that can be done to a vampire with the silver sword.
//
// Vampires do not have a health bar: damage reads through staggers, knockback, the
// brightening of the burn in their skin, and finally the ash dissolve. A hit in the
// lantern's light is holy and lands for full damage; a hit swung in the dark barely
// scratches them, which is what makes preserving fuel matter in a fight.
[RequireComponent(typeof(VampireAI))]
public class VampireHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("Damage in one hit above this always staggers, however tough the vampire is.")]
    public float staggerThreshold = 18f;
    [Tooltip("Seconds the vampire is held helpless after a staggering hit.")]
    public float staggerSeconds = 0.75f;
    [Tooltip("Seconds it cannot be hit again, so one swing cannot land twice.")]
    public float hitCooldown = 0.12f;

    [Header("Knockback")]
    public float knockbackFromSlash = 1.6f;
    public float knockbackFromRadiant = 7f;
    [Tooltip("Seconds a radiant burst keeps them reeling away.")]
    public float radiantStunSeconds = 1.4f;

    [Header("Death")]
    [Tooltip("Seconds the body takes to crumble to ash.")]
    public float dissolveSeconds = 1.6f;

    public float Current { get; private set; }
    public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;
    public bool IsDead { get; private set; }
    public bool IsStaggered => Time.time < staggerUntil;

    [Header("References")]
    public VampireAnimator visual;

    VampireAI ai;
    float staggerUntil;
    float lastHitTime = -99f;
    float dissolveTimer;

    void Awake()
    {
        ai = GetComponent<VampireAI>();
        if (!visual) visual = GetComponentInChildren<VampireAnimator>();
    }

    // Called by the spawner every time this pooled vampire is reused.
    public void ResetHealth()
    {
        Current = maxHealth;
        IsDead = false;
        staggerUntil = 0f;
        lastHitTime = -99f;
        dissolveTimer = 0f;
    }

    void OnEnable() => ResetHealth();

    /// <summary>
    /// Lands a sword hit. <paramref name="holy01"/> is how well lit the vampire was (1 = full
    /// lantern light, 0 = pitch black) and already scales the damage the caller passes in.
    /// Returns false if the hit was swallowed by the per-hit cooldown.
    /// </summary>
    public bool TakeSwordHit(float damage, Vector3 fromPosition, float holy01, bool radiant = false)
    {
        if (IsDead || Time.time - lastHitTime < hitCooldown) return false;
        lastHitTime = Time.time;

        Current = Mathf.Max(0f, Current - damage);

        Vector3 away = transform.position - fromPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        float push = radiant ? knockbackFromRadiant : knockbackFromSlash;
        bool staggers = radiant || damage >= staggerThreshold;

        if (Current <= 0f)
        {
            Die(away, push, holy01);
            return true;
        }

        if (staggers)
        {
            staggerUntil = Time.time + (radiant ? radiantStunSeconds : staggerSeconds);
            if (ai) ai.ApplyStagger(staggerUntil - Time.time, away * push);
            AudioManager.PlayAt(Sfx.VampireStagger, transform.position + Vector3.up * 1.4f, 0.9f,
                                Random.Range(0.9f, 1.1f));
        }
        else if (ai)
        {
            ai.ApplyStagger(0.12f, away * push * 0.4f);
        }

        // Burning light drives the hiss: a lit hit sears, a dark hit just thumps.
        AudioManager.PlayAt(Sfx.SwordHit, transform.position + Vector3.up * 1.3f,
                            Mathf.Lerp(0.55f, 1f, holy01), Random.Range(0.94f, 1.08f));
        if (holy01 > 0.45f)
            AudioManager.PlayAt(Sfx.VampireHiss, transform.position + Vector3.up * 1.6f,
                                holy01 * 0.8f, Random.Range(1.15f, 1.35f));

        if (visual) visual.FlashBurn(holy01);
        return true;
    }

    void Die(Vector3 away, float push, float holy01)
    {
        IsDead = true;
        dissolveTimer = dissolveSeconds;
        staggerUntil = Time.time + dissolveSeconds;

        AudioManager.PlayAt(Sfx.LethalStrike, transform.position + Vector3.up * 1.3f);
        AudioManager.PlayAt(Sfx.VampireDeath, transform.position + Vector3.up * 1.5f, 1f,
                            Random.Range(0.85f, 1f));
        if (visual) { visual.FlashBurn(1f); visual.BeginAsh(); }
        if (ai) ai.BeginDeath(away * push, dissolveSeconds);
        Haptics.Pulse(0.6f, 0.18f);
    }

    void Update()
    {
        if (!IsDead) return;
        dissolveTimer -= Time.deltaTime;
        if (visual) visual.SetFade(Mathf.Clamp01(dissolveTimer / dissolveSeconds));
    }
}
