using UnityEngine;
using UnityEngine.AI;

// One vampire. A small state machine driven by the lantern:
//   bright lantern  -> they keep away, and bolt if you walk at them (Hidden / Flee)
//   weaker lantern  -> they circle at the edge of your light (Stalk)
//   critical / out  -> they rush you and attack (Aggressive / Attack)
//   prayer          -> they flee in terror
[RequireComponent(typeof(NavMeshAgent))]
public class VampireAI : MonoBehaviour
{
    public enum State { Hidden, Flee, Stalk, Aggressive, Attack, Banished }

    [Header("References")]
    public VampireAnimator visual;

    [Header("Detection")]
    [Tooltip("Beyond this distance the vampire is Hidden and drifts toward the player.")]
    public float engageRange = 30f;
    public float driftSpeed = 1.8f;

    [Header("Fear Of The Light")]
    [Tooltip("With a strong lantern, a vampire closer than (light radius + this) turns and runs.")]
    public float fleeBuffer = 3f;
    [Tooltip("With a strong lantern, vampires watch from this far beyond the edge of your light.")]
    public float lurkBuffer = 6f;
    public float fleeSpeed = 5.6f;

    [Header("Stalk")]
    public float stalkSpeed = 2.4f;
    [Tooltip("How far outside the light's edge stalking vampires circle.")]
    public float stalkBuffer = 1.2f;
    [Tooltip("Degrees per second they circle around the player.")]
    public float circleSpeed = 16f;

    [Header("Aggressive")]
    public float criticalSpeed = 4.1f;
    [Tooltip("Speed once the flame is out. Keep it below the player's run speed so escape is possible.")]
    public float darkSpeed = 4.8f;

    [Header("Attack")]
    public float attackRange = 1.7f;
    [Tooltip("Telegraph before the strike. Long enough to react and step away.")]
    public float windUpTime = 0.6f;
    public float strikeReach = 2.1f;
    public float damage = 34f;
    public float attackCooldown = 1.8f;

    [Header("Prayer")]
    public float prayerFleeSpeed = 7.5f;

    [Header("Performance")]
    [Tooltip("Seconds between decisions. Movement stays smooth; only the thinking is throttled.")]
    public float thinkInterval = 0.2f;

    public State CurrentState { get; private set; } = State.Hidden;
    public float Aggression { get; private set; }
    
    public float health = 30f;

    NavMeshAgent agent;
    VampireSpawner spawner;
    Transform player;
    PlayerHealth playerHealth;
    LanternFuel fuel;
    LanternLight lantern;
    PrayerSystem prayer;

