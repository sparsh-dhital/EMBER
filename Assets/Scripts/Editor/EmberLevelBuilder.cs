using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the single compact map (~60x60 m playable) around the radio centre:
// terrain with a walkable hill, seven landmarks, forest, rocks, pickups, lighting, fog, post-processing and the NavMesh.
public static class EmberLevelBuilder
{
    const float TerrainSize = 80f;
    const float TerrainHeight = 12f;
    const float BaseY = -1f;
    const string LevelMeshDir = "Assets/Art/Meshes/Level";

    static Terrain terrain;
    static System.Random rng;

    struct Pad { public Vector2 c; public float inner, outer; public Pad(float x, float z, float i, float o) { c = new Vector2(x, z); inner = i; outer = o; } }

    static readonly Vector2 Centre = Vector2.zero;
    static readonly Vector2 Cabin = new Vector2(-17f, 14f);
    static readonly Vector2 Truck = new Vector2(16f, 16f);
    static readonly Vector2 WatchPost = new Vector2(17f, -15f);
    static readonly Vector2 Cave = new Vector2(-19f, -17f);
    static readonly Vector2 Clearing = new Vector2(23f, -2f);
    static readonly Vector2 Supplies = new Vector2(-23f, 1f);
    static readonly Vector2 Chapel = new Vector2(1f, 25f);

    static readonly Pad[] Pads =
    {
        new Pad(0f, 0f, 10f, 14f), new Pad(-17f, 14f, 5f, 8f), new Pad(16f, 16f, 5f, 8f), new Pad(17f, -15f, 3.5f, 6f),
        new Pad(-17.5f, -15.5f, 2.5f, 4.5f), new Pad(23f, -2f, 5f, 8f), new Pad(-23f, 1f, 5f, 7f), new Pad(1f, 25f, 6f, 9f)
    };

    static List<Vector2[]> paths;

    public class Result
    {
        public Transform root, playerStart, radioCentreRoot, endingCameraAnchor;
        public RadioCentre radioCentre;
        public Light moon;
        public Transform[] spawnPoints;
        public Volume volume;
        public LocketPickup locket;
    }

    // ------------------------------------------------------------------ entry

    public static Result Build()
    {
        rng = new System.Random(1234);
        EmberArt.CreateFolderRecursive(LevelMeshDir);
        BuildPaths();

        var old = GameObject.Find("Level");
        if (old) Object.DestroyImmediate(old);
        var r = new Result();
        var root = new GameObject("Level").transform;
        r.root = root;

        terrain = BuildTerrain(root);

        var landmarks = Group("Landmarks", root, Vector3.zero, 0f);
        BuildRadioCentre(landmarks, r);
        BuildCabin(landmarks);
        BuildTruck(landmarks);
        BuildWatchPost(landmarks);
        BuildCave(landmarks);
        BuildClearing(landmarks);
        BuildSupplies(landmarks);
        r.locket = BuildChapel(landmarks);

        BuildForest(root);
        BuildRocks(root);
        BuildFuel(root);
        BuildBounds(root);
        r.playerStart = Group("PlayerStart", root, new Vector3(0f, GroundY(0f, 6.5f), 6.5f), 20f);

        r.moon = BuildLighting(root);
        r.volume = BuildPostFX(root);

        BakeNavMesh(root);
        r.spawnPoints = BuildSpawnPoints(root);

        Debug.Log("EMBER: level built.");
        return r;
    }

    // ------------------------------------------------------------------ terrain

    static void BuildPaths()
    {
        var targets = new[] { Cabin, Truck, new Vector2(11f, -11.5f), new Vector2(-16f, -13.5f), Clearing, Supplies, new Vector2(1f, 20f) };
        paths = new List<Vector2[]>();
        var prng = new System.Random(99);
        foreach (var t in targets)
        {
            Vector2 mid = t * 0.5f;
            Vector2 perp = new Vector2(-t.y, t.x).normalized;
            mid += perp * (float)(prng.NextDouble() * 6.0 - 3.0);
            paths.Add(new[] { Vector2.zero, mid, t });
        }
    }

    static float PathDistance(Vector2 p)
    {
        float best = 999f;
        foreach (var path in paths)
            for (int i = 0; i < path.Length - 1; i++)
                best = Mathf.Min(best, SegDist(p, path[i], path[i + 1]));
        return best;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static float Bump(float x, float z, float cx, float cz, float r)
    {
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
        return d >= r ? 0f : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * d / r);
    }

    static float RawHeight(float x, float z)
    {
        float h = (Mathf.PerlinNoise(x * 0.06f + 100f, z * 0.06f + 100f) - 0.5f) * 0.7f
                + (Mathf.PerlinNoise(x * 0.17f + 50f, z * 0.17f + 50f) - 0.5f) * 0.25f;
        h += 2.4f * Bump(x, z, 17f, -15f, 10f);
        h += 1.3f * Bump(x, z, -22f, -20f, 8f);
        h += 0.9f * Bump(x, z, -10f, 30f, 9f);
        h += 0.6f * Bump(x, z, 28f, 22f, 8f);
        return h;
    }

