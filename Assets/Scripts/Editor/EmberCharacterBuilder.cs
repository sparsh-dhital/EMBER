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
        public Transform swordRoot;
        public Renderer[] bladeRenderers;
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

        // Left hand carries the lantern, right hand carries the sword.
        Transform handR = BuildArm("R", chest, 1f, coat, skin);
        Transform handL = BuildArm("L", chest, -1f, coat, skin);
        BuildLeg("L", hips, -1f, pants, boots);
        BuildLeg("R", hips, 1f, pants, boots);

        p.animator = model.gameObject.AddComponent<Animator>();
        p.animator.applyRootMotion = false;
        p.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        p.animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        BuildLantern(p, handL, player.transform);
        BuildSword(p, handR);

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

    // ------------------------------------------------------------------ sword

    // The sacred silver sword: a cruciform hilt, a fullered blade, and a silver edge
    // bright enough to read against the dark. Emission is driven at runtime by
    // SwordController, which flares it on a radiant strike and on a parry.
    static void BuildSword(PlayerParts p, Transform hand)
    {
        Material steel = EmberArt.Load("Metal"), grip = EmberArt.Load("WoodDark"),
                 gold = EmberArt.Load("Gold"), silver = EmberArt.Load("LanternMetal");

        // Held point-down-forward in a relaxed grip; the swing animation does the rest.
        var root = Bone("Sword", hand, new Vector3(0f, -0.06f, 0.03f));
        root.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        p.swordRoot = root;

        // Grip and pommel.
        NoShadow(Part("Grip", PrimitiveType.Cylinder, root, new Vector3(0f, -0.02f, 0f), new Vector3(0.022f, 0.06f, 0.022f), grip));
        NoShadow(Part("Pommel", PrimitiveType.Sphere, root, new Vector3(0f, -0.09f, 0f), new Vector3(0.042f, 0.042f, 0.042f), gold));

        // Cruciform guard: this is a holy weapon, and the silhouette should say so.
        NoShadow(Part("Guard", PrimitiveType.Cube, root, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.022f, 0.032f), gold));
        NoShadow(Part("GuardBoss", PrimitiveType.Cube, root, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.05f, 0.042f), gold));

        // Blade: a broad core with a bright silver edge laid over it.
        var blade = Part("Blade", PrimitiveType.Cube, root, new Vector3(0f, 0.46f, 0f), new Vector3(0.05f, 0.82f, 0.016f), steel);
        var edge = Part("BladeEdge", PrimitiveType.Cube, root, new Vector3(0f, 0.46f, 0f), new Vector3(0.056f, 0.8f, 0.007f), silver);
        // A diamond point: the same cube turned 45 degrees on its face.
        var tip = Part("BladeTip", PrimitiveType.Cube, root, new Vector3(0f, 0.9f, 0f),
                       new Vector3(0.036f, 0.036f, 0.016f), steel, new Vector3(0f, 0f, 45f));
        NoShadow(tip);
        // A thin fuller down the centre, so the blade catches the lantern rather than reading flat.
        NoShadow(Part("Fuller", PrimitiveType.Cube, root, new Vector3(0f, 0.46f, 0f), new Vector3(0.014f, 0.76f, 0.019f), gold));

        p.bladeRenderers = new[]
        {
            blade.GetComponent<Renderer>(),
            edge.GetComponent<Renderer>(),
            tip.GetComponent<Renderer>(),
        };
        NoShadow(edge);
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
        // The prayer sign is the Hindu Om. It is a single alpha-cut plate rather than built
        // geometry, so the glyph stays perfectly formed at any distance, and it always faces
        // the camera (PrayerSystem billboards crossRoot each frame).
        var omMat = EmberArt.Load("OmSymbol");
        var om = Bone("PrayerOm", player, new Vector3(0f, 2.55f, 0f));
        p.crossRoot = om;

        var plate = Part("OmPlate", PrimitiveType.Quad, om, Vector3.zero, new Vector3(1.05f, 1.05f, 1f), omMat);
        NoShadow(plate);
        p.crossRenderers = new[] { plate.GetComponent<Renderer>() };

        // Orange flame-light behind the glyph: a warm outer bloom, a hot inner core.
        EmberFX.GlowSprite(om, new Vector3(0f, 0.02f, 0.06f), new Color(1f, 0.46f, 0.12f, 0.55f), 2.9f);
        EmberFX.GlowSprite(om, new Vector3(0f, 0.02f, 0.07f), new Color(1f, 0.74f, 0.32f, 0.5f), 1.4f);

        // Embers lifting off the symbol, so it reads as burning rather than printed.
        var halo = EmberFX.Motes(om, Vector3.zero, new Color(1f, 0.55f, 0.16f), 12f, 0.6f, 0.04f, 36, "OmEmbers");
        var hm = halo.main;
        hm.playOnAwake = true;
        hm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        hm.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        hm.gravityModifier = -0.12f;      // embers rise

        var lightGo = new GameObject("HolyLight");
        lightGo.transform.SetParent(player, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.58f, 0.22f);   // firelight, not moonlight
        light.range = 17f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        p.holyLight = light;

        p.prayerMotes = EmberFX.Motes(player, new Vector3(0f, 0.2f, 0f), new Color(1f, 0.6f, 0.2f), 22f, 3f, 0.06f, 110, "PrayerMotes");
        var pm = p.prayerMotes.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        pm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        var sh = p.prayerMotes.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.rotation = new Vector3(-90f, 0f, 0f); sh.radius = 3f;

        p.prayerRing = EmberFX.Ring(player, new Vector3(0f, 0.08f, 0f), new Color(1f, 0.6f, 0.2f, 0.9f), 34f, 1.6f, "PrayerRing");
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
            // Left arm holds the lantern out and steady; right arm hangs ready on the hilt.
            [UAL] = new Vector3(-18f + breathe, 0f, -12f),
            [FAL] = new Vector3(-36f, 0f, 0f),
            [HAL] = new Vector3(12f, 0f, 0f),
            [UAR] = new Vector3(4f + breathe, 0f, 7f),
            [FAR] = new Vector3(-12f, 0f, 0f),
            [ULL] = new Vector3(2f, 0f, -2f),
            [LLL] = new Vector3(4f, 0f, 0f),
            [FTL] = new Vector3(-5f, 0f, 0f),
            [ULR] = new Vector3(-1f, 0f, 2f),
            [LLR] = new Vector3(3f, 0f, 0f),
            [FTR] = new Vector3(-2f, 0f, 0f),
        };
    }

    // Locomotion clips are authored at these native speeds (m/s) and blended by the real speed, so feet don't slide.
    public const float WalkSpeed = 1.8f, JogSpeed = 3.6f, RunSpeed = 5.8f, CrawlSpeed = 1.1f;
    // Length of the sword swing clip (seconds). SwordController times its hit window inside it.
    public const float AttackLength = 0.75f;

    static Pose Merge(Pose basePose, Pose overrides)
    {
        var p = new Pose();
        foreach (var kv in basePose) p[kv.Key] = kv.Value;
        foreach (var kv in overrides) p[kv.Key] = kv.Value;
        return p;
    }

    // The lantern arm. It lives on the left side now, so the roll angle mirrors.
    static Pose LanternArm(float x = -20f, float fore = -38f, float hand = 10f, float z = 12f)
    {
        return new Pose { [UAL] = new Vector3(x, 0f, -z), [FAL] = new Vector3(fore, 0f, 0f), [HAL] = new Vector3(hand, 0f, 0f) };
    }

    static Pose CrawlBase(float breathe)
    {
        return new Pose
        {
            [H] = new Vector3(78f, 0f, 0f),
            [SP] = new Vector3(4f + breathe, 0f, 0f),
            [CH] = new Vector3(breathe * 0.5f, 0f, 0f),
            [NK] = new Vector3(-52f, 0f, 0f),
            [HD] = new Vector3(-12f, 0f, 0f),
            [UAL] = new Vector3(-76f, 0f, -4f), [FAL] = new Vector3(-6f, 0f, 0f), [HAL] = new Vector3(55f, 0f, 0f),
            [UAR] = new Vector3(-104f, 0f, 8f), [FAR] = new Vector3(-16f, 0f, 0f), [HAR] = new Vector3(30f, 0f, 0f),
            [ULL] = new Vector3(-78f, 0f, -3f), [LLL] = new Vector3(92f, 0f, 0f), [FTL] = new Vector3(38f, 0f, 0f),
            [ULR] = new Vector3(-78f, 0f, 3f), [LLR] = new Vector3(92f, 0f, 0f), [FTR] = new Vector3(38f, 0f, 0f),
        };
    }

    public static AnimatorController BuildAnimatorController()
    {
        EmberArt.CreateFolderRecursive(AnimDir);
        const float TAU = Mathf.PI * 2f;
        float Pos(float v) => Mathf.Max(0f, v);

        // ---------------- idle: breathing and a slow weight shift
        var idle = Cycle("Idle", 3.6f, 12, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = IdlePose(s);
            pose[H] = new Vector3(0f, 0f, 1.2f * Mathf.Sin(p * TAU + 0.6f));
            pose[HD] = new Vector3(0f, 5f * Mathf.Sin(p * TAU + 1f), 0f);
            return (pose, 0.98f - 0.006f * (1f - Mathf.Cos(p * TAU)));
        });

        // ---------------- walk (1.8 m/s): heel strike, toe-off, counter-rotating shoulders, steady head
        var walk = Cycle("Walk", 0.98f, 20, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = Merge(new Pose
            {
                [H] = new Vector3(0f, 5f * s, -1.5f * Mathf.Sin(2f * p * TAU)),
                [SP] = new Vector3(3f, -7f * s, 0f),
                [CH] = new Vector3(3f, -3f * s, 1.5f * s),
                [NK] = new Vector3(-2f, 0f, 0f),
                [HD] = new Vector3(0f, 8f * s, 0f),
                [UAL] = new Vector3(20f * s + 2f, 0f, -7f),
                [FAL] = new Vector3(-16f - 14f * Pos(-s), 0f, 0f),
                [ULL] = new Vector3(-28f * s - 4f, 0f, 0f),
                [LLL] = new Vector3(6f + 52f * Mathf.Pow(Pos(c), 2f) + 8f * Mathf.Pow(Pos(-c), 4f), 0f, 0f),
                [FTL] = new Vector3(-10f * Pos(s) + 18f * Mathf.Pow(Pos(-s), 2f), 0f, 0f),
                [ULR] = new Vector3(28f * s - 4f, 0f, 0f),
                [LLR] = new Vector3(6f + 52f * Mathf.Pow(Pos(-c), 2f) + 8f * Mathf.Pow(Pos(c), 4f), 0f, 0f),
                [FTR] = new Vector3(-10f * Pos(-s) + 18f * Mathf.Pow(Pos(s), 2f), 0f, 0f),
            }, LanternArm(-20f - 4f * s, -38f, 10f));
            return (pose, 0.965f + 0.022f * Mathf.Cos(2f * p * TAU));
        });

        // ---------------- jog (3.6 m/s): bent arms, more knee lift, slight lean
        var jog = Cycle("Jog", 0.66f, 20, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = Merge(new Pose
            {
                [H] = new Vector3(0f, 6f * s, -2f * Mathf.Sin(2f * p * TAU)),
                [SP] = new Vector3(7f, -9f * s, 0f),
                [CH] = new Vector3(4f, -3f * s, 2f * s),
                [NK] = new Vector3(-6f, 0f, 0f),
                [HD] = new Vector3(0f, 9f * s, 0f),
                [UAL] = new Vector3(38f * s, 0f, -9f),
                [FAL] = new Vector3(-70f - 8f * Pos(-s), 0f, 0f),
                [ULL] = new Vector3(-38f * s - 8f, 0f, 0f),
                [LLL] = new Vector3(12f + 75f * Mathf.Pow(Pos(c), 1.6f), 0f, 0f),
                [FTL] = new Vector3(-8f * Pos(s) + 24f * Mathf.Pow(Pos(-s), 2f), 0f, 0f),
                [ULR] = new Vector3(38f * s - 8f, 0f, 0f),
                [LLR] = new Vector3(12f + 75f * Mathf.Pow(Pos(-c), 1.6f), 0f, 0f),
                [FTR] = new Vector3(-8f * Pos(-s) + 24f * Mathf.Pow(Pos(s), 2f), 0f, 0f),
            }, LanternArm(-26f - 7f * s, -55f, 18f, 13f));
            return (pose, 0.95f + 0.035f * Mathf.Cos(2f * p * TAU));
        });

        // ---------------- run (5.8 m/s): knee drive, strong arm swing, forward lean
        var run = Cycle("Run", 0.6f, 20, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = Merge(new Pose
            {
                [H] = new Vector3(0f, 7f * s, -2.5f * Mathf.Sin(2f * p * TAU)),
                [SP] = new Vector3(12f, -11f * s, 0f),
                [CH] = new Vector3(6f, -4f * s, 2.5f * s),
                [NK] = new Vector3(-10f, 0f, 0f),
                [HD] = new Vector3(0f, 10f * s, 0f),
                [UAL] = new Vector3(52f * s - 5f, 0f, -10f),
                [FAL] = new Vector3(-85f, 0f, 0f),
                [ULL] = new Vector3(-44f * s - 12f, 0f, 0f),
                [LLL] = new Vector3(18f + 90f * Mathf.Pow(Pos(c), 1.4f), 0f, 0f),
                [FTL] = new Vector3(-12f * Pos(s) + 28f * Mathf.Pow(Pos(-s), 2f), 0f, 0f),
                [ULR] = new Vector3(44f * s - 12f, 0f, 0f),
                [LLR] = new Vector3(18f + 90f * Mathf.Pow(Pos(-c), 1.4f), 0f, 0f),
                [FTR] = new Vector3(-12f * Pos(-s) + 28f * Mathf.Pow(Pos(s), 2f), 0f, 0f),
            }, LanternArm(-32f - 12f * s, -68f, 22f, 14f));
            return (pose, 0.925f + 0.05f * Mathf.Cos(2f * p * TAU));
        });

        // ---------------- jump: anticipation crouch, push-off, airborne, landing
        var jsB = new ClipBuilder();
        jsB.Key(0f, IdlePose(), 0.98f);
        jsB.Key(0.08f, Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(14f, 0f, 0f), [NK] = new Vector3(-8f, 0f, 0f),
            [UAL] = new Vector3(28f, 0f, -10f), [FAL] = new Vector3(-20f, 0f, 0f),
            [ULL] = new Vector3(-28f, 0f, -3f), [LLL] = new Vector3(48f, 0f, 0f), [FTL] = new Vector3(-16f, 0f, 0f),
            [ULR] = new Vector3(-28f, 0f, 3f), [LLR] = new Vector3(48f, 0f, 0f), [FTR] = new Vector3(-16f, 0f, 0f),
        }), 0.86f);
        jsB.Key(0.16f, Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(4f, 0f, 0f),
            [UAL] = new Vector3(-45f, 0f, -14f), [FAL] = new Vector3(-30f, 0f, 0f),
            [ULL] = new Vector3(-6f, 0f, 0f), [LLL] = new Vector3(6f, 0f, 0f), [FTL] = new Vector3(22f, 0f, 0f),
            [ULR] = new Vector3(-4f, 0f, 0f), [LLR] = new Vector3(6f, 0f, 0f), [FTR] = new Vector3(22f, 0f, 0f),
        }), 0.98f);
        var jumpStart = jsB.Build("JumpStart", false);

        var airRise = Cycle("AirRise", 0.8f, 8, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = Merge(new Pose
            {
                [SP] = new Vector3(6f, 0f, 0f),
                [UAL] = new Vector3(-30f + 4f * s, 0f, -25f), [FAL] = new Vector3(-35f, 0f, 0f),
                [ULL] = new Vector3(-38f, 0f, -3f), [LLL] = new Vector3(58f, 0f, 0f), [FTL] = new Vector3(10f, 0f, 0f),
                [ULR] = new Vector3(-14f, 0f, 3f), [LLR] = new Vector3(34f, 0f, 0f), [FTR] = new Vector3(14f, 0f, 0f),
            }, LanternArm(-36f, -50f, 16f, 20f));
            return (pose, 0.98f);
        });
        var airFall = Cycle("AirFall", 0.8f, 8, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = Merge(new Pose
            {
                [SP] = new Vector3(2f, 0f, 0f), [NK] = new Vector3(14f, 0f, 0f),
                [UAL] = new Vector3(-48f + 5f * s, 0f, -45f), [FAL] = new Vector3(-20f, 0f, 0f),
                [ULL] = new Vector3(-22f, 0f, -4f), [LLL] = new Vector3(22f, 0f, 0f), [FTL] = new Vector3(-6f, 0f, 0f),
                [ULR] = new Vector3(-8f, 0f, 4f), [LLR] = new Vector3(14f, 0f, 0f), [FTR] = new Vector3(-6f, 0f, 0f),
            }, LanternArm(-44f, -40f, 12f, 32f));
            return (pose, 0.98f);
        });

        var landB = new ClipBuilder();
        landB.Key(0f, Merge(IdlePose(), new Pose { [ULL] = new Vector3(-20f, 0f, -4f), [LLL] = new Vector3(20f, 0f, 0f), [ULR] = new Vector3(-8f, 0f, 4f), [LLR] = new Vector3(14f, 0f, 0f) }), 0.98f);
        landB.Key(0.07f, Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(18f, 0f, 0f), [CH] = new Vector3(6f, 0f, 0f), [NK] = new Vector3(-10f, 0f, 0f),
            [UAL] = new Vector3(-20f, 0f, -18f), [FAL] = new Vector3(-30f, 0f, 0f),
            [ULL] = new Vector3(-42f, 0f, -4f), [LLL] = new Vector3(72f, 0f, 0f), [FTL] = new Vector3(-24f, 0f, 0f),
            [ULR] = new Vector3(-36f, 0f, 4f), [LLR] = new Vector3(66f, 0f, 0f), [FTR] = new Vector3(-22f, 0f, 0f),
        }), 0.8f);
        landB.Key(0.3f, IdlePose(), 0.98f);
        var land = landB.Build("Land", false);

        // ---------------- crawl: hands and knees, lantern held clear of the ground
        var crawlIdle = Cycle("CrawlIdle", 2.4f, 8, p => (CrawlBase(Mathf.Sin(p * TAU) * 1.5f), 0.52f));
        var crawlMove = Cycle("CrawlMove", 0.95f, 16, p =>
        {
            float s = Mathf.Sin(p * TAU), c = Mathf.Cos(p * TAU);
            var pose = CrawlBase(0f);
            pose[H] = new Vector3(78f, 5f * s, 0f);
            pose[SP] = new Vector3(4f, -6f * s, 0f);
            pose[UAL] = new Vector3(-76f - 14f * s, 0f, -4f);
            pose[FAL] = new Vector3(-6f - 22f * Mathf.Pow(Pos(c), 2f), 0f, 0f);
            pose[UAR] = new Vector3(-104f + 4f * s, 0f, 8f);
            pose[ULL] = new Vector3(-78f + 12f * s, 0f, -3f);
            pose[LLL] = new Vector3(92f + 10f * Pos(-c), 0f, 0f);
            pose[ULR] = new Vector3(-78f - 12f * s, 0f, 3f);
            pose[LLR] = new Vector3(92f + 10f * Pos(c), 0f, 0f);
            return (pose, 0.52f + 0.02f * Mathf.Cos(2f * p * TAU));
        });

        // ---------------- hit reaction
        var hitB = new ClipBuilder();
        hitB.Key(0f, IdlePose(), 0.98f);
        hitB.Key(0.08f, Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(-10f, 8f, 0f), [CH] = new Vector3(-16f, 0f, 5f), [NK] = new Vector3(-16f, 0f, 0f),
            [UAL] = new Vector3(-42f, 0f, -30f), [FAL] = new Vector3(-72f, 0f, 0f),
            [ULL] = new Vector3(-14f, 0f, -4f), [LLL] = new Vector3(22f, 0f, 0f),
            [ULR] = new Vector3(12f, 0f, 4f), [LLR] = new Vector3(20f, 0f, 0f),
        }), 0.93f);
        hitB.Key(0.45f, IdlePose(), 0.98f);
        var hit = hitB.Build("Hit", false);

        // ---------------- death: stagger back, knees buckle, fall forward, lie still
        var dieB = new ClipBuilder();
        dieB.Key(0f, IdlePose(), 0.98f);
        dieB.Key(0.14f, Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(-18f, 10f, 0f), [CH] = new Vector3(-12f, 0f, 4f), [NK] = new Vector3(-22f, 0f, 0f),
            [UAL] = new Vector3(-62f, 0f, -42f), [FAL] = new Vector3(-40f, 0f, 0f),
            [UAR] = new Vector3(-50f, 0f, 38f), [FAR] = new Vector3(-30f, 0f, 0f),
            [ULL] = new Vector3(12f, 0f, -4f), [LLL] = new Vector3(18f, 0f, 0f),
            [ULR] = new Vector3(-10f, 0f, 4f), [LLR] = new Vector3(14f, 0f, 0f),
        }), 0.95f);
        dieB.Key(0.5f, Merge(IdlePose(), new Pose
        {
            [CH] = new Vector3(22f, 0f, 0f), [NK] = new Vector3(24f, 0f, 0f),
            [ULL] = new Vector3(-55f, 0f, -4f), [LLL] = new Vector3(100f, 0f, 0f),
            [ULR] = new Vector3(-35f, 0f, 4f), [LLR] = new Vector3(95f, 0f, 0f),
            [UAL] = new Vector3(10f, 0f, -10f), [UAR] = new Vector3(0f, 0f, 14f),
        }), 0.6f);
        dieB.Key(1.05f, Merge(IdlePose(), new Pose
        {
            [H] = new Vector3(70f, 0f, 0f), [SP] = new Vector3(10f, 0f, 0f), [NK] = new Vector3(15f, 0f, 0f),
            [ULL] = new Vector3(-20f, 0f, -4f), [LLL] = new Vector3(60f, 0f, 0f),
            [ULR] = new Vector3(-10f, 0f, 4f), [LLR] = new Vector3(50f, 0f, 0f),
            [UAL] = new Vector3(-70f, 0f, -20f), [UAR] = new Vector3(-80f, 0f, 20f),
        }), 0.3f);
        dieB.Key(1.7f, Merge(IdlePose(), new Pose
        {
            [H] = new Vector3(86f, 0f, 0f), [SP] = new Vector3(2f, 0f, 0f), [NK] = new Vector3(-10f, 25f, 0f),
            [ULL] = new Vector3(-6f, 0f, -6f), [LLL] = new Vector3(18f, 0f, 0f),
            [ULR] = new Vector3(-4f, 0f, 6f), [LLR] = new Vector3(10f, 0f, 0f),
            [UAL] = new Vector3(-150f, 0f, -25f), [FAL] = new Vector3(-10f, 0f, 0f),
            [UAR] = new Vector3(-110f, 0f, 30f), [FAR] = new Vector3(-20f, 0f, 0f),
        }), 0.17f);
        var death = dieB.Build("Death", false);

        // ---------------- prayer: kneel on one knee, hands together, head bowed
        Pose Kneel(float breathe) => new Pose
        {
            [SP] = new Vector3(4f + breathe, 0f, 0f), [CH] = new Vector3(4f + breathe, 0f, 0f),
            [NK] = new Vector3(24f + breathe * 2f, 0f, 0f), [HD] = new Vector3(10f, 0f, 0f),
            [UAL] = new Vector3(-28f, -8f, 26f), [FAL] = new Vector3(-105f, 0f, 0f),
            [UAR] = new Vector3(-28f, 8f, -26f), [FAR] = new Vector3(-105f, 0f, 0f), [HAR] = new Vector3(20f, 0f, 0f),
            [ULL] = new Vector3(-88f, 0f, -4f), [LLL] = new Vector3(88f, 0f, 0f), [FTL] = Vector3.zero,
            [ULR] = new Vector3(-4f, 0f, 3f), [LLR] = new Vector3(96f, 0f, 0f), [FTR] = new Vector3(40f, 0f, 0f),
        };
        var enterB = new ClipBuilder();
        enterB.Key(0f, IdlePose(), 0.98f);
        enterB.Key(0.9f, Kneel(0f), 0.52f);
        var prayEnter = enterB.Build("PrayEnter", false);
        var pray = Cycle("Pray", 3.4f, 8, p => (Kneel(Mathf.Sin(p * TAU) * 1.2f), 0.52f - 0.004f * Mathf.Sin(p * TAU)));

        // ---------------- working at the radio
        var work = Cycle("Work", 1.1f, 8, p =>
        {
            float s = Mathf.Sin(p * TAU);
            var pose = new Pose
            {
                [SP] = new Vector3(12f, 0f, 0f), [CH] = new Vector3(14f, 0f, 0f), [NK] = new Vector3(18f, 0f, 0f),
                [UAL] = new Vector3(-62f + 8f * s, 0f, 10f), [FAL] = new Vector3(-45f - 10f * s, 0f, 0f),
                [UAR] = new Vector3(-58f - 8f * s, 0f, -10f), [FAR] = new Vector3(-50f + 10f * s, 0f, 0f),
                [ULL] = new Vector3(-55f, 0f, -6f), [LLL] = new Vector3(80f, 0f, 0f), [FTL] = new Vector3(-22f, 0f, 0f),
                [ULR] = new Vector3(-40f, 0f, 6f), [LLR] = new Vector3(70f, 0f, 0f), [FTR] = new Vector3(-28f, 0f, 0f),
            };
            return (pose, 0.8f);
        });

        // ---------------- sword swing (right hand), upper body only: anticipation, strike, follow-through, recovery
        // The keyframes below were authored for a left-handed swing; mirroring the yaw and roll
        // here flips the whole motion onto the sword arm without retuning a single number.
        var atkB = new ClipBuilder();
        Pose Swing(Vector3 arm, float fore, float spineY, float spineX, float chestY) => Merge(IdlePose(), new Pose
        {
            [SP] = new Vector3(spineX, -spineY, 0f), [CH] = new Vector3(2f, -chestY, 0f), [NK] = new Vector3(0f, (spineY + chestY) * 0.7f, 0f),
            [UAR] = new Vector3(arm.x, -arm.y, -arm.z), [FAR] = new Vector3(fore, 0f, 0f), [HAR] = new Vector3(-10f, 0f, 0f),
        });
        atkB.Key(0f, Swing(new Vector3(4f, 0f, -7f), -16f, 0f, 2f, 0f), 0.98f);
        atkB.Key(0.2f, Swing(new Vector3(-128f, 20f, -50f), -62f, 26f, -4f, 10f), 0.97f);
        atkB.Key(0.32f, Swing(new Vector3(-58f, -10f, 26f), -14f, -30f, 10f, -12f), 0.95f);
        atkB.Key(0.44f, Swing(new Vector3(-22f, 0f, 40f), -30f, -38f, 8f, -15f), 0.95f);
        atkB.Key(AttackLength, Swing(new Vector3(4f, 0f, -7f), -16f, 0f, 2f, 0f), 0.98f);
        var attack = atkB.Build("Attack", false);

        // ---------------- guard: sword brought up across the body, lantern tucked in close.
        // A short loop rather than a still frame, so the block breathes under pressure.
        var guardB = new ClipBuilder();
        Pose Guard(float b) => Merge(IdlePose(b), Merge(new Pose
        {
            [SP] = new Vector3(6f + b, -14f, 0f), [CH] = new Vector3(4f, -10f, 0f), [NK] = new Vector3(6f, 12f, 0f),
            [UAR] = new Vector3(-74f - b * 2f, -26f, -38f), [FAR] = new Vector3(-96f, 0f, 0f), [HAR] = new Vector3(-16f, 0f, 0f),
        }, LanternArm(-48f - b, -72f, 14f, 26f)));
        guardB.Key(0f, Guard(0f), 0.97f);
        guardB.Key(0.6f, Guard(2.5f), 0.965f);
        guardB.Key(1.2f, Guard(0f), 0.97f);
        var guard = guardB.Build("Guard", true);

        // ================= controller
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var f in new[] { "Speed", "MoveMul", "VerticalSpeed" }) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);
        foreach (var b in new[] { "Grounded", "Crawl", "Dead", "Pray", "Work", "Guard" }) ctrl.AddParameter(b, AnimatorControllerParameterType.Bool);
        foreach (var t in new[] { "Jump", "Land", "Hit", "Attack" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        var parameters = ctrl.parameters;
        foreach (var prm in parameters)
        {
            if (prm.name == "Grounded") prm.defaultBool = true;
            else if (prm.name == "MoveMul") prm.defaultFloat = 1f;
        }
        ctrl.parameters = parameters;

        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        tree.AddChild(jog, JogSpeed);
        tree.AddChild(run, RunSpeed);
        loco.speedParameterActive = true;
        loco.speedParameter = "MoveMul";
        sm.defaultState = loco;

        var crawlState = sm.AddState("CrawlMove");
        var crawlTree = new BlendTree { name = "CrawlBlend", blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(crawlTree, ctrl);
        crawlTree.AddChild(crawlIdle, 0f);
        crawlTree.AddChild(crawlMove, CrawlSpeed);
        crawlState.motion = crawlTree;
        crawlState.speedParameterActive = true;
        crawlState.speedParameter = "MoveMul";

        var sJump = sm.AddState("JumpStart"); sJump.motion = jumpStart;
        var sAir = sm.AddState("InAir");
        var airTree = new BlendTree { name = "AirBlend", blendParameter = "VerticalSpeed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(airTree, ctrl);
        airTree.AddChild(airFall, -6f);
        airTree.AddChild(airRise, 4f);
        sAir.motion = airTree;
        var sLand = sm.AddState("Land"); sLand.motion = land;
        var sHit = sm.AddState("Hit"); sHit.motion = hit;
        var sDeath = sm.AddState("Death"); sDeath.motion = death;
        var sEnter = sm.AddState("PrayEnter"); sEnter.motion = prayEnter;
        var sPray = sm.AddState("Pray"); sPray.motion = pray;
        var sWork = sm.AddState("Work"); sWork.motion = work;

        AnimatorStateTransition T(AnimatorState from, AnimatorState to, float duration, bool exit = false, float exitTime = 0f)
        {
            var tr = from.AddTransition(to);
            tr.duration = duration;
            tr.hasExitTime = exit;
            tr.exitTime = exitTime;
            return tr;
        }

        var toDeath = sm.AddAnyStateTransition(sDeath);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        toDeath.duration = 0.12f; toDeath.canTransitionToSelf = false; toDeath.hasExitTime = false;

        var toHit = sm.AddAnyStateTransition(sHit);
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crawl");
        toHit.duration = 0.05f; toHit.canTransitionToSelf = false; toHit.hasExitTime = false;
        T(sHit, loco, 0.15f, true, 0.85f);

        // Jumping and falling.
        T(loco, sJump, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        T(sJump, sAir, 0.08f, true, 0.95f);
        var walkOff = T(loco, sAir, 0.2f);
        walkOff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        walkOff.AddCondition(AnimatorConditionMode.Less, -3f, "VerticalSpeed");
        T(sAir, sLand, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        T(sLand, loco, 0.15f, true, 0.7f);
        T(sLand, loco, 0.12f).AddCondition(AnimatorConditionMode.Greater, 3.2f, "Speed");

        // Crawling.
        T(loco, crawlState, 0.35f).AddCondition(AnimatorConditionMode.If, 0f, "Crawl");
        T(crawlState, loco, 0.35f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crawl");

        // Prayer and radio work.
        T(loco, sEnter, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Pray");
        T(crawlState, sEnter, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Pray");
        T(sEnter, sPray, 0.1f, true, 1f);
        T(sPray, loco, 0.5f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        T(sEnter, loco, 0.4f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        T(loco, sWork, 0.25f).AddCondition(AnimatorConditionMode.If, 0f, "Work");
        T(sWork, loco, 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Work");

        // ---- upper-body layer: the sword swing plays over any locomotion. SwordController drives its weight.
        var mask = BuildUpperBodyMask();
        var upperMachine = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(upperMachine, ctrl);
        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBody",
            defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
            stateMachine = upperMachine
        });
        var empty = upperMachine.AddState("Empty");
        upperMachine.defaultState = empty;
        var sAttack = upperMachine.AddState("Attack"); sAttack.motion = attack;
        var toAttack = empty.AddTransition(sAttack);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.duration = 0.05f; toAttack.hasExitTime = false;
        var attackDone = sAttack.AddTransition(empty);
        attackDone.hasExitTime = true; attackDone.exitTime = 1f; attackDone.duration = 0.05f;

        // Guard is held, so it is a bool with no exit time: up while the button is down.
        var sGuard = upperMachine.AddState("Guard"); sGuard.motion = guard;
        var toGuard = empty.AddTransition(sGuard);
        toGuard.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
        toGuard.duration = 0.12f; toGuard.hasExitTime = false;
        var guardDone = sGuard.AddTransition(empty);
        guardDone.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
        guardDone.duration = 0.18f; guardDone.hasExitTime = false;
        // A swing always beats a guard, so the attack trigger can interrupt it.
        var guardToAttack = sGuard.AddTransition(sAttack);
        guardToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        guardToAttack.duration = 0.05f; guardToAttack.hasExitTime = false;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("EMBER: player animator built (locomotion, jump, crawl, attack + guard layer).");
        return ctrl;
    }

    static AvatarMask BuildUpperBodyMask()
    {
        string path = AnimDir + "/UpperBody.mask";
        AssetDatabase.DeleteAsset(path);
        var mask = new AvatarMask();
        mask.transformCount = Bones.Length;
        for (int i = 0; i < Bones.Length; i++)
        {
            mask.SetTransformPath(i, Bones[i]);
            mask.SetTransformActive(i, Bones[i].StartsWith(SP));
        }
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }
}
