using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor helpers that generate EMBER's placeholder art in code: textures, UI sprites, materials and simple meshes.
// Everything is saved under Assets/Art so it can be swapped for real assets later.
public static class EmberArt
{
    public const string ArtRoot = "Assets/Art";
    public const string TexDir = "Assets/Art/Textures";
    public const string SpriteDir = "Assets/Art/UI";
    public const string MatDir = "Assets/Art/Materials";
    public const string MeshDir = "Assets/Art/Meshes";

    [MenuItem("EMBER/Build/1. Generate Art (textures, sprites, materials, meshes)")]
    public static void BuildAll()
    {
        EnsureFolders();
        BuildTextures();
        BuildSprites();
        BuildMeshes();
        BuildMaterials();
        AssetDatabase.SaveAssets();
        Debug.Log("EMBER: art generated.");
    }

    public static void EnsureFolders()
    {
        foreach (var dir in new[] { TexDir, SpriteDir, MatDir, MeshDir, "Assets/Art/Fonts", "Assets/Prefabs/Pickups", "Assets/Prefabs/Enemies", "Assets/Prefabs/Player", "Assets/Prefabs/Environment", "Assets/Animation" })
            CreateFolderRecursive(dir);
    }

    public static void CreateFolderRecursive(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        CreateFolderRecursive(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ------------------------------------------------------------------ textures

    static void BuildTextures()
    {
        SaveTexture(TexDir + "/T_GroundNoise.png", 256, (x, y) =>
        {
            float n = Fbm(x / 256f * 8f, y / 256f * 8f, 4, true, 8);
            float v = 0.72f + 0.28f * n;
            return new Color(v, v, v, 1f);
        }, true, false);

        SaveTexture(TexDir + "/T_Grain.png", 128, (x, y) =>
        {
            float n = Fbm(x / 128f * 16f, y / 128f * 16f, 3, true, 16);
            float v = 0.8f + 0.2f * n;
            return new Color(v, v, v, 1f);
        }, true, false);

        SaveTexture(TexDir + "/T_WoodGrain.png", 128, (x, y) =>
        {
            float u = x / 128f, w = y / 128f;
            float grain = Mathf.Sin((u * 40f + Fbm(u * 4f, w * 1f, 3, true, 4) * 6f) * Mathf.PI) * 0.5f + 0.5f;
            float v = 0.75f + 0.25f * grain;
            return new Color(v, v * 0.97f, v * 0.94f, 1f);
        }, true, false);

        SaveTexture(TexDir + "/T_SoftGlow.png", 128, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            a *= a;
            return new Color(1f, 1f, 1f, a);
        }, false, true);

        SaveTexture(TexDir + "/T_Spark.png", 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            float a = Mathf.Clamp01(1f - d);
            a = Mathf.Pow(a, 3f);
            return new Color(1f, 1f, 1f, a);
        }, false, true);

        SaveTexture(TexDir + "/T_Smoke.png", 128, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
            float n = Fbm(x / 128f * 5f, y / 128f * 5f, 4, false, 0);
            float a = Mathf.Clamp01(1f - d) * (0.55f + 0.45f * n);
            a = Mathf.SmoothStep(0f, 1f, a);
            return new Color(1f, 1f, 1f, a * 0.8f);
        }, false, true);

