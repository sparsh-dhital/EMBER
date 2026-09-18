using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns enemies at spawn points that are currently in the dark, faster the longer the player survives.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // How soon to try again when nothing could spawn (at the cap, or every spawn point is lit / too far).
    private const float RetryDelay = 0.5f;

    [Header("Spawning")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Difficulty")]
    [Tooltip("X = seconds survived, Y = seconds between spawns. Lower Y later on means more pressure.")]
    [SerializeField] private AnimationCurve spawnIntervalOverTime = new AnimationCurve(
        new Keyframe(0f, 6f),
        new Keyframe(180f, 1.5f));
    [SerializeField, Min(0.1f)] private float minSpawnInterval = 0.75f;
    [Tooltip("Hard cap on enemies alive at once. Keep it low for mobile.")]
    [SerializeField, Min(1)] private int maxConcurrentEnemies = 8;

    [Header("Spawn Point Rules")]
    [Tooltip("A spawn point must be at least this far beyond the light's edge to be used.")]
    [SerializeField, Min(0f)] private float lightEdgeMargin = 2f;
    [Tooltip("Spawn points farther than this from the player are skipped. 0 = no limit.")]
    [SerializeField, Min(0f)] private float maxSpawnDistance = 30f;
    [Tooltip("How far a spawn point may be from the NavMesh and still be snapped onto it.")]
    [SerializeField, Min(0.1f)] private float navMeshSnapDistance = 2f;

    [Header("References")]
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] private Transform player;

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private readonly List<Transform> usablePoints = new List<Transform>();
    private float nextSpawnTime;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
            else
                Debug.LogWarning($"{name}: no player assigned and no GameObject tagged 'Player' found.", this);
        }

        if (enemyPrefabs.Count == 0 || spawnPoints.Count == 0)
            Debug.LogWarning($"{name}: assign at least one enemy prefab and one spawn point.", this);

        nextSpawnTime = Time.time + GetSpawnInterval();
    }

    private void Update()
    {
        if (player == null || !IsGameActive() || Time.time < nextSpawnTime)
            return;

        PruneDestroyedEnemies();
        bool spawned = aliveEnemies.Count < maxConcurrentEnemies && TrySpawnEnemy();
        nextSpawnTime = Time.time + (spawned ? GetSpawnInterval() : RetryDelay);
    }

    private bool TrySpawnEnemy()
    {
        if (enemyPrefabs.Count == 0 || !TryPickSpawnPoint(out Transform spawnPoint))
            return false;

        if (!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
        {
            Debug.LogWarning($"{name}: spawn point '{spawnPoint.name}' is not near the NavMesh.", spawnPoint);
            return false;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null)
            return false;

        Vector3 toPlayer = player.position - hit.position;
        toPlayer.y = 0f;
        Quaternion rotation = toPlayer.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toPlayer) : spawnPoint.rotation;

        GameObject enemy = Instantiate(prefab, hit.position, rotation);
        if (enemy.TryGetComponent(out EnemyStateMachine stateMachine))
            stateMachine.SetTarget(player);

        aliveEnemies.Add(enemy);
        return true;
    }

    // Picks a random spawn point that is in the dark and within range of the player.
    private bool TryPickSpawnPoint(out Transform spawnPoint)
    {
        float minDistance = GetLightRadius() + lightEdgeMargin;
        usablePoints.Clear();

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            float distance = FlatDistance(point.position, player.position);
            bool tooFar = maxSpawnDistance > 0f && distance > maxSpawnDistance;
            if (distance >= minDistance && !tooFar)
                usablePoints.Add(point);
        }

        spawnPoint = usablePoints.Count > 0 ? usablePoints[Random.Range(0, usablePoints.Count)] : null;
        return spawnPoint != null;
    }

    private float GetSpawnInterval()
    {
        // Falls back to time since scene load when there's no GameManager (e.g. a test scene).
        float survived = GameManager.Instance != null ? GameManager.Instance.SurvivalTime : Time.timeSinceLevelLoad;
        return Mathf.Max(minSpawnInterval, spawnIntervalOverTime.Evaluate(survived));
    }

    private void PruneDestroyedEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
                aliveEnemies.RemoveAt(i);
        }
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
        Gizmos.color = Color.magenta;
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }
}
