using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Idle -> Chase -> Attack enemy brain. Enemies belong to the dark: they never walk into the
/// lantern light on purpose, and they burn away if the light covers them for too long.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyStateMachine : MonoBehaviour
{
    public enum EnemyState { Idle, Chase, Attack }

    // Attack ends slightly past attackRange so the enemy doesn't flicker between Chase and Attack.
    private const float AttackExitRangeMultiplier = 1.15f;
    private const float TurnSpeedDegrees = 540f;

    [Header("Detection")]
    [Tooltip("The enemy notices the player within this distance, as long as the enemy is in the dark. Keep it larger than the max light radius.")]
    [SerializeField, Min(0f)] private float detectionRange = 15f;

    [Header("Chase")]
    [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
    [Tooltip("How far outside the light's edge the enemy waits.")]
    [SerializeField, Min(0f)] private float lightEdgeBuffer = 0.5f;
    [Tooltip("Seconds between path recalculations. Higher is cheaper on mobile.")]
    [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;

    [Header("Attack")]
    [Tooltip("Attacks only land once the light radius + Light Edge Buffer drops below this.")]
    [SerializeField, Min(0f)] private float attackRange = 2f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [Tooltip("Seconds between hits.")]
    [SerializeField, Min(0f)] private float attackCooldown = 1.5f;

    [Header("Light")]
    [Tooltip("Seconds the enemy can stay inside the light before it burns away.")]
    [SerializeField, Min(0f)] private float burnTimeInLight = 1.5f;

    [Header("References")]
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] private Transform player;

    private NavMeshAgent agent;
    private float nextRepathTime;
    private float nextAttackTime;
    private float timeInLight;

    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    /// <summary>Called by EnemySpawner right after Instantiate so the enemy doesn't search for the player.</summary>
    public void SetTarget(Transform target)
    {
        player = target;
    }

    /// <summary>Removes this enemy. Public so other systems (flares, traps) can kill it too.</summary>
    public void Despawn()
    {
        Destroy(gameObject);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
        else
            Debug.LogWarning($"{name}: no player assigned and no GameObject tagged 'Player' found.", this);
    }

    private void Update()
    {
        if (player == null || !IsGameActive())
        {
            StopMoving();
            return;
        }

        float lightRadius = GetLightRadius();
        float distanceToPlayer = FlatDistance(transform.position, player.position);
        bool inLight = distanceToPlayer < lightRadius;

        timeInLight = inLight ? timeInLight + Time.deltaTime : 0f;
        if (timeInLight >= burnTimeInLight)
        {
            Despawn();
            return;
        }

        switch (CurrentState)
        {
            case EnemyState.Idle:
                UpdateIdle(distanceToPlayer, inLight);
                break;
            case EnemyState.Chase:
                UpdateChase(distanceToPlayer, inLight, lightRadius);
                break;
            case EnemyState.Attack:
                UpdateAttack(distanceToPlayer, inLight);
                break;
        }
    }

    private void UpdateIdle(float distanceToPlayer, bool inLight)
    {
        if (!inLight && distanceToPlayer <= detectionRange)
            ChangeState(EnemyState.Chase);
    }

    private void UpdateChase(float distanceToPlayer, bool inLight, float lightRadius)
    {
        if (!inLight && distanceToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        if (Time.time < nextRepathTime || !agent.isOnNavMesh)
            return;

        nextRepathTime = Time.time + repathInterval;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(GetChaseDestination(lightRadius));
    }

    private void UpdateAttack(float distanceToPlayer, bool inLight)
    {
        if (inLight || distanceToPlayer > attackRange * AttackExitRangeMultiplier)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        FacePlayer();

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        // TakeDamage(float) lives in the Player scripts. SendMessage keeps this file compiling
        // whatever that class ends up being called; it must be on the same GameObject as `player`.
        player.SendMessage("TakeDamage", damage, SendMessageOptions.RequireReceiver);
    }

    // The point nearest the player that is still just outside the light. If the enemy is
    // already inside the light, this point is behind it, so chasing it backs the enemy out.
    private Vector3 GetChaseDestination(float lightRadius)
    {
        Vector3 awayFromPlayer = transform.position - player.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.0001f)
            awayFromPlayer = -transform.forward;

        float stopDistance = Mathf.Max(lightRadius + lightEdgeBuffer, attackRange * 0.5f);
        return player.position + awayFromPlayer.normalized * stopDistance;
    }

    private void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        if (newState == EnemyState.Chase)
            nextRepathTime = 0f;
        else
            StopMoving();
    }

    private void StopMoving()
    {
        if (!agent.isOnNavMesh || agent.isStopped)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toPlayer);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, TurnSpeedDegrees * Time.deltaTime);
    }

    private static bool IsGameActive()
    {
        // No GameManager (e.g. a test scene) counts as playing.
        return GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Playing;
    }

    private static float GetLightRadius()
    {
        // LanternSystem is owned by the Player/Lantern side of the project.
        return LanternSystem.Instance != null ? LanternSystem.Instance.CurrentRadius : 0f;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