    static float Height(float x, float z)
    {
        float h = RawHeight(x, z);
        foreach (var pad in Pads)
        {
            float target = pad.c == Vector2.zero ? 0f : RawHeight(pad.c.x, pad.c.y);
            float d = Vector2.Distance(new Vector2(x, z), pad.c);
            float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pad.inner, pad.outer, d));
            h = Mathf.Lerp(h, target, w);
        }
        // Paths are slightly worn into the ground.
        float pd = PathDistance(new Vector2(x, z));
        h -= 0.08f * (1f - Mathf.SmoothStep(0f, 1f, pd / 1.8f));

        // A steep, unclimbable bank around the edge keeps the world compact.
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        if (edge > 31f) h += Mathf.Min(9f, Mathf.Pow((edge - 31f) / 5f, 2f) * 6f);
        return Mathf.Clamp(h, -0.9f, TerrainHeight - 1.1f);
    }

    static Terrain BuildTerrain(Transform root)
    {
        var data = new TerrainData { heightmapResolution = 129, alphamapResolution = 128, baseMapResolution = 256 };
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float x = -TerrainSize / 2f + xi / (float)(res - 1) * TerrainSize;
                float z = -TerrainSize / 2f + zi / (float)(res - 1) * TerrainSize;
                heights[zi, xi] = (Height(x, z) - BaseY) / TerrainHeight;
            }
        data.SetHeights(0, 0, heights);

        var layers = new[]
        {
            Layer("TL_Grass", "T_TerrainGrass", 7f),
            Layer("TL_Dirt", "T_TerrainDirt", 5f),
            Layer("TL_Litter", "T_TerrainLitter", 6f)
        };
        data.terrainLayers = layers;

        int ar = data.alphamapResolution;
        var alpha = new float[ar, ar, 3];
        for (int zi = 0; zi < ar; zi++)
            for (int xi = 0; xi < ar; xi++)
            {
                float x = -TerrainSize / 2f + (xi + 0.5f) / ar * TerrainSize;
                float z = -TerrainSize / 2f + (zi + 0.5f) / ar * TerrainSize;
                Vector2 p = new Vector2(x, z);
                float dirt = 1f - Mathf.SmoothStep(0f, 1f, (PathDistance(p) - 0.6f) / 1.6f);
                foreach (var pad in Pads) dirt = Mathf.Max(dirt, 0.8f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pad.inner * 0.5f, pad.inner, Vector2.Distance(p, pad.c)))));
                dirt *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.4f, z * 0.4f);
                float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                float litter = Mathf.Clamp01((edge - 22f) / 8f) * 0.8f + Mathf.Clamp01(Mathf.PerlinNoise(x * 0.12f + 7f, z * 0.12f + 3f) * 1.6f - 0.9f);
                litter = Mathf.Clamp01(litter) * (1f - dirt);
                float grass = Mathf.Max(0f, 1f - dirt - litter);
                alpha[zi, xi, 0] = grass;
                alpha[zi, xi, 1] = dirt;
                alpha[zi, xi, 2] = litter;
            }
        data.SetAlphamaps(0, 0, alpha);

        string dataPath = LevelMeshDir + "/TerrainData.asset";
        AssetDatabase.DeleteAsset(dataPath);
        AssetDatabase.CreateAsset(data, dataPath);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(-TerrainSize / 2f, BaseY, -TerrainSize / 2f);
        var t = go.GetComponent<Terrain>();
        t.materialTemplate = TerrainMaterial();
        t.drawInstanced = true;
        t.heightmapPixelError = 6f;
        t.basemapDistance = 45f;
        t.shadowCastingMode = ShadowCastingMode.On;
        SetStatic(go);
        return t;
    }

    static TerrainLayer Layer(string name, string tex, float tile)
    {
        string path = LevelMeshDir + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(EmberArt.TexDir + "/" + tex + ".png");
        layer.tileSize = new Vector2(tile, tile);
        layer.smoothness = 0.05f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static Material TerrainMaterial()
    {
        string path = EmberArt.MatDir + "/Terrain.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Terrain" };
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static float GroundY(float x, float z)
    {
        if (!terrain) return 0f;
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    // ------------------------------------------------------------------ primitive helpers

    static Transform Group(string name, Transform parent, Vector3 pos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    static Transform Landmark(string name, Transform parent, Vector2 xz, float yaw) => Group(name, parent, new Vector3(xz.x, GroundY(xz.x, xz.y), xz.y), yaw);

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lpos, Vector3 scale, string mat, Vector3 euler, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = EmberArt.Load(mat);
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        SetStatic(go);
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 lpos, Vector3 size, string mat, Vector3 euler = default, bool collider = true)
        => Prim(PrimitiveType.Cube, name, parent, lpos, size, mat, euler, collider);

    static GameObject Cyl(string name, Transform parent, Vector3 lpos, float radius, float height, string mat, Vector3 euler = default, bool collider = true)
        => Prim(PrimitiveType.Cylinder, name, parent, lpos, new Vector3(radius * 2f, height / 2f, radius * 2f), mat, euler, collider);

    static GameObject Ball(string name, Transform parent, Vector3 lpos, float size, string mat, bool collider = false)
        => Prim(PrimitiveType.Sphere, name, parent, lpos, Vector3.one * size, mat, Vector3.zero, collider);

    static GameObject Rock(string name, Transform parent, Vector3 lpos, Vector3 scale, float yaw, int variant, string mat = "Rock", bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(EmberArt.MeshDir + "/M_Rock" + (variant % 3) + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = EmberArt.Load(mat);
        if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; }
        SetStatic(go);
        return go;
    }

    static void SetStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
    }

    static GameObject PlacePrefab(string path, Transform parent, Vector3 worldPos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    static RadioPart PlaceRadioPart(Transform parent, Vector3 localPos, float yaw, string partName)
    {
        var go = PlacePrefab(EmberPrefabBuilder.RadioPartPath, parent, parent.TransformPoint(localPos), parent.eulerAngles.y + yaw);
        go.name = "RadioPart_" + partName.Replace(" ", "");
        var part = go.GetComponent<RadioPart>();
        part.partName = partName;
        return part;
    }

    static float YawTowardsCentre(Vector2 from) => Mathf.Atan2(-from.x, -from.y) * Mathf.Rad2Deg;

    // ------------------------------------------------------------------ landmarks

    static void BuildRadioCentre(Transform parent, Result r)
    {
        var g = Landmark("RadioCentre", parent, Centre, 0f);
        r.radioCentreRoot = g;

        // Shack, open to the north so the lamp-lit radio is visible from the start.
        Box("Floor", g, new Vector3(0f, 0.1f, -1.3f), new Vector3(3.8f, 0.2f, 3f), "WoodDark");
        Box("WallBack", g, new Vector3(0f, 1.35f, -2.75f), new Vector3(3.8f, 2.5f, 0.14f), "Wood");
        Box("WallL", g, new Vector3(-1.83f, 1.35f, -1.3f), new Vector3(0.14f, 2.5f, 3f), "Wood");
        Box("WallR", g, new Vector3(1.83f, 1.35f, -1.3f), new Vector3(0.14f, 2.5f, 3f), "Wood");
        Box("FrontL", g, new Vector3(-1.4f, 1.35f, 0.15f), new Vector3(0.9f, 2.5f, 0.14f), "Wood");
        Box("FrontR", g, new Vector3(1.4f, 1.35f, 0.15f), new Vector3(0.9f, 2.5f, 0.14f), "Wood");
        Box("Lintel", g, new Vector3(0f, 2.45f, 0.15f), new Vector3(1.9f, 0.3f, 0.14f), "Wood");
        Box("Roof", g, new Vector3(0f, 2.78f, -0.9f), new Vector3(4.4f, 0.12f, 4.4f), "Roof", new Vector3(-9f, 0f, 0f));
        Cyl("PorchPostL", g, new Vector3(-1.9f, 1.3f, 1.1f), 0.07f, 2.6f, "WoodDark");
        Cyl("PorchPostR", g, new Vector3(1.9f, 1.3f, 1.1f), 0.07f, 2.6f, "WoodDark");

        // Radio desk.
        Box("Desk", g, new Vector3(0f, 0.86f, -2.1f), new Vector3(1.8f, 0.08f, 0.8f), "Wood");
        foreach (var x in new[] { -0.8f, 0.8f }) foreach (var z in new[] { -2.4f, -1.8f })
                Box("DeskLeg", g, new Vector3(x, 0.45f, z), new Vector3(0.07f, 0.8f, 0.07f), "WoodDark", default, false);
        var radio = Box("RadioSet", g, new Vector3(0f, 1.14f, -2.2f), new Vector3(0.9f, 0.48f, 0.42f), "RadioPanel", default, false).transform;
        Box("RadioFace", g, new Vector3(0f, 1.15f, -1.985f), new Vector3(0.8f, 0.38f, 0.02f), "Metal", default, false);
        for (int i = 0; i < 3; i++) Cyl("Dial" + i, g, new Vector3(-0.25f + i * 0.2f, 1.1f, -1.97f), 0.045f, 0.03f, "Metal", new Vector3(90f, 0f, 0f), false);
        var status = Ball("StatusLight", g, new Vector3(0.3f, 1.25f, -1.97f), 0.05f, "StatusLight");
        Box("Microphone", g, new Vector3(0.55f, 0.95f, -1.9f), new Vector3(0.08f, 0.12f, 0.08f), "Metal", default, false);
        Box("Chair", g, new Vector3(0.2f, 0.45f, -1.3f), new Vector3(0.5f, 0.08f, 0.5f), "WoodDark", default, false);
        Box("ChairBack", g, new Vector3(0.2f, 0.75f, -1.05f), new Vector3(0.5f, 0.55f, 0.06f), "WoodDark", default, false);

        // Generator, drums and crates outside.
        Box("Generator", g, new Vector3(3f, 0.42f, -1.6f), new Vector3(1.2f, 0.84f, 0.75f), "RustMetal");
        Cyl("Exhaust", g, new Vector3(3.4f, 1.1f, -1.8f), 0.05f, 0.7f, "Metal", default, false);
        Cyl("DrumA", g, new Vector3(-2.7f, 0.45f, -0.6f), 0.3f, 0.9f, "RustMetal");
        Cyl("DrumB", g, new Vector3(-2.75f, 0.45f, -1.35f), 0.3f, 0.9f, "RustMetal");
        Cyl("DrumFallen", g, new Vector3(-3.3f, 0.3f, 0.5f), 0.3f, 0.9f, "RustMetal", new Vector3(90f, 30f, 0f));
        Box("Crate", g, new Vector3(2.6f, 0.35f, 0.3f), new Vector3(0.7f, 0.7f, 0.7f), "Wood", new Vector3(0f, 18f, 0f));

        // Antenna mast with guy wires and a blinking beacon.
        Vector3 mast = new Vector3(-2.4f, 0f, -3.6f);
        Cyl("MastPole", g, mast + new Vector3(0f, 5.5f, 0f), 0.08f, 11f, "Metal");
        for (int i = 0; i < 5; i++) Box("MastArm" + i, g, mast + new Vector3(0f, 3f + i * 1.8f, 0f), new Vector3(0.9f - i * 0.12f, 0.05f, 0.05f), "Metal", new Vector3(0f, i * 37f, 0f), false);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f + 20f;
            Vector3 anchor = mast + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 5f);
            Vector3 top = mast + new Vector3(0f, 9f, 0f);
            Vector3 mid = (anchor + top) * 0.5f;
            var wire = Cyl("GuyWire" + i, g, mid, 0.012f, Vector3.Distance(anchor, top), "Metal", default, false);
            wire.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top - anchor);
            Cyl("Anchor" + i, g, anchor + Vector3.up * 0.1f, 0.1f, 0.25f, "Metal", default, false);
        }
        var beacon = Ball("Beacon", g, mast + new Vector3(0f, 11.1f, 0f), 0.22f, "Beacon");

        // Warm lamp over the door: the one inviting light in the world.
        var bulb = Ball("LampBulb", g, new Vector3(1.05f, 2.25f, 0.35f), 0.14f, "WarmBulb");
        Box("LampArm", g, new Vector3(1.05f, 2.38f, 0.28f), new Vector3(0.04f, 0.04f, 0.3f), "Metal", default, false);
        var lampGo = new GameObject("LampLight");
        lampGo.transform.SetParent(g, false);
        lampGo.transform.localPosition = new Vector3(1.05f, 2.1f, 0.6f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.7f, 0.42f);
        lamp.range = 9f;
        lamp.intensity = 6f;
        lamp.shadows = LightShadows.None;
        EmberFX.GlowSprite(bulb.transform, Vector3.zero, new Color(1f, 0.7f, 0.4f, 0.35f), 6f);

        // Gameplay object.
        var centreGo = new GameObject("RadioCentreInteract");
        centreGo.transform.SetParent(g, false);
        centreGo.layer = LayerMask.NameToLayer("Interactable");
        var trigger = centreGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, -1f);
        trigger.size = new Vector3(3.2f, 2f, 3.4f);
        var rc = centreGo.AddComponent<RadioCentre>();
        rc.radioSet = radio;
        rc.statusLight = status.GetComponent<Renderer>();
        rc.antennaBeacon = beacon.GetComponent<Renderer>();

        var staticSrc = radio.gameObject.AddComponent<AudioSource>();
        staticSrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EmberAudioGenerator.LoopDir + "/loop_radio_static.wav");
        staticSrc.loop = true;
        staticSrc.playOnAwake = false;
        staticSrc.spatialBlend = 1f;
        staticSrc.rolloffMode = AudioRolloffMode.Linear;
        staticSrc.minDistance = 2f;
        staticSrc.maxDistance = 30f;
        staticSrc.volume = 0f;
        rc.staticLoop = staticSrc;

        var waves = EmberFX.Ring(g, mast + new Vector3(0f, 10.5f, 0f), new Color(0.6f, 1f, 0.85f, 0.5f), 14f, 2.2f, "SignalWaves");
        var wm = waves.main; wm.loop = true; wm.maxParticles = 4;
        var we = waves.emission; we.SetBursts(new ParticleSystem.Burst[0]); we.rateOverTime = 1.3f;
        waves.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
        rc.signalWaves = waves;

        var flareGo = new GameObject("FlareLight");
        flareGo.transform.SetParent(g, false);
        flareGo.transform.localPosition = new Vector3(0f, 3f, 2f);
        var flareLight = flareGo.AddComponent<Light>();
        flareLight.type = LightType.Point;
        flareLight.color = new Color(1f, 0.35f, 0.3f);
        flareLight.range = 45f;
        flareLight.intensity = 60f;
        flareLight.shadows = LightShadows.None;
        flareLight.enabled = false;
        rc.flareLight = flareLight;
        var flare = EmberFX.Motes(flareGo.transform, Vector3.zero, new Color(1f, 0.45f, 0.35f), 90f, 0.1f, 0.12f, 160, "FlareSparks");
        var fm = flare.main;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        fm.gravityModifier = 0.35f;
        fm.duration = 5f;
        fm.loop = false;
        var flareGlow = EmberFX.GlowSprite(flare.transform, Vector3.zero, new Color(1f, 0.5f, 0.45f, 0.8f), 3f);
        var fgm = flareGlow.main; fgm.playOnAwake = false; fgm.startLifetime = 5f; fgm.loop = false;
        flareGlow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rc.flare = flare;

        r.radioCentre = rc;
        r.endingCameraAnchor = Group("EndingCameraAnchor", g, g.position + new Vector3(9f, 5.5f, 12f), 0f);
    }

    static void BuildCabin(Transform parent)
    {
        var g = Landmark("AbandonedCabin", parent, Cabin, YawTowardsCentre(Cabin));
        Box("Floor", g, new Vector3(0f, 0.12f, 0f), new Vector3(5f, 0.24f, 4f), "WoodDark");
        Box("WallBackLow", g, new Vector3(0f, 0.75f, -1.95f), new Vector3(5f, 1.1f, 0.14f), "Wood");
        Box("WallBackHigh", g, new Vector3(0f, 2.35f, -1.95f), new Vector3(5f, 0.7f, 0.14f), "Wood");
        Box("WallBackL", g, new Vector3(-1.8f, 1.65f, -1.95f), new Vector3(1.4f, 0.8f, 0.14f), "Wood");
        Box("WallBackR", g, new Vector3(1.8f, 1.65f, -1.95f), new Vector3(1.4f, 0.8f, 0.14f), "Wood");
        Box("WallL", g, new Vector3(-2.45f, 1.35f, 0f), new Vector3(0.14f, 2.5f, 4f), "Wood");
        Box("WallR", g, new Vector3(2.45f, 1.1f, 0.4f), new Vector3(0.14f, 2f, 3.2f), "Wood", new Vector3(0f, 0f, -4f));
        Box("FrontL", g, new Vector3(-1.6f, 1.35f, 1.95f), new Vector3(1.8f, 2.5f, 0.14f), "Wood");
        Box("FrontR", g, new Vector3(1.6f, 1.35f, 1.95f), new Vector3(1.8f, 2.5f, 0.14f), "Wood");
        Box("RoofL", g, new Vector3(-1.3f, 3.1f, 0f), new Vector3(2.9f, 0.12f, 4.6f), "Roof", new Vector3(0f, 0f, 28f));
        Box("RoofR", g, new Vector3(1.1f, 3.05f, -0.6f), new Vector3(2.6f, 0.12f, 3.2f), "Roof", new Vector3(0f, 0f, -30f));
        Box("Door", g, new Vector3(0.9f, 1.05f, 2.4f), new Vector3(0.08f, 2f, 0.95f), "WoodDark", new Vector3(0f, 55f, 0f));
        Box("Step", g, new Vector3(0f, 0.08f, 2.4f), new Vector3(1.4f, 0.16f, 0.6f), "WoodDark");

        Box("Table", g, new Vector3(-1.2f, 0.82f, -1.1f), new Vector3(1.3f, 0.08f, 0.8f), "Wood");
        Box("TableLegs", g, new Vector3(-1.2f, 0.42f, -1.1f), new Vector3(1.1f, 0.8f, 0.6f), "WoodDark", default, true);
        Box("ChairTipped", g, new Vector3(0.3f, 0.3f, -0.4f), new Vector3(0.5f, 0.5f, 0.08f), "WoodDark", new Vector3(80f, 20f, 0f));
        Box("Shelf", g, new Vector3(2.2f, 1.2f, -1.2f), new Vector3(0.35f, 1.8f, 1.2f), "WoodDark");
        Box("Bed", g, new Vector3(-1.6f, 0.4f, 1f), new Vector3(1.4f, 0.4f, 1.9f), "Canvas");
        PlaceRadioPart(g, new Vector3(-1.3f, 0.86f, -1.15f), 20f, "Antenna Coil");

        for (int i = 0; i < 7; i++)
        {
            float x = -4f + i * 1.3f;
            float lean = (float)(rng.NextDouble() * 24.0 - 12.0);
            Cyl("FencePost" + i, g, new Vector3(x, 0.55f, 4.2f), 0.06f, 1.2f, "WoodDark", new Vector3(lean, 0f, lean * 0.5f));
        }
        Box("FenceRail", g, new Vector3(-1.5f, 0.85f, 4.2f), new Vector3(5f, 0.08f, 0.06f), "WoodDark", new Vector3(0f, 0f, 3f));
    }

    static void BuildTruck(Transform parent)
    {
        var g = Landmark("BrokenTruck", parent, Truck, 30f);
        var body = Group("Body", g, g.position, 30f);
        body.localRotation = Quaternion.Euler(0f, 0f, 4f);
        Box("Chassis", body, new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.3f, 5f), "RustMetal");
        Box("Cab", body, new Vector3(0f, 1.45f, 1.5f), new Vector3(1.9f, 1.4f, 1.5f), "TruckPaint");
        Box("Windshield", body, new Vector3(0f, 1.7f, 2.26f), new Vector3(1.7f, 0.7f, 0.04f), "DarkGlass", new Vector3(-12f, 0f, 0f), false);
        Box("Hood", body, new Vector3(0f, 1.05f, 2.8f), new Vector3(1.9f, 0.55f, 1.2f), "TruckPaint");
        Box("Grille", body, new Vector3(0f, 1f, 3.41f), new Vector3(1.6f, 0.45f, 0.04f), "Metal", default, false);
        Box("BedFloor", body, new Vector3(0f, 0.95f, -1.1f), new Vector3(1.9f, 0.1f, 2.7f), "WoodDark");
        Box("BedSideL", body, new Vector3(-0.92f, 1.25f, -1.1f), new Vector3(0.07f, 0.5f, 2.7f), "TruckPaint");
        Box("BedSideR", body, new Vector3(0.92f, 1.25f, -1.1f), new Vector3(0.07f, 0.5f, 2.7f), "TruckPaint");
        Box("Tailgate", body, new Vector3(0f, 0.72f, -2.65f), new Vector3(1.9f, 0.5f, 0.07f), "TruckPaint", new Vector3(-70f, 0f, 0f));
        Box("DoorOpen", body, new Vector3(1.35f, 1.35f, 1.8f), new Vector3(0.07f, 1.1f, 1.1f), "TruckPaint", new Vector3(0f, 58f, 0f));
        Box("Canvas", body, new Vector3(0.2f, 1.08f, -1.8f), new Vector3(1.2f, 0.12f, 0.9f), "Canvas", new Vector3(0f, 12f, 0f), false);
        Cyl("WheelFL", body, new Vector3(-1f, 0.45f, 1.9f), 0.45f, 0.3f, "Tyre", new Vector3(0f, 0f, 90f));
        Cyl("WheelRL", body, new Vector3(-1f, 0.45f, -1.7f), 0.45f, 0.3f, "Tyre", new Vector3(0f, 0f, 90f));
        Cyl("WheelRR", body, new Vector3(1f, 0.45f, -1.7f), 0.45f, 0.3f, "Tyre", new Vector3(0f, 0f, 90f));
        Cyl("WheelLoose", g, new Vector3(2.8f, 0.16f, 2.5f), 0.45f, 0.3f, "Tyre", new Vector3(0f, 0f, 8f));
        PlaceRadioPart(body, new Vector3(-0.3f, 1f, -1.2f), -30f, "Battery Pack");
        Rock("Rock", g, new Vector3(-2.8f, 0.2f, -1.5f), new Vector3(1.2f, 0.9f, 1f), 30f, 1);
    }

    static void BuildWatchPost(Transform parent)
    {
        // Stairs face the centre (local -X).
        var g = Landmark("WatchPost", parent, WatchPost, 41.4f);
        const float deck = 3.2f;
        foreach (var x in new[] { -1.2f, 1.2f }) foreach (var z in new[] { -1.2f, 1.2f })
            {
                Box("Leg", g, new Vector3(x, deck / 2f - 0.6f, z), new Vector3(0.2f, deck + 1.2f, 0.2f), "WoodDark");
                Box("RoofPost", g, new Vector3(x, deck + 1.1f, z), new Vector3(0.1f, 2.2f, 0.1f), "WoodDark");
            }
        Box("BraceA", g, new Vector3(0f, 1.4f, 1.2f), new Vector3(2.6f, 0.1f, 0.1f), "WoodDark", new Vector3(0f, 0f, 35f), false);
        Box("BraceB", g, new Vector3(0f, 1.4f, -1.2f), new Vector3(2.6f, 0.1f, 0.1f), "WoodDark", new Vector3(0f, 0f, -35f), false);
        Box("BraceC", g, new Vector3(1.2f, 1.4f, 0f), new Vector3(0.1f, 0.1f, 2.6f), "WoodDark", new Vector3(35f, 0f, 0f), false);
        Box("Deck", g, new Vector3(0f, deck, 0f), new Vector3(2.9f, 0.15f, 2.9f), "Wood");
        Box("RailN", g, new Vector3(0f, deck + 0.95f, 1.4f), new Vector3(2.9f, 0.08f, 0.08f), "WoodDark");
        Box("RailS", g, new Vector3(0f, deck + 0.95f, -1.4f), new Vector3(2.9f, 0.08f, 0.08f), "WoodDark");
        Box("RailE", g, new Vector3(1.4f, deck + 0.95f, 0f), new Vector3(0.08f, 0.08f, 2.9f), "WoodDark");
        Box("GuardN", g, new Vector3(0f, deck + 0.45f, 1.42f), new Vector3(2.9f, 0.9f, 0.04f), "Wood", default, true);
        Box("GuardS", g, new Vector3(0f, deck + 0.45f, -1.42f), new Vector3(2.9f, 0.9f, 0.04f), "Wood", default, true);
        Box("GuardE", g, new Vector3(1.42f, deck + 0.45f, 0f), new Vector3(0.04f, 0.9f, 2.9f), "Wood", default, true);
        Box("Roof", g, new Vector3(0f, deck + 2.25f, 0f), new Vector3(3.4f, 0.1f, 3.4f), "Roof", new Vector3(0f, 0f, 6f));

        // Real steps (0.25 m rise) so the CharacterController climbs them. The post stands on a hill, so the flight
        // keeps going downhill from the deck until it meets the terrain; every step is solid down into the ground.
        float rise = deck / 13f;
        int count = 0;
        for (int k = 0; k < 40; k++)
        {
            float top = deck - k * rise;
            float x = -1.45f - k * 0.34f - 0.17f;
            Vector3 world = g.TransformPoint(new Vector3(x, 0f, -0.5f));
            float ground = GroundY(world.x, world.z) - g.position.y;
            if (top <= ground + 0.05f) break;
            float bottom = ground - 0.5f;
            Box("Step" + k, g, new Vector3(x, (top + bottom) / 2f, -0.5f), new Vector3(0.34f, top - bottom, 1.6f), "Wood");
            count++;
        }
        float railLength = count * 0.34f;
        float railDrop = count * rise;
        Box("StairRail", g, new Vector3(-1.45f - railLength / 2f, deck - railDrop / 2f + 0.9f, -1.35f),
            new Vector3(Mathf.Sqrt(railLength * railLength + railDrop * railDrop), 0.07f, 0.07f), "WoodDark",
            new Vector3(0f, 0f, Mathf.Atan2(railDrop, railLength) * Mathf.Rad2Deg), false);

        Box("Crate", g, new Vector3(0.7f, deck + 0.3f, 0.7f), new Vector3(0.6f, 0.45f, 0.6f), "Wood", new Vector3(0f, 20f, 0f));
        PlaceRadioPart(g, new Vector3(0.65f, deck + 0.53f, 0.65f), 10f, "Transmitter Valve");
    }

    static void BuildCave(Transform parent)
    {
        var g = Landmark("CaveEntrance", parent, Cave, YawTowardsCentre(Cave));
        // A narrow rock passage leading into a dark alcove. The entrance faces the centre (+Z).
        Rock("WallL1", g, new Vector3(-2.5f, 0.8f, 2.4f), new Vector3(2.6f, 2.8f, 2.4f), 10f, 0);
        Rock("WallL2", g, new Vector3(-2.7f, 1f, -0.4f), new Vector3(2.8f, 3.4f, 2.6f), 70f, 1);
        Rock("WallL3", g, new Vector3(-2.3f, 1f, -3.2f), new Vector3(2.6f, 3.2f, 2.4f), 140f, 2);
        Rock("WallR1", g, new Vector3(2.5f, 0.8f, 2.2f), new Vector3(2.4f, 2.6f, 2.6f), 200f, 2);
        Rock("WallR2", g, new Vector3(2.8f, 1f, -0.6f), new Vector3(2.8f, 3.6f, 2.8f), 250f, 0);
        Rock("WallR3", g, new Vector3(2.2f, 1f, -3.3f), new Vector3(2.4f, 3f, 2.6f), 300f, 1);
        Rock("Back", g, new Vector3(0f, 1.2f, -5.2f), new Vector3(3.6f, 3.6f, 2.4f), 20f, 1);
        Rock("Roof", g, new Vector3(0f, 3.4f, -1.4f), new Vector3(3.8f, 1.1f, 4.8f), 90f, 0);
        Rock("Boulder1", g, new Vector3(-4.5f, 0.3f, 4.6f), new Vector3(1.4f, 1.1f, 1.3f), 45f, 2);
        Rock("Boulder2", g, new Vector3(4.2f, 0.2f, 4.9f), new Vector3(1f, 0.8f, 1.1f), 130f, 1);
        Rock("Pebble", g, new Vector3(1f, 0.1f, 3.5f), new Vector3(0.5f, 0.4f, 0.5f), 10f, 0, "Rock", false);
        Box("OldCrate", g, new Vector3(-0.4f, 0.3f, -3.4f), new Vector3(0.6f, 0.6f, 0.6f), "WoodDark", new Vector3(0f, 25f, 0f));
        PlaceRadioPart(g, new Vector3(-0.4f, 0.62f, -3.4f), 0f, "Tuning Crystal");
    }

    static void BuildClearing(Transform parent)
    {
        var g = Landmark("ForestClearing", parent, Clearing, 0f);
        Cyl("Stump", g, new Vector3(0.5f, 0.25f, 0.3f), 0.38f, 0.5f, "Bark");
        Cyl("FallenLog", g, new Vector3(-2.2f, 0.3f, 1.8f), 0.3f, 4.2f, "Bark", new Vector3(88f, 60f, 0f));
        Cyl("FallenLog2", g, new Vector3(2.5f, 0.25f, -2.2f), 0.25f, 3f, "Bark", new Vector3(90f, -20f, 0f));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Rock("FireStone" + i, g, new Vector3(-1.2f + Mathf.Cos(a) * 0.55f, 0.06f, -0.8f + Mathf.Sin(a) * 0.55f), Vector3.one * 0.22f, i * 40f, i, "Stone", false);
        }
        Cyl("CharredLogA", g, new Vector3(-1.2f, 0.08f, -0.8f), 0.06f, 0.8f, "Tyre", new Vector3(90f, 30f, 0f), false);
        Cyl("CharredLogB", g, new Vector3(-1.2f, 0.1f, -0.8f), 0.06f, 0.8f, "Tyre", new Vector3(90f, -40f, 0f), false);
        var embers = EmberFX.Embers(g, new Vector3(-1.2f, 0.12f, -0.8f), new Color(1f, 0.35f, 0.1f), 1.5f, 0.03f);
        embers.GetComponent<ParticleSystemRenderer>().sharedMaterial = EmberArt.Load("P_Spark");
        PlaceRadioPart(g, new Vector3(0.5f, 0.5f, 0.3f), 40f, "Microphone");
    }

    static void BuildSupplies(Transform parent)
    {
        var g = Landmark("SupplyCache", parent, Supplies, YawTowardsCentre(Supplies));
        Box("CrateA", g, new Vector3(-1.5f, 0.45f, -1f), Vector3.one * 0.9f, "Wood", new Vector3(0f, 10f, 0f));
        Box("CrateB", g, new Vector3(-1.4f, 1.3f, -1.1f), Vector3.one * 0.8f, "Wood", new Vector3(0f, 35f, 0f));
        Box("CrateC", g, new Vector3(-0.4f, 0.4f, -1.3f), Vector3.one * 0.8f, "WoodDark", new Vector3(0f, -12f, 0f));
        Cyl("BarrelA", g, new Vector3(1.4f, 0.45f, -1.2f), 0.3f, 0.9f, "RustMetal");
        Cyl("BarrelB", g, new Vector3(2f, 0.45f, -0.6f), 0.3f, 0.9f, "RustMetal");
        Cyl("BarrelDown", g, new Vector3(2.2f, 0.3f, 0.8f), 0.3f, 0.9f, "RustMetal", new Vector3(90f, 70f, 0f));
        foreach (var x in new[] { -2.5f, 2.5f }) Cyl("TarpPole", g, new Vector3(x, 1.1f, -2.2f), 0.05f, 2.2f, "WoodDark");
        Box("Tarp", g, new Vector3(0f, 1.9f, -1.4f), new Vector3(5.4f, 0.04f, 2.2f), "Canvas", new Vector3(-22f, 0f, 0f), false);
        Box("Workbench", g, new Vector3(0.3f, 0.75f, 1.6f), new Vector3(1.8f, 0.1f, 0.7f), "Wood");
        Box("WorkbenchLegs", g, new Vector3(0.3f, 0.35f, 1.6f), new Vector3(1.6f, 0.7f, 0.5f), "WoodDark");
    }

    static LocketPickup BuildChapel(Transform parent)
    {
        var g = Landmark("ChapelRuin", parent, Chapel, 0f);
        // Local +Z points north; the open front faces south, toward the centre.
        Box("Floor", g, new Vector3(0f, 0.08f, 0f), new Vector3(5.4f, 0.16f, 8.4f), "Stone");
        Box("WallW1", g, new Vector3(-2.6f, 1.3f, -2.6f), new Vector3(0.4f, 2.6f, 2.8f), "Stone");
        Box("WallW2", g, new Vector3(-2.6f, 0.6f, 0.4f), new Vector3(0.4f, 1.2f, 2.2f), "Stone");
        Box("WallW3", g, new Vector3(-2.6f, 1.6f, 2.9f), new Vector3(0.4f, 3.2f, 2.6f), "Stone");
        Box("WallE1", g, new Vector3(2.6f, 1f, -2.9f), new Vector3(0.4f, 2f, 2.2f), "Stone");
        Box("WallE2", g, new Vector3(2.6f, 2f, 1.9f), new Vector3(0.4f, 4f, 4.2f), "Stone");
        Box("BackWallL", g, new Vector3(-1.7f, 1.8f, 4.1f), new Vector3(2.2f, 3.6f, 0.4f), "Stone");
        Box("BackWallR", g, new Vector3(1.7f, 1.8f, 4.1f), new Vector3(2.2f, 3.6f, 0.4f), "Stone");
        Box("BackWallTop", g, new Vector3(0f, 3.9f, 4.1f), new Vector3(1.2f, 0.6f, 0.4f), "Stone");
        Box("Gable", g, new Vector3(0f, 4.5f, 4.1f), new Vector3(3.2f, 0.4f, 0.4f), "Stone", new Vector3(0f, 0f, 8f));
        Box("Rubble1", g, new Vector3(-1.6f, 0.25f, -3.8f), new Vector3(1.1f, 0.5f, 0.8f), "Stone", new Vector3(10f, 25f, 6f));
        Box("Rubble2", g, new Vector3(1.9f, 0.2f, 0.2f), new Vector3(0.7f, 0.4f, 0.6f), "Stone", new Vector3(-8f, 40f, 12f));
        Box("Pew1", g, new Vector3(-1f, 0.35f, -0.8f), new Vector3(1.5f, 0.12f, 0.45f), "WoodDark", new Vector3(0f, 6f, 0f));
        Box("Pew2", g, new Vector3(1.1f, 0.25f, -1.9f), new Vector3(1.5f, 0.12f, 0.45f), "WoodDark", new Vector3(0f, -10f, 14f));

        Box("Altar", g, new Vector3(0f, 0.55f, 3f), new Vector3(1.6f, 0.95f, 0.8f), "Stone");
        Box("AltarCloth", g, new Vector3(0f, 1.035f, 3f), new Vector3(1.7f, 0.03f, 0.5f), "Scarf", default, false);
        Box("CrossUpright", g, new Vector3(0f, 2.6f, 3.75f), new Vector3(0.22f, 2.4f, 0.2f), "Stone");
        Box("CrossBeam", g, new Vector3(0f, 3.15f, 3.75f), new Vector3(1.1f, 0.22f, 0.2f), "Stone");
        foreach (var x in new[] { -0.6f, 0.6f })
        {
            Cyl("Candle", g, new Vector3(x, 1.12f, 3.1f), 0.035f, 0.16f, "Canvas", default, false);
            var flame = Ball("CandleFlame", g, new Vector3(x, 1.23f, 3.1f), 0.035f, "CandleFlame");
            flame.transform.localScale = new Vector3(0.03f, 0.05f, 0.03f);
            EmberFX.GlowSprite(flame.transform, Vector3.zero, new Color(1f, 0.7f, 0.35f, 0.35f), 12f);
        }

        var locketGo = PlacePrefab(EmberPrefabBuilder.LocketPath, g, g.TransformPoint(new Vector3(0f, 1.05f, 3f)), 180f);
        locketGo.name = "SacredLocket";

        // Graveyard outside.
        var grng = new System.Random(5);
        for (int i = 0; i < 11; i++)
        {
            float gx = (float)(grng.NextDouble() * 9.0 - 4.5), gz = (float)(-5.5 - grng.NextDouble() * 3.5);
            if (i % 3 == 0) gx = (grng.NextDouble() < 0.5 ? -1f : 1f) * (4f + (float)grng.NextDouble() * 2f);
            if (i % 3 == 0) gz = (float)(grng.NextDouble() * 6.0 - 3.0);
            if (Mathf.Abs(gx) < 1.2f) gx = gx < 0 ? -1.6f : 1.6f;
            var tilt = new Vector3((float)(grng.NextDouble() * 16.0 - 8.0), (float)(grng.NextDouble() * 30.0 - 15.0), (float)(grng.NextDouble() * 12.0 - 6.0));
            Box("Grave" + i, g, new Vector3(gx, 0.35f, gz), new Vector3(0.55f, 0.8f, 0.14f), "Stone", tilt);
        }
        return locketGo.GetComponent<LocketPickup>();
    }

    // ------------------------------------------------------------------ nature

    static bool IsClearArea(Vector2 p, float extra)
    {
        if (p.magnitude < 12f + extra) return false;
        float[] radii = { 6.5f, 6f, 7f, 7f, 6.5f, 6.5f, 8.5f };
        Vector2[] spots = { Cabin, Truck, WatchPost + new Vector2(-3f, 3f), Cave, Clearing, Supplies, Chapel };
        for (int i = 0; i < spots.Length; i++) if (Vector2.Distance(p, spots[i]) < radii[i] + extra) return false;
        if (PathDistance(p) < 2.4f + extra) return false;
        return true;
    }

    static void BuildForest(Transform root)
    {
        var forest = Group("Forest", root, Vector3.zero, 0f);
        var colliders = Group("TreeColliders", forest, Vector3.zero, 0f);
        var temp = Group("TreeTemp", forest, Vector3.zero, 0f);

        var points = new List<Vector2>();
        for (int attempt = 0; attempt < 6000 && points.Count < 230; attempt++)
        {
            var p = new Vector2((float)(rng.NextDouble() * 76.0 - 38.0), (float)(rng.NextDouble() * 76.0 - 38.0));
            float edge = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
            bool border = edge > 29f;
            float minDist = border ? 2.2f : 3.1f;
            if (!border && rng.NextDouble() > 0.6) continue;
            if (!IsClearArea(p, 0f)) continue;
            bool ok = true;
            foreach (var q in points) if ((q - p).sqrMagnitude < minDist * minDist) { ok = false; break; }
            if (ok) points.Add(p);
        }

        var cone = AssetDatabase.LoadAssetAtPath<Mesh>(EmberArt.MeshDir + "/M_Cone.asset");
        int index = 0;
        foreach (var p in points)
        {
            float y = GroundY(p.x, p.y);
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            float yaw = (float)rng.NextDouble() * 360f;
            bool dead = rng.NextDouble() < 0.12;
            var t = Group("Tree" + index++, temp, new Vector3(p.x, y - 0.1f, p.y), yaw);
            t.localScale = Vector3.one * s;
            if (dead)
            {
                Cyl("Trunk", t, new Vector3(0f, 2.2f, 0f), 0.15f, 4.6f, "Bark", default, false);
                Cyl("BranchA", t, new Vector3(0.35f, 3.1f, 0f), 0.05f, 1.6f, "Bark", new Vector3(0f, 0f, -50f), false);
                Cyl("BranchB", t, new Vector3(-0.3f, 3.7f, 0.1f), 0.045f, 1.3f, "Bark", new Vector3(20f, 0f, 45f), false);
                Cyl("BranchC", t, new Vector3(0.1f, 2.4f, -0.35f), 0.04f, 1.1f, "Bark", new Vector3(-55f, 0f, 0f), false);
            }
            else
            {
                Cyl("Trunk", t, new Vector3(0f, 1.1f, 0f), 0.2f, 2.4f, "Bark", default, false);
                string m = rng.NextDouble() < 0.5 ? "Pine" : "PineDark";
                ConePart(t, cone, new Vector3(0f, 1.55f, 0f), 1.35f, 2.3f, m);
                ConePart(t, cone, new Vector3(0f, 2.75f, 0f), 1.1f, 2.1f, m);
                ConePart(t, cone, new Vector3(0f, 3.9f, 0f), 0.8f, 1.8f, m);
            }
            var col = new GameObject("TreeCollider");
            col.transform.SetParent(colliders, false);
            float colHeight = dead ? 3.5f : 5.5f * s;
            col.transform.position = new Vector3(p.x, y + colHeight * 0.5f, p.y);
            var cap = col.AddComponent<CapsuleCollider>();
            // Pines block roughly at the edge of their lowest branches, not just the trunk, so the player never walks into foliage.
            cap.radius = dead ? 0.22f * s : 0.8f * s;
            cap.height = colHeight;
            SetStatic(col);
        }

        CombineByMaterial(temp, forest, "Forest");
        Object.DestroyImmediate(temp.gameObject);
    }

    static void ConePart(Transform parent, Mesh cone, Vector3 lpos, float radius, float height, string mat)
    {
        var go = new GameObject("Cone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        go.transform.localScale = new Vector3(radius, height, radius);
        go.AddComponent<MeshFilter>().sharedMesh = cone;
        go.AddComponent<MeshRenderer>().sharedMaterial = EmberArt.Load(mat);
    }

    // Merges many small meshes into a few big ones per material and map quadrant: far fewer draw calls on mobile.
    static void CombineByMaterial(Transform source, Transform target, string prefix)
    {
        var groups = new Dictionary<string, List<CombineInstance>>();
        var mats = new Dictionary<string, Material>();
        foreach (var mf in source.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (!mr || !mf.sharedMesh) continue;
            Vector3 p = mf.transform.position;
            string key = mr.sharedMaterial.name + "_" + (p.x < 0 ? "W" : "E") + (p.z < 0 ? "S" : "N");
            if (!groups.ContainsKey(key)) { groups[key] = new List<CombineInstance>(); mats[key] = mr.sharedMaterial; }
            groups[key].Add(new CombineInstance { mesh = mf.sharedMesh, transform = mf.transform.localToWorldMatrix });
        }

        foreach (var kv in groups)
        {
            var mesh = new Mesh { name = prefix + "_" + kv.Key, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            string path = LevelMeshDir + "/" + mesh.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(mesh.name);
            go.transform.SetParent(target, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats[kv.Key];
            mr.shadowCastingMode = ShadowCastingMode.On;
            SetStatic(go);
        }
    }

    static void BuildRocks(Transform root)
    {
        var rocks = Group("Rocks", root, Vector3.zero, 0f);
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < 34; attempt++)
        {
            var p = new Vector2((float)(rng.NextDouble() * 62.0 - 31.0), (float)(rng.NextDouble() * 62.0 - 31.0));
            if (!IsClearArea(p, 1f)) continue;
            float s = 0.5f + (float)rng.NextDouble() * 1.4f;
            var scale = new Vector3(s * (0.8f + (float)rng.NextDouble() * 0.5f), s * (0.6f + (float)rng.NextDouble() * 0.5f), s);
            Rock("Rock" + placed, rocks, new Vector3(p.x, GroundY(p.x, p.y) + 0.1f * s, p.y), scale, (float)rng.NextDouble() * 360f, placed, placed % 4 == 0 ? "Stone" : "Rock");
            placed++;
        }
    }

    static void BuildFuel(Transform root)
    {
        var fuel = Group("FuelCans", root, Vector3.zero, 0f);
        // Near the start (safe), on the way to landmarks, at the supply cache, and a couple of risky ones.
        Vector2[] spots =
        {
            new Vector2(2.5f, 8.5f), new Vector2(-11f, 7f), new Vector2(11f, 11.5f), new Vector2(-21.5f, 3.2f),
            new Vector2(-24.2f, -1.2f), new Vector2(9f, -9f), new Vector2(-9f, -21f), new Vector2(6f, 21.5f), new Vector2(26f, 3.5f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var p = spots[i];
            var go = PlacePrefab(EmberPrefabBuilder.FuelPath, fuel, new Vector3(p.x, GroundY(p.x, p.y), p.y), (float)rng.NextDouble() * 360f);
            go.name = "FuelCan_" + (i + 1);
        }
    }

    static void BuildBounds(Transform root)
    {
        var b = Group("WorldBounds", root, Vector3.zero, 0f);
        const float e = 33.5f;
        void Wall(string n, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(n);
            go.transform.SetParent(b, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
            SetStatic(go);
        }
        Wall("North", new Vector3(0f, 5f, e), new Vector3(70f, 14f, 1f));
        Wall("South", new Vector3(0f, 5f, -e), new Vector3(70f, 14f, 1f));
        Wall("East", new Vector3(e, 5f, 0f), new Vector3(1f, 14f, 70f));
        Wall("West", new Vector3(-e, 5f, 0f), new Vector3(1f, 14f, 70f));
    }

    // ------------------------------------------------------------------ lighting & mood

    static Light BuildLighting(Transform root)
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);

        var go = new GameObject("Moonlight");
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
        var moon = go.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.5f, 0.6f, 0.9f);
        moon.intensity = 0.55f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.75f;
        RenderSettings.sun = moon;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.11f, 0.14f, 0.2f);
        RenderSettings.ambientEquatorColor = new Color(0.06f, 0.075f, 0.1f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.035f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // Fog slightly lighter than the trees, so the forest reads as layered silhouettes.
        RenderSettings.fogColor = new Color(0.06f, 0.075f, 0.1f);
        RenderSettings.fogDensity = 0.042f;
        RenderSettings.reflectionIntensity = 0.25f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        return moon;
    }

    static Volume BuildPostFX(Transform root)
    {
        const string path = "Assets/Settings/Ember_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var tone = profile.Add<Tonemapping>(true);
        // Neutral keeps the moonlit darks readable; ACES crushed them to black.
        tone.mode.Override(TonemappingMode.Neutral);
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.85f);
        bloom.scatter.Override(0.68f);
        bloom.tint.Override(new Color(1f, 0.92f, 0.82f));
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.3f);
        vig.smoothness.Override(0.45f);
        vig.color.Override(Color.black);
        var col = profile.Add<ColorAdjustments>(true);
        col.postExposure.Override(0.2f);
        col.contrast.Override(10f);
        col.saturation.Override(-12f);
        col.colorFilter.Override(new Color(0.94f, 0.97f, 1f));
        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0f);
        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.92f, 0.97f, 1.08f, 0f));
        smh.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0f));
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = new GameObject("PostFX");
        go.transform.SetParent(root, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.priority = 1f;
        return volume;
    }

    // ------------------------------------------------------------------ navigation

    static void BakeNavMesh(Transform root)
    {
        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Interactable")));
        surface.BuildNavMesh();

        string path = "Assets/Scenes/Ember_Main_NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }

    static Transform[] BuildSpawnPoints(Transform root)
    {
        var group = Group("VampireSpawnPoints", root, Vector3.zero, 0f);
        var list = new List<Transform>();
        var srng = new System.Random(77);
        for (int attempt = 0; attempt < 400 && list.Count < 18; attempt++)
        {
            float a = (float)(srng.NextDouble() * Mathf.PI * 2.0);
            float d = 16f + (float)srng.NextDouble() * 13f;
            var p = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
            if (!IsClearArea(p, -1f)) continue;
            var world = new Vector3(p.x, GroundY(p.x, p.y), p.y);
            if (!NavMesh.SamplePosition(world, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            bool near = false;
            foreach (var t in list) if (Vector3.Distance(t.position, hit.position) < 6f) near = true;
            if (near) continue;
            list.Add(Group("SpawnPoint" + list.Count, group, hit.position, 0f));
        }
        return list.ToArray();
    }
}
