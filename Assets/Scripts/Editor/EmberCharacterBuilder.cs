using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the player character: a jointed humanoid made from primitives (swap for a Mixamo model later),
// a physical lantern held in the right hand, the prayer cross, and an Animator Controller with generated clips.
public static class EmberCharacterBuilder
{
    public const string AnimDir = "Assets/Animation";
    public const string ControllerPath = AnimDir + "/Player.controller";

    // Bone paths relative to the Animator (the "Model" object).
    const string H = "Hips", SP = H + "/Spine", CH = SP + "/Chest", NK = CH + "/Neck", HD = NK + "/Head";
    const string UAL = CH + "/ShoulderL/UpperArmL", FAL = UAL + "/ForearmL", HAL = FAL + "/HandL";
    const string UAR = CH + "/ShoulderR/UpperArmR", FAR = UAR + "/ForearmR", HAR = FAR + "/HandR";
    const string ULL = H + "/UpperLegL", LLL = ULL + "/LowerLegL", FTL = LLL + "/FootL";
    const string ULR = H + "/UpperLegR", LLR = ULR + "/LowerLegR", FTR = LLR + "/FootR";
    static readonly string[] Bones = { H, SP, CH, NK, HD, UAL, FAL, HAL, UAR, FAR, HAR, ULL, LLL, FTL, ULR, LLR, FTR };

    public class PlayerParts
    {
        public Transform model, cameraTarget, lanternRoot, flameGroup, crossRoot;
        public Animator animator;
        public Light lanternLight, holyLight;
        public Renderer flameRenderer, glassRenderer;
        public Renderer[] crossRenderers;
        public ParticleSystem embers, smoke, pickupFlare, prayerMotes, prayerRing;
    }

    [MenuItem("EMBER/Build/2. Player Animations")]
    public static void BuildAnimationsMenu() => BuildAnimatorController();

    // ------------------------------------------------------------------ model

