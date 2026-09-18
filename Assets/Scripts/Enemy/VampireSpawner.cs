using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The "director": decides how many vampires should be out there and spawns them fairly —
// never in your light, never in view, never next to you, never on a fuel can.
// Vampires are pooled: they're created once and reused, never Instantiated mid-game.
public class VampireSpawner : MonoBehaviour
{
    [Header("References")]
    public VampireAI vampirePrefab;
    public Transform player;
    public PlayerHealth playerHealth;
    public LanternFuel fuel;
    public LanternLight lantern;
    public PrayerSystem prayer;
    public RadioMissionSystem mission;
    [Tooltip("The radio centre. No vampires spawn close to it (until the final call).")]
    public Transform safeZoneCentre;

    [Header("Population")]
    [Tooltip("Hard cap for mobile performance.")]
    public int maxVampires = 6;
    [Tooltip("Base number of vampires over seconds survived.")]
    public AnimationCurve countOverTime = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(70f, 2f), new Keyframe(160f, 3f), new Keyframe(280f, 4f));
    public int extraWhenLightMedium = 1;
    public int extraWhenLightCritical = 1;
    public int extraWhenDark = 2;
    [Tooltip("One extra vampire for every this-many radio parts collected.")]
    public int partsPerExtraVampire = 2;
    public int finalWaveCount = 6;

    [Header("Timing")]
    [Tooltip("A quiet start: seconds before the first vampire appears.")]
    public float firstSpawnDelay = 22f;
    public float secondsBetweenSpawns = 7f;
    public float finalWaveSecondsBetweenSpawns = 2.5f;

    [Header("Spawn Rules")]
    public float minSpawnDistance = 14f;
    public float maxSpawnDistance = 30f;
    [Tooltip("Spawns must also be this far beyond the edge of the lantern light.")]
    public float lightEdgeMargin = 5f;
    public float safeZoneRadius = 12f;
    public float pickupClearance = 3f;
    [Tooltip("Optional hand-placed spawn points. Random points around the player are also tried.")]
    public Transform[] spawnPoints;

    public bool FinalWave { get; private set; }
    public int AliveCount { get; private set; }
    public float NearestVampireDistance { get; private set; } = 999f;

    readonly List<VampireAI> pool = new List<VampireAI>();
    readonly List<VampireAI> alive = new List<VampireAI>();
    float nextSpawnTime;
    float nextThinTime;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        for (int i = 0; i < maxVampires; i++)
        {
            var v = Instantiate(vampirePrefab, transform);
            v.name = "Vampire_" + (i + 1);
            v.Init(this, player, playerHealth, fuel, lantern, prayer);
            v.gameObject.SetActive(false);
            pool.Add(v);
        }
        nextSpawnTime = firstSpawnDelay;
    }

    public void StartFinalWave()
    {
        FinalWave = true;
        nextSpawnTime = 0f;
    }

    public void BanishAll()
    {
        FinalWave = false;
        foreach (var v in alive.ToArray()) v.Banish();
        enabled = false;
    }

    public void Release(VampireAI v)
    {
        v.gameObject.SetActive(false);
        alive.Remove(v);
        AliveCount = alive.Count;
    }

    int DesiredCount()
    {
        var gm = GameManager.Instance;
        float t = gm ? gm.SurvivalTime : Time.timeSinceLevelLoad;
        if (FinalWave) return Mathf.Min(maxVampires, finalWaveCount);

        int count = Mathf.RoundToInt(countOverTime.Evaluate(t));
        if (fuel)
        {
            if (fuel.Band == LightBand.Medium) count += extraWhenLightMedium;
            else if (fuel.Band == LightBand.Critical) count += extraWhenLightMedium + extraWhenLightCritical;
            else if (fuel.Band == LightBand.Out) count += extraWhenDark + extraWhenLightMedium;
        }
        if (mission && partsPerExtraVampire > 0) count += mission.Collected / partsPerExtraVampire;
        return Mathf.Clamp(count, 0, maxVampires);
    }

    void Update()
    {
        UpdateNearest();

        var gm = GameManager.Instance;
        if (gm && !gm.IsGameplayActive) return;
        float t = gm ? gm.SurvivalTime : Time.timeSinceLevelLoad;

        int desired = DesiredCount();
        if (alive.Count < desired && t >= nextSpawnTime)
        {
            bool spawned = TrySpawn();
            nextSpawnTime = t + (spawned ? (FinalWave ? finalWaveSecondsBetweenSpawns : secondsBetweenSpawns) : 0.5f);
        }
        else if (alive.Count > desired && Time.time >= nextThinTime)
        {
            nextThinTime = Time.time + 4f;
            ThinOut();
        }
    }

    void UpdateNearest()
    {
        float best = 999f;
        foreach (var v in alive)
        {
            if (v.CurrentState == VampireAI.State.Banished) continue;
            float d = Vector3.Distance(v.transform.position, player.position);
            if (d < best) best = d;
        }
        NearestVampireDistance = best;
    }

    // Too many out there (e.g. fuel was refilled): quietly remove one that's far away and out of sight.
    void ThinOut()
    {
        VampireAI farthest = null;
        float farDist = 18f;
        foreach (var v in alive)
        {
            float d = Vector3.Distance(v.transform.position, player.position);
            if (d > farDist && !IsInView(v.transform.position)) { farDist = d; farthest = v; }
        }
        if (farthest) farthest.Banish();
    }

    bool TrySpawn()
    {
        VampireAI v = null;
        foreach (var p in pool) if (!p.gameObject.activeSelf) { v = p; break; }
        if (!v) return false;

        // Hand-placed points first (shuffled), then random points on a ring around the player.
        var candidates = new List<Vector3>();
        if (spawnPoints != null) foreach (var sp in spawnPoints) if (sp) candidates.Add(sp.position);
        for (int i = candidates.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
        Vector3 ringCentre = FinalWave && safeZoneCentre ? safeZoneCentre.position : player.position;
        for (int i = 0; i < 12; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(minSpawnDistance + 2f, maxSpawnDistance);
            candidates.Add(ringCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
        }

        foreach (var c in candidates)
        {
            if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
            if (!IsFairSpawn(hit.position)) continue;
            v.Spawn(hit.position);
            alive.Add(v);
            AliveCount = alive.Count;
            return true;
        }
        return false;
    }

    bool IsFairSpawn(Vector3 pos)
    {
        float dist = Vector3.Distance(pos, player.position);
        float light = lantern ? lantern.LightRadius : 0f;
        if (dist < Mathf.Max(minSpawnDistance, light + lightEdgeMargin)) return false;
        if (dist > maxSpawnDistance + 4f) return false;
        if (!FinalWave && safeZoneCentre && Vector3.Distance(pos, safeZoneCentre.position) < safeZoneRadius) return false;
        if (prayer && prayer.IsActive && dist < prayer.FearRadius + 4f) return false;
        foreach (var f in FuelPickup.Active)
            if (f && Vector3.Distance(pos, f.transform.position) < pickupClearance) return false;
        if (IsInView(pos)) return false;
        return true;
    }

    bool IsInView(Vector3 pos)
    {
        if (!cam) return false;
        Vector3 vp = cam.WorldToViewportPoint(pos + Vector3.up);
        return vp.z > 0f && vp.z < 40f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
    }
}
