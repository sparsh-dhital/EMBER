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
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Prototype pieces that the new systems replace (the original P1_MovementTest scene is untouched).
        foreach (var n in new[] { "Plane", "Pickups", "HUD_Canvas", "GameSystems", "CM_Gameplay", "CM_Menu", "CM_Ending" })
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
        public EmberCharacterBuilder.PlayerParts parts;
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

        p.fuel = Get<LanternFuel>(go);
        p.fuel.maxFuel = 100f;
        p.fuel.drainPerSecond = 0.8f;
        p.controller = Get<PlayerController>(go);
        p.controller.groundLayers = ~((1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Interactable")));
        p.health = Get<PlayerHealth>(go);
        p.interactor = Get<PlayerInteractor>(go);
        p.interactor.interactableLayers = 1 << LayerMask.NameToLayer("Interactable");
        p.animator = Get<PlayerAnimator>(go);
        p.prayer = Get<PrayerSystem>(go);

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

        EmberCharacterBuilder.SetLayerRecursive(go, LayerMask.NameToLayer("Player"));
        return p;
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
        public CinemachineCamera gameplay, menu, ending;
        public CameraController controller;
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
        deocc.CollideAgainst = 1 << 0;
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
        return c;
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
        hud.radioCentre = rc;
        hud.player = p.go.transform;

        ui.touch.interactor = p.interactor;
        ui.touch.prayer = p.prayer;

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

        // Only the player's own collisions matter; vampires and pickups never push each other around.
        int player = LayerMask.NameToLayer("Player"), enemy = LayerMask.NameToLayer("Enemy"), inter = LayerMask.NameToLayer("Interactable");
        Physics.IgnoreLayerCollision(enemy, enemy, true);
        Physics.IgnoreLayerCollision(enemy, inter, true);
        Physics.IgnoreLayerCollision(inter, inter, true);
        Physics.IgnoreLayerCollision(player, enemy, true);
        Physics.IgnoreLayerCollision(player, inter, false);

        PlayerSettings.productName = "EMBER";
        AssetDatabase.SaveAssets();
    }
}
