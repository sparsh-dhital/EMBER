using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// The realistic survivor: a MakeHuman-generated human (Tools/survivor/build_survivor.py) driven
// by CMU motion capture (Tools/survivor/bvh_to_fbx.py), replacing the primitive player model.
//
// Import settings and clip ranges are applied by RealisticSurvivorImport below. The menu item
// builds the materials and the Humanoid animator, then swaps the model onto the player of the
// open scene; EmberSceneBuilder calls ApplyToPlayer too, so a full rebuild keeps it.
public static class RealisticSurvivor
{
    public const string Root = "Assets/Characters/Survivor/";
    public const string ModelPath = Root + "Survivor.fbx";
    public const string ControllerPath = Root + "SurvivorHumanoid.controller";
    public const string MaskPath = Root + "SwordArm.mask";

    public static bool Available => File.Exists(ModelPath);

    public struct ClipSpec
    {
        public string file, name;
        public int first, last;          // absolute take frames (takes start at frame 2, 30 fps)
        public bool loop, bakeY;
        public float offset;
        public ClipSpec(string file, string name, int first, int last, bool loop, bool bakeY, float offset = 0f)
        { this.file = file; this.name = name; this.first = first; this.last = last; this.loop = loop; this.bakeY = bakeY; this.offset = offset; }
    }

    // Ranges picked by Tools/survivor/analyze_clips.py: every loop starts on a left heel strike
    // (the run via its cycle offset), so the blend tree mixes gaits in step and footsteps land
    // on the same phase in every clip.
    public static readonly ClipSpec[] Clips =
    {
        new ClipSpec("CMU_77_02", "Idle", 84, 213, true, true),
        new ClipSpec("CMU_77_28", "WalkCareful", 182, 214, true, true),
        new ClipSpec("CMU_35_01", "Walk", 43, 78, true, true),
        new ClipSpec("CMU_35_17", "Jog", 13, 35, true, true),
        new ClipSpec("CMU_09_02", "Run", 2, 24, true, true, 0.864f),
        new ClipSpec("CMU_111_03", "CrawlMove", 195, 214, true, true),
        new ClipSpec("CMU_111_03", "CrawlIdle", 175, 190, true, true),
        new ClipSpec("CMU_16_01", "JumpStart", 25, 34, false, true),
        new ClipSpec("CMU_16_01", "AirRise", 34, 39, false, false),
        new ClipSpec("CMU_16_01", "AirFall", 39, 45, false, false),
        new ClipSpec("CMU_16_01", "Land", 45, 59, false, true),
        new ClipSpec("CMU_23_03", "PrayEnter", 9, 47, false, true),
        new ClipSpec("CMU_23_03", "Pray", 47, 125, true, true),
        new ClipSpec("CMU_02_07", "SwordSlash", 354, 379, false, true),
        new ClipSpec("CMU_02_07", "SwordGuard", 23, 27, true, true),
    };

    // Native ground speed (m/s) of the locomotion clips, measured from the capture.
    const float CarefulSpeed = 0.94f, WalkSpeed = 1.30f, JogSpeed = 3.17f, RunSpeed = 3.55f, CrawlSpeed = 0.25f;

