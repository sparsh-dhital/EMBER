#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
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
        errorsLogged++;
        report.Add("  ! runtime error: " + msg);
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
        // T5 lantern is parented to the right hand and stays there while moving.
        Transform hand = null;
        for (var p = lantern.transform.parent; p; p = p.parent) if (p.name == "HandR") { hand = p; break; }
        Vector3 open = GroundAt(new Vector3(-3f, 0f, 10f));
        Teleport(open, 0f);
        yield return HoldKeys(0.8f, Key.W);
        float handGap = hand ? Vector3.Distance(hand.position, lantern.transform.position) : 99f;
        Check("T5", "Lantern physically attached to the right hand", hand && handGap < 0.2f, "parent chain has HandR: " + (hand != null) + ", hand→handle " + handGap.ToString("F2") + " m while walking");

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
        Check("T13", "Prayer cannot be used before fuel reaches zero", !prayer.IsActive && !prayer.Used, "active " + prayer.IsActive + ", used " + prayer.Used);
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

        // T20 it lasts about 60 s (fast-forwarded).
        Time.timeScale = 8f;
        t = 0f;
        while (prayer.IsActive && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        Time.timeScale = 1f;
        float lasted = Time.time - startTime + (prayer.duration - left0);
        Check("T20", "Prayer lasts about 60 seconds", !prayer.IsActive && Mathf.Abs(lasted - 60f) < 2f, "lasted " + lasted.ToString("F1") + " s of game time");

        // T21 only once.
        InputReader.Instance.PressTouchPray();
        yield return Wait(0.3f);
        Check("T21", "Prayer can only be used once", !prayer.IsActive && prayer.Used, "second attempt active: " + prayer.IsActive);

        BanishAll();
        fuel.AddFuel(100f);
        yield return Wait(2.6f);
    }

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
