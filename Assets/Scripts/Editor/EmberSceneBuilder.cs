using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-click assembly of the playable scene. Reuses the prototype's Player (CharacterController,
// LanternFuel, PlayerController) and Main Camera, then adds the level, systems, cameras and UI, and wires them together.
public static class EmberSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Ember_Main.unity";

    [MenuItem("EMBER/Build/5. Assemble Ember_Main Scene")]
    public static void BuildScene()
    {
        EnsureLayers();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Prototype pieces that the new systems replace (the original P1_MovementTest scene is untouched).
        foreach (var n in new[] { "Plane", "Pickups", "HUD_Canvas", "GameSystems", "CM_Gameplay", "CM_Menu", "CM_Ending", "CM_FirstPerson" })
        {
            var go = GameObject.Find(n);
            if (go) Object.DestroyImmediate(go);
        }

        var level = EmberLevelBuilder.Build();
        var player = SetupPlayer(level);
        var systems = SetupSystems(player, level);
        var cams = SetupCameras(player, level);
        var ui = EmberUIBuilder.Build();
        Wire(player, systems, cams, ui, level);
        ConfigureProject();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("EMBER: Ember_Main assembled and saved.");
    }

    // ------------------------------------------------------------------ player

    class PlayerRefs
    {
        public GameObject go;
        public PlayerController controller;
        public LanternFuel fuel;
        public PlayerHealth health;
        public PlayerInteractor interactor;
        public PlayerAnimator animator;
        public PrayerSystem prayer;
        public LanternLight lantern;
        public SwordController sword;
        public EmberCharacterBuilder.PlayerParts parts;
        public CapsuleCollider hitbox;
    }

    static PlayerRefs SetupPlayer(EmberLevelBuilder.Result level)
    {
        var go = GameObject.Find("Player");
        if (!go) go = new GameObject("Player");
        var p = new PlayerRefs { go = go };

        // Remove the prototype capsule visual and floating lantern light; keep the gameplay components.
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        foreach (var n in new[] { "Lantern", "AmbientDust", "GroundFog" })
        {
            var old = go.transform.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }

        go.tag = "Player";
        go.transform.SetPositionAndRotation(level.playerStart.position + Vector3.up * 0.05f, level.playerStart.rotation);

        var cc = Get<CharacterController>(go);
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.92f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.35f;
        cc.skinWidth = 0.04f;
        cc.minMoveDistance = 0f;

        // Reused components are reset to their code defaults so tuning changes in the scripts actually apply.
        p.fuel = Fresh<LanternFuel>(go);
        p.controller = Fresh<PlayerController>(go);
        p.controller.groundLayers = EmberLayers.World;
        p.health = Fresh<PlayerHealth>(go);
        p.interactor = Fresh<PlayerInteractor>(go);
        p.interactor.interactableLayers = EmberLayers.Interactables;
        p.animator = Fresh<PlayerAnimator>(go);
        p.prayer = Fresh<PrayerSystem>(go);

        p.parts = EmberCharacterBuilder.BuildModel(go);
        p.lantern = p.parts.lanternRoot.gameObject.AddComponent<LanternLight>();
        p.lantern.fuel = p.fuel;
        p.lantern.lanternLight = p.parts.lanternLight;
        p.lantern.flame = p.parts.flameGroup;
        p.lantern.flameRenderer = p.parts.flameRenderer;
        p.lantern.glassRenderer = p.parts.glassRenderer;
        p.lantern.embers = p.parts.embers;
        p.lantern.extinguishSmoke = p.parts.smoke;
        p.lantern.pickupBurst = p.parts.pickupFlare;

        p.animator.animator = p.parts.animator;
        p.animator.controller = p.controller;
        p.animator.leanRoot = p.parts.model;

        // The sword lives on the player root, not on the blade, so its hit arcs are measured
        // from where the character is standing rather than from wherever the animation has
        // flung the blade this frame.
        p.sword = Fresh<SwordController>(go);
        p.sword.swordRoot = p.parts.swordRoot;
        p.sword.bladeRenderers = p.parts.bladeRenderers;
        p.sword.controller = p.controller;
        p.sword.playerAnimator = p.animator;
        p.sword.fuel = p.fuel;
        p.sword.lantern = p.lantern;
        p.animator.sword = p.sword;

        var feet = Fresh<FootPlacement>(go);
        feet.controller = p.controller;
        feet.groundLayers = p.controller.groundLayers;
        feet.hips = FindDeep(p.parts.model, "Hips");
        feet.thighL = FindDeep(p.parts.model, "UpperLegL");
        feet.shinL = FindDeep(p.parts.model, "LowerLegL");
        feet.footL = FindDeep(p.parts.model, "FootL");
        feet.thighR = FindDeep(p.parts.model, "UpperLegR");
        feet.shinR = FindDeep(p.parts.model, "LowerLegR");
        feet.footR = FindDeep(p.parts.model, "FootR");

        p.health.controller = p.controller;
        p.health.animator = p.animator;
        p.health.lantern = p.lantern;
        p.health.fuel = p.fuel;

        p.prayer.fuel = p.fuel;
        p.prayer.health = p.health;
        p.prayer.controller = p.controller;
        p.prayer.playerAnimator = p.animator;
        p.prayer.crossRoot = p.parts.crossRoot;
        p.prayer.crossRenderers = p.parts.crossRenderers;
        p.prayer.holyLight = p.parts.holyLight;
        p.prayer.risingMotes = p.parts.prayerMotes;
        p.prayer.activationRing = p.parts.prayerRing;

        // Drifting dust and low fog around the player (world-space, so they don't follow rigidly).
        var dust = EmberFX.Motes(go.transform, new Vector3(0f, 1.5f, 0f), new Color(0.8f, 0.82f, 0.9f, 0.18f), 5f, 1f, 0.03f, 40, "AmbientDust");
        var dm = dust.main; dm.playOnAwake = true; dm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f); dm.gravityModifier = 0f;
        var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(18f, 4f, 18f);
        var dn = dust.noise; dn.enabled = true; dn.strength = 0.2f; dn.frequency = 0.3f;
        var fog = EmberFX.Create("GroundFog", go.transform, new Vector3(0f, 0.4f, 0f), "P_Smoke", true, 14);
        var fm = fog.main;
        fm.playOnAwake = true; fm.loop = true;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        fm.startSize = new ParticleSystem.MinMaxCurve(5f, 8f);
        fm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        fm.startColor = new Color(0.45f, 0.5f, 0.6f, 0.09f);
        var fe = fog.emission; fe.rateOverTime = 1.3f;
        var fs = fog.shape; fs.enabled = true; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(30f, 0.3f, 30f);
        var fc = fog.colorOverLifetime; fc.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fc.color = grad;

        EmberCharacterBuilder.SetLayerRecursive(go, LayerMask.NameToLayer(EmberLayers.Player));

        // What enemy attacks test against: a trigger around the body on its own layer, so nothing else ever hits it.
        var hitbox = go.transform.Find("Hitbox") ? go.transform.Find("Hitbox").gameObject : new GameObject("Hitbox");
        hitbox.transform.SetParent(go.transform, false);
        hitbox.layer = LayerMask.NameToLayer(EmberLayers.PlayerHitbox);
        var hc = Get<CapsuleCollider>(hitbox);
        hc.isTrigger = true;
        hc.radius = 0.34f;
        hc.height = 1.8f;
        hc.center = new Vector3(0f, 0.92f, 0f);
        p.hitbox = hc;
        return p;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var t = FindDeep(c, name);
            if (t) return t;
        }
        return null;
    }

    static T Fresh<T>(GameObject go) where T : Component
    {
        var c = Get<T>(go);
        var temp = new GameObject("_defaults") { hideFlags = HideFlags.HideAndDontSave };
        var defaults = temp.AddComponent(typeof(T));
        EditorUtility.CopySerialized(defaults, c);
        Object.DestroyImmediate(temp);
        return c;
    }

    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    // ------------------------------------------------------------------ systems

    class SystemRefs
    {
        public GameManager game;
        public InputReader input;
        public AudioManager audio;
        public RadioMissionSystem mission;
        public VampireSpawner spawner;
        public PostFXController postFx;
    }

    static SystemRefs SetupSystems(PlayerRefs p, EmberLevelBuilder.Result level)
    {
        var root = new GameObject("GameSystems");
        var s = new SystemRefs
        {
            game = root.AddComponent<GameManager>(),
            input = root.AddComponent<InputReader>(),
            mission = root.AddComponent<RadioMissionSystem>(),
        };

        var audioGo = new GameObject("AudioManager");
        audioGo.transform.SetParent(root.transform, false);
        s.audio = audioGo.AddComponent<AudioManager>();
        FillAudio(s.audio);

        var spawnerGo = new GameObject("VampireDirector");
        spawnerGo.transform.SetParent(root.transform, false);
        s.spawner = spawnerGo.AddComponent<VampireSpawner>();
        s.spawner.vampirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmberPrefabBuilder.VampirePath).GetComponent<VampireAI>();
        s.spawner.spawnPoints = level.spawnPoints;

        s.postFx = level.volume.gameObject.AddComponent<PostFXController>();
        s.postFx.volume = level.volume;
        return s;
    }

    static void FillAudio(AudioManager a)
    {
        AudioClip C(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(EmberAudioGenerator.SfxDir + "/" + n + ".wav");
        AudioClip L(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(EmberAudioGenerator.LoopDir + "/" + n + ".wav");
        SfxEntry E(Sfx id, float vol, float pv, params string[] names)
        {
            var clips = new AudioClip[names.Length];
            for (int i = 0; i < names.Length; i++) clips[i] = C(names[i]);
            return new SfxEntry { id = id, clips = clips, volume = vol, pitchVariance = pv };
        }

        a.sounds = new[]
        {
            E(Sfx.Footstep, 0.55f, 0.08f, "footstep_0", "footstep_1", "footstep_2", "footstep_3"),
            E(Sfx.FuelPickup, 0.8f, 0.04f, "fuel_pickup"),
            E(Sfx.RadioPickup, 0.8f, 0.02f, "radio_pickup"),
            E(Sfx.LocketPickup, 0.85f, 0f, "locket_pickup"),
            E(Sfx.Interact, 0.5f, 0.05f, "interact"),
            E(Sfx.Denied, 0.5f, 0f, "denied"),
            E(Sfx.LowFuelWarning, 0.7f, 0f, "low_fuel_warning"),
            E(Sfx.LanternOut, 0.9f, 0f, "lantern_out"),
            E(Sfx.LanternRelight, 0.8f, 0f, "lantern_relight"),
            E(Sfx.VampireHiss, 0.9f, 0.12f, "vampire_hiss_0", "vampire_hiss_1", "vampire_hiss_2"),
            E(Sfx.VampireScreech, 0.8f, 0.1f, "vampire_screech_0", "vampire_screech_1"),
            E(Sfx.VampireAttack, 0.9f, 0.08f, "vampire_attack"),
            E(Sfx.PlayerHurt, 0.9f, 0.06f, "player_hurt"),
            E(Sfx.PlayerDeath, 1f, 0f, "player_death"),
            E(Sfx.PrayerStart, 0.9f, 0f, "prayer_start"),
            E(Sfx.PrayerTick, 0.6f, 0f, "prayer_tick"),
            E(Sfx.PrayerEnd, 0.7f, 0f, "prayer_end"),
            E(Sfx.RadioBeep, 0.6f, 0.02f, "radio_beep"),
            E(Sfx.RadioSignal, 0.7f, 0f, "radio_signal"),
            E(Sfx.RadioVoice, 0.8f, 0.05f, "radio_voice"),
            E(Sfx.Flare, 0.9f, 0f, "flare"),
            E(Sfx.Victory, 0.85f, 0f, "victory"),
            E(Sfx.Defeat, 0.85f, 0f, "defeat"),
            E(Sfx.UiClick, 0.5f, 0.05f, "ui_click"),
            E(Sfx.Heartbeat, 0.9f, 0f, "heartbeat"),

            // Surface-dependent footsteps. FootstepSurface picks between these from what
            // is actually under the boot; Sfx.Footstep stays the fallback.
            E(Sfx.FootstepDirt, 0.55f, 0.09f, "footstep_dirt_0", "footstep_dirt_1", "footstep_dirt_2"),
            E(Sfx.FootstepLeaves, 0.5f, 0.11f, "footstep_leaves_0", "footstep_leaves_1", "footstep_leaves_2"),
            E(Sfx.FootstepWood, 0.6f, 0.08f, "footstep_wood_0", "footstep_wood_1", "footstep_wood_2"),
            E(Sfx.FootstepStone, 0.58f, 0.08f, "footstep_stone_0", "footstep_stone_1", "footstep_stone_2"),

            // Sword combat.
            E(Sfx.SwordSwing, 0.6f, 0.07f, "sword_swing_0", "sword_swing_1", "sword_swing_2"),
            E(Sfx.SwordHit, 0.9f, 0.08f, "sword_hit_0", "sword_hit_1", "sword_hit_2"),
            E(Sfx.SwordBreak, 0.95f, 0.04f, "sword_parry"),
            E(Sfx.SwordPickup, 0.9f, 0f, "sword_pickup"),
            E(Sfx.LethalStrike, 1f, 0.03f, "lethal_strike"),
            E(Sfx.VampireStagger, 0.85f, 0.1f, "vampire_stagger_0", "vampire_stagger_1"),
            E(Sfx.VampireDeath, 0.95f, 0.06f, "vampire_death"),

            // Repair-puzzle interface.
            E(Sfx.PuzzleOpen, 0.7f, 0.02f, "puzzle_open"),
            E(Sfx.PuzzleClose, 0.6f, 0.02f, "puzzle_close"),
            E(Sfx.PuzzleClick, 0.45f, 0.06f, "puzzle_click_0", "puzzle_click_1", "puzzle_click_2"),
            E(Sfx.PuzzleTone, 0.6f, 0f, "puzzle_tone_0", "puzzle_tone_1", "puzzle_tone_2", "puzzle_tone_3", "puzzle_tone_4"),
            E(Sfx.PuzzleSolved, 0.85f, 0f, "puzzle_solved"),
            E(Sfx.PuzzleFail, 0.6f, 0.03f, "puzzle_fail"),
            E(Sfx.PuzzleHint, 0.5f, 0f, "puzzle_hint"),
        };
        a.windLoop = L("loop_wind");
        a.insectsLoop = L("loop_insects");
        a.tensionDroneLoop = L("loop_tension_drone");
        a.prayerChoirLoop = L("loop_prayer_choir");
        a.lanternCrackleLoop = L("loop_lantern_crackle");
    }

    // ------------------------------------------------------------------ cameras

    class CameraRefs
    {
        public Camera main;
        public CinemachineCamera gameplay, menu, ending, firstPerson;
        public CameraController controller;
        public CameraModeController mode;
    }

    static CameraRefs SetupCameras(PlayerRefs p, EmberLevelBuilder.Result level)
    {
        var c = new CameraRefs();
        var camGo = GameObject.FindWithTag("MainCamera");
        if (!camGo) { camGo = new GameObject("Main Camera") { tag = "MainCamera" }; camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>(); }
        var oldFollow = camGo.GetComponent<ThirdPersonCamera>();
        if (oldFollow) Object.DestroyImmediate(oldFollow);
        c.main = camGo.GetComponent<Camera>();
        c.main.clearFlags = CameraClearFlags.SolidColor;
        c.main.backgroundColor = RenderSettings.fogColor;
        c.main.nearClipPlane = 0.1f;
        c.main.farClipPlane = 90f;
        var urp = camGo.GetComponent<UniversalAdditionalCameraData>() ?? camGo.AddComponent<UniversalAdditionalCameraData>();
        urp.renderPostProcessing = true;
        urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var brain = camGo.GetComponent<CinemachineBrain>() ?? camGo.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.8f);

        // Gameplay: over-the-shoulder orbit with damping, collision and a little handheld life.
        var gp = new GameObject("CM_Gameplay");
        c.gameplay = gp.AddComponent<CinemachineCamera>();
        c.gameplay.Follow = p.parts.cameraTarget;
        c.gameplay.LookAt = p.parts.cameraTarget;
        var lens = c.gameplay.Lens; lens.FieldOfView = 50f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 90f; c.gameplay.Lens = lens;
        c.gameplay.Priority.Enabled = true; c.gameplay.Priority.Value = 10;

        var orbit = gp.AddComponent<CinemachineOrbitalFollow>();
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = 5.2f;
        orbit.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        orbit.TrackerSettings.PositionDamping = new Vector3(0.35f, 0.25f, 0.35f);
        orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbit.HorizontalAxis.Wrap = true;
        orbit.HorizontalAxis.Value = level.playerStart.eulerAngles.y;
        orbit.VerticalAxis.Range = new Vector2(-10f, 65f);
        orbit.VerticalAxis.Wrap = false;
        orbit.VerticalAxis.Value = 17f;
        orbit.RadialAxis.Value = 1f;

        var composer = gp.AddComponent<CinemachineRotationComposer>();
        composer.Damping = new Vector2(0.45f, 0.35f);
        var comp = composer.Composition;
        comp.ScreenPosition = new Vector2(-0.07f, 0.06f);
        composer.Composition = comp;

        var deocc = gp.AddComponent<CinemachineDeoccluder>();
        deocc.CollideAgainst = EmberLayers.World;
        deocc.IgnoreTag = "Player";
        deocc.MinimumDistanceFromTarget = 0.6f;
        var avoid = deocc.AvoidObstacles;
        avoid.Enabled = true;
        avoid.CameraRadius = 0.25f;
        avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
        avoid.Damping = 0.35f;
        avoid.DampingWhenOccluded = 0.1f;
        avoid.SmoothingTime = 0.4f;
        deocc.AvoidObstacles = avoid;

        var noise = gp.AddComponent<CinemachineBasicMultiChannelPerlin>();
        noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
        noise.AmplitudeGain = 0.25f;
        noise.FrequencyGain = 1f;

        c.controller = gp.AddComponent<CameraController>();
        c.controller.cinemachineCamera = c.gameplay;
        c.controller.orbit = orbit;
        c.controller.noise = noise;
        c.controller.player = p.controller;
        c.controller.fuel = p.fuel;
        c.controller.followTarget = p.parts.cameraTarget;

        // Title screen: slow orbit around the radio centre.
        var mg = new GameObject("CM_Menu");
        c.menu = mg.AddComponent<CinemachineCamera>();
        var ml = c.menu.Lens; ml.FieldOfView = 42f; ml.FarClipPlane = 90f; c.menu.Lens = ml;
        c.menu.Priority.Enabled = true; c.menu.Priority.Value = 20;
        var orbitScript = mg.AddComponent<MenuCameraOrbit>();
        orbitScript.centre = level.radioCentreRoot;

        // Ending shot: high and wide as dawn breaks over the radio centre.
        var eg = new GameObject("CM_Ending");
        eg.transform.position = level.endingCameraAnchor.position;
        eg.transform.LookAt(level.radioCentreRoot.position + Vector3.up * 3f);
        c.ending = eg.AddComponent<CinemachineCamera>();
        var el = c.ending.Lens; el.FieldOfView = 45f; el.FarClipPlane = 120f; c.ending.Lens = el;
        c.ending.Priority.Enabled = true; c.ending.Priority.Value = 0;

        // First person: the camera sits at the eyes and is posed every frame by CameraModeController.
        var fg = new GameObject("CM_FirstPerson");
        c.firstPerson = fg.AddComponent<CinemachineCamera>();
        var fl = c.firstPerson.Lens; fl.FieldOfView = 62f; fl.NearClipPlane = 0.05f; fl.FarClipPlane = 90f; c.firstPerson.Lens = fl;
        c.firstPerson.Priority.Enabled = true; c.firstPerson.Priority.Value = 0;
        var fpNoise = fg.AddComponent<CinemachineBasicMultiChannelPerlin>();
        fpNoise.NoiseProfile = noise.NoiseProfile;
        fpNoise.AmplitudeGain = 0.12f;
        fpNoise.FrequencyGain = 0.6f;

        c.mode = fg.AddComponent<CameraModeController>();
        c.mode.thirdPerson = c.gameplay;
        c.mode.firstPerson = c.firstPerson;
        c.mode.thirdPersonController = c.controller;
        c.mode.player = p.controller;
        c.mode.upperArmR = FindDeep(p.parts.model, "UpperArmR");
        c.mode.forearmR = FindDeep(p.parts.model, "ForearmR");
        c.mode.handR = FindDeep(p.parts.model, "HandR");
        c.mode.lantern = p.parts.lanternRoot;
        c.mode.eyeBlockers = EmberLayers.World;
        var hidden = new List<Renderer>();
        var head = FindDeep(p.parts.model, "Neck");
        if (head) hidden.AddRange(head.GetComponentsInChildren<Renderer>(true));
        var scarf = FindDeep(p.parts.model, "Scarf");
        if (scarf) hidden.AddRange(scarf.GetComponentsInChildren<Renderer>(true));
        c.mode.hideInFirstPerson = hidden.ToArray();

        brain.CustomBlends = BuildBlends();
        return c;
    }

    // Menu -> gameplay is a slow cinematic move, the view toggle is quick, and the ending shot drifts in.
    static CinemachineBlenderSettings BuildBlends()
    {
        const string path = "Assets/Settings/Ember_CameraBlends.asset";
        var asset = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(path);
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            AssetDatabase.CreateAsset(asset, path);
        }
        CinemachineBlenderSettings.CustomBlend B(string from, string to, float seconds) => new CinemachineBlenderSettings.CustomBlend
        {
            From = from, To = to,
            Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, seconds),
        };
        asset.CustomBlends = new[]
        {
            B("CM_Menu", "CM_Gameplay", 1.8f),
            B("CM_Menu", "CM_FirstPerson", 1.8f),
            B("CM_Gameplay", "CM_Menu", 1.2f),
            B("CM_FirstPerson", "CM_Menu", 1.2f),
            B("CM_Gameplay", "CM_FirstPerson", 0.45f),
            B("CM_FirstPerson", "CM_Gameplay", 0.45f),
            B("**ANY CAMERA**", "CM_Ending", 2.5f),
        };
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // ------------------------------------------------------------------ wiring

    static void Wire(PlayerRefs p, SystemRefs s, CameraRefs c, EmberUIBuilder.Refs ui, EmberLevelBuilder.Result level)
    {
        s.game.player = p.controller;
        s.game.fuel = p.fuel;
        s.game.health = p.health;
        s.game.mission = s.mission;
        s.game.prayer = p.prayer;
        s.game.menuCamera = c.menu;
        s.game.gameplayCamera = c.gameplay;

        s.audio.fuel = p.fuel;
        s.audio.health = p.health;
        s.audio.vampires = s.spawner;

        s.spawner.player = p.go.transform;
        s.spawner.playerHealth = p.health;
        s.spawner.fuel = p.fuel;
        s.spawner.lantern = p.lantern;
        s.spawner.prayer = p.prayer;
        s.spawner.mission = s.mission;
        s.spawner.safeZoneCentre = level.radioCentreRoot;

        s.postFx.fuel = p.fuel;
        s.postFx.health = p.health;
        s.postFx.prayer = p.prayer;

        var rc = level.radioCentre;
        rc.mission = s.mission;
        rc.spawner = s.spawner;
        rc.playerHealth = p.health;
        rc.moonLight = level.moon;
        rc.endingCamera = c.ending;

        level.locket.prayer = p.prayer;

        var hud = ui.hud;
        hud.fuel = p.fuel;
        hud.mission = s.mission;
        hud.prayer = p.prayer;
        hud.interactor = p.interactor;
        hud.health = p.health;
        hud.sword = p.sword;
        hud.radioCentre = rc;
        hud.player = p.go.transform;

        ui.touch.interactor = p.interactor;
        ui.touch.prayer = p.prayer;
        ui.touch.controller = p.controller;

        foreach (var comp in new Object[] { s.game, s.audio, s.spawner, s.postFx, rc, level.locket, hud, ui.touch, ui.menu, c.controller, p.lantern, p.prayer, p.health, p.animator })
            EditorUtility.SetDirty(comp);
    }

    // ------------------------------------------------------------------ project settings

    static void ConfigureProject()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!rp) continue;
            bool mobile = path.Contains("Mobile");
            rp.shadowDistance = mobile ? 25f : 35f;
            rp.maxAdditionalLightsCount = 4;
            rp.supportsHDR = true;
            // The lantern casts shadows on PC; on phones it doesn't (the biggest single saving).
            var so = new SerializedObject(rp);
            var addShadows = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (addShadows != null) { addShadows.boolValue = !mobile; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(rp);
        }

        ConfigureCollisionMatrix();

        PlayerSettings.productName = "EMBER";
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ layers

    public static void EnsureLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        foreach (var (index, name) in EmberLayers.Slots)
        {
            var slot = layers.GetArrayElementAtIndex(index);
            if (slot.stringValue != name) slot.stringValue = name;
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    // Only pairs that need physical contact collide. Hitboxes are found by explicit queries, never by the physics step.
    static void ConfigureCollisionMatrix()
    {
        int L(string n) => LayerMask.NameToLayer(n);
        int player = L(EmberLayers.Player), enemy = L(EmberLayers.Enemy), inter = L(EmberLayers.Interactable),
            enemyHit = L(EmberLayers.EnemyHitbox), playerHit = L(EmberLayers.PlayerHitbox), env = L(EmberLayers.Environment), prop = L(EmberLayers.DynamicProp);
        for (int i = 0; i < 32; i++)
        {
            Physics.IgnoreLayerCollision(enemyHit, i, true);
            Physics.IgnoreLayerCollision(playerHit, i, true);
            // Loose debris (sword fragments) only rests on the world.
            Physics.IgnoreLayerCollision(prop, i, !(i == 0 || i == env || i == prop));
        }
        Physics.IgnoreLayerCollision(enemy, enemy, true);    // vampires separate with NavMesh avoidance, not physics
        Physics.IgnoreLayerCollision(enemy, inter, true);
        Physics.IgnoreLayerCollision(inter, inter, true);
        Physics.IgnoreLayerCollision(inter, env, true);
        Physics.IgnoreLayerCollision(player, enemy, false);  // bodies block each other: no walking through a vampire
        Physics.IgnoreLayerCollision(player, inter, false);  // pickup triggers
        Physics.IgnoreLayerCollision(player, env, false);
        Physics.IgnoreLayerCollision(enemy, env, false);
    }
}