    [MenuItem("EMBER/Build/2c. Realistic Survivor (apply to open scene)")]
    public static void ApplyMenu()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (!pc) { Debug.LogError("EMBER: no PlayerController in the open scene."); return; }
        if (!ApplyToPlayer(pc.gameObject)) return;
        EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
        EditorSceneManager.SaveScene(pc.gameObject.scene);
        Debug.Log("EMBER: realistic survivor applied and scene saved.");
    }

    // ------------------------------------------------------------------ swap onto the player

    public static bool ApplyToPlayer(GameObject player)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (!modelAsset || !avatar || !avatar.isHuman || !avatar.isValid)
        {
            Debug.LogError("EMBER: Survivor.fbx is missing or its Humanoid avatar is not valid.");
            return false;
        }
        var ctrl = BuildController();
        if (!ctrl) return false;

        // Park the kit on the player root while the models are swapped.
        var lantern = player.GetComponentInChildren<LanternLight>(true);
        var sword = player.GetComponent<SwordController>();
        var locket = player.GetComponentInChildren<LocketPendant>(true);
        Transform lanternRoot = lantern ? lantern.transform : null;
        Transform swordRoot = sword ? sword.swordRoot : null;
        Transform locketRoot = locket ? locket.transform : null;
        foreach (var t in new[] { lanternRoot, swordRoot, locketRoot })
            if (t) t.SetParent(player.transform, true);

        foreach (var c in player.transform.Cast<Transform>().ToArray())
            if (c.name == "Model" || c.name == "SurvivorModel")
                Object.DestroyImmediate(c.gameObject);

        // A wrapper takes the PlayerAnimator's lean; the model inside can then be turned to face
        // forward without the lean overwriting that.
        var wrap = new GameObject("SurvivorModel").transform;
        wrap.SetParent(player.transform, false);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, wrap);
        inst.name = "Survivor";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var anim = inst.GetComponent<Animator>();
        if (!anim) anim = inst.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform B(HumanBodyBones b) => Bone(inst.transform, avatar, b);
        // Humanoid retargeting faces +Z during playback. Keep the Animator root aligned
        // with the player, even when the imported bind pose faces the other way.
        // Attach carried props using the bind pose's forward so their local grip offsets
        // follow the hands correctly when the Animator evaluates the first frame.
        Vector3 across = B(HumanBodyBones.RightUpperLeg).position - B(HumanBodyBones.LeftUpperLeg).position;
        Vector3 fwd = Vector3.Cross(across, Vector3.up).normalized;

        AssignMaterials(inst);

        // The bind pose is a T-pose with the palms down: grips sit in the palm, below the knuckles.
        var lh = B(HumanBodyBones.LeftHand);
        var rh = B(HumanBodyBones.RightHand);
        if (lanternRoot)
        {
            Vector3 dir = (B(HumanBodyBones.LeftMiddleProximal).position - lh.position).normalized;
            lanternRoot.SetParent(lh, true);
            lanternRoot.position = lh.position + dir * 0.075f + Vector3.down * 0.035f;
        }
        if (swordRoot)
        {
            Vector3 dir = (B(HumanBodyBones.RightMiddleProximal).position - rh.position).normalized;
            swordRoot.SetParent(rh, true);
            swordRoot.position = rh.position + dir * 0.085f + Vector3.down * 0.035f;
            // Blade out of the thumb side of the fist (forward, in the T-pose), guard across the knuckles.
            swordRoot.rotation = Quaternion.LookRotation(Vector3.Cross(dir, fwd), fwd);
        }
        if (locketRoot)
        {
            var chest = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest);
            locketRoot.SetParent(chest, true);
            locketRoot.position = B(HumanBodyBones.Neck).position + fwd * 0.105f + Vector3.down * 0.085f;
            locketRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Re-point everything that drove the old rig.
        var pa = player.GetComponent<PlayerAnimator>();
        if (pa)
        {
            pa.animator = anim;
            pa.leanRoot = wrap;
            pa.walkClipSpeed = CarefulSpeed;
            pa.speedDamping = 0.12f;
            pa.crawlClipSpeed = CrawlSpeed;
            pa.leftContact = 0.02f;
            pa.rightContact = 0.52f;
            EditorUtility.SetDirty(pa);
        }
        var feet = player.GetComponent<FootPlacement>();
        if (feet)
        {
            feet.hips = B(HumanBodyBones.Hips);
            feet.thighL = B(HumanBodyBones.LeftUpperLeg); feet.shinL = B(HumanBodyBones.LeftLowerLeg); feet.footL = B(HumanBodyBones.LeftFoot);
            feet.thighR = B(HumanBodyBones.RightUpperLeg); feet.shinR = B(HumanBodyBones.RightLowerLeg); feet.footR = B(HumanBodyBones.RightFoot);
            EditorUtility.SetDirty(feet);
        }

        // First person: the right arm reaches with the same IK, and the head is hidden from the
        // inside (shadows kept) so the camera never sees the back of his face.
        var mode = Object.FindFirstObjectByType<CameraModeController>(FindObjectsInactive.Include);
        if (mode)
        {
            mode.upperArmR = B(HumanBodyBones.RightUpperArm);
            mode.forearmR = B(HumanBodyBones.RightLowerArm);
            mode.handR = rh;
            var head = new[] { "Survivor_Body", "Survivor_Eyes", "Survivor_Eyebrows", "Survivor_Eyelashes", "Survivor_Hair", "Survivor_Beanie" };
            mode.hideInFirstPerson = inst.GetComponentsInChildren<Renderer>(true).Where(r => head.Contains(r.name)).ToArray();
            EditorUtility.SetDirty(mode);
        }

        var carry = wrap.gameObject.AddComponent<LanternCarry>();
        carry.controller = player.GetComponent<PlayerController>();
        carry.fuel = player.GetComponent<LanternFuel>();
        carry.prayer = player.GetComponent<PrayerSystem>();
        carry.upperArm = B(HumanBodyBones.LeftUpperArm);
        carry.forearm = B(HumanBodyBones.LeftLowerArm);
        carry.hand = lh;

        inst.AddComponent<HandGrip>().animator = anim;
        inst.AddComponent<SurvivorIK>().animator = anim;
        BuildRagdoll(inst, anim, avatar, player);

        EmberCharacterBuilder.SetLayerRecursive(wrap.gameObject, LayerMask.NameToLayer("Player"));
        return true;
    }

    static Transform Bone(Transform root, Avatar avatar, HumanBodyBones bone)
    {
        string human = HumanTrait.BoneName[(int)bone];
        var map = avatar.humanDescription.human.FirstOrDefault(h => h.humanName == human);
        if (string.IsNullOrEmpty(map.boneName)) return null;
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == map.boneName);
    }

    // ------------------------------------------------------------------ materials

    static void AssignMaterials(GameObject inst)
    {
        string T = Root + "Textures/";
        var table = new (string mesh, Material mat)[]
        {
            ("Survivor_Body", Mat("Skin", T + "middleage_lightskinned_male_diffuse.png", null, new Color(1f, 0.86f, 0.74f), 0.32f)),
            ("Survivor_Eyes", Mat("Eyes", T + "brown_eye.png", null, new Color(0.85f, 0.85f, 0.9f), 0.85f)),
            ("Survivor_Eyebrows", Mat("Eyebrows", T + "eyebrow001.png", null, new Color(0.22f, 0.16f, 0.12f), 0.2f, true)),
            ("Survivor_Eyelashes", Mat("Eyelashes", T + "eyelashes01.png", null, new Color(0.12f, 0.1f, 0.09f), 0.2f, true)),
            ("Survivor_Hair", Mat("Hair", T + "short02_diffuse.png", null, new Color(0.35f, 0.27f, 0.21f), 0.3f, true)),
            ("Survivor_Shirt", Mat("Shirt", T + "male_casualsuit01_diffuse.png", T + "male_casualsuit01_normal.png", new Color(0.42f, 0.40f, 0.38f), 0.2f)),
            ("Survivor_Coat", Mat("Coat", T + "F_Cardigan_Long_Open_Front.png", T + "F_Cardigan_Long_Open_Front_NRM.png", new Color(0.30f, 0.20f, 0.12f), 0.15f, false, true)),
            ("Survivor_Beanie", Mat("Beanie", T + "Knitted_Hat_01.png", T + "Knitted_Hat_01_NRM.png", new Color(0.30f, 0.08f, 0.07f), 0.1f)),
            ("Survivor_Boots", Mat("Boots", T + "Shoes_Biker_Boots.png", T + "Shoes_Biker_Boots_NRM.png", new Color(0.75f, 0.68f, 0.62f), 0.45f)),
            ("Survivor_Gloves", Mat("Gloves", T + "Gloves03UV.png", null, new Color(0.5f, 0.42f, 0.35f), 0.3f)),
        };
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var hit = table.FirstOrDefault(e => e.mesh == r.name);
            if (!hit.mat) continue;
            r.sharedMaterials = Enumerable.Repeat(hit.mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            r.shadowCastingMode = r.name == "Survivor_Eyelashes" || r.name == "Survivor_Eyebrows"
                ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }

    static Material Mat(string name, string tex, string normal, Color tint, float smooth, bool clip = false, bool twoSided = false)
    {
        Directory.CreateDirectory(Root + "Materials");
        string path = Root + "Materials/M_Survivor_" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        var n = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
        m.SetTexture("_BumpMap", n);
        if (n) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        m.SetFloat("_AlphaClip", clip ? 1f : 0f);
        if (clip) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.4f); m.renderQueue = (int)RenderQueue.AlphaTest; }
        else { m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; }
        // Brows, lashes, hair cards and the open coat are seen from both sides.
        m.SetFloat("_Cull", (clip || twoSided) ? (float)CullMode.Off : (float)CullMode.Back);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ ragdoll

    static void BuildRagdoll(GameObject inst, Animator anim, Avatar avatar, GameObject player)
    {
        Transform B(HumanBodyBones b) => Bone(inst.transform, avatar, b);
        var hips = B(HumanBodyBones.Hips);
        var chest = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
        var head = B(HumanBodyBones.Head);
        var bodies = new System.Collections.Generic.List<Rigidbody>();
        var cols = new System.Collections.Generic.List<Collider>();

        Rigidbody Body(Transform t, float mass, Rigidbody parent, Vector2 twist, float swing)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bodies.Add(rb);
            if (parent)
            {
                var j = t.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.enableProjection = true;
                j.lowTwistLimit = new SoftJointLimit { limit = twist.x };
                j.highTwistLimit = new SoftJointLimit { limit = twist.y };
                j.swing1Limit = new SoftJointLimit { limit = swing };
                j.swing2Limit = new SoftJointLimit { limit = swing };
            }
            return rb;
        }

        void Capsule(Transform a, Transform b, float radiusFrac)
        {
            var c = a.gameObject.AddComponent<CapsuleCollider>();
            Vector3 local = a.InverseTransformPoint(b.position);
            int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            c.direction = axis;
            c.center = local * 0.5f;
            c.height = local.magnitude;
            c.radius = local.magnitude * radiusFrac;
            cols.Add(c);
        }

        var rbHips = Body(hips, 12f, null, Vector2.zero, 0f);
        var hb = hips.gameObject.AddComponent<BoxCollider>();
        hb.center = hips.InverseTransformPoint(Vector3.Lerp(hips.position, chest.position, 0.25f));
        hb.size = new Vector3(0.32f, 0.24f, 0.22f);
        cols.Add(hb);

        var rbChest = Body(chest, 14f, rbHips, new Vector2(-20f, 20f), 20f);
        var cb = chest.gameObject.AddComponent<BoxCollider>();
        cb.center = chest.InverseTransformPoint(Vector3.Lerp(chest.position, B(HumanBodyBones.Neck).position, 0.5f));
        cb.size = new Vector3(0.36f, Vector3.Distance(chest.position, B(HumanBodyBones.Neck).position) + 0.06f, 0.24f);
        cols.Add(cb);

        Body(head, 5f, rbChest, new Vector2(-40f, 25f), 30f);
        var hs = head.gameObject.AddComponent<SphereCollider>();
        hs.center = head.InverseTransformPoint(head.position + Vector3.up * 0.08f);
        hs.radius = 0.11f;
        cols.Add(hs);

        foreach (var side in new[] { 0, 1 })
        {
            var ua = B(side == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var la = B(side == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = B(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var ul = B(side == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var ll = B(side == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = B(side == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);

            var rbUA = Body(ua, 2.5f, rbChest, new Vector2(-70f, 10f), 50f);
            Capsule(ua, la, 0.2f);
            Body(la, 1.8f, rbUA, new Vector2(-90f, 0f), 20f);
            Capsule(la, hand, 0.17f);
            var rbUL = Body(ul, 8f, rbHips, new Vector2(-20f, 70f), 30f);
            Capsule(ul, ll, 0.17f);
            Body(ll, 4.5f, rbUL, new Vector2(-80f, 0f), 15f);
            Capsule(ll, foot, 0.13f);
        }

        var rd = inst.AddComponent<RagdollDeath>();
        rd.animator = anim;
        rd.health = player.GetComponent<PlayerHealth>();
        rd.controller = player.GetComponent<CharacterController>();
        rd.bodies = bodies.ToArray();
        rd.colliders = cols.ToArray();
        foreach (var c in cols) c.enabled = false;
    }

    // ------------------------------------------------------------------ animator

    static AnimatorController BuildController()
    {
        var clips = AssetDatabase.FindAssets("CMU_ t:Model", new[] { Root + "Animations" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
            .Where(c => !c.name.StartsWith("__preview__"))
            .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
        foreach (var s in Clips)
            if (!clips.ContainsKey(s.name)) { Debug.LogError("EMBER: missing clip " + s.name); return null; }
        AnimationClip C(string n) => clips[n];

        AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var f in new[] { "Speed", "MoveMul", "VerticalSpeed" }) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);
        foreach (var b in new[] { "Grounded", "Crawl", "Dead", "Pray", "Work", "Guard" }) ctrl.AddParameter(b, AnimatorControllerParameterType.Bool);
        foreach (var t in new[] { "Jump", "Land", "Hit", "Attack" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        var ps = ctrl.parameters;
        foreach (var p in ps) if (p.name == "MoveMul") p.defaultFloat = 1f;
        foreach (var p in ps) if (p.name == "Grounded") p.defaultBool = true;
        ctrl.parameters = ps;
        var layers = ctrl.layers;
        layers[0].iKPass = true;           // SurvivorIK: hands together in prayer
        ctrl.layers = layers;
        var sm = ctrl.layers[0].stateMachine;

        // Locomotion: thresholds are the clips' own speeds, plus timescaled copies at the game's
        // walk (1.8) and sprint (5.8) so the feet match there too.
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(C("Idle"), 0f);
        tree.AddChild(C("WalkCareful"), CarefulSpeed);
        tree.AddChild(C("Walk"), WalkSpeed);
        tree.AddChild(C("Walk"), 1.8f);
        tree.AddChild(C("Jog"), JogSpeed);
        tree.AddChild(C("Run"), RunSpeed);
        tree.AddChild(C("Run"), 5.8f);
        var ch = tree.children;
        ch[3].timeScale = 1.8f / WalkSpeed;
        ch[6].timeScale = 5.8f / RunSpeed;
        tree.children = ch;
        loco.speedParameter = "MoveMul";
        loco.speedParameterActive = true;
        sm.defaultState = loco;

        // Crawl: the capture crawls at 0.25 m/s; the game crawls at 1.1, so the top entry plays
        // faster - capped at 2.6x, past which it stops reading as a person.
        var crawlState = sm.AddState("CrawlMove");
        var crawlTree = new BlendTree { name = "Crawl", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(crawlTree, ctrl);
        crawlTree.AddChild(C("CrawlIdle"), 0f);
        crawlTree.AddChild(C("CrawlMove"), CrawlSpeed);
        crawlTree.AddChild(C("CrawlMove"), 1.1f);
        var cc = crawlTree.children;
        cc[2].timeScale = 2.6f;
        crawlTree.children = cc;
        crawlState.motion = crawlTree;
        crawlState.speedParameter = "MoveMul";
        crawlState.speedParameterActive = true;

        var sJump = sm.AddState("JumpStart"); sJump.motion = C("JumpStart"); sJump.speed = 2.6f;
        var sAir = sm.AddState("InAir");
        var airTree = new BlendTree { name = "Air", blendParameter = "VerticalSpeed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(airTree, ctrl);
        airTree.AddChild(C("AirFall"), -6f);
        airTree.AddChild(C("AirRise"), 4f);
        sAir.motion = airTree;
        var sLand = sm.AddState("Land"); sLand.motion = C("Land"); sLand.speed = 1.3f;
        var sHit = sm.AddState("Hit"); sHit.motion = C("Land"); sHit.speed = 1.6f;
        var sDeath = sm.AddState("Death"); sDeath.motion = C("Land");
        var sEnter = sm.AddState("PrayEnter"); sEnter.motion = C("PrayEnter"); sEnter.speed = 1.4f;
        var sPray = sm.AddState("Pray"); sPray.motion = C("Pray");
        var sWork = sm.AddState("Work"); sWork.motion = C("Idle");

        AnimatorStateTransition T(AnimatorState from, AnimatorState to, float dur, bool exit = false, float exitTime = 0f)
        {
            var tr = from.AddTransition(to);
            tr.duration = dur;
            tr.hasExitTime = exit;
            tr.exitTime = exitTime;
            return tr;
        }

        var toDeath = sm.AddAnyStateTransition(sDeath);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        toDeath.duration = 0.1f; toDeath.canTransitionToSelf = false; toDeath.hasExitTime = false;
        var toHit = sm.AddAnyStateTransition(sHit);
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crawl");
        toHit.duration = 0.05f; toHit.canTransitionToSelf = false; toHit.hasExitTime = false;
        T(sHit, loco, 0.15f, true, 0.85f);

        T(loco, sJump, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        T(sJump, sAir, 0.08f, true, 0.95f);
        var walkOff = T(loco, sAir, 0.2f);
        walkOff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        walkOff.AddCondition(AnimatorConditionMode.Less, -3f, "VerticalSpeed");
        T(sAir, sLand, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        T(sLand, loco, 0.15f, true, 0.7f);
        T(sLand, loco, 0.12f).AddCondition(AnimatorConditionMode.Greater, 3.2f, "Speed");

        T(loco, crawlState, 0.35f).AddCondition(AnimatorConditionMode.If, 0f, "Crawl");
        T(crawlState, loco, 0.35f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crawl");

        T(loco, sEnter, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Pray");
        T(crawlState, sEnter, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Pray");
        T(sEnter, sPray, 0.2f, true, 0.95f);
        T(sPray, loco, 0.5f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        T(sEnter, loco, 0.4f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Pray");
        T(loco, sWork, 0.25f).AddCondition(AnimatorConditionMode.If, 0f, "Work");
        T(sWork, loco, 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Work");

        // Sword arm: swings and guards over whatever the legs are doing. The left arm is left out:
        // it carries the lantern, which LanternCarry holds up to light the way.
        AssetDatabase.DeleteAsset(MaskPath);
        var mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                                                 part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers ||
                                                 part == AvatarMaskBodyPart.RightHandIK);
        }
        AssetDatabase.CreateAsset(mask, MaskPath);

        var upper = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(upper, ctrl);
        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBody", defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask, stateMachine = upper
        });
        var empty = upper.AddState("Empty");
        upper.defaultState = empty;
        var sAttack = upper.AddState("Attack"); sAttack.motion = C("SwordSlash"); sAttack.speed = 1.85f;
        var toAttack = empty.AddTransition(sAttack);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.duration = 0.05f; toAttack.hasExitTime = false;
        var attackDone = sAttack.AddTransition(empty);
        attackDone.hasExitTime = true; attackDone.exitTime = 1f; attackDone.duration = 0.05f;
        var sGuard = upper.AddState("Guard"); sGuard.motion = C("SwordGuard");
        var toGuard = empty.AddTransition(sGuard);
        toGuard.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
        toGuard.duration = 0.12f; toGuard.hasExitTime = false;
        var guardDone = sGuard.AddTransition(empty);
        guardDone.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
        guardDone.duration = 0.18f; guardDone.hasExitTime = false;
        var guardToAttack = sGuard.AddTransition(sAttack);
        guardToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        guardToAttack.duration = 0.05f; guardToAttack.hasExitTime = false;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }
}

// Import settings for everything under Assets/Characters/Survivor.
public class RealisticSurvivorImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(RealisticSurvivor.Root)) return;
        var mi = (ModelImporter)assetImporter;
        bool anim = Path.GetFileName(assetPath).StartsWith("CMU_");
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.bakeAxisConversion = true;
        mi.importBlendShapes = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importVisibility = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importAnimation = anim;
        mi.optimizeGameObjects = false;
        if (anim)
        {
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
            mi.resampleCurves = true;
        }
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(RealisticSurvivor.Root)) return;
        var mi = (ModelImporter)assetImporter;
        string file = Path.GetFileNameWithoutExtension(assetPath);
        var specs = RealisticSurvivor.Clips.Where(c => c.file == file).ToArray();
        var defaults = mi.defaultClipAnimations;
        if (specs.Length == 0 || defaults.Length == 0) return;
        var take = defaults[0];
        // The takes start at frame 2 in Blender; respect however Unity numbers the take.
        float shift = take.firstFrame - 2f;
        mi.clipAnimations = specs.Select(s => new ModelImporterClipAnimation
        {
            name = s.name,
            takeName = take.takeName,
            firstFrame = s.first + shift,
            lastFrame = s.last + shift,
            loopTime = s.loop,
            loopPose = s.loop,
            cycleOffset = s.offset,
            // Rotation and ground position become root motion, which the player ignores: the
            // character controller moves him, the clips only animate the body in place.
            lockRootRotation = false,
            keepOriginalOrientation = false,
            lockRootPositionXZ = false,
            keepOriginalPositionXZ = false,
            // Height stays in the pose, measured from the feet, so stance and crouch keep their
            // feet on the ground; the airborne clips leave height to the jump physics.
            lockRootHeightY = s.bakeY,
            heightFromFeet = s.bakeY,
            keepOriginalPositionY = false,
        }).ToArray();
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(RealisticSurvivor.Root)) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 2048;
        string n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        if (n.EndsWith("_nrm") || n.EndsWith("_normal")) ti.textureType = TextureImporterType.NormalMap;
    }
}