    float nextThink;
    float circleSign = 1f;
    float circleAngle;
    float attackTimer;
    float cooldownUntil;
    bool struck;
    float banishTimer;
    float nextVoice;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!visual) visual = GetComponentInChildren<VampireAnimator>();
    }

    public void Init(VampireSpawner owner, Transform target, PlayerHealth health, LanternFuel lanternFuel, LanternLight light, PrayerSystem prayerSystem)
    {
        spawner = owner;
        player = target;
        playerHealth = health;
        fuel = lanternFuel;
        lantern = light;
        prayer = prayerSystem;
    }

    public void Spawn(Vector3 position)
    {
        gameObject.SetActive(true);
        agent.Warp(position);
        agent.isStopped = false;
        agent.updateRotation = true;
        circleSign = Random.value < 0.5f ? -1f : 1f;
        agent.avoidancePriority = Random.Range(40, 60);
        CurrentState = State.Hidden;
        nextThink = 0f;
        cooldownUntil = Time.time + 1f;
        banishTimer = 0f;
        health = 30f;
        if (visual) visual.ResetVisual();
        Vector3 facing = player ? Flat(player.position - position) : Vector3.zero;
        if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(facing);
    }

    public void TakeDamage(float amount)
    {
        if (CurrentState == State.Banished) return;
        
        health -= amount;
        if (visual) visual.Animate(CurrentState, 0f, 1f); // Brief twitch/aggression
        
        if (health <= 0f)
        {
            Banish();
        }
        else
        {
            // Back off slightly when hit
            agent.isStopped = false;
            agent.speed = fleeSpeed * 0.5f;
            SetFleeDestination(1.5f);
            cooldownUntil = Time.time + 0.8f;
        }
    }

    // Victory or the spawner thinning the numbers: back off into the dark and vanish.
    public void Banish()
    {
        if (!gameObject.activeSelf || CurrentState == State.Banished) return;
        EnterState(State.Banished);
        banishTimer = 2.5f;
        agent.isStopped = false;
        agent.speed = fleeSpeed;
        SetFleeDestination(12f);
    }

    void Update()
    {
        if (!player) return;

        if (CurrentState == State.Banished)
        {
            banishTimer -= Time.deltaTime;
            if (visual) visual.SetFade(Mathf.Clamp01(banishTimer / 1.2f));
            if (banishTimer <= 0f) spawner.Release(this);
            return;
        }

        var gm = GameManager.Instance;
        if (gm && !gm.IsGameplayActive)
        {
            if (!agent.isStopped) agent.isStopped = true;
            if (visual) visual.Animate(CurrentState, 0f, Aggression);
            return;
        }
        if (agent.isStopped && CurrentState != State.Attack) agent.isStopped = false;

        if (CurrentState == State.Attack) UpdateAttack();
        else if (Time.time >= nextThink)
        {
            nextThink = Time.time + thinkInterval;
            Think();
        }

        if (visual) visual.Animate(CurrentState, agent.velocity.magnitude / Mathf.Max(0.1f, darkSpeed), Aggression);
    }

    // ---------------------------------------------------------------- decisions

    void Think()
    {
        float dist = FlatDistance(player.position);
        float radius = lantern ? lantern.LightRadius : 0f;
        LightBand band = EffectiveBand();

        Aggression = band == LightBand.Out ? 1f : band == LightBand.Critical ? 0.8f : band == LightBand.Medium ? 0.45f : 0.15f;

        // Prayer beats everything: run.
        if (prayer && prayer.IsActive && dist < prayer.FearRadius + 8f)
        {
            Aggression = 0f;
            EnterState(State.Flee);
            agent.speed = prayerFleeSpeed;
            SetFleeDestination(prayer.FearRadius + 10f - dist);
            return;
        }

        switch (band)
        {
            case LightBand.Strong:
                if (dist < radius + fleeBuffer)
                {
                    EnterState(State.Flee);
                    agent.speed = fleeSpeed;
                    SetFleeDestination(radius + lurkBuffer + 3f - dist);
                }
                else
                {
                    EnterState(State.Hidden);
                    agent.speed = dist > engageRange ? driftSpeed : stalkSpeed * 0.8f;
                    // Watch from the dark just outside your light.
                    MoveToRing(radius + lurkBuffer, 0f);
                }
                break;

            case LightBand.Medium:
                EnterState(State.Stalk);
                agent.speed = dist < radius * 0.8f ? fleeSpeed * 0.8f : stalkSpeed;
                circleAngle += circleSign * circleSpeed * thinkInterval;
                MoveToRing(radius + stalkBuffer, circleAngle);
                break;

            default: // Critical or Out
                EnterState(State.Aggressive);
                agent.speed = band == LightBand.Out ? darkSpeed : criticalSpeed;
                if (dist <= attackRange && Time.time >= cooldownUntil) BeginAttack();
                else agent.SetDestination(player.position);
                break;
        }
    }

    LightBand EffectiveBand()
    {
        LightBand band = fuel ? fuel.Band : LightBand.Strong;
        // During the final radio call the signal draws them in: every band counts as one step darker.
        if (spawner && spawner.FinalWave && (band == LightBand.Strong || band == LightBand.Medium)) band = (LightBand)((int)band + 1);
        return band;
    }

    void EnterState(State next)
    {
        if (next == CurrentState) return;
        CurrentState = next;

        if (Time.time < nextVoice) return;
        nextVoice = Time.time + Random.Range(2.5f, 5f);
        if (next == State.Flee) AudioManager.PlayAt(Sfx.VampireHiss, transform.position + Vector3.up * 1.6f, 0.8f, Random.Range(0.9f, 1.15f));
        else if (next == State.Aggressive) AudioManager.PlayAt(Sfx.VampireScreech, transform.position + Vector3.up * 1.6f, 0.7f, Random.Range(0.85f, 1.05f));
    }

    // ---------------------------------------------------------------- attack

    void BeginAttack()
    {
        EnterState(State.Attack);
        attackTimer = 0f;
        struck = false;
        agent.isStopped = true;
        agent.updateRotation = false;
        if (visual) visual.BeginWindUp();
        AudioManager.PlayAt(Sfx.VampireScreech, transform.position + Vector3.up * 1.6f, 1f, Random.Range(1.05f, 1.2f));
    }

    void UpdateAttack()
    {
        attackTimer += Time.deltaTime;
        Vector3 toPlayer = Flat(player.position - transform.position);
        if (toPlayer.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 540f * Time.deltaTime);

        bool protectedNow = (prayer && prayer.IsActive) || (playerHealth && playerHealth.IsProtected);

        if (!struck && attackTimer >= windUpTime)
        {
            struck = true;
            if (visual) visual.Strike();
            AudioManager.PlayAt(Sfx.VampireAttack, transform.position + Vector3.up * 1.2f);
            if (!protectedNow && toPlayer.magnitude <= strikeReach && playerHealth) playerHealth.TakeHit(damage, transform.position);
        }

        if (attackTimer >= windUpTime + 0.45f || (protectedNow && !struck))
        {
            cooldownUntil = Time.time + attackCooldown;
            agent.isStopped = false;
            agent.updateRotation = true;
            // Step back after a strike so hits come in readable beats, not a blender.
            agent.speed = stalkSpeed;
            SetFleeDestination(2.5f);
            CurrentState = State.Aggressive;
            nextThink = Time.time + 0.6f;
        }
    }

    // ---------------------------------------------------------------- movement helpers

    void MoveToRing(float ringRadius, float angleOffset)
    {
        Vector3 fromPlayer = Flat(transform.position - player.position);
        if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * fromPlayer.normalized;
        Vector3 goal = player.position + dir * ringRadius;
        if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void SetFleeDestination(float distance)
    {
        distance = Mathf.Max(4f, distance);
        Vector3 away = Flat(transform.position - player.position);
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        // Try straight away first, then angle off if a wall is in the way.
        float[] angles = { 0f, 40f, -40f, 80f, -80f, 120f, -120f };
        foreach (float a in angles)
        {
            Vector3 goal = transform.position + Quaternion.Euler(0f, a, 0f) * away * distance;
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    float FlatDistance(Vector3 p) => Flat(p - transform.position).magnitude;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