        SaveTexture(TexDir + "/T_Ring.png", 128, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
            float a = Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.08f, 2f));
            return new Color(1f, 1f, 1f, a);
        }, false, true);

        // Terrain layers: dark grass, trodden dirt, and leaf litter.
        SaveTexture(TexDir + "/T_TerrainGrass.png", 256, (x, y) =>
        {
            float n = Fbm(x / 256f * 6f, y / 256f * 6f, 5, true, 6);
            float blades = Fbm(x / 256f * 48f, y / 256f * 48f, 2, true, 48);
            Color a = new Color(0.13f, 0.17f, 0.10f), b = new Color(0.19f, 0.22f, 0.13f);
            return WithAlpha(Color.Lerp(a, b, 0.5f + 0.5f * n) * (0.85f + 0.15f * blades), 0.08f);
        }, true, false);
        SaveTexture(TexDir + "/T_TerrainDirt.png", 256, (x, y) =>
        {
            float n = Fbm(x / 256f * 5f, y / 256f * 5f, 5, true, 5);
            float pebbles = Mathf.Clamp01(Fbm(x / 256f * 32f, y / 256f * 32f, 2, true, 32) * 3f - 1.6f);
            Color a = new Color(0.20f, 0.16f, 0.12f), b = new Color(0.28f, 0.23f, 0.17f);
            return WithAlpha(Color.Lerp(Color.Lerp(a, b, 0.5f + 0.5f * n), new Color(0.36f, 0.34f, 0.31f), pebbles), 0.12f);
        }, true, false);
        SaveTexture(TexDir + "/T_TerrainLitter.png", 256, (x, y) =>
        {
            float n = Fbm(x / 256f * 10f, y / 256f * 10f, 4, true, 10);
            float leaves = Fbm(x / 256f * 24f, y / 256f * 24f, 3, true, 24);
            Color a = new Color(0.12f, 0.09f, 0.06f), b = new Color(0.22f, 0.14f, 0.08f);
            return WithAlpha(Color.Lerp(a, b, Mathf.Clamp01(0.5f + n * 0.5f + leaves * 0.4f)), 0.1f);
        }, true, false);
    }

    // ------------------------------------------------------------------ UI sprites

    static void BuildSprites()
    {
        // Circle (for joystick, buttons, bars).
        SaveSprite(SpriteDir + "/S_Circle.png", 256, (x, y) => Disc(x, y, 256, 127f, 1.5f), Vector4.zero);
        // Thin ring.
        SaveSprite(SpriteDir + "/S_Ring.png", 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(128f, 128f));
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 118f) / 5f);
            return new Color(1f, 1f, 1f, a);
        }, Vector4.zero);
        // Thick ring for the prayer countdown (filled radially).
        SaveSprite(SpriteDir + "/S_RingThick.png", 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(128f, 128f));
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 112f) / 8f) ;
            a = Mathf.Clamp01(a * 2f);
            return new Color(1f, 1f, 1f, a);
        }, Vector4.zero);
        // Soft radial glow.
        SaveSprite(SpriteDir + "/S_Glow.png", 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(128f, 128f)) / 128f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }, Vector4.zero);
        // Rounded rectangle, 9-sliced.
        SaveSprite(SpriteDir + "/S_Rounded.png", 64, (x, y) =>
        {
            float r = 16f;
            float px = Mathf.Clamp(x + 0.5f, r, 64f - r), py = Mathf.Clamp(y + 0.5f, r, 64f - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(px, py));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
        }, new Vector4(20, 20, 20, 20));
        // Screen-edge vignette for the damage flash.
        SaveSprite(SpriteDir + "/S_EdgeVignette.png", 256, (x, y) =>
        {
            float u = (x + 0.5f) / 256f * 2f - 1f, v = (y + 0.5f) / 256f * 2f - 1f;
            float d = Mathf.Sqrt(u * u * 0.8f + v * v);
            float a = Mathf.SmoothStep(0.55f, 1.35f, d);
            return new Color(1f, 1f, 1f, a);
        }, Vector4.zero);
        // Horizontal gradient for the title screen backdrop.
        SaveSprite(SpriteDir + "/S_GradientLeft.png", 256, (x, y) =>
        {
            float a = 1f - Mathf.SmoothStep(0.2f, 1f, (x + 0.5f) / 256f);
            return new Color(1f, 1f, 1f, a);
        }, Vector4.zero);
        // Chevron arrow (points up).
        SaveSprite(SpriteDir + "/S_Chevron.png", 128, (x, y) =>
        {
            Vector2 p = new Vector2((x + 0.5f) / 128f - 0.5f, (y + 0.5f) / 128f - 0.5f);
            float d1 = SegmentDistance(p, new Vector2(-0.3f, -0.12f), new Vector2(0f, 0.2f));
            float d2 = SegmentDistance(p, new Vector2(0.3f, -0.12f), new Vector2(0f, 0.2f));
            float a = Mathf.Clamp01((0.07f - Mathf.Min(d1, d2)) * 128f);
            return new Color(1f, 1f, 1f, a);
        }, Vector4.zero);
        // Flame icon.
        SaveSprite(SpriteDir + "/S_Flame.png", 128, (x, y) =>
        {
            Vector2 p = new Vector2((x + 0.5f) / 128f - 0.5f, (y + 0.5f) / 128f - 0.35f);
            float outer = FlameShape(p, 1f);
            float inner = FlameShape(new Vector2(p.x, p.y + 0.06f) * 1.8f, 1f);
            float a = Mathf.Clamp01(outer * 60f);
            float hole = Mathf.Clamp01(inner * 60f) * 0.55f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a - hole));
        }, Vector4.zero);
        // Latin cross icon.
        SaveSprite(SpriteDir + "/S_Cross.png", 128, (x, y) =>
        {
            float u = (x + 0.5f) / 128f, v = (y + 0.5f) / 128f;
            bool vertical = Mathf.Abs(u - 0.5f) < 0.07f && v > 0.08f && v < 0.92f;
            bool horizontal = Mathf.Abs(v - 0.66f) < 0.07f && u > 0.25f && u < 0.75f;
            return new Color(1f, 1f, 1f, vertical || horizontal ? 1f : 0f);
        }, Vector4.zero);
        // Signal bar (rounded).
        SaveSprite(SpriteDir + "/S_Bar.png", 32, (x, y) =>
        {
            float r = 6f;
            float px = Mathf.Clamp(x + 0.5f, r, 32f - r), py = Mathf.Clamp(y + 0.5f, r, 32f - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(px, py));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
        }, new Vector4(8, 8, 8, 8));
    }

    // URP terrain reads smoothness from the albedo alpha, so terrain textures store a low value there.
    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    static float FlameShape(Vector2 p, float scale)
    {
        // Teardrop: circle at the bottom tapering to a point at the top.
        float r = 0.22f * scale;
        float y = p.y;
        float width = y < 0f ? Mathf.Sqrt(Mathf.Max(0f, r * r - y * y)) : r * Mathf.Pow(Mathf.Max(0f, 1f - y / (0.5f * scale)), 1.4f);
        return width - Mathf.Abs(p.x + Mathf.Sin(y * 9f) * 0.02f * Mathf.Max(0f, y) * 10f);
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static Color Disc(int x, int y, int size, float radius, float feather)
    {
        float c = size * 0.5f;
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
        return new Color(1f, 1f, 1f, Mathf.Clamp01((radius - d) / feather));
    }

    // ------------------------------------------------------------------ meshes

    static void BuildMeshes()
    {
        SaveMesh(MakeCone(10, 1f, 1f), MeshDir + "/M_Cone.asset");
        for (int i = 0; i < 3; i++) SaveMesh(MakeRock(i * 17 + 3), MeshDir + "/M_Rock" + i + ".asset");
    }

    public static Mesh MakeCone(int sides, float radius, float height)
    {
        var mesh = new Mesh { name = "Cone" };
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        var normals = new System.Collections.Generic.List<Vector3>();
        float slope = radius / height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            Vector3 top = new Vector3(0f, height, 0f);
            Vector3 n = Vector3.Cross(p1 - p0, top - p0).normalized;
            if (n.y < 0f) n = -n;
            int b = verts.Count;
            verts.Add(p0); verts.Add(top); verts.Add(p1);
            normals.Add(n); normals.Add(n); normals.Add(n);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            // base cap
            int c = verts.Count;
            verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
            normals.Add(Vector3.down); normals.Add(Vector3.down); normals.Add(Vector3.down);
            tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
        }
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        FixWinding(mesh);
        return mesh;
    }

    // Low-poly rock: an icosahedron with its vertices pushed around, flat shaded.
    public static Mesh MakeRock(int seed)
    {
        var rng = new System.Random(seed);
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        for (int i = 0; i < v.Length; i++)
        {
            float s = 0.75f + (float)rng.NextDouble() * 0.5f;
            v[i] = v[i].normalized * s;
            v[i].y *= 0.62f;
        }
        var verts = new Vector3[f.Length];
        var tris = new int[f.Length];
        for (int i = 0; i < f.Length; i++) { verts[i] = v[f[i]]; tris[i] = i; }
        var mesh = new Mesh { name = "Rock" + seed, vertices = verts, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        FixWinding(mesh);
        return mesh;
    }

    // Makes sure every triangle faces outward from the mesh centre (both generators are convex).
    static void FixWinding(Mesh mesh)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        Vector3 centre = mesh.bounds.center;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - centre) < 0f) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
        }
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }

    static void SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); return; }
        AssetDatabase.CreateAsset(mesh, path);
    }

    // ------------------------------------------------------------------ materials

    static void BuildMaterials()
    {
        var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_Grain.png");
        var groundTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_GroundNoise.png");
        var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_WoodGrain.png");

        Lit("Ground", new Color(0.16f, 0.19f, 0.13f), 0.05f, groundTex, new Vector2(24f, 24f));
        Lit("Dirt", new Color(0.23f, 0.19f, 0.15f), 0.05f, groundTex, new Vector2(10f, 10f));
        Lit("Rock", new Color(0.30f, 0.30f, 0.31f), 0.1f, grain, Vector2.one * 2f);
        Lit("Stone", new Color(0.42f, 0.41f, 0.39f), 0.12f, grain, Vector2.one * 2f);
        Lit("Wood", new Color(0.30f, 0.21f, 0.14f), 0.15f, wood, Vector2.one);
        Lit("WoodDark", new Color(0.17f, 0.12f, 0.09f), 0.1f, wood, Vector2.one);
        Lit("Bark", new Color(0.16f, 0.12f, 0.09f), 0.05f, grain, Vector2.one);
        Lit("Pine", new Color(0.08f, 0.14f, 0.10f), 0.05f, grain, Vector2.one);
        Lit("PineDark", new Color(0.05f, 0.09f, 0.07f), 0.05f, grain, Vector2.one);
        Lit("Metal", new Color(0.32f, 0.33f, 0.35f), 0.45f, grain, Vector2.one, 0.6f);
        Lit("RustMetal", new Color(0.33f, 0.19f, 0.12f), 0.25f, grain, Vector2.one, 0.3f);
        Lit("Roof", new Color(0.20f, 0.13f, 0.11f), 0.1f, grain, Vector2.one * 2f);
        Lit("TruckPaint", new Color(0.18f, 0.24f, 0.21f), 0.35f, grain, Vector2.one, 0.2f);
        Lit("Tyre", new Color(0.05f, 0.05f, 0.05f), 0.2f, null, Vector2.one);
        Lit("Canvas", new Color(0.26f, 0.25f, 0.18f), 0.05f, grain, Vector2.one);
        Lit("DarkGlass", new Color(0.03f, 0.04f, 0.05f), 0.9f, null, Vector2.one);
        Lit("RadioPanel", new Color(0.12f, 0.14f, 0.13f), 0.4f, grain, Vector2.one, 0.4f);

        // Characters.
        Lit("Coat", new Color(0.22f, 0.19f, 0.14f), 0.12f, grain, Vector2.one);
        Lit("Pants", new Color(0.12f, 0.13f, 0.15f), 0.1f, grain, Vector2.one);
        Lit("Boots", new Color(0.09f, 0.07f, 0.06f), 0.25f, null, Vector2.one);
        Lit("Skin", new Color(0.66f, 0.50f, 0.40f), 0.3f, null, Vector2.one);
        Lit("Beanie", new Color(0.38f, 0.14f, 0.10f), 0.05f, grain, Vector2.one);
        Lit("Scarf", new Color(0.42f, 0.36f, 0.26f), 0.05f, grain, Vector2.one);
        var vampSkin = Lit("VampireSkin", new Color(0.30f, 0.035f, 0.05f), 0.35f, grain, Vector2.one);
        Emissive(vampSkin, new Color(0.06f, 0.003f, 0.006f));
        var vampFace = Lit("VampireFace", new Color(0.55f, 0.07f, 0.08f), 0.45f, null, Vector2.one);
        Emissive(vampFace, new Color(0.12f, 0.01f, 0.01f));
        Lit("VampireCloak", new Color(0.10f, 0.015f, 0.025f), 0.2f, grain, Vector2.one);
        Emissive(Lit("VampireEye", new Color(1f, 0.1f, 0.05f), 0.8f, null, Vector2.one), new Color(6f, 0.25f, 0.12f));

        // Lantern.
        Lit("LanternMetal", new Color(0.18f, 0.16f, 0.14f), 0.55f, grain, Vector2.one, 0.7f);
        Lit("LanternBrass", new Color(0.55f, 0.40f, 0.18f), 0.6f, null, Vector2.one, 0.8f);
        var glass = Lit("LanternGlass", new Color(1f, 0.85f, 0.6f, 0.22f), 0.95f, null, Vector2.one);
        MakeTransparent(glass);
        Emissive(glass, new Color(1.1f, 0.5f, 0.16f));
        Unlit("Flame", new Color(4f, 1.9f, 0.55f), false);

        // Pickups and mission objects.
        Lit("FuelCan", new Color(0.46f, 0.14f, 0.08f), 0.45f, grain, Vector2.one, 0.35f);
        Emissive(Lit("FuelLabel", new Color(0.9f, 0.6f, 0.2f), 0.4f, null, Vector2.one), new Color(1.6f, 0.75f, 0.18f));
        Emissive(Lit("RadioLED", new Color(0.2f, 1f, 0.5f), 0.6f, null, Vector2.one), new Color(0.4f, 2.2f, 1.1f));
        Emissive(Lit("StatusLight", new Color(1f, 0.1f, 0.05f), 0.6f, null, Vector2.one), new Color(1.2f, 0.05f, 0.03f));
        Emissive(Lit("Beacon", new Color(1f, 0.1f, 0.05f), 0.6f, null, Vector2.one), new Color(4f, 0.2f, 0.1f));
        Emissive(Lit("Gold", new Color(0.85f, 0.65f, 0.28f), 0.85f, null, Vector2.one, 1f), new Color(0.5f, 0.35f, 0.1f));
        Emissive(Lit("WarmBulb", new Color(1f, 0.8f, 0.5f), 0.6f, null, Vector2.one), new Color(3f, 1.8f, 0.8f));
        Emissive(Lit("CandleFlame", new Color(1f, 0.7f, 0.3f), 0.5f, null, Vector2.one), new Color(4f, 2f, 0.6f));
        // The cross uses URP Lit with strong emission so bloom makes it radiate.
        var holy = Lit("HolyLight", new Color(1f, 0.93f, 0.75f), 0.5f, null, Vector2.one);
        Emissive(holy, new Color(6f, 4.6f, 2.6f));

        // Particles (additive, soft).
        var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_SoftGlow.png");
        var spark = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_Spark.png");
        var smoke = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_Smoke.png");
        var ring = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_Ring.png");
        Particle("P_Glow", glow, true);
        Particle("P_Spark", spark, true);
        Particle("P_Smoke", smoke, false);
        Particle("P_Ring", ring, true);
        Particle("P_Dust", spark, false);
    }

    public static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");

    static Material GetOrCreate(string name, Shader shader)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader) mat.shader = shader;
        return mat;
    }

    static Material Lit(string name, Color color, float smoothness, Texture2D tex, Vector2 tiling, float metallic = 0f)
    {
        var mat = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (tex)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Emissive(Material mat, Color hdr)
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", hdr);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
    }

    static Material Unlit(string name, Color color, bool transparent)
    {
        var mat = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        if (transparent) MakeTransparent(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material Particle(string name, Texture2D tex, bool additive)
    {
        var mat = GetOrCreate(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) mat.EnableKeyword("_BLENDMODE_ADD"); else mat.DisableKeyword("_BLENDMODE_ADD");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ texture helpers

    delegate Color Pixel(int x, int y);

    static void SaveTexture(string path, int size, Pixel pixel, bool repeat, bool alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = alpha;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = size;
        imp.SaveAndReimport();
    }

    static void SaveSprite(string path, int size, Pixel pixel, Vector4 border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spriteBorder = border;
        imp.SaveAndReimport();
    }

    public static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");

    // Tileable fractal noise.
    static float Fbm(float x, float y, int octaves, bool tile, int period)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n;
            if (tile)
            {
                float p = period * freq;
                float fx = x * freq, fy = y * freq;
                float a = Mathf.PerlinNoise(fx, fy), b = Mathf.PerlinNoise(fx - p, fy);
                float c = Mathf.PerlinNoise(fx, fy - p), d = Mathf.PerlinNoise(fx - p, fy - p);
                float u = (fx % p) / p, v = (fy % p) / p;
                n = Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
            }
            else n = Mathf.PerlinNoise(x * freq + 13.1f, y * freq + 7.7f);
            sum += n * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm * 2f - 1f;
    }
}
