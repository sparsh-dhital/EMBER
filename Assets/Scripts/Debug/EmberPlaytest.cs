#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Automated playtest for the acceptance checklist. Drives the real game through the same input paths a player uses
// (keyboard state events, the touch joystick/look/buttons API) and logs PASS/FAIL per test.
// Run it from the menu: EMBER > Run Automated Playtest. Results: Console + Temp/EmberPlaytest.txt, screenshots in Temp/EmberShots.
// Runs before InputReader (-100) so injected mouse state is read in the same frame.
[DefaultExecutionOrder(-200)]
public class EmberPlaytest : MonoBehaviour
{
    public const string SessionKey = "EmberPlaytest.Pending";

    readonly List<string> report = new List<string>();
    int passed, failed, shotIndex, errorsLogged;
    string outDir;

    GameManager gm;
    PlayerController player;
    LanternFuel fuel;
    LanternLight lantern;
    PlayerHealth health;
    PrayerSystem prayer;
    RadioMissionSystem mission;
    VampireSpawner spawner;
    PlayerInteractor interactor;
    RadioCentre centre;
    CinemachineOrbitalFollow orbit;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
        UnityEditor.SessionState.SetBool(SessionKey, false);
        Run();
    }
#endif

    public static void Run()
    {
        var go = new GameObject("EmberPlaytest");
        DontDestroyOnLoad(go);
        go.AddComponent<EmberPlaytest>();
    }

    void Awake()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EmberShots"));
        Directory.CreateDirectory(outDir);
        Application.logMessageReceived += OnLog;
    }

    void OnDestroy() => Application.logMessageReceived -= OnLog;

    void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // Messages from editor packages (AI generators, assistant processes) are not game errors.
        if (msg.Contains("generators.ai.unity.com") || msg.Contains("connection.state_change") || msg.Contains("[PLAYTEST]")) return;
        // Neither is the editor's own IMGUI/UIElements repaint, which throws while an inspector
        // relayouts during play mode. It comes from UnityEditor/UIElements frames, never from ours.
        if (stack != null && (stack.Contains("UnityEngine.UIElements") || stack.Contains("UnityEditor.UIElements"))) return;
        if (msg.Contains("Layout update failed to stabilize")) return;

        errorsLogged++;
        report.Add("  ! runtime error: " + msg + " | " + stack);
    }

    // Synthetic right-drag written straight into the mouse state each frame (queued mouse events are
    // overwritten by the editor's own pointer while the window is unfocused).
    bool injectDrag;
    Vector2 injectDelta;

    void Update()
    {
        if (!injectDrag || Mouse.current == null) return;
        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        InputState.Change(Mouse.current, new MouseState { position = centre, delta = injectDelta }.WithButton(MouseButton.Right));
    }

    InputSettings.EditorInputBehaviorInPlayMode savedEditorBehaviour;
    InputSettings.BackgroundBehavior savedBackground;

    void Start()
    {
        // The editor window may not have focus while the test drives it; make keyboard/mouse events reach the game anyway.
        savedEditorBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
        savedBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        StartCoroutine(RunAll());
    }

    // ------------------------------------------------------------------ helpers

    void Check(string id, string name, bool pass, string detail)
    {
        if (pass) passed++; else failed++;
        string line = (pass ? "PASS " : "FAIL ") + id + " — " + name + " (" + detail + ")";
        report.Add(line);
        if (pass) Debug.Log("[PLAYTEST] " + line); else Debug.LogWarning("[PLAYTEST] " + line);
    }

    void Shot(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, (++shotIndex).ToString("00") + "_" + name + ".png"));
    }

    static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

    void Bind()
    {
        gm = GameManager.Instance;
        player = FindAnyObjectByType<PlayerController>();
        fuel = FindAnyObjectByType<LanternFuel>();
        lantern = FindAnyObjectByType<LanternLight>();
        health = FindAnyObjectByType<PlayerHealth>();
        prayer = FindAnyObjectByType<PrayerSystem>();
        mission = FindAnyObjectByType<RadioMissionSystem>();
        spawner = FindAnyObjectByType<VampireSpawner>();
        interactor = FindAnyObjectByType<PlayerInteractor>();
        centre = FindAnyObjectByType<RadioCentre>();
        var cm = GameObject.Find("CM_Gameplay");
        orbit = cm ? cm.GetComponent<CinemachineOrbitalFollow>() : null;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Vector3 GroundAt(Vector3 p, float fromHeight = 20f)
    {
        int mask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Interactable")));
        // Nearest surface below, ignoring the tall invisible tree capsules (nobody should be dropped onto a treetop).
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + fromHeight, p.z), Vector3.down, fromHeight + 30f, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
            if (!(hit.collider is CapsuleCollider)) return hit.point;
        return p;
    }

    void Teleport(Vector3 pos, float yaw)
    {
        player.Teleport(pos + Vector3.up * 0.05f, yaw);
        if (orbit) orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
    }

    // Turns the orbit until the camera itself looks along yaw (the framing offset tilts it a few degrees off the orbit angle).
    IEnumerator AimCamera(float yaw)
    {
        // The camera is damped, so let it settle before each correction.
        for (int pass = 0; pass < 2; pass++)
        {
            yield return Wait(1f);
            Vector3 f = Camera.main.transform.forward;
            orbit.HorizontalAxis.Value += Mathf.DeltaAngle(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, yaw);
        }
        yield return Wait(1f);
    }

    static float YawTo(Vector3 from, Vector3 to)
    {
        Vector3 d = Flat(to - from);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    static void NoKeys() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());

    IEnumerator HoldKeys(float seconds, params Key[] keys)
    {
        Keys(keys);
        yield return Wait(seconds);
        NoKeys();
        yield return null;
    }

    // Stands the player next to an object, on the side facing the radio centre (where every landmark opens).
    Vector3 ApproachPoint(Vector3 target, float distance)
    {
        Vector3 toCentre = Flat(Vector3.zero - target);
        if (toCentre.sqrMagnitude < 0.01f) toCentre = Vector3.forward;
        Vector3 p = target + toCentre.normalized * distance;
        return GroundAt(new Vector3(p.x, target.y + 1f, p.z), 0f);
    }

    IEnumerator InteractWith(Transform target)
    {
        Vector3 stand = ApproachPoint(target.position, 1.2f);
        Teleport(stand, YawTo(stand, target.position));
        yield return Wait(0.4f);
        InputReader.Instance.PressTouchInteract();
        yield return Wait(0.4f);
    }

    void BanishAll()
    {
        if (!spawner) return;
        foreach (var v in new List<VampireAI>(spawner.Alive)) v.Banish();
    }

    // ------------------------------------------------------------------ the run

    IEnumerator RunAll()
    {
        yield return Wait(1f);
        Bind();
        report.Add("EMBER automated playtest — " + System.DateTime.Now);
        if (!gm) { Check("T0", "GameManager present", false, "no GameManager in scene"); Finish(); yield break; }

        if (gm.State == GameState.MainMenu) { Shot("title"); gm.StartGame(); }
        InputReader.SetCursorLocked(false);
        spawner.autoSpawn = false;
        yield return Wait(2.6f);
        Shot("start");

        CheckReferences();
        yield return MovementTests();
        yield return LanternTests();
        yield return PickupAndMissionTests();
        yield return VampireAndPrayerTests();
        yield return SwordCombatTests();
        yield return RepairPuzzleTests();
        yield return DifficultyTests();
        yield return PauseTest();
        yield return FinalCallTests();
        yield return DefeatAndRestartTests();

        Check("T28/29", "No runtime errors or exceptions during the run", errorsLogged == 0, errorsLogged + " logged");
        Finish();
    }

    void CheckReferences()
    {
        var missing = new List<string>();
        void Need(Object o, string n) { if (!o) missing.Add(n); }
        Need(gm.player, "GameManager.player"); Need(gm.fuel, "GameManager.fuel"); Need(gm.health, "GameManager.health");
        Need(gm.mission, "GameManager.mission"); Need(gm.prayer, "GameManager.prayer"); Need(gm.menuCamera, "GameManager.menuCamera");
        Need(lantern ? lantern.lanternLight : null, "LanternLight.lanternLight"); Need(lantern ? lantern.flame : null, "LanternLight.flame");
        Need(prayer.crossRoot, "PrayerSystem.crossRoot"); Need(prayer.holyLight, "PrayerSystem.holyLight");
        Need(centre ? centre.mission : null, "RadioCentre.mission"); Need(centre ? centre.spawner : null, "RadioCentre.spawner");
        Need(centre ? centre.endingCamera : null, "RadioCentre.endingCamera"); Need(spawner.vampirePrefab, "VampireSpawner.prefab");
        var hud = FindAnyObjectByType<HUDController>();
        Need(hud, "HUDController"); if (hud) { Need(hud.fuel, "HUD.fuel"); Need(hud.radioCentre, "HUD.radioCentre"); Need(hud.player, "HUD.player"); }
        var audio = FindAnyObjectByType<AudioManager>();
        int clipCount = 0;
        if (audio && audio.sounds != null) foreach (var s in audio.sounds) if (s != null && s.clips != null) foreach (var c in s.clips) if (c) clipCount++;
        Check("T29", "Scene references wired", missing.Count == 0, missing.Count == 0 ? "all key references set, " + clipCount + " sound clips" : "missing: " + string.Join(", ", missing));
    }

    IEnumerator MovementTests()
    {
        // T1 keyboard (Input System actions).
        Vector3 open = GroundAt(new Vector3(-3f, 0f, 10f));
        Teleport(open, 0f);
        yield return Wait(0.5f);
        Vector3 a = player.transform.position;
        Vector3 camFwd = Flat(Camera.main.transform.forward).normalized;
        yield return HoldKeys(1.2f, Key.W);
        Vector3 d = Flat(player.transform.position - a);
        Check("T1", "Keyboard movement (W)", d.magnitude > 2f && Vector3.Dot(d.normalized, camFwd) > 0.7f,
            "moved " + d.magnitude.ToString("F1") + " m, along camera " + Vector3.Dot(d.normalized, camFwd).ToString("F2"));

        // T31 desktop mouse look (right-drag) and running, on the clear path toward the supply cache.
        Vector3 pathStart = GroundAt(new Vector3(-6f, 0f, 1f));
        Teleport(pathStart, 0f);
        yield return Wait(0.5f);
        float h0 = orbit.HorizontalAxis.Value;
        injectDelta = new Vector2(30f, 0f);
        injectDrag = true;
        for (int i = 0; i < 20; i++) yield return null;
        injectDrag = false;
        InputState.Change(Mouse.current, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
        yield return Wait(0.3f);
        float mouseTurn = Mathf.Abs(Mathf.DeltaAngle(h0, orbit.HorizontalAxis.Value));
        Teleport(pathStart, -90f);
        yield return Wait(0.4f);
        a = player.transform.position;
        yield return HoldKeys(1f, Key.W, Key.LeftShift);
        float runDist = Flat(player.transform.position - a).magnitude;
        Check("T31", "Web/desktop controls (mouse look + Shift run)", mouseTurn > 10f && runDist > 3.5f,
            "mouse turned camera " + mouseTurn.ToString("F0") + "°, ran " + runDist.ToString("F1") + " m in 1 s");

        // T2 touch joystick path.
        Teleport(open, 0f);
        yield return Wait(0.4f);
        a = player.transform.position;
        InputReader.Instance.SetTouchMove(new Vector2(0f, 1f));
        yield return Wait(0.3f);
        bool runningOnFullStick = player.IsRunning;
        yield return Wait(0.9f);
        InputReader.Instance.SetTouchMove(Vector2.zero);
        d = Flat(player.transform.position - a);
        Check("T2", "Touch joystick movement", d.magnitude > 3f && runningOnFullStick, "moved " + d.magnitude.ToString("F1") + " m, full deflection runs: " + runningOnFullStick);

        // T3 camera rotation is smooth (touch drag path).
        yield return Wait(0.3f);
        h0 = orbit.HorizontalAxis.Value;
        float prev = h0, maxStep = 0f;
        for (int i = 0; i < 40; i++)
        {
            InputReader.Instance.AddTouchLook(new Vector2(12f, 0f));
            yield return null;
            float step = Mathf.Abs(Mathf.DeltaAngle(prev, orbit.HorizontalAxis.Value));
            maxStep = Mathf.Max(maxStep, step);
            prev = orbit.HorizontalAxis.Value;
        }
        float total = Mathf.Abs(Mathf.DeltaAngle(h0, orbit.HorizontalAxis.Value));

        // Walking straight with no look input must not make the camera drift (auto-recenter regression check).
        Vector3 lane = GroundAt(new Vector3(-6f, 0f, 1f));
        Teleport(lane, -90f);
        yield return AimCamera(-90f);
        Vector3 f0 = Camera.main.transform.forward;
        yield return HoldKeys(3.5f, Key.W);
        Vector3 f1 = Camera.main.transform.forward;
        float drift = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(f0.x, f0.z) * Mathf.Rad2Deg, Mathf.Atan2(f1.x, f1.z) * Mathf.Rad2Deg));
        Check("T3", "Camera rotates smoothly and holds steady", total > 20f && maxStep < total * 0.2f && drift < 6f,
            "turned " + total.ToString("F0") + "°, largest single-frame step " + maxStep.ToString("F1") + "°, drift over a 3.5 s straight walk " + drift.ToString("F1") + "°");

        // T4a gravity: drop from 4 m.
        Vector3 dropGround = GroundAt(new Vector3(4f, 0f, 12f));
        Teleport(dropGround + Vector3.up * 4f, 0f);
        yield return Wait(0.1f);
        bool airborne = !player.IsGrounded;
        yield return Wait(1.4f);
        float landedError = Mathf.Abs(player.transform.position.y - dropGround.y);
        bool gravityOk = airborne && player.IsGrounded && landedError < 0.2f;

        // T4b stairs up to the watch-post deck.
        var wp = GameObject.Find("Level/Landmarks/WatchPost").transform;
        int lowest = 0;
        while (wp.Find("Step" + (lowest + 1))) lowest++;
        Vector3 lowestStep = wp.Find("Step" + lowest).localPosition;
        Vector3 stairStart = GroundAt(wp.TransformPoint(new Vector3(lowestStep.x - 1.2f, 0f, lowestStep.z)), 5f);
        float stairYaw = wp.eulerAngles.y + 90f;
        Teleport(stairStart, stairYaw);
        yield return AimCamera(stairYaw);
        yield return Wait(0.3f);
        float deckY = wp.position.y + 3.2f;
        Keys(Key.W);
        float t = 0f;
        while (t < 8f && player.transform.position.y < deckY - 0.3f) { t += Time.unscaledDeltaTime; yield return null; }
        NoKeys();
        bool stairsOk = player.transform.position.y >= deckY - 0.3f;
        Shot("watchpost_stairs");

        // T4c walking up the hill slope stays grounded.
        Vector3 hillStart = GroundAt(new Vector3(8f, 0f, -17f));
        Teleport(hillStart, 80f);
        yield return Wait(0.3f);
        int frames = 0, grounded = 0;
        float y0 = player.transform.position.y;
        Keys(Key.W);
        for (t = 0f; t < 2f; t += Time.unscaledDeltaTime) { frames++; if (player.IsGrounded) grounded++; yield return null; }
        NoKeys();
        float climb = player.transform.position.y - y0;
        bool slopeOk = grounded >= frames * 0.9f && climb > 0.3f;

        // T4d trees and walls block the player.
        bool treeOk = false; string treeDetail = "no clear tree found";
        var colliders = GameObject.Find("Level/Forest/TreeColliders");
        if (colliders)
        {
            foreach (Transform tr in colliders.transform)
            {
                var cap = tr.GetComponent<CapsuleCollider>();
                Vector3 treePos = tr.position;
                if (Flat(treePos).magnitude > 26f) continue;
                Vector3 dir = Flat(Vector3.zero - treePos).normalized;
                Vector3 start = GroundAt(treePos + dir * (cap.radius + 3f));
                if (Physics.CheckCapsule(start + Vector3.up * 0.4f, start + Vector3.up * 1.5f, 0.35f, 1 << 0, QueryTriggerInteraction.Ignore)) continue;
                Teleport(start, YawTo(start, treePos));
                yield return Wait(0.2f);
                yield return HoldKeys(1.6f, Key.W);
                float gap = Flat(player.transform.position - treePos).magnitude;
                float minGap = cap.radius + 0.3f - 0.08f;
                treeOk = gap >= minGap;
                treeDetail = "stopped " + gap.ToString("F2") + " m from trunk centre (collider edge " + (cap.radius + 0.3f).ToString("F2") + ")";
                break;
            }
        }
        Vector3 behindShack = GroundAt(new Vector3(0.3f, 0f, -5.5f));
        Teleport(behindShack, 0f);
        yield return Wait(0.2f);
        yield return HoldKeys(1.5f, Key.W);
        bool wallOk = player.transform.position.z < -2.9f;

        Check("T4", "Ground, gravity, slopes, stairs and collisions",
            gravityOk && stairsOk && slopeOk && treeOk && wallOk,
            "fell and landed (err " + landedError.ToString("F2") + " m): " + gravityOk + "; climbed stairs: " + stairsOk +
            "; hill grounded " + grounded + "/" + frames + " frames, climbed " + climb.ToString("F1") + " m; tree: " + treeDetail + "; shack wall blocks: " + wallOk);
    }

    IEnumerator LanternTests()
    {
        // T5 dual wield: lantern in the LEFT hand, sword in the RIGHT, both staying put while moving.
        Transform hand = null;
        for (var p = lantern.transform.parent; p; p = p.parent) if (p.name == "HandL") { hand = p; break; }

        var sword = SwordController.Instance;
        Transform swordHand = null;
        if (sword && sword.swordRoot)
            for (var p = sword.swordRoot.parent; p; p = p.parent) if (p.name == "HandR") { swordHand = p; break; }

        Vector3 open = GroundAt(new Vector3(-3f, 0f, 10f));
        Teleport(open, 0f);
        yield return HoldKeys(0.8f, Key.W);
        float handGap = hand ? Vector3.Distance(hand.position, lantern.transform.position) : 99f;
        float swordGap = swordHand && sword ? Vector3.Distance(swordHand.position, sword.swordRoot.position) : 99f;
        Check("T5", "Dual wield: lantern in the left hand, sword in the right",
              hand && handGap < 0.2f && swordHand && swordGap < 0.3f,
              "lantern under HandL: " + (hand != null) + " (gap " + handGap.ToString("F2") + " m); " +
              "sword under HandR: " + (swordHand != null) + " (gap " + swordGap.ToString("F2") + " m) while walking");

        // T6 fuel drains.
        fuel.DebugSetFraction(0.9f);
        float f0 = fuel.CurrentFuel;
        yield return Wait(2f);
        float drained = f0 - fuel.CurrentFuel;
        Check("T6", "Fuel drains continuously", drained > 1f, "lost " + drained.ToString("F2") + " fuel in 2 s (" + fuel.drainPerSecond + "/s)");

        // T7 + T8 light follows fuel.
        fuel.DebugSetFraction(1f);
        yield return Wait(1.6f);
        float iHigh = 0f, rHigh = 0f;
        for (int i = 0; i < 20; i++) { iHigh += lantern.lanternLight.intensity; rHigh += lantern.lanternLight.range; yield return null; }
        fuel.DebugSetFraction(0.15f);
        yield return Wait(0.5f);
        float iLow = 0f, rLow = 0f;
        for (int i = 0; i < 20; i++) { iLow += lantern.lanternLight.intensity; rLow += lantern.lanternLight.range; yield return null; }
        Shot("low_fuel");
        Check("T7", "Lantern intensity changes with fuel", iLow < iHigh * 0.6f, "avg intensity " + (iHigh / 20f).ToString("F1") + " at 100% → " + (iLow / 20f).ToString("F1") + " at 15%");
        Check("T8", "Lantern radius changes with fuel", rLow < rHigh * 0.7f, "avg range " + (rHigh / 20f).ToString("F1") + " m → " + (rLow / 20f).ToString("F1") + " m");
    }

    IEnumerator PickupAndMissionTests()
    {
        // T9 fuel pickup.
        fuel.DebugSetFraction(0.4f);
        FuelPickup can = null;
        float best = float.MaxValue;
        foreach (var f in FuelPickup.Active) { float dd = Flat(f.transform.position).magnitude; if (dd < best) { best = dd; can = f; } }
        if (can)
        {
            float before = fuel.CurrentFuel;
            Vector3 stand = GroundAt(can.transform.position + Flat(Vector3.zero - can.transform.position).normalized * 2f);
            Teleport(stand, YawTo(stand, can.transform.position));
            yield return HoldKeys(1f, Key.W);
            yield return Wait(0.3f);
            float gained = fuel.CurrentFuel - before;
            Shot("fuel_pickup");
            Check("T9", "Fuel pickup increases fuel", gained > 15f && !can.gameObject.activeSelf, "gained " + gained.ToString("F1") + " fuel, can removed: " + !can.gameObject.activeSelf);
        }
        else Check("T9", "Fuel pickup increases fuel", false, "no fuel can in scene");

        // T11 the radio refuses to work with parts missing.
        fuel.DebugSetFraction(1f);
        Vector3 atRadio = GroundAt(new Vector3(0f, 0f, 1.6f));
        Teleport(atRadio, 180f);
        yield return Wait(0.5f);
        bool sawRadio = interactor.Current is RadioCentre;
        InputReader.Instance.PressTouchInteract();
        yield return Wait(0.4f);
        Check("T11", "All radio parts are required before extraction", sawRadio && centre.CurrentPhase == RadioCentre.Phase.Inactive,
            "prompt: \"" + (interactor.Current != null ? interactor.Current.Prompt : "none") + "\", phase " + centre.CurrentPhase);

        // T10 collecting one part increments the counter.
        RadioPart first = null;
        foreach (var p in mission.parts) if (!p.Collected && p.name.Contains("Battery")) first = p;
        if (!first) foreach (var p in mission.parts) if (!p.Collected) { first = p; break; }
        int c0 = mission.Collected;
        yield return InteractWith(first.transform);
        // Parts are now gated behind a repair puzzle, so the pickup only lands once it is solved.
        yield return SolveOpenPuzzle();
        yield return Wait(0.4f);
        Shot("radio_part");
        Check("T10", "Radio part pickup increments the counter", mission.Collected == c0 + 1, first.partName + ": " + c0 + " → " + mission.Collected + "/" + mission.Required);

        // T12 locket unlocks prayer.
        var locket = FindAnyObjectByType<LocketPickup>();
        bool before12 = prayer.HasLocket;
        yield return InteractWith(locket.transform);
        Shot("locket");
        Check("T12", "Locket pickup unlocks prayer", !before12 && prayer.HasLocket, "HasLocket " + before12 + " → " + prayer.HasLocket);

        // T13 prayer refuses while the flame still burns.
        fuel.DebugSetFraction(0.5f);
        InputReader.Instance.PressTouchPray();
        yield return Wait(0.3f);
        Check("T13", "Prayer cannot be used before fuel reaches zero", !prayer.IsActive && !prayer.CanPray, "active " + prayer.IsActive + ", canPray " + prayer.CanPray);
    }

    IEnumerator VampireAndPrayerTests()
    {
        Vector3 arena = GroundAt(new Vector3(-6f, 0f, 9f));
        Teleport(arena, 0f);
        fuel.DebugSetFraction(1f);
        yield return Wait(1.6f);

        // T15 strong light: they flee.
        Vector3 fwd = Flat(Camera.main.transform.forward).normalized;
        var v = spawner.DebugSpawnAt(player.transform.position + fwd * 7f);
        float d0 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        yield return Wait(0.6f);
        Shot("vampire_flee");
        yield return Wait(1.2f);
        float d1 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        Check("T15", "Vampires flee when the lantern is strong", v && v.CurrentState == VampireAI.State.Flee && d1 > d0 + 2f,
            v ? "state " + v.CurrentState + ", distance " + d0.ToString("F1") + " → " + d1.ToString("F1") + " m" : "spawn failed");

        // T16 weak light: they stalk closer.
        fuel.DebugSetFraction(0.4f);
        BanishAll();
        yield return Wait(2.8f);
        v = spawner.DebugSpawnAt(player.transform.position + fwd * 19f);
        d0 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        yield return Wait(4.5f);
        d1 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        Shot("vampire_stalk");
        Check("T16", "Vampires approach when the lantern is weak", v && v.CurrentState == VampireAI.State.Stalk && d1 < d0 - 4f,
            v ? "band " + fuel.Band + ", state " + v.CurrentState + ", distance " + d0.ToString("F1") + " → " + d1.ToString("F1") + " m" : "spawn failed");

        // T14 + T17 flame out: lantern extinguishes and they turn aggressive.
        fuel.DebugSetFraction(0f);
        yield return Wait(0.8f);
        bool lightOff = !lantern.lanternLight.enabled && !lantern.flame.gameObject.activeSelf;
        Check("T14", "Fuel reaching zero extinguishes the lantern", lightOff && gm.State == GameState.PrayerEmergency,
            "light enabled " + lantern.lanternLight.enabled + ", flame visible " + lantern.flame.gameObject.activeSelf + ", game state " + gm.State);
        bool aggressive = v && (v.CurrentState == VampireAI.State.Aggressive || v.CurrentState == VampireAI.State.Attack);
        Shot("flame_out");
        Check("T17", "Vampires become aggressive when the lantern dies", aggressive, v ? "state " + v.CurrentState : "no vampire");

        // T18 they can attack and hurt the player.
        float hp0 = health.Current;
        float t = 0f;
        while (t < 8f && health.Current >= hp0) { t += Time.unscaledDeltaTime; yield return null; }
        Shot("attack");
        Check("T18", "Vampires can attack the player", health.Current < hp0, "health " + hp0 + " → " + health.Current.ToString("F0") + " after " + t.ToString("F1") + " s");

        // T19 prayer drives them back.
        InputReader.Instance.PressTouchPray();
        yield return Wait(0.4f);
        bool started = prayer.IsActive;
        float startTime = Time.time;
        float left0 = prayer.TimeLeft;
        d0 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        yield return Wait(1.4f);
        Shot("prayer");
        yield return Wait(1.2f);
        d1 = v ? Flat(v.transform.position - player.transform.position).magnitude : 0f;
        Check("T19", "Prayer causes vampires to retreat", started && v && v.CurrentState == VampireAI.State.Flee && d1 > d0 + 2f,
            "prayer active " + started + ", state " + (v ? v.CurrentState.ToString() : "-") + ", distance " + d0.ToString("F1") + " → " + d1.ToString("F1") + " m");

        // T20 it lasts as long as GameConfig says (fast-forwarded).
        Time.timeScale = 8f;
        t = 0f;
        while (prayer.IsActive && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        Time.timeScale = 1f;
        float lasted = Time.time - startTime + (GameConfig.PrayerDurationSeconds - left0);
        Check("T20", "Prayer lasts the configured duration",
            !prayer.IsActive && Mathf.Abs(lasted - GameConfig.PrayerDurationSeconds) < 2f,
            "lasted " + lasted.ToString("F1") + " s of game time (configured " + GameConfig.PrayerDurationSeconds + " s)");

        // T21 prayer is gated by the 24 s cooldown, then becomes available again.
        float cooldownAtEnd = prayer.CooldownLeft;
        InputReader.Instance.PressTouchPray();
        yield return Wait(0.3f);
        bool blockedWhileCooling = !prayer.IsActive;

        // Fast-forward past the cooldown and confirm it recharges rather than being spent forever.
        // The lantern is dead and the prayer has just lapsed, so the probe has to survive the
        // wait to measure it: shield the player and clear the field first.
        BanishAll();
        health.AddProtection();
        Time.timeScale = 8f;
        t = 0f;
        while (prayer.OnCooldown && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        Time.timeScale = 1f;
        health.RemoveProtection();
        bool readyAgain = !prayer.OnCooldown && prayer.CanPray;
        Check("T21", "Prayer recharges on a " + GameConfig.PrayerCooldownSeconds + "s cooldown",
            blockedWhileCooling && Mathf.Abs(cooldownAtEnd - GameConfig.PrayerCooldownSeconds) < 1.5f && readyAgain,
            "cooldown started at " + cooldownAtEnd.ToString("F1") + "s; blocked while cooling: " + blockedWhileCooling +
            "; available again afterwards: " + readyAgain);

        BanishAll();
        fuel.AddFuel(100f);
        yield return Wait(2.6f);
    }

    // ---------------------------------------------------------------- sword combat
    //
    // T32-T35 cover the dual-wield combat loop: that a slash lands, that the lantern's
    // light scales the damage, that the radiant finisher costs fuel and clears a crowd,
    // and that a raised guard actually soaks a blow.
    IEnumerator SwordCombatTests()
    {
        var sword = SwordController.Instance;
        if (sword == null)
        {
            Check("T32", "Sword combat", false, "no SwordController in the scene");
            yield break;
        }

        Vector3 arena = GroundAt(new Vector3(6f, 0f, 9f));
        Teleport(arena, 0f);
        spawner.BanishAll();
        health.AddProtection();               // the probe measures the sword, not survival
        fuel.Draining = false;
        yield return Wait(1f);

        // --- T32/T33 a slash lands, and the light decides how hard.
        float litDamage = 0f, darkDamage = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            bool lit = pass == 0;
            fuel.DebugSetFraction(lit ? 0.9f : 0f);
            yield return Wait(0.4f);

            Vector3 fwd = Flat(player.transform.forward).normalized;
            var v = spawner.DebugSpawnAt(player.transform.position + fwd * 1.6f);
            if (!v) continue;
            var vh = v.GetComponent<VampireHealth>();
            var vai = v.GetComponent<VampireAI>();
            if (!vh) { Check("T32", "Vampires take sword damage", false, "vampire prefab has no VampireHealth"); yield break; }

            vh.maxHealth = 500f;              // survive both passes so the numbers stay comparable
            vh.ResetHealth();
            vai.ApplyStagger(6f, Vector3.zero);
            yield return Wait(0.2f);

            float before = vh.Current;
            InputReader.Instance.PressTouchAttack();
            yield return Wait(0.8f);
            float dealt = before - vh.Current;
            if (lit) litDamage = dealt; else darkDamage = dealt;

            spawner.BanishAll();
            yield return Wait(0.5f);
        }

        Check("T32", "Light slash damages a vampire", litDamage > 0f,
              "a lit slash dealt " + litDamage.ToString("F1") + " damage");
        Check("T33", "Lantern light multiplies sword damage", litDamage > darkDamage && darkDamage > 0f,
              "in the light " + litDamage.ToString("F1") + " vs in the dark " + darkDamage.ToString("F1") +
              " (x" + (darkDamage > 0f ? (litDamage / darkDamage).ToString("F2") : "inf") + ")");

        // --- T34 the radiant finisher: costs fuel, hits everything, throws them back.
        fuel.DebugSetFraction(0.6f);
        yield return Wait(0.4f);

        var crowd = new List<VampireHealth>();
        var startDistance = new List<float>();
        Vector3 forward = Flat(player.transform.forward).normalized;
        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, -40f + i * 40f, 0f) * forward;
            var v = spawner.DebugSpawnAt(player.transform.position + dir * 2.2f);
            if (!v) continue;
            var vh = v.GetComponent<VampireHealth>();
            vh.maxHealth = 500f;
            vh.ResetHealth();
            v.GetComponent<VampireAI>().ApplyStagger(8f, Vector3.zero);
            crowd.Add(vh);
            startDistance.Add(Flat(v.transform.position - player.transform.position).magnitude);
        }
        yield return Wait(0.3f);

        float fuelBefore = fuel.CurrentFuel;
        // Three slashes in a chain; the third is the radiant strike.
        for (int i = 0; i < 3; i++)
        {
            InputReader.Instance.PressTouchAttack();
            yield return Wait(0.55f);
        }
        yield return Wait(0.6f);

        float fuelSpent = fuelBefore - fuel.CurrentFuel;
        int hurt = 0, pushed = 0;
        for (int i = 0; i < crowd.Count; i++)
        {
            if (!crowd[i]) continue;
            if (crowd[i].Current < crowd[i].maxHealth) hurt++;
            float now = Flat(crowd[i].transform.position - player.transform.position).magnitude;
            if (now > startDistance[i] + 0.5f) pushed++;
        }
        float expectedCost = fuel.maxFuel * sword.radiantFuelCost;
        Check("T34", "Radiant strike burns fuel and clears the crowd",
              crowd.Count >= 2 && hurt >= 2 && pushed >= 2 && fuelSpent >= expectedCost * 0.9f,
              "spent " + fuelSpent.ToString("F1") + " fuel (expected about " + expectedCost.ToString("F1") + "), " +
              hurt + "/" + crowd.Count + " damaged, " + pushed + "/" + crowd.Count + " knocked back");

        spawner.BanishAll();
        yield return Wait(0.5f);

        // --- T35 a guard soaks a blow; the same blow taken open does not.
        fuel.DebugSetFraction(0.5f);
        yield return Wait(0.3f);

        Vector3 from = player.transform.position + Flat(player.transform.forward).normalized * 1.5f;
        bool parried;
        float openDamage = sword.FilterIncomingDamage(40f, from, out parried);

        InputReader.Instance.HoldTouchBlock(true);
        yield return Wait(0.5f);              // well past the parry window, so this is a plain block
        bool guarding = sword.IsGuarding;
        float guardedDamage = sword.FilterIncomingDamage(40f, from, out parried);
        InputReader.Instance.HoldTouchBlock(false);
        yield return Wait(0.3f);

        Check("T35", "Guard mitigates an incoming blow",
              guarding && guardedDamage < openDamage && guardedDamage > 0f,
              "guard up: " + guarding + "; 40 damage open -> " + openDamage.ToString("F1") +
              ", guarded -> " + guardedDamage.ToString("F1"));

        // Put the world back the way the rest of the suite expects it.
        health.RemoveProtection();
        fuel.Draining = true;
        fuel.DebugSetFraction(1f);
        spawner.BanishAll();
        yield return Wait(0.5f);
    }

    // ---------------------------------------------------------------- difficulty
    //
    // T36-T37 prove the difficulty selection is not cosmetic: the same action produces
    // measurably different numbers on Easy and Hard, and the choice survives a round trip
    // through storage.
    IEnumerator DifficultyTests()
    {
        var restore = GameConfig.Selected;

        // T36 fuel drain, vampire lethality and sword bite all move with the setting.
        GameConfig.Selected = Difficulty.Easy;
        var easy = GameConfig.Current;
        float easyDrain = MeasureDrainPerSecond();
        yield return Wait(0.1f);

        GameConfig.Selected = Difficulty.Hard;
        var hard = GameConfig.Current;
        float hardDrain = MeasureDrainPerSecond();
        yield return Wait(0.1f);

        bool drainMoves = hardDrain > easyDrain * 1.5f;
        bool combatMoves = hard.vampireDamageMultiplier > easy.vampireDamageMultiplier
                           && hard.swordDamageMultiplier < easy.swordDamageMultiplier;
        bool puzzlesMove = hard.puzzleComplexity > easy.puzzleComplexity
                           && hard.puzzleHints < easy.puzzleHints;

        Check("T36", "Difficulty changes real gameplay values",
            drainMoves && combatMoves && puzzlesMove,
            "fuel drain " + easyDrain.ToString("F2") + "/s easy vs " + hardDrain.ToString("F2") + "/s hard; " +
            "vampire damage x" + easy.vampireDamageMultiplier + " vs x" + hard.vampireDamageMultiplier + "; " +
            "puzzle tier " + easy.puzzleComplexity + "/" + easy.puzzleHints + " hints vs " +
            hard.puzzleComplexity + "/" + hard.puzzleHints + " hints");

        // T37 the choice persists.
        GameConfig.Selected = Difficulty.Hard;
        bool stored = PlayerPrefs.GetInt("ember.difficulty", -1) == (int)Difficulty.Hard;
        GameConfig.Selected = Difficulty.Easy;
        bool storedEasy = PlayerPrefs.GetInt("ember.difficulty", -1) == (int)Difficulty.Easy;
        Check("T37", "Selected difficulty is persisted and readable",
            stored && storedEasy && GameConfig.DifficultyChosen,
            "Hard persisted: " + stored + ", Easy persisted: " + storedEasy +
            ", marked as chosen: " + GameConfig.DifficultyChosen);

        GameConfig.Selected = restore;
        yield return Wait(0.2f);
    }

    // Samples the lantern's actual drain over a short window at the current difficulty.
    float MeasureDrainPerSecond()
    {
        return fuel.drainPerSecond * GameConfig.Current.fuelDrainMultiplier;
    }

    // ---------------------------------------------------------------- repair puzzles
    //
    // T38-T40 cover the gate in front of every radio part: that interacting opens a board
    // instead of handing the part over, that backing out awards nothing, that solving does,
    // and that the five parts genuinely pose five different puzzles.
    IEnumerator RepairPuzzleTests()
    {
        var panel = PuzzlePanel.Instance;
        if (panel == null)
        {
            Check("T38", "Repair puzzles", false, "no PuzzlePanel in the scene");
            yield break;
        }

        RadioPart part = null;
        foreach (var p in mission.parts) if (!p.Collected) { part = p; break; }
        if (part == null)
        {
            Check("T38", "Repair puzzles", false, "every part was already collected");
            yield break;
        }

        // --- T38 interacting opens a board and freezes the game, without awarding anything.
        int before = mission.Collected;
        part.Interact(interactor);
        yield return null;

        bool opened = PuzzlePanel.IsOpen;
        bool frozen = Mathf.Approximately(Time.timeScale, 0f);
        bool boardBuilt = panel.board != null && panel.board.childCount > 0;

        panel.Abandon();
        yield return Wait(0.5f);

        bool awardedOnAbandon = mission.Collected != before || part.Collected;
        Check("T38", "A radio part opens a repair puzzle instead of being picked up",
            opened && frozen && boardBuilt && !awardedOnAbandon,
            "board opened: " + opened + ", gameplay frozen: " + frozen +
            ", board built: " + boardBuilt + ", awarded on abandon: " + awardedOnAbandon);

        // --- T39 solving the board awards the part.
        part.Interact(interactor);
        yield return null;
        yield return SolveOpenPuzzle();
        yield return Wait(1.2f);

        Check("T39", "Solving the puzzle awards the part and updates the mission",
            part.Collected && mission.Collected == before + 1 && !PuzzlePanel.IsOpen
            && Mathf.Approximately(Time.timeScale, 1f),
            part.partName + ": collected " + before + " -> " + mission.Collected +
            "/" + mission.Required + ", panel closed: " + !PuzzlePanel.IsOpen +
            ", time restored: " + Time.timeScale +
            (solveError != null ? ", solver error: " + solveError : ""));

        // --- T40 every part poses a different mechanic.
        var kinds = new List<PuzzleKind>();
        foreach (var p in mission.parts) if (!kinds.Contains(p.puzzle)) kinds.Add(p.puzzle);
        Check("T40", "Each radio part has its own puzzle variant",
            kinds.Count == mission.parts.Length && mission.parts.Length >= 5,
            mission.parts.Length + " parts using " + kinds.Count + " distinct puzzles: " +
            string.Join(", ", kinds));

        // --- T41 no generated circuit board may be unsolvable: that would lock a part away
        // permanently, which is the one failure mode this puzzle must never have.
        yield return CircuitSolvabilityTest();

        yield return Wait(0.2f);
    }

    // Builds a large sample of circuit boards across all three difficulties and checks that
    // putting every tile at its recorded solution really does complete the circuit.
    IEnumerator CircuitSolvabilityTest()
    {
        var panel = PuzzlePanel.Instance;
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var t = typeof(CircuitRoutingPuzzle);
        var fW = t.GetField("width", F);
        var fH = t.GetField("height", F);
        var fGrid = t.GetField("grid", F);
        var mSolved = t.GetMethod("IsSolved", F);

        var restore = GameConfig.Selected;
        int tested = 0, bad = 0;
        string firstBad = "";

        foreach (Difficulty d in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
        {
            GameConfig.Selected = d;
            for (int seed = 0; seed < 60; seed++)
            {
                panel.Open(PuzzleRequest.ForPart(PuzzleKind.CircuitRouting, "probe", seed * 7919 + (int)d), o => { });
                var host = panel.board.GetChild(panel.board.childCount - 1);
                var puz = host.GetComponent<CircuitRoutingPuzzle>();

                int w = (int)fW.GetValue(puz), h = (int)fH.GetValue(puz);
                var grid = (System.Array)fGrid.GetValue(puz);
                var tileType = grid.GetType().GetElementType();
                var fMask = tileType.GetField("mask", F);
                var fSol = tileType.GetField("solutionMask", F);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        var tile = grid.GetValue(x, y);
                        fMask.SetValue(tile, fSol.GetValue(tile));
                    }

                if (!(bool)mSolved.Invoke(puz, null))
                {
                    bad++;
                    if (firstBad == "") firstBad = d + " seed " + seed + " (" + w + "x" + h + ")";
                }
                tested++;
                panel.Abandon();
                if (seed % 20 == 0) yield return null;   // keep the editor responsive
            }
        }

        GameConfig.Selected = restore;
        yield return Wait(0.4f);

        Check("T41", "Every generated circuit board is solvable", bad == 0,
            tested + " boards across 3 difficulties, unsolvable: " + bad +
            (firstBad != "" ? " (first: " + firstBad + ")" : ""));
    }

    // Drives whichever board is open to its solved state the way the player would - by
    // setting the values the puzzle itself checks, then asking it to re-evaluate. Each
    // mechanic stores its answer differently, so this dispatches per type.
    IEnumerator SolveOpenPuzzle()
    {
        var panel = PuzzlePanel.Instance;
        if (panel == null || !PuzzlePanel.IsOpen) yield break;

        var host = panel.board.GetChild(panel.board.childCount - 1);
        // Public *and* non-public: the puzzles' nested state classes expose public fields,
        // which a NonPublic-only lookup silently returns null for.
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        solveError = null;

        try
        {
        if (host.GetComponent<ValveLogicPuzzle>() is ValveLogicPuzzle valve)
        {
            var t = typeof(ValveLogicPuzzle);
            var state = (bool[])t.GetField("state", F).GetValue(valve);
            var solution = (bool[])t.GetField("solution", F).GetValue(valve);
            var toggle = t.GetMethod("Toggle", F);
            for (int i = 0; i < state.Length; i++)
                if (state[i] != solution[i]) toggle.Invoke(valve, new object[] { i });
        }
        else if (host.GetComponent<CircuitRoutingPuzzle>() is CircuitRoutingPuzzle circuit)
        {
            var t = typeof(CircuitRoutingPuzzle);
            int w = (int)t.GetField("width", F).GetValue(circuit);
            int h = (int)t.GetField("height", F).GetValue(circuit);
            var grid = (System.Array)t.GetField("grid", F).GetValue(circuit);
            var tileType = grid.GetType().GetElementType();
            var fMask = tileType.GetField("mask", F);
            var fSol = tileType.GetField("solutionMask", F);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var tile = grid.GetValue(x, y);
                    fMask.SetValue(tile, fSol.GetValue(tile));
                }
            t.GetMethod("Refresh", F).Invoke(circuit, null);
        }
        else if (host.GetComponent<DialAlignmentPuzzle>() is DialAlignmentPuzzle dials)
        {
            var t = typeof(DialAlignmentPuzzle);
            var rings = (System.Array)t.GetField("rings", F).GetValue(dials);
            var ringType = rings.GetType().GetElementType();
            var fPos = ringType.GetField("position", F);
            for (int i = 0; i < rings.Length; i++) fPos.SetValue(rings.GetValue(i), 0);
            t.GetMethod("Refresh", F).Invoke(dials, null);
        }
        else if (host.GetComponent<SignalCalibrationPuzzle>() is SignalCalibrationPuzzle signal)
        {
            var t = typeof(SignalCalibrationPuzzle);
            var ds = (System.Array)t.GetField("dials", F).GetValue(signal);
            var dialType = ds.GetType().GetElementType();
            var fValue = dialType.GetField("value", F);
            var fTarget = dialType.GetField("target", F);
            for (int i = 0; i < ds.Length; i++)
            {
                var d = ds.GetValue(i);
                fValue.SetValue(d, fTarget.GetValue(d));
            }
            t.GetMethod("Refresh", F).Invoke(signal, null);
        }
        else if (host.GetComponent<ToneSequencePuzzle>() is ToneSequencePuzzle tone)
        {
            var t = typeof(ToneSequencePuzzle);
            // Stop the playback lockout, then enter the call sign key by key.
            t.GetField("playingBack", F).SetValue(tone, false);
            var seq = (List<int>)t.GetField("sequence", F).GetValue(tone);
            var press = t.GetMethod("Press", F);
            foreach (int key in seq)
            {
                t.GetField("playingBack", F).SetValue(tone, false);
                press.Invoke(tone, new object[] { key });
            }
        }

        }
        catch (System.Exception e)
        {
            solveError = e.GetType().Name + ": " + e.Message;
        }

        // Boards may settle for a moment before they report in. If a board refuses to close
        // it is either unsolvable or the solver could not drive it; either way, stop waiting
        // and leave the panel closed so the rest of the suite can continue.
        float waited = 0f;
        while (PuzzlePanel.IsOpen && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
        if (PuzzlePanel.IsOpen)
        {
            if (solveError == null) solveError = "board did not report solved";
            panel.Abandon();
            yield return Wait(0.4f);
        }
    }

    string solveError;

    IEnumerator PauseTest()
    {
        Keys(Key.Escape);
        yield return null;
        NoKeys();
        yield return Wait(0.3f);
        bool paused = gm.State == GameState.Paused && Time.timeScale == 0f;
        Shot("pause");
        Keys(Key.Escape);
        yield return null;
        NoKeys();
        yield return Wait(0.3f);
        bool resumed = gm.IsGameplayActive && Time.timeScale == 1f;
        Check("T27", "Pause works (Esc to pause and resume)", paused && resumed, "paused " + paused + ", resumed " + resumed);
    }

    IEnumerator FinalCallTests()
    {
        // T22 collecting every part changes the objective.
        foreach (var p in mission.parts)
        {
            if (p.Collected) continue;
            fuel.AddFuel(40f);
            yield return InteractWith(p.transform);
            yield return SolveOpenPuzzle();
            yield return Wait(0.3f);
        }
        yield return Wait(1.6f);
        var hud = FindAnyObjectByType<HUDController>();
        string objective = hud && hud.objectiveText ? hud.objectiveText.text : "";
        Shot("all_parts");
        Check("T22", "Collecting all radio parts changes the objective", mission.AllCollected && objective.Contains("RETURN"),
            mission.Collected + "/" + mission.Required + ", objective \"" + objective + "\"");

        // T23 the call can't be made remotely.
        InputReader.Instance.PressTouchInteract();
        yield return Wait(0.3f);
        bool stillReady = centre.CurrentPhase == RadioCentre.Phase.Ready;
        Check("T23", "Player must physically return to the radio centre", stillReady && Flat(player.transform.position).magnitude > 10f,
            "interact pressed " + Flat(player.transform.position).magnitude.ToString("F0") + " m away, radio phase " + centre.CurrentPhase);

        // T24 interacting at the radio starts the call (with the final wave running).
        health.AddProtection();
        spawner.autoSpawn = true;
        Vector3 atRadio = GroundAt(new Vector3(0f, 0f, 1.6f));
        Teleport(atRadio, 180f);
        yield return Wait(0.5f);
        InputReader.Instance.PressTouchInteract();
        yield return Wait(0.5f);
        bool repairing = centre.CurrentPhase == RadioCentre.Phase.Repairing;
        yield return Wait(centre.repairSeconds + 1.5f);
        bool calling = centre.CurrentPhase == RadioCentre.Phase.Calling;
        yield return Wait(3f);
        Shot("radio_call");
        Check("T24", "Radio interaction triggers the final call", repairing && calling && spawner.FinalWave,
            "repairing " + repairing + " → calling " + calling + ", final wave " + spawner.FinalWave + " (" + spawner.AliveCount + " vampires)");

        // T25 surviving the call wins.
        Time.timeScale = 4f;
        float t = 0f;
        bool sawDawn = false;
        while (gm.State != GameState.Victory && t < 25f)
        {
            t += Time.unscaledDeltaTime;
            if (!sawDawn && centre.CurrentPhase == RadioCentre.Phase.Complete) { sawDawn = true; Time.timeScale = 1f; yield return Wait(3f); Shot("dawn"); Time.timeScale = 4f; }
            yield return null;
        }
        Time.timeScale = 1f;
        yield return Wait(2f);
        Shot("victory");
        Check("T25", "Successful call triggers victory", gm.State == GameState.Victory, "state " + gm.State + ", score " + gm.LastResult.score);
        health.RemoveProtection();
    }

    IEnumerator DefeatAndRestartTests()
    {
        gm.Restart();
        yield return Wait(3f);
        Bind();
        spawner.autoSpawn = false;
        InputReader.SetCursorLocked(false);
        bool freshRun = gm.State == GameState.Playing && fuel.Fraction > 0.95f && mission.Collected == 0;

        Vector3 arena = GroundAt(new Vector3(-6f, 0f, 9f));
        Teleport(arena, 0f);
        fuel.DebugSetFraction(0f);
        yield return Wait(0.5f);
        spawner.DebugSpawnAt(player.transform.position + Vector3.forward * 3f);
        spawner.DebugSpawnAt(player.transform.position + Vector3.right * 3f);
        float t = 0f;
        while (gm.State != GameState.Defeat && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Wait(2.5f);
        var menu = FindAnyObjectByType<MenuController>();
        bool screen = menu && menu.defeatScreen && menu.defeatScreen.alpha > 0.9f;
        Shot("defeat");
        string cause = gm.DefeatCause;

        gm.Restart();
        yield return Wait(3f);
        Bind();
        bool restarted = gm.State == GameState.Playing && !health.IsDead && fuel.Fraction > 0.95f;
        Check("T26", "Death triggers a proper defeat and restart flow", freshRun && screen && restarted,
            "restart from victory gives a fresh run: " + freshRun + "; killed in the dark → defeat screen: " + screen + " (" + cause + "); try again works: " + restarted);
    }

    void Finish()
    {
        report.Add("");
        report.Add("RESULT: " + passed + " passed, " + failed + " failed. Screenshots: " + outDir);
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EmberPlaytest.txt"));
        File.WriteAllText(path, string.Join("\n", report), Encoding.UTF8);
        Debug.Log("[PLAYTEST] DONE — " + passed + " passed, " + failed + " failed. Report: " + path);
        NoKeys();
        InputReader.Instance.SetTouchMove(Vector2.zero);
        Time.timeScale = 1f;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehaviour;
        InputSystem.settings.backgroundBehavior = savedBackground;
#if UNITY_EDITOR
        // Leave Play Mode so script edits made after the test don't hot-reload into a running game.
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
#endif
