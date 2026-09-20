using System;
using UnityEngine;

/// <summary>
/// Runtime knowledge of the archipelago.
///
/// The level builder owns the island shapes, but it lives in the editor assembly and cannot
/// be reached from gameplay code. This component is written into the scene at build time
/// carrying the same numbers, so the boat, the HUD and the objective system can all answer
/// "which island is this?" and "where is the water?" without duplicating the definitions.
/// </summary>
[DefaultExecutionOrder(-90)]
public class EmberIslands : MonoBehaviour
{
    [Serializable]
    public class IslandData
    {
        public string name = "Island";
        public Vector2 centre;
        public float radius = 20f;
        [Tooltip("Jetties, just off the shore. A hub island has one facing each neighbour.")]
        public Vector2[] docks = System.Array.Empty<Vector2>();
    }

    [Header("World")]
    public IslandData[] islands = Array.Empty<IslandData>();
    [Tooltip("World height of the waterline.")]
    public float seaLevel = 0.35f;
    [Tooltip("What counts as ground when probing for a landing spot.")]
    public LayerMask groundLayers = ~0;

    static EmberIslands instance;

    public static float SeaLevel => instance ? instance.seaLevel : 0.35f;
    public static int Count => instance && instance.islands != null ? instance.islands.Length : 0;

    void Awake() => instance = this;
    void OnEnable() => instance = this;
    void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>Index of the island containing this point, or -1 when it is at sea.</summary>
    public static int IslandAt(Vector2 p)
    {
        if (!instance || instance.islands == null) return -1;
        // Nearest containing island, so overlapping shorelines resolve sensibly.
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < instance.islands.Length; i++)
        {
            var isle = instance.islands[i];
            float d = Vector2.Distance(p, isle.centre);
            if (d > isle.radius || d >= bestDistance) continue;
            best = i;
            bestDistance = d;
        }
        return best;
    }

    public static int IslandAt(Vector3 world) => IslandAt(new Vector2(world.x, world.z));

    public static string Name(int index)
    {
        if (!instance || instance.islands == null) return string.Empty;
        if (index < 0 || index >= instance.islands.Length) return string.Empty;
        return instance.islands[index].name;
    }

    /// <summary>A jetty position for an island, at the waterline.</summary>
    public static Vector3 Dock(int index, int which = 0)
    {
        if (!instance || instance.islands == null) return Vector3.zero;
        if (index < 0 || index >= instance.islands.Length) return Vector3.zero;
        var docks = instance.islands[index].docks;
        if (docks == null || docks.Length == 0) return Vector3.zero;
        var d = docks[Mathf.Clamp(which, 0, docks.Length - 1)];
        return new Vector3(d.x, SeaLevel, d.y);
    }

    /// <summary>The jetty of <paramref name="index"/> that lies closest to a point.</summary>
    public static Vector3 NearestDock(int index, Vector3 to)
    {
        if (!instance || instance.islands == null) return Vector3.zero;
        if (index < 0 || index >= instance.islands.Length) return Vector3.zero;
        var docks = instance.islands[index].docks;
        if (docks == null || docks.Length == 0) return Vector3.zero;

        var flat = new Vector2(to.x, to.z);
        Vector2 best = docks[0];
        float bestDistance = float.MaxValue;
        foreach (var d in docks)
        {
            float dist = Vector2.Distance(flat, d);
            if (dist >= bestDistance) continue;
            best = d;
            bestDistance = dist;
        }
        return new Vector3(best.x, SeaLevel, best.y);
    }

    /// <summary>
    /// Finds the ground directly under a world point. Returns false when there is nothing
    /// solid there at all, which in practice means open sea.
    /// </summary>
    public static bool TryGetGround(Vector3 world, out Vector3 ground)
    {
        ground = world;
        var origin = new Vector3(world.x, world.y + 12f, world.z);
        int mask = instance ? instance.groundLayers.value : ~0;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 40f, mask, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Height of the terrain itself under a point, ignoring props entirely.
    ///
    /// The boat needs this rather than a raycast: moored at a jetty, a downward ray hits the
    /// planking above it and the hull reports itself aground on open water.
    /// </summary>
    public static float SeabedHeight(Vector3 world)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return TryGetGround(world, out Vector3 g) ? g.y : world.y;
        return t.SampleHeight(world) + t.transform.position.y;
    }

    /// <summary>Water depth at a point. Negative when the ground is above the waterline.</summary>
    public static float DepthAt(Vector3 world) => SeaLevel - SeabedHeight(world);

    /// <summary>True when this point is dry land the player can stand on.</summary>
    public static bool IsLand(Vector3 world)
    {
        return TryGetGround(world, out Vector3 ground) && ground.y > SeaLevel + 0.2f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (islands == null) return;
        foreach (var isle in islands)
        {
            Gizmos.color = new Color(0.4f, 0.85f, 0.5f, 0.5f);
            DrawCircle(new Vector3(isle.centre.x, seaLevel, isle.centre.y), isle.radius);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            if (isle.docks == null) continue;
            foreach (var d in isle.docks)
                Gizmos.DrawWireSphere(new Vector3(d.x, seaLevel, d.y), 1.5f);
        }
    }

    static void DrawCircle(Vector3 centre, float radius, int segments = 48)
    {
        Vector3 prev = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