    public static PlayerParts BuildModel(GameObject player)
    {
        var p = new PlayerParts();
        var old = player.transform.Find("Model");
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var oldCross = player.transform.Find("PrayerCross");
        if (oldCross) UnityEngine.Object.DestroyImmediate(oldCross.gameObject);
        foreach (var n in new[] { "CameraTarget", "HolyLight", "PrayerMotes", "PrayerRing" })
        {
            var t = player.transform.Find(n);
            if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        Material coat = EmberArt.Load("Coat"), pants = EmberArt.Load("Pants"), boots = EmberArt.Load("Boots"),
                 skin = EmberArt.Load("Skin"), beanie = EmberArt.Load("Beanie"), scarf = EmberArt.Load("Scarf"), canvas = EmberArt.Load("Canvas");

        var model = Bone("Model", player.transform, Vector3.zero);
        p.model = model;
        var hips = Bone("Hips", model, new Vector3(0f, 0.98f, 0f));
        Part("Pelvis", PrimitiveType.Cube, hips, new Vector3(0f, -0.02f, 0f), new Vector3(0.33f, 0.2f, 0.22f), pants);
        Part("CoatSkirt", PrimitiveType.Cube, hips, new Vector3(0f, -0.1f, -0.01f), new Vector3(0.4f, 0.32f, 0.27f), coat);

        var spine = Bone("Spine", hips, new Vector3(0f, 0.1f, 0f));
        Part("Belly", PrimitiveType.Cube, spine, new Vector3(0f, 0.08f, 0f), new Vector3(0.37f, 0.22f, 0.24f), coat);
        var chest = Bone("Chest", spine, new Vector3(0f, 0.2f, 0f));
        Part("Torso", PrimitiveType.Capsule, chest, new Vector3(0f, 0.1f, 0f), new Vector3(0.44f, 0.2f, 0.28f), coat);
        Part("Scarf", PrimitiveType.Cube, chest, new Vector3(0f, 0.23f, 0.015f), new Vector3(0.27f, 0.08f, 0.25f), scarf);
        Part("Pack", PrimitiveType.Cube, chest, new Vector3(0f, 0.04f, -0.19f), new Vector3(0.3f, 0.34f, 0.13f), canvas);
        Part("PackRoll", PrimitiveType.Cylinder, chest, new Vector3(0f, 0.25f, -0.19f), new Vector3(0.12f, 0.17f, 0.12f), coat, new Vector3(0f, 0f, 90f));

        var neck = Bone("Neck", chest, new Vector3(0f, 0.24f, 0f));
        Part("NeckMesh", PrimitiveType.Cylinder, neck, new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skin);
        var head = Bone("Head", neck, new Vector3(0f, 0.07f, 0f));
        Part("Face", PrimitiveType.Sphere, head, new Vector3(0f, 0.11f, 0.01f), new Vector3(0.2f, 0.24f, 0.22f), skin);
        Part("Nose", PrimitiveType.Cube, head, new Vector3(0f, 0.1f, 0.115f), new Vector3(0.03f, 0.045f, 0.035f), skin);
        Part("Beanie", PrimitiveType.Sphere, head, new Vector3(0f, 0.165f, -0.005f), new Vector3(0.225f, 0.17f, 0.235f), beanie);
        Part("BeanieRim", PrimitiveType.Cylinder, head, new Vector3(0f, 0.14f, 0f), new Vector3(0.23f, 0.025f, 0.24f), beanie);

        Transform handR = BuildArm("R", chest, 1f, coat, skin);
        BuildArm("L", chest, -1f, coat, skin);
        BuildLeg("L", hips, -1f, pants, boots);
        BuildLeg("R", hips, 1f, pants, boots);

        p.animator = model.gameObject.AddComponent<Animator>();
        p.animator.applyRootMotion = false;
        p.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        p.animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        BuildLantern(p, handR, player.transform);

        p.cameraTarget = Bone("CameraTarget", player.transform, new Vector3(0f, 1.55f, 0f));
        BuildPrayerVisuals(p, player.transform);

        SetLayerRecursive(player, LayerMask.NameToLayer("Player"));
        return p;
    }

    static Transform BuildArm(string side, Transform chest, float sign, Material coat, Material skin)
    {
        var shoulder = Bone("Shoulder" + side, chest, new Vector3(0.2f * sign, 0.2f, 0f));
        var upper = Bone("UpperArm" + side, shoulder, Vector3.zero);
        Part("Sleeve", PrimitiveType.Capsule, upper, new Vector3(0f, -0.14f, 0f), new Vector3(0.115f, 0.16f, 0.115f), coat);
        var fore = Bone("Forearm" + side, upper, new Vector3(0f, -0.29f, 0f));
        Part("Cuff", PrimitiveType.Capsule, fore, new Vector3(0f, -0.12f, 0f), new Vector3(0.095f, 0.14f, 0.095f), coat);
        var hand = Bone("Hand" + side, fore, new Vector3(0f, -0.26f, 0f));
        Part("Glove", PrimitiveType.Sphere, hand, new Vector3(0f, -0.04f, 0.005f), new Vector3(0.075f, 0.1f, 0.06f), skin);
        return hand;
    }

    static void BuildLeg(string side, Transform hips, float sign, Material pants, Material boots)
    {
        var upper = Bone("UpperLeg" + side, hips, new Vector3(0.1f * sign, -0.04f, 0f));
        Part("Thigh", PrimitiveType.Capsule, upper, new Vector3(0f, -0.21f, 0f), new Vector3(0.16f, 0.24f, 0.16f), pants);
        var lower = Bone("LowerLeg" + side, upper, new Vector3(0f, -0.45f, 0f));
        Part("Shin", PrimitiveType.Capsule, lower, new Vector3(0f, -0.2f, 0f), new Vector3(0.125f, 0.22f, 0.125f), pants);
        Part("BootShaft", PrimitiveType.Cylinder, lower, new Vector3(0f, -0.35f, 0f), new Vector3(0.135f, 0.08f, 0.135f), boots);
        var foot = Bone("Foot" + side, lower, new Vector3(0f, -0.43f, 0f));
        Part("Boot", PrimitiveType.Cube, foot, new Vector3(0f, -0.01f, 0.05f), new Vector3(0.12f, 0.085f, 0.26f), boots);
    }

    // ------------------------------------------------------------------ lantern

    static void BuildLantern(PlayerParts p, Transform hand, Transform playerRoot)
    {
        Material metal = EmberArt.Load("LanternMetal"), brass = EmberArt.Load("LanternBrass"), glass = EmberArt.Load("LanternGlass"), flame = EmberArt.Load("Flame");

        var root = Bone("Lantern", hand, new Vector3(0f, -0.07f, 0.02f));
        root.localScale = Vector3.one * 1.3f;
        p.lanternRoot = root;

        // Bail handle.
        NoShadow(Part("HandleTop", PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.012f), brass, new Vector3(0f, 0f, 90f)));
        NoShadow(Part("HandleL", PrimitiveType.Cylinder, root, new Vector3(-0.05f, -0.035f, 0f), new Vector3(0.01f, 0.035f, 0.01f), brass, new Vector3(0f, 0f, 20f)));
        NoShadow(Part("HandleR", PrimitiveType.Cylinder, root, new Vector3(0.05f, -0.035f, 0f), new Vector3(0.01f, 0.035f, 0.01f), brass, new Vector3(0f, 0f, -20f)));
        // Cap and chimney.
        NoShadow(Part("Chimney", PrimitiveType.Cylinder, root, new Vector3(0f, -0.06f, 0f), new Vector3(0.05f, 0.018f, 0.05f), metal));
        NoShadow(Part("Cap", PrimitiveType.Cylinder, root, new Vector3(0f, -0.085f, 0f), new Vector3(0.14f, 0.012f, 0.14f), metal));
        NoShadow(Part("CapRing", PrimitiveType.Cylinder, root, new Vector3(0f, -0.097f, 0f), new Vector3(0.125f, 0.006f, 0.125f), brass));
        // Frame bars around the glass (these cast lovely shadows from the flame).
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            Part("FrameBar" + i, PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * 0.058f, -0.175f, Mathf.Sin(a) * 0.058f), new Vector3(0.01f, 0.16f, 0.01f), metal);
        }
        var glassR = Part("Glass", PrimitiveType.Cylinder, root, new Vector3(0f, -0.175f, 0f), new Vector3(0.104f, 0.075f, 0.104f), glass);
        NoShadow(glassR);
        p.glassRenderer = glassR.GetComponent<Renderer>();
        NoShadow(Part("BaseRing", PrimitiveType.Cylinder, root, new Vector3(0f, -0.254f, 0f), new Vector3(0.13f, 0.006f, 0.13f), brass));
        NoShadow(Part("Tank", PrimitiveType.Cylinder, root, new Vector3(0f, -0.275f, 0f), new Vector3(0.14f, 0.02f, 0.14f), metal));

        // Flame.
        var flameGroup = Bone("FlameGroup", root, new Vector3(0f, -0.19f, 0f));
        p.flameGroup = flameGroup;
        var outer = Part("FlameOuter", PrimitiveType.Sphere, flameGroup, new Vector3(0f, 0.02f, 0f), new Vector3(0.032f, 0.06f, 0.032f), flame);
        NoShadow(outer);
        p.flameRenderer = outer.GetComponent<Renderer>();
        NoShadow(Part("FlameCore", PrimitiveType.Sphere, flameGroup, new Vector3(0f, 0.012f, 0f), new Vector3(0.016f, 0.03f, 0.016f), flame));
        NoShadow(Part("Wick", PrimitiveType.Cylinder, root, new Vector3(0f, -0.22f, 0f), new Vector3(0.008f, 0.012f, 0.008f), metal));
        EmberFX.GlowSprite(flameGroup, new Vector3(0f, 0.02f, 0f), new Color(1f, 0.6f, 0.25f, 0.35f), 0.45f);

        var lightGo = new GameObject("LanternPointLight");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, -0.17f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.32f);
        light.range = 13.5f;
        light.intensity = 14f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.85f;
        light.shadowNearPlane = 0.2f;
        light.renderMode = LightRenderMode.ForcePixel;
        p.lanternLight = light;

        p.embers = EmberFX.Embers(root, new Vector3(0f, -0.06f, 0f), new Color(1f, 0.6f, 0.25f), 3f, 0.02f);
        p.smoke = EmberFX.Smoke(root, new Vector3(0f, -0.05f, 0f), 8);
        p.pickupFlare = EmberFX.Burst(root, new Vector3(0f, -0.17f, 0f), new Color(1f, 0.7f, 0.3f), 18, 1.4f, 0.04f, "PickupFlare");

        var sway = root.gameObject.AddComponent<LanternSway>();
        sway.yawReference = playerRoot;
    }

    // ------------------------------------------------------------------ prayer

    static void BuildPrayerVisuals(PlayerParts p, Transform player)
    {
        var holy = EmberArt.Load("HolyLight");
        var cross = Bone("PrayerCross", player, new Vector3(0f, 2.55f, 0f));
        p.crossRoot = cross;
        var v = Part("Upright", PrimitiveType.Cube, cross, new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.9f, 0.05f), holy);
        var h = Part("Crossbeam", PrimitiveType.Cube, cross, new Vector3(0f, 0.19f, 0f), new Vector3(0.52f, 0.085f, 0.05f), holy);
        NoShadow(v); NoShadow(h);
        p.crossRenderers = new[] { v.GetComponent<Renderer>(), h.GetComponent<Renderer>() };
        EmberFX.GlowSprite(cross, new Vector3(0f, 0.05f, 0.05f), new Color(1f, 0.85f, 0.55f, 0.45f), 2.6f);
        EmberFX.GlowSprite(cross, new Vector3(0f, 0.05f, 0.06f), new Color(1f, 0.95f, 0.8f, 0.5f), 1.1f);
        var halo = EmberFX.Motes(cross, Vector3.zero, new Color(1f, 0.9f, 0.6f), 10f, 0.5f, 0.035f, 30, "CrossMotes");
        var hm = halo.main; hm.playOnAwake = true;

        var lightGo = new GameObject("HolyLight");
        lightGo.transform.SetParent(player, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.86f, 0.62f);
        light.range = 17f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        p.holyLight = light;

        p.prayerMotes = EmberFX.Motes(player, new Vector3(0f, 0.2f, 0f), new Color(1f, 0.85f, 0.5f), 22f, 3f, 0.06f, 110, "PrayerMotes");
        var pm = p.prayerMotes.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        pm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        var sh = p.prayerMotes.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.rotation = new Vector3(-90f, 0f, 0f); sh.radius = 3f;

        p.prayerRing = EmberFX.Ring(player, new Vector3(0f, 0.08f, 0f), new Color(1f, 0.85f, 0.5f, 0.9f), 34f, 1.6f, "PrayerRing");
    }

    // ------------------------------------------------------------------ helpers

    public static Transform Bone(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    public static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.On;
        return go.transform;
    }

    static void NoShadow(Transform t) => t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    // ------------------------------------------------------------------ animation clips

    class Pose : Dictionary<string, Vector3> { }

    class ClipBuilder
    {
        readonly Dictionary<string, AnimationCurve[]> curves = new Dictionary<string, AnimationCurve[]>();
        readonly AnimationCurve hipsY = new AnimationCurve();
        public float length;

        public void Key(float t, Pose pose, float hipY)
        {
            foreach (var bone in Bones)
            {
                if (!curves.TryGetValue(bone, out var c))
                {
                    c = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    curves[bone] = c;
                }
                Vector3 e = pose.TryGetValue(bone, out var v) ? v : Vector3.zero;
                c[0].AddKey(t, e.x); c[1].AddKey(t, e.y); c[2].AddKey(t, e.z);
            }
            hipsY.AddKey(t, hipY);
            length = Mathf.Max(length, t);
        }

        public AnimationClip Build(string name, bool loop)
        {
            string path = AnimDir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.frameRate = 30f;

            foreach (var kv in curves)
            {
                for (int i = 0; i < 3; i++)
                {
                    Smooth(kv.Value[i]);
                    string prop = "localEulerAnglesRaw." + "xyz"[i];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(kv.Key, typeof(Transform), prop), kv.Value[i]);
                }
            }
            Smooth(hipsY);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(H, typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Constant(0f, length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(H, typeof(Transform), "m_LocalPosition.y"), hipsY);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(H, typeof(Transform), "m_LocalPosition.z"), AnimationCurve.Constant(0f, length, 0f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        }
    }

    // Samples a looping cycle function at even steps.
    static AnimationClip Cycle(string name, float seconds, int samples, Func<float, (Pose pose, float hipY)> f)
    {
        var b = new ClipBuilder();
        for (int i = 0; i <= samples; i++)
        {
            float phase = i / (float)samples;
            var (pose, hipY) = f(phase % 1f);
            b.Key(phase * seconds, pose, hipY);
        }
        return b.Build(name, true);
    }

    static Pose IdlePose(float breathe = 0f)
    {
        return new Pose
        {
            [SP] = new Vector3(2f + breathe, 0f, 0f),
            [CH] = new Vector3(1.5f * breathe, 0f, 0f),
            [NK] = new Vector3(3f, 0f, 0f),
            [UAL] = new Vector3(4f + breathe, 0f, -7f),
            [FAL] = new Vector3(-12f, 0f, 0f),
            [UAR] = new Vector3(-18f + breathe, 0f, 12f),
            [FAR] = new Vector3(-36f, 0f, 0f),
            [HAR] = new Vector3(12f, 0f, 0f),
            [ULL] = new Vector3(2f, 0f, -2f),
            [LLL] = new Vector3(4f, 0f, 0f),
            [FTL] = new Vector3(-5f, 0f, 0f),
            [ULR] = new Vector3(-1f, 0f, 2f),
            [LLR] = new Vector3(3f, 0f, 0f),
            [FTR] = new Vector3(-2f, 0f, 0f),
        };
    }

    public static AnimatorController BuildAnimatorController()
    {
        EmberArt.CreateFolderRecursive(AnimDir);
        const float TAU = Mathf.PI * 2f;

        var idle = Cycle("Idle", 3.2f, 12, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = IdlePose(s);
            pose[HD] = new Vector3(0f, 5f * Mathf.Sin(p * TAU + 1f), 0f);
            return (pose, 0.98f - 0.006f * (1f - Mathf.Cos(p * TAU)));
        });

        var walk = Cycle("Walk", 0.8f, 16, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = new Pose
            {
                [H] = new Vector3(0f, 5f * s, 0f),
                [SP] = new Vector3(3f, -7f * s, 0f),
                [CH] = new Vector3(3f, 0f, 0f),
                [NK] = new Vector3(-2f, 0f, 0f),
                [UAL] = new Vector3(22f * s + 2f, 0f, -8f),
                [FAL] = new Vector3(-18f - 12f * Mathf.Max(0f, -s), 0f, 0f),
                [UAR] = new Vector3(-20f - 5f * s, 0f, 12f),
                [FAR] = new Vector3(-38f, 0f, 0f),
                [HAR] = new Vector3(10f, 0f, 0f),
                [ULL] = new Vector3(-26f * s - 3f, 0f, 0f),
                [LLL] = new Vector3(6f + 48f * Mathf.Pow(Mathf.Max(0f, c), 2f), 0f, 0f),
                [FTL] = new Vector3(-8f * s, 0f, 0f),
                [ULR] = new Vector3(26f * s - 3f, 0f, 0f),
                [LLR] = new Vector3(6f + 48f * Mathf.Pow(Mathf.Max(0f, -c), 2f), 0f, 0f),
                [FTR] = new Vector3(8f * s, 0f, 0f),
            };
            return (pose, 0.965f + 0.022f * Mathf.Cos(2f * p * TAU));
        });

        var run = Cycle("Run", 0.62f, 16, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = new Pose
            {
                [H] = new Vector3(0f, 7f * s, 0f),
                [SP] = new Vector3(8f, -10f * s, 0f),
                [CH] = new Vector3(6f, 0f, 0f),
                [NK] = new Vector3(-8f, 0f, 0f),
                [UAL] = new Vector3(45f * s, 0f, -10f),
                [FAL] = new Vector3(-75f, 0f, 0f),
                [UAR] = new Vector3(-30f - 10f * s, 0f, 14f),
                [FAR] = new Vector3(-62f, 0f, 0f),
                [HAR] = new Vector3(20f, 0f, 0f),
                [ULL] = new Vector3(-42f * s - 8f, 0f, 0f),
                [LLL] = new Vector3(14f + 80f * Mathf.Pow(Mathf.Max(0f, c), 1.5f), 0f, 0f),
                [FTL] = new Vector3(-12f * s, 0f, 0f),
                [ULR] = new Vector3(42f * s - 8f, 0f, 0f),
                [LLR] = new Vector3(14f + 80f * Mathf.Pow(Mathf.Max(0f, -c), 1.5f), 0f, 0f),
                [FTR] = new Vector3(12f * s, 0f, 0f),
            };
            return (pose, 0.93f + 0.045f * Mathf.Cos(2f * p * TAU));
        });

        // Hit reaction: a sharp flinch back, then recover.
        var hitB = new ClipBuilder();
        hitB.Key(0f, IdlePose(), 0.98f);
        var flinch = IdlePose();
        flinch[SP] = new Vector3(-8f, 6f, 0f);
        flinch[CH] = new Vector3(-14f, 0f, 4f);
        flinch[NK] = new Vector3(-14f, 0f, 0f);
        flinch[UAL] = new Vector3(-40f, 0f, -28f);
        flinch[FAL] = new Vector3(-70f, 0f, 0f);
        flinch[ULL] = new Vector3(-12f, 0f, -4f);
        flinch[LLL] = new Vector3(20f, 0f, 0f);
        flinch[ULR] = new Vector3(10f, 0f, 4f);
        flinch[LLR] = new Vector3(18f, 0f, 0f);
        hitB.Key(0.09f, flinch, 0.94f);
        hitB.Key(0.45f, IdlePose(), 0.98f);
        var hit = hitB.Build("Hit", false);

        // Death: knees buckle, then the body falls forward.
        var dieB = new ClipBuilder();
        dieB.Key(0f, IdlePose(), 0.98f);
        var buckle = IdlePose();
        buckle[CH] = new Vector3(20f, 0f, 0f);
        buckle[NK] = new Vector3(20f, 0f, 0f);
        buckle[ULL] = new Vector3(-55f, 0f, -4f); buckle[LLL] = new Vector3(100f, 0f, 0f);
        buckle[ULR] = new Vector3(-35f, 0f, 4f); buckle[LLR] = new Vector3(95f, 0f, 0f);
        buckle[UAL] = new Vector3(10f, 0f, -10f); buckle[UAR] = new Vector3(0f, 0f, 14f);
        dieB.Key(0.4f, buckle, 0.6f);
        var fall = IdlePose();
        fall[H] = new Vector3(70f, 0f, 0f);
        fall[SP] = new Vector3(10f, 0f, 0f);
        fall[NK] = new Vector3(15f, 0f, 0f);
        fall[ULL] = new Vector3(-20f, 0f, -4f); fall[LLL] = new Vector3(60f, 0f, 0f);
        fall[ULR] = new Vector3(-10f, 0f, 4f); fall[LLR] = new Vector3(50f, 0f, 0f);
        fall[UAL] = new Vector3(-70f, 0f, -20f); fall[UAR] = new Vector3(-80f, 0f, 20f);
        dieB.Key(0.9f, fall, 0.3f);
        var lie = IdlePose();
        lie[H] = new Vector3(86f, 0f, 0f);
        lie[SP] = new Vector3(2f, 0f, 0f);
        lie[NK] = new Vector3(-10f, 25f, 0f);
        lie[ULL] = new Vector3(-6f, 0f, -6f); lie[LLL] = new Vector3(18f, 0f, 0f);
        lie[ULR] = new Vector3(-4f, 0f, 6f); lie[LLR] = new Vector3(10f, 0f, 0f);
        lie[UAL] = new Vector3(-150f, 0f, -25f); lie[FAL] = new Vector3(-10f, 0f, 0f);
        lie[UAR] = new Vector3(-110f, 0f, 30f); lie[FAR] = new Vector3(-20f, 0f, 0f);
        dieB.Key(1.5f, lie, 0.17f);
        var death = dieB.Build("Death", false);

        // Prayer: kneel on one knee, hands together, head bowed.
        Pose Kneel(float breathe)
        {
            return new Pose
            {
                [SP] = new Vector3(4f + breathe, 0f, 0f),
                [CH] = new Vector3(4f + breathe, 0f, 0f),
                [NK] = new Vector3(24f + breathe * 2f, 0f, 0f),
                [HD] = new Vector3(10f, 0f, 0f),
                [UAL] = new Vector3(-28f, -8f, 26f),
                [FAL] = new Vector3(-105f, 0f, 0f),
                [UAR] = new Vector3(-28f, 8f, -26f),
                [FAR] = new Vector3(-105f, 0f, 0f),
                [HAR] = new Vector3(20f, 0f, 0f),
                [ULL] = new Vector3(-88f, 0f, -4f),
                [LLL] = new Vector3(88f, 0f, 0f),
                [FTL] = new Vector3(0f, 0f, 0f),
                [ULR] = new Vector3(-4f, 0f, 3f),
                [LLR] = new Vector3(96f, 0f, 0f),
                [FTR] = new Vector3(40f, 0f, 0f),
            };
        }
        var enterB = new ClipBuilder();
        enterB.Key(0f, IdlePose(), 0.98f);
        enterB.Key(0.9f, Kneel(0f), 0.52f);
        var prayEnter = enterB.Build("PrayEnter", false);
        var pray = Cycle("Pray", 3f, 8, p => (Kneel(Mathf.Sin(p * TAU) * 1.5f), 0.52f - 0.004f * Mathf.Sin(p * TAU)));

        // Working at the radio: crouched, hands busy.
        var work = Cycle("Work", 1.1f, 8, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = new Pose
            {
                [SP] = new Vector3(12f, 0f, 0f),
                [CH] = new Vector3(14f, 0f, 0f),
                [NK] = new Vector3(18f, 0f, 0f),
                [UAL] = new Vector3(-62f + 8f * s, 0f, 10f),
                [FAL] = new Vector3(-45f - 10f * s, 0f, 0f),
                [UAR] = new Vector3(-58f - 8f * s, 0f, -10f),
                [FAR] = new Vector3(-50f + 10f * s, 0f, 0f),
                [ULL] = new Vector3(-55f, 0f, -6f), [LLL] = new Vector3(80f, 0f, 0f), [FTL] = new Vector3(-22f, 0f, 0f),
                [ULR] = new Vector3(-40f, 0f, 6f), [LLR] = new Vector3(70f, 0f, 0f), [FTR] = new Vector3(-28f, 0f, 0f),
            };
            return (pose, 0.8f);
        });

        // ---- controller
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl) AssetDatabase.DeleteAsset(ControllerPath);
        ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Pray", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Work", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.5f);
        tree.AddChild(run, 1f);
        sm.defaultState = loco;

        var sHit = sm.AddState("Hit"); sHit.motion = hit;
        var sDeath = sm.AddState("Death"); sDeath.motion = death;
        var sEnter = sm.AddState("PrayEnter"); sEnter.motion = prayEnter;
        var sPray = sm.AddState("Pray"); sPray.motion = pray;
        var sWork = sm.AddState("Work"); sWork.motion = work;

        var toDeath = sm.AddAnyStateTransition(sDeath);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        toDeath.duration = 0.15f; toDeath.canTransitionToSelf = false; toDeath.hasExitTime = false;

        var toHit = sm.AddAnyStateTransition(sHit);
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        toHit.duration = 0.05f; toHit.canTransitionToSelf = false; toHit.hasExitTime = false;

        var hitBack = sHit.AddTransition(loco);
        hitBack.hasExitTime = true; hitBack.exitTime = 0.85f; hitBack.duration = 0.15f;

        var toEnter = loco.AddTransition(sEnter);
        toEnter.AddCondition(AnimatorConditionMode.If, 0f, "Pray");
        toEnter.hasExitTime = false; toEnter.duration = 0.2f;
        var enterToPray = sEnter.AddTransition(sPray);
        enterToPray.hasExitTime = true; enterToPray.exitTime = 1f; enterToPray.duration = 0.1f;
        var prayBack = sPray.AddTransition(loco);
        prayBack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        prayBack.hasExitTime = false; prayBack.duration = 0.5f;
        var enterBack = sEnter.AddTransition(loco);
        enterBack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        enterBack.hasExitTime = false; enterBack.duration = 0.4f;

        var toWork = loco.AddTransition(sWork);
        toWork.AddCondition(AnimatorConditionMode.If, 0f, "Work");
        toWork.hasExitTime = false; toWork.duration = 0.25f;
        var workBack = sWork.AddTransition(loco);
        workBack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Work");
        workBack.hasExitTime = false; workBack.duration = 0.3f;

        AssetDatabase.SaveAssets();
        Debug.Log("EMBER: player animator built.");
        return ctrl;
    }
}
