using System.Collections.Generic;
using UnityEngine;

// The sacred silver sword in the player's right hand, and everything you can do with it.
//
//   Light Slash    - the bread-and-butter swing. Chains: two quick slashes flow into each other.
//   Radiant Strike - the third slash in a chain. Burns 5% of the lantern's fuel and detonates a
//                    flash of holy light that throws every vampire around you backwards.
//   Parry / Block  - hold to guard. A guard raised in the instant before a blow lands is a parry:
//                    it negates the hit outright and leaves the attacker staggered and open.
//
// Damage is scaled by how well lit the target is. A vampire standing in the lantern's glow takes
// the full holy strike; one you swing at in pitch blackness barely feels it. Letting the lantern
// die does not just blind you, it blunts your sword.
public class SwordController : MonoBehaviour
{
    public enum Stance { Idle, Slash, Radiant, Guard }

    static SwordController instance;
    public static bool Armed => instance && instance.HasSword;
    public static SwordController Instance => instance;

    [Header("References")]
    public Transform swordRoot;
    [Tooltip("Origin of the hit arc. Defaults to the sword itself, or the player's chest.")]
    public Transform strikeOrigin;
    public PlayerController controller;
    public PlayerAnimator playerAnimator;
    public LanternFuel fuel;
    public LanternLight lantern;
    public Renderer[] bladeRenderers;

    [Header("Carry")]
    [Tooltip("Start the run already holding the sword. Off means you have to find it.")]
    public bool startArmed = true;

    [Header("Light Slash")]
    public float slashDamage = 34f;
    [Tooltip("Reach of the swing, measured from the strike origin.")]
    public float slashRange = 2.4f;
    [Tooltip("Half-angle of the arc in front of you that a slash sweeps.")]
    public float slashArcDegrees = 80f;
    [Tooltip("Seconds into the swing before the blade connects.")]
    public float slashHitDelay = 0.18f;
    public float slashCooldown = 0.5f;
    [Tooltip("How long after a slash the next one still counts as part of the same chain.")]
    public float comboWindow = 0.9f;

    [Header("Radiant Strike")]
    [Tooltip("Slashes in a chain before the finisher fires. 3 = light, light, radiant.")]
    public int comboLength = 3;
    [Tooltip("Fraction of maximum fuel the burst consumes.")]
    [Range(0f, 0.25f)] public float radiantFuelCost = 0.05f;
    public float radiantDamage = 55f;
    public float radiantRadius = 6.5f;
    public float radiantCooldown = 1.1f;
    [Tooltip("Peak brightness of the flash.")]
    public float radiantFlashIntensity = 45f;
    public float radiantFlashSeconds = 0.45f;

    [Header("Guard / Parry")]
    [Tooltip("Damage still taken through a raised guard. 0.35 = 65% mitigated.")]
    [Range(0f, 1f)] public float blockMitigation = 0.35f;
    [Tooltip("Guard raised within this many seconds of the blow landing is a perfect parry.")]
    public float parryWindow = 0.28f;
    [Tooltip("Seconds a parried vampire is left open.")]
    public float parryStagger = 1.25f;
    [Tooltip("Movement speed multiplier while guarding.")]
    [Range(0.1f, 1f)] public float guardSpeedMultiplier = 0.45f;

    [Header("Light Multiplier")]
    [Tooltip("Damage multiplier for a hit landed in total darkness.")]
    [Range(0f, 1f)] public float darkDamageMultiplier = 0.4f;

    public bool HasSword { get; private set; }
    public Stance CurrentStance { get; private set; } = Stance.Idle;
    public bool IsGuarding => CurrentStance == Stance.Guard;
    /// <summary>0..1 progress of the current chain, for the HUD.</summary>
    public float ComboCharge => comboCount / (float)Mathf.Max(1, comboLength);
    public bool RadiantReady => HasSword && fuel && !fuel.Depleted && fuel.Fraction >= radiantFuelCost;

