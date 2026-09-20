using System;
using UnityEngine;

// Simple health with no health bar: injury shows through the red vignette and the heartbeat.
// Health slowly recovers if you avoid being hit, so deaths come from repeated mistakes, not one.
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("Seconds without being hit before health starts recovering.")]
    public float regenDelay = 6f;
    public float regenPerSecond = 5f;
    [Tooltip("Seconds of invulnerability after a hit, so two vampires can't delete you in one frame.")]
    public float invulnerableAfterHit = 0.9f;

    [Header("Hit Reaction")]
    public float knockback = 4.5f;
    public float stagger = 0.35f;

    [Header("References")]
    public PlayerController controller;
    public PlayerAnimator animator;
    public LanternLight lantern;
    public LanternFuel fuel;

    public float Current { get; private set; }
    public float Fraction => Current / maxHealth;
    public bool IsDead { get; private set; }
    public bool IsProtected => protectionSources > 0;

    public event Action<float> OnDamaged;
    public event Action OnDied;

    float lastHitTime = -99f;
    int protectionSources;

    void Awake()
    {
        // Difficulty decides how much punishment the run allows before it ends.
        maxHealth *= GameConfig.Current.playerHealthMultiplier;
        Current = maxHealth;
        if (!controller) controller = GetComponent<PlayerController>();
        if (!animator) animator = GetComponentInChildren<PlayerAnimator>();
        if (!fuel) fuel = GetComponent<LanternFuel>();
    }

    public void AddProtection() => protectionSources++;
    public void RemoveProtection() => protectionSources = Mathf.Max(0, protectionSources - 1);

    public bool TakeHit(float damage, Vector3 fromPosition)
    {
        if (IsDead || IsProtected || Time.time - lastHitTime < invulnerableAfterHit) return false;
        if (GameManager.Instance && !GameManager.Instance.IsGameplayActive) return false;

        // The sword gets first say: a raised guard soaks most of the blow, and a guard raised
        // in the instant before it lands turns the whole thing aside.
        bool parried = false;
        var sword = SwordController.Instance;
        if (sword) damage = sword.FilterIncomingDamage(damage, fromPosition, out parried);

        // A parry is not a hit: no stagger, no knockback, no invulnerability window to burn.
        if (parried) return false;

        lastHitTime = Time.time;
        Current = Mathf.Max(0f, Current - damage);

        bool blocked = sword && sword.IsGuarding;
        float reaction = blocked ? 0.45f : 1f;

        Vector3 away = transform.position - fromPosition;
        away.y = 0f;
        if (controller)
        {
            controller.AddImpulse(away.normalized * knockback * reaction);
            controller.LockMovement(stagger * reaction);
        }
        if (!blocked && animator) animator.PlayHit();
        if (lantern) lantern.Disturb(reaction);

        OnDamaged?.Invoke(damage);
        GameEvents.RaisePlayerDamaged(damage);
        if (!blocked) AudioManager.Play(Sfx.PlayerHurt);
        Haptics.Pulse(0.8f * reaction, 0.25f);
        CameraController.Shake(0.9f * reaction);

        if (Current <= 0f) Die();
        return true;
    }

    void Die()
    {
        IsDead = true;
        if (controller) controller.ControlEnabled = false;
        if (animator) animator.SetDead();
        if (GameManager.Instance)
        {
            bool dark = fuel && fuel.Depleted;
            GameManager.Instance.DefeatCause = dark
                ? "Your flame had gone out. They found you in the dark."
                : "The vampires caught you before you could escape.";
        }
        AudioManager.Play(Sfx.PlayerDeath);
        OnDied?.Invoke();
        GameEvents.RaisePlayerDied();
    }

    void Update()
    {
        if (IsDead || Current >= maxHealth) return;
        if (Time.time - lastHitTime > regenDelay) Current = Mathf.Min(maxHealth, Current + regenPerSecond * Time.deltaTime);
    }
}