    int comboCount;
    float comboExpires;
    float nextSwingTime;
    float swingHitAt = -1f;
    float swingIsRadiant;
    float guardRaisedAt = -99f;
    float stanceResetAt;
    Light flashLight;
    float flashTimer;

    readonly Collider[] overlap = new Collider[24];
    readonly HashSet<VampireHealth> hitThisSwing = new HashSet<VampireHealth>();
    // Lazily created rather than built in Awake: a domain reload (recompiling while play
    // mode is running) clears non-serialized fields without calling Awake again, which
    // used to leave this null and throw once per renderer per frame.
    MaterialPropertyBlock mpbCache;
    MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        instance = this;
        if (!controller) controller = GetComponentInParent<PlayerController>();
        if (!playerAnimator) playerAnimator = GetComponentInChildren<PlayerAnimator>();
        if (!fuel) fuel = GetComponentInParent<LanternFuel>();
        if (!lantern) lantern = GetComponentInParent<LanternLight>();
        if (!strikeOrigin) strikeOrigin = swordRoot ? swordRoot : transform;
        HasSword = startArmed;
        if (swordRoot) swordRoot.gameObject.SetActive(HasSword);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Start()
    {
        // A dim point light parented to the blade: the radiant burst's flash.
        var go = new GameObject("RadiantFlash");
        go.transform.SetParent(strikeOrigin ? strikeOrigin : transform, false);
        flashLight = go.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.94f, 0.76f);
        flashLight.range = radiantRadius * 2.2f;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
    }

    /// <summary>Picked the sword up off the ground.</summary>
    public void Equip()
    {
        if (HasSword) return;
        HasSword = true;
        if (swordRoot) swordRoot.gameObject.SetActive(true);
        AudioManager.Play(Sfx.SwordPickup);
        GameEvents.ShowMessage("Sacred Silver Sword",
            "Left click to slash • hold right click to guard • chain three for a radiant strike", 4.5f);
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        UpdateFlash();

        var gm = GameManager.Instance;
        bool active = HasSword && (!gm || gm.IsGameplayActive) && (!controller || controller.ControlEnabled);
        if (!active)
        {
            if (CurrentStance != Stance.Idle) SetStance(Stance.Idle);
            return;
        }

        // A swing already in flight owns the frame until its blade lands.
        if (swingHitAt > 0f && Time.time >= swingHitAt)
        {
            swingHitAt = -1f;
            if (swingIsRadiant > 0f) ResolveRadiant();
            else ResolveSlash();
        }

        if (!InputReader.Exists) return;
        var input = InputReader.Instance;

        bool guardHeld = input.BlockHeld;
        bool swinging = CurrentStance == Stance.Slash || CurrentStance == Stance.Radiant;

        if (guardHeld && !swinging)
        {
            if (CurrentStance != Stance.Guard)
            {
                guardRaisedAt = Time.time;
                SetStance(Stance.Guard);
                AudioManager.Play(Sfx.SwordSwing, 0.35f, 1.5f);
            }
        }
        else if (CurrentStance == Stance.Guard && !guardHeld)
        {
            SetStance(Stance.Idle);
        }

        if (input.AttackPressed && Time.time >= nextSwingTime && !swinging) BeginSwing();

        // A swing's stance falls back to idle once its animation is done.
        if (swinging && Time.time >= stanceResetAt) SetStance(guardHeld ? Stance.Guard : Stance.Idle);

        if (Time.time > comboExpires && comboCount > 0) comboCount = 0;
    }

    void SetStance(Stance next)
    {
        if (next == CurrentStance) return;
        CurrentStance = next;
        if (controller) controller.speedMultiplier = next == Stance.Guard ? guardSpeedMultiplier : 1f;
        if (playerAnimator) playerAnimator.SetGuarding(next == Stance.Guard);
    }

    // ---------------------------------------------------------------- swinging

    void BeginSwing()
    {
        bool chained = Time.time <= comboExpires;
        comboCount = chained ? comboCount + 1 : 1;

        // The finisher only fires if there is fuel to burn; otherwise the chain just resets.
        bool radiant = comboCount >= comboLength && RadiantReady;
        if (comboCount >= comboLength) comboCount = 0;

        hitThisSwing.Clear();
        swingIsRadiant = radiant ? 1f : 0f;
        swingHitAt = Time.time + slashHitDelay;
        comboExpires = Time.time + comboWindow;
        nextSwingTime = Time.time + (radiant ? radiantCooldown : slashCooldown);
        stanceResetAt = Time.time + (radiant ? 0.55f : 0.4f);

        SetStance(radiant ? Stance.Radiant : Stance.Slash);
        if (playerAnimator) playerAnimator.PlayAttack();
        AudioManager.Play(Sfx.SwordSwing, radiant ? 1f : 0.8f, radiant ? 0.82f : Random.Range(0.95f, 1.1f));
        if (lantern) lantern.Disturb(radiant ? 0.8f : 0.25f);
    }

    void ResolveSlash()
    {
        Vector3 origin = StrikePoint();
        Vector3 forward = Flat(transform.forward);
        int count = Physics.OverlapSphereNonAlloc(origin, slashRange, overlap,
                                                  EmberLayers.EnemyHitboxes | LayerMask.GetMask(EmberLayers.Enemy),
                                                  QueryTriggerInteraction.Collide);
        bool connected = false;

        for (int i = 0; i < count; i++)
        {
            var health = overlap[i] ? overlap[i].GetComponentInParent<VampireHealth>() : null;
            if (health == null || health.IsDead || !hitThisSwing.Add(health)) continue;

            // Only what is actually in front of you, inside the arc of the swing.
            Vector3 toTarget = Flat(health.transform.position - origin);
            if (toTarget.sqrMagnitude > 0.0001f &&
                Vector3.Angle(forward, toTarget.normalized) > slashArcDegrees) continue;

            float holy01 = HolyFactor(health.transform.position);
            float damage = slashDamage * Mathf.Lerp(darkDamageMultiplier, 1f, holy01)
                           * GameConfig.Current.swordDamageMultiplier;
            if (health.TakeSwordHit(damage, transform.position, holy01)) connected = true;
        }

        if (connected)
        {
            CameraController.Shake(0.35f);
            Haptics.Pulse(0.45f, 0.1f);
        }
    }

    void ResolveRadiant()
    {
        // Pay for the burst out of the lantern, then detonate.
        if (fuel) fuel.Burn(fuel.maxFuel * radiantFuelCost);

        Vector3 centre = transform.position + Vector3.up * 1.1f;
        int count = Physics.OverlapSphereNonAlloc(centre, radiantRadius, overlap,
                                                  EmberLayers.EnemyHitboxes | LayerMask.GetMask(EmberLayers.Enemy),
                                                  QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            var health = overlap[i] ? overlap[i].GetComponentInParent<VampireHealth>() : null;
            if (health == null || health.IsDead || !hitThisSwing.Add(health)) continue;

            // The burst is its own light source, so it always lands holy - that is what you paid for.
            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(centre, health.transform.position) / radiantRadius);
            health.TakeSwordHit(radiantDamage * Mathf.Lerp(0.5f, 1f, falloff)
                                * GameConfig.Current.swordDamageMultiplier,
                                transform.position, 1f, true);
        }

        flashTimer = radiantFlashSeconds;
        if (flashLight) flashLight.enabled = true;
        AudioManager.Play(Sfx.Flare, 1f, 0.9f);
        CameraController.Shake(1.1f);
        Haptics.Pulse(1f, 0.3f);
        GameEvents.ShowMessage("Radiant Strike", "", 1.2f);
    }

    void UpdateFlash()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(flashTimer / radiantFlashSeconds);
        // Snaps to full brightness then decays, so it reads as a detonation rather than a fade-in.
        float curve = t * t;
        if (flashLight)
        {
            flashLight.intensity = radiantFlashIntensity * curve;
            if (flashTimer <= 0f) flashLight.enabled = false;
        }
        SetBladeGlow(curve);
    }

    void SetBladeGlow(float amount)
    {
        if (bladeRenderers == null) return;
        Color glow = new Color(2.6f, 2.4f, 1.9f) * amount;
        foreach (var r in bladeRenderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, glow);
            r.SetPropertyBlock(mpb);
        }
    }

    // ---------------------------------------------------------------- defence

    /// <summary>
    /// Asked by PlayerHealth before a vampire's blow lands. Returns the damage that gets through:
    /// zero on a parry, a fraction of it through a held guard, all of it otherwise.
    /// </summary>
    public float FilterIncomingDamage(float damage, Vector3 fromPosition, out bool parried)
    {
        parried = false;
        if (!HasSword || CurrentStance != Stance.Guard) return damage;

        // You can only guard what you are facing.
        Vector3 toAttacker = Flat(fromPosition - transform.position);
        if (toAttacker.sqrMagnitude > 0.0001f &&
            Vector3.Angle(Flat(transform.forward), toAttacker.normalized) > 100f) return damage;

        if (Time.time - guardRaisedAt <= parryWindow)
        {
            parried = true;
            AudioManager.PlayAt(Sfx.SwordBreak, transform.position + Vector3.up * 1.2f, 1f, 1.25f);
            CameraController.Shake(0.5f);
            Haptics.Pulse(0.7f, 0.12f);
            SetBladeGlow(1f);
            flashTimer = Mathf.Max(flashTimer, 0.2f);

            // Leave whoever swung wide open.
            var attacker = FindVampireNear(fromPosition);
            if (attacker) attacker.TakeSwordHit(0f, transform.position, 1f, true);
            if (attacker && attacker.GetComponent<VampireAI>() is VampireAI ai)
                ai.ApplyStagger(parryStagger, Flat(fromPosition - transform.position).normalized * 3f);

            GameEvents.ShowMessage("Parry!", "", 0.9f);
            return 0f;
        }

        AudioManager.PlayAt(Sfx.SwordHit, transform.position + Vector3.up * 1.2f, 0.8f, 0.85f);
        CameraController.Shake(0.4f);
        return damage * blockMitigation;
    }

    VampireHealth FindVampireNear(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, 1.5f, overlap,
                                                  LayerMask.GetMask(EmberLayers.Enemy),
                                                  QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var h = overlap[i] ? overlap[i].GetComponentInParent<VampireHealth>() : null;
            if (h != null && !h.IsDead) return h;
        }
        return null;
    }

    // ---------------------------------------------------------------- light

    /// <summary>
    /// How holy a strike at <paramref name="worldPosition"/> is: 1 inside a healthy lantern's
    /// glow, falling to 0 at the edge of the light and in the dark beyond it.
    /// </summary>
    public float HolyFactor(Vector3 worldPosition)
    {
        if (!fuel || fuel.Depleted || !lantern) return 0f;

        float radius = lantern.LightRadius;
        if (radius <= 0.01f) return 0f;

        Vector3 lanternPos = lantern.lanternLight ? lantern.lanternLight.transform.position : transform.position;
        float distance = Vector3.Distance(lanternPos, worldPosition);

        // Full strength through the core of the light, tapering over its outer third.
        float core = radius * 0.65f;
        float reach = Mathf.Clamp01(1f - Mathf.Max(0f, distance - core) / Mathf.Max(0.5f, radius - core));

        // A guttering lantern is a weaker lantern: brightness folds into the holy strength.
        float brightness = Mathf.Clamp01(fuel.Fraction / 0.6f);
        return reach * Mathf.Lerp(0.45f, 1f, brightness);
    }

    Vector3 StrikePoint()
    {
        Vector3 basePoint = transform.position + Vector3.up * 1.15f;
        return basePoint + Flat(transform.forward).normalized * 0.5f;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(StrikePoint(), slashRange);
        Gizmos.color = new Color(1f, 1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.1f, radiantRadius);
    }
#endif
}
