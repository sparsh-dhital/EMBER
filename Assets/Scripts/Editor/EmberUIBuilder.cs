using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the whole uGUI canvas: HUD, touch controls and the menu screens, laid out for 1920x1080
// and scaled by height, inside a safe-area container so phones with notches work.
public static class EmberUIBuilder
{
    static TMP_FontAsset regular, semibold;
    static readonly Color Warm = new Color(0.97f, 0.8f, 0.52f);
    static readonly Color Cream = new Color(0.93f, 0.9f, 0.84f);
    static readonly Color Dim = new Color(0.62f, 0.64f, 0.68f);
    static readonly Color Holy = new Color(1f, 0.92f, 0.7f);

    public class Refs
    {
        public HUDController hud;
        public MenuController menu;
        public TouchControls touch;
    }

    // ------------------------------------------------------------------ fonts

    static TMP_FontAsset Font(string sourcePath, string name)
    {
        string dir = "Assets/Art/Fonts";
        EmberArt.CreateFolderRecursive(dir);
        string ttf = dir + "/" + Path.GetFileName(sourcePath);
        if (!File.Exists(ttf)) AssetDatabase.CopyAsset(sourcePath, ttf);
        string faPath = dir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(faPath);
        if (existing) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = name;
        AssetDatabase.CreateAsset(fa, faPath);
        fa.atlasTexture.name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        fa.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789%/+-—·…:;.,!?'\"()[]<>", out _);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // ------------------------------------------------------------------ build

    public static Refs Build()
    {
        regular = Font("Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-Regular.ttf", "Inter-Regular SDF");
        semibold = Font("Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-SemiBold.ttf", "Inter-SemiBold SDF");

        var old = GameObject.Find("UI");
        if (old) Object.DestroyImmediate(old);
        var oldEs = Object.FindAnyObjectByType<EventSystem>();
        if (oldEs) Object.DestroyImmediate(oldEs.gameObject);

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("UI");
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        var refs = new Refs();
        var damage = Img("DamageFlash", root, "S_EdgeVignette", new Color(0.55f, 0.02f, 0.02f, 0f));
        Stretch(damage.rectTransform);

        var safe = Rect("SafeArea", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        refs.touch = BuildTouch(safe);
        refs.hud = BuildHUD(safe, damage);
        refs.menu = BuildMenus(safe);
        return refs;
    }

    // ------------------------------------------------------------------ HUD

    static HUDController BuildHUD(Transform parent, Image damage)
    {
        var hudRect = Rect("HUD", parent);
        Stretch(hudRect);
        var hud = hudRect.gameObject.AddComponent<HUDController>();
        hud.hudGroup = hudRect.gameObject.AddComponent<CanvasGroup>();
        hud.hudGroup.blocksRaycasts = true;
        hud.damageFlash = damage;

        // Fuel, top-left: the main metaphor.
        var fuel = Place(Rect("Fuel", hudRect), TL, TL, new Vector2(56f, -44f), new Vector2(320f, 100f));
        hud.fuelIcon = Place(Img("FlameIcon", fuel, "S_Flame", Warm), TL, TL, Vector2.zero, new Vector2(48f, 48f));
        Place(Txt("Label", fuel, "FUEL", 17f, Dim, TextAlignmentOptions.TopLeft, regular, 14f), TL, TL, new Vector2(62f, -2f), new Vector2(200f, 24f));
        hud.fuelValue = Place(Txt("Value", fuel, "100%", 46f, Warm, TextAlignmentOptions.TopLeft, semibold, 2f), TL, new Vector2(0f, 0.5f), new Vector2(60f, -46f), new Vector2(220f, 52f));
        var barBg = Place(Img("BarBg", fuel, "S_Bar", new Color(1f, 1f, 1f, 0.12f)), TL, TL, new Vector2(0f, -84f), new Vector2(240f, 5f));
        barBg.type = Image.Type.Sliced;
        hud.fuelBar = Place(Img("BarFill", fuel, "S_Bar", Warm), TL, TL, new Vector2(0f, -84f), new Vector2(240f, 5f));
        hud.fuelBar.type = Image.Type.Filled;
        hud.fuelBar.fillMethod = Image.FillMethod.Horizontal;
        hud.fuelFloater = Place(Txt("Floater", fuel, "+25", 28f, Warm, TextAlignmentOptions.Left, semibold, 0f), TL, TL, new Vector2(190f, -30f), new Vector2(100f, 36f));

        // Mission, top-right.
        var mission = Place(Rect("Mission", hudRect), TR, TR, new Vector2(-140f, -44f), new Vector2(460f, 130f));
        hud.partsText = Place(Txt("Parts", mission, "RADIO PARTS  0/5", 26f, Warm, TextAlignmentOptions.TopRight, semibold, 8f), TR, TR, Vector2.zero, new Vector2(460f, 34f));
        hud.prayerText = Place(Txt("Prayer", mission, "PRAYER LOCKED", 18f, Dim, TextAlignmentOptions.TopRight, regular, 10f), TR, TR, new Vector2(0f, -40f), new Vector2(460f, 26f));
        var signal = Place(Rect("Signal", mission), TR, TR, new Vector2(0f, -76f), new Vector2(200f, 30f));
        hud.signalGroup = signal.gameObject.AddComponent<CanvasGroup>();
        Place(Txt("Label", signal, "SIGNAL", 14f, Dim, TextAlignmentOptions.Right, regular, 10f), TR, TR, new Vector2(-62f, -6f), new Vector2(140f, 20f));
        hud.signalBars = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var bar = Img("Bar" + i, signal, "S_Bar", new Color(1f, 1f, 1f, 0.14f));
            bar.type = Image.Type.Sliced;
            Place(bar, new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(-50f + i * 13f, -28f), new Vector2(8f, 8f + i * 6f));
            hud.signalBars[i] = bar;
        }

        // Pause (works with mouse and touch).
        var pause = Place(Rect("PauseButton", hudRect), TR, TR, new Vector2(-44f, -38f), new Vector2(70f, 70f));
        var pauseBg = Img("Bg", pause, "S_Circle", new Color(0f, 0f, 0f, 0.3f), true);
        Stretch(pauseBg.rectTransform);
        Stretch(Img("Ring", pause, "S_Ring", new Color(1f, 1f, 1f, 0.3f)).rectTransform);
        Place(Img("BarL", pause, "S_Bar", new Color(1f, 1f, 1f, 0.8f)), C, C, new Vector2(-7f, 0f), new Vector2(7f, 22f)).type = Image.Type.Sliced;
        Place(Img("BarR", pause, "S_Bar", new Color(1f, 1f, 1f, 0.8f)), C, C, new Vector2(7f, 0f), new Vector2(7f, 22f)).type = Image.Type.Sliced;
        var pb = pause.gameObject.AddComponent<TouchButton>();
        pb.action = TouchButton.Action.Pause;
        pb.pressVisual = pause;

        // Objective line and its helpers, top-centre.
        hud.objectiveText = Place(Txt("Objective", hudRect, "Find the radio parts", 24f, Cream, TextAlignmentOptions.Center, regular, 8f), T, T, new Vector2(0f, -50f), new Vector2(1000f, 36f));

        var dir = Place(Rect("Direction", hudRect), T, T, new Vector2(0f, -94f), new Vector2(200f, 70f));
        hud.directionGroup = dir.gameObject.AddComponent<CanvasGroup>();
        hud.directionGroup.alpha = 0f;
        hud.directionArrow = Place(Img("Arrow", dir, "S_Chevron", Warm), T, C, new Vector2(0f, -18f), new Vector2(38f, 38f)).rectTransform;
        hud.directionText = Place(Txt("Distance", dir, "34 m", 16f, Dim, TextAlignmentOptions.Center, regular, 6f), T, T, new Vector2(0f, -40f), new Vector2(200f, 24f));

        var call = Place(Rect("RadioCall", hudRect), T, T, new Vector2(0f, -96f), new Vector2(420f, 60f));
        hud.callGroup = call.gameObject.AddComponent<CanvasGroup>();
        hud.callGroup.alpha = 0f;
        hud.callLabel = Place(Txt("Label", call, "SIGNAL 0%", 20f, Warm, TextAlignmentOptions.Center, semibold, 10f), T, T, Vector2.zero, new Vector2(420f, 28f));
        Place(Img("BarBg", call, "S_Bar", new Color(1f, 1f, 1f, 0.12f)), T, T, new Vector2(0f, -38f), new Vector2(400f, 6f)).type = Image.Type.Sliced;
        hud.callBar = Place(Img("BarFill", call, "S_Bar", Warm), T, T, new Vector2(0f, -38f), new Vector2(400f, 6f));
        hud.callBar.type = Image.Type.Filled;
        hud.callBar.fillMethod = Image.FillMethod.Horizontal;

        var cd = Place(Rect("PrayerCountdown", hudRect), TL, T, new Vector2(122f, -120f), new Vector2(130f, 150f));
        hud.countdownGroup = cd.gameObject.AddComponent<CanvasGroup>();
        hud.countdownGroup.alpha = 0f;
        Place(Img("Glow", cd, "S_Glow", new Color(1f, 0.85f, 0.5f, 0.12f)), T, C, new Vector2(0f, -60f), new Vector2(220f, 220f));
        Place(Img("RingBg", cd, "S_RingThick", new Color(1f, 1f, 1f, 0.12f)), T, C, new Vector2(0f, -60f), new Vector2(112f, 112f));
        hud.countdownRing = Place(Img("Ring", cd, "S_RingThick", Holy), T, C, new Vector2(0f, -60f), new Vector2(112f, 112f));
        hud.countdownRing.type = Image.Type.Filled;
        hud.countdownRing.fillMethod = Image.FillMethod.Radial360;
        hud.countdownRing.fillOrigin = (int)Image.Origin360.Top;
        hud.countdownRing.fillClockwise = false;
        hud.countdownText = Place(Txt("Seconds", cd, "60", 42f, Holy, TextAlignmentOptions.Center, semibold, 0f), T, C, new Vector2(0f, -60f), new Vector2(120f, 60f));
        Place(Txt("Label", cd, "PROTECTED", 14f, Holy, TextAlignmentOptions.Center, regular, 10f), T, T, new Vector2(0f, -124f), new Vector2(200f, 22f));

        // Big centred messages.
        var msg = Place(Rect("Message", hudRect), C, C, new Vector2(0f, 250f), new Vector2(1600f, 160f));
        hud.messageGroup = msg.gameObject.AddComponent<CanvasGroup>();
        hud.messageGroup.alpha = 0f;
        hud.messageTitle = Place(Txt("Title", msg, "THE FLAME IS OUT", 64f, Warm, TextAlignmentOptions.Center, semibold, 18f), C, C, new Vector2(0f, 30f), new Vector2(1600f, 80f));
        hud.messageSubtitle = Place(Txt("Subtitle", msg, "", 26f, Cream, TextAlignmentOptions.Center, regular, 4f), C, C, new Vector2(0f, -40f), new Vector2(1600f, 40f));

        // Interaction prompt and radio subtitles, bottom-centre above the thumbs.
        var prompt = Place(Rect("Prompt", hudRect), B, B, new Vector2(0f, 150f), new Vector2(1000f, 56f));
        hud.promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        hud.promptGroup.alpha = 0f;
        var row = prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 16f;
        row.childControlWidth = true;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        var key = Img("Key", prompt, "S_Rounded", new Color(1f, 1f, 1f, 0.14f));
        key.type = Image.Type.Sliced;
        key.rectTransform.sizeDelta = new Vector2(48f, 48f);
        var keyLe = key.gameObject.AddComponent<LayoutElement>();
        keyLe.preferredWidth = 48f; keyLe.preferredHeight = 48f;
        var keyText = Txt("Letter", key.transform, "E", 24f, Cream, TextAlignmentOptions.Center, semibold, 0f);
        Stretch(keyText.rectTransform);
        hud.promptKey = key.gameObject;
        hud.promptText = Txt("Text", prompt, "Take radio part", 26f, Cream, TextAlignmentOptions.Left, regular, 4f);
        hud.promptText.rectTransform.sizeDelta = new Vector2(600f, 40f);

        hud.subtitleText = Place(Txt("RadioSubtitle", hudRect, "", 26f, Cream, TextAlignmentOptions.Center, regular, 2f), B, B, new Vector2(0f, 240f), new Vector2(1400f, 40f));
        hud.subtitleText.fontStyle = FontStyles.Italic;

        return hud;
    }

    // ------------------------------------------------------------------ touch

    static TouchControls BuildTouch(Transform parent)
    {
        var rootRect = Rect("TouchControls", parent);
        Stretch(rootRect);
        var tc = rootRect.gameObject.AddComponent<TouchControls>();
        tc.group = rootRect.gameObject.AddComponent<CanvasGroup>();
        tc.group.alpha = 0f;

        var look = Img("LookArea", rootRect, null, new Color(0f, 0f, 0f, 0f), true);
        look.rectTransform.anchorMin = new Vector2(0.38f, 0f);
        look.rectTransform.anchorMax = new Vector2(1f, 0.86f);
        look.rectTransform.offsetMin = look.rectTransform.offsetMax = Vector2.zero;
        look.gameObject.AddComponent<TouchLookArea>();

        var zone = Place(Img("JoystickZone", rootRect, null, new Color(0f, 0f, 0f, 0f), true), BL, BL, Vector2.zero, new Vector2(720f, 620f));
        var stickBase = Place(Rect("StickBase", zone.transform), BL, C, new Vector2(240f, 230f), new Vector2(230f, 230f));
        var visuals = stickBase.gameObject.AddComponent<CanvasGroup>();
        visuals.blocksRaycasts = false;
        Stretch(Img("Fill", stickBase, "S_Circle", new Color(1f, 1f, 1f, 0.05f)).rectTransform);
        Stretch(Img("Ring", stickBase, "S_Ring", new Color(1f, 1f, 1f, 0.35f)).rectTransform);
        var knob = Place(Img("Knob", stickBase, "S_Circle", new Color(1f, 0.92f, 0.8f, 0.5f)), C, C, Vector2.zero, new Vector2(100f, 100f));
        var joy = zone.gameObject.AddComponent<TouchJoystick>();
        joy.stickBase = stickBase;
        joy.knob = knob.rectTransform;
        joy.visuals = visuals;
        joy.radius = 95f;

        var interact = Place(Rect("InteractButton", rootRect), BR, C, new Vector2(-290f, 190f), new Vector2(150f, 150f));
        tc.interactButton = interact.gameObject.AddComponent<CanvasGroup>();
        Stretch(Img("Bg", interact, "S_Circle", new Color(0f, 0f, 0f, 0.35f), true).rectTransform);
        Stretch(Img("Ring", interact, "S_Ring", new Color(Warm.r, Warm.g, Warm.b, 0.75f)).rectTransform);
        tc.interactLabel = Txt("Label", interact, "TAKE", 26f, Warm, TextAlignmentOptions.Center, semibold, 6f);
        Stretch(tc.interactLabel.rectTransform);
        var ib = interact.gameObject.AddComponent<TouchButton>();
        ib.action = TouchButton.Action.Interact;
        ib.pressVisual = interact;

        var pray = Place(Rect("PrayButton", rootRect), BR, C, new Vector2(-140f, 370f), new Vector2(136f, 136f));
        tc.prayButton = pray.gameObject.AddComponent<CanvasGroup>();
        var glow = Place(Img("Glow", pray, "S_Glow", new Color(1f, 0.85f, 0.5f, 0.35f)), C, C, Vector2.zero, new Vector2(240f, 240f));
        tc.prayGlow = glow.rectTransform;
        Stretch(Img("Bg", pray, "S_Circle", new Color(0f, 0f, 0f, 0.35f), true).rectTransform);
        Stretch(Img("Ring", pray, "S_Ring", new Color(Holy.r, Holy.g, Holy.b, 0.8f)).rectTransform);
        Place(Img("Cross", pray, "S_Cross", Holy), C, C, new Vector2(0f, 14f), new Vector2(50f, 50f));
        Place(Txt("Label", pray, "PRAY", 20f, Holy, TextAlignmentOptions.Center, semibold, 8f), C, C, new Vector2(0f, -36f), new Vector2(136f, 28f));
        var prb = pray.gameObject.AddComponent<TouchButton>();
        prb.action = TouchButton.Action.Pray;
        prb.pressVisual = pray;

        return tc;
    }

    // ------------------------------------------------------------------ menus

    static MenuController BuildMenus(Transform parent)
    {
        var menusRect = Rect("Menus", parent);
        Stretch(menusRect);
        var menu = menusRect.gameObject.AddComponent<MenuController>();

        // Title.
        var title = Screen("TitleScreen", menusRect, new Color(0f, 0f, 0f, 0f));
        menu.titleScreen = title.GetComponent<CanvasGroup>();
        var shade = Img("Shade", title, "S_GradientLeft", new Color(0.01f, 0.012f, 0.02f, 0.92f));
        shade.rectTransform.anchorMin = new Vector2(0f, 0f);
        shade.rectTransform.anchorMax = new Vector2(0f, 1f);
        shade.rectTransform.pivot = new Vector2(0f, 0.5f);
        shade.rectTransform.sizeDelta = new Vector2(1250f, 0f);
        shade.rectTransform.anchoredPosition = Vector2.zero;
        Place(Img("TitleGlow", title, "S_Glow", new Color(1f, 0.6f, 0.25f, 0.07f)), L, C, new Vector2(420f, 170f), new Vector2(1000f, 520f));
        Place(Img("Flame", title, "S_Flame", Warm), L, L, new Vector2(154f, 300f), new Vector2(42f, 42f));
        Place(Txt("Title", title, "EMBER", 170f, Warm, TextAlignmentOptions.Left, semibold, 38f), L, L, new Vector2(140f, 175f), new Vector2(1100f, 200f));
        Place(Txt("Subtitle", title, "KEEP THE LIGHT ALIVE", 26f, new Color(Cream.r, Cream.g, Cream.b, 0.8f), TextAlignmentOptions.Left, regular, 22f), L, L, new Vector2(156f, 60f), new Vector2(900f, 36f));
        Place(Img("Divider", title, null, new Color(1f, 1f, 1f, 0.16f)), L, L, new Vector2(158f, 18f), new Vector2(380f, 2f));
        menu.startButton = TextButton("Start", title, "START", L, new Vector2(150f, -60f), 40f, Warm);
        menu.howToPlayButton = TextButton("HowToPlay", title, "HOW TO PLAY", L, new Vector2(150f, -140f), 30f, Cream);
        menu.quitButton = TextButton("Quit", title, "QUIT", L, new Vector2(150f, -210f), 30f, Cream);
        menu.bestText = Place(Txt("Best", title, "", 18f, Dim, TextAlignmentOptions.BottomLeft, regular, 8f), BL, BL, new Vector2(158f, 70f), new Vector2(1000f, 30f));

        // How to play.
        var how = Screen("HowToPlayScreen", menusRect, new Color(0.01f, 0.012f, 0.02f, 0.9f));
        menu.howToPlayScreen = how.GetComponent<CanvasGroup>();
        Place(Txt("Title", how, "HOW TO PLAY", 44f, Warm, TextAlignmentOptions.Center, semibold, 18f), C, C, new Vector2(0f, 360f), new Vector2(1200f, 60f));
        string[] lines =
        {
            "Your lantern protects you.",
            "Find fuel before the flame dies.",
            "Vampires fear strong light.",
            "Find the radio parts.",
            "Find the locket.",
            "When the flame dies, prayer gives you one final chance.",
            "Return to the radio centre and call for help."
        };
        string[] icons = { "S_Flame", "S_Flame", "S_Glow", "S_Chevron", "S_Circle", "S_Cross", "S_Chevron" };
        for (int i = 0; i < lines.Length; i++)
        {
            float y = 250f - i * 62f;
            Place(Img("Icon" + i, how, icons[i], i == 5 ? Holy : Warm), C, C, new Vector2(-470f, y), new Vector2(26f, 26f));
            Place(Txt("Line" + i, how, lines[i], 29f, Cream, TextAlignmentOptions.Left, regular, 2f), C, new Vector2(0f, 0.5f), new Vector2(-430f, y), new Vector2(1000f, 44f));
        }
        menu.controlsText = Place(Txt("Controls", how, "", 20f, Dim, TextAlignmentOptions.Center, regular, 4f), C, C, new Vector2(0f, -240f), new Vector2(1500f, 70f));
        menu.howToPlayBackButton = TextButton("Back", how, "BACK", C, new Vector2(0f, -350f), 30f, Warm, TextAlignmentOptions.Center);

        // Pause.
        var pause = Screen("PauseScreen", menusRect, new Color(0.01f, 0.012f, 0.02f, 0.75f));
        menu.pauseScreen = pause.GetComponent<CanvasGroup>();
        Place(Txt("Title", pause, "PAUSED", 70f, Warm, TextAlignmentOptions.Center, semibold, 30f), C, C, new Vector2(0f, 160f), new Vector2(1000f, 90f));
        menu.resumeButton = TextButton("Resume", pause, "RESUME", C, new Vector2(0f, 20f), 34f, Warm, TextAlignmentOptions.Center);
        menu.pauseRestartButton = TextButton("Restart", pause, "RESTART", C, new Vector2(0f, -60f), 30f, Cream, TextAlignmentOptions.Center);
        menu.pauseMenuButton = TextButton("MainMenu", pause, "MAIN MENU", C, new Vector2(0f, -140f), 30f, Cream, TextAlignmentOptions.Center);

        // Victory.
        var win = Screen("VictoryScreen", menusRect, new Color(0.06f, 0.045f, 0.03f, 0.62f));
        menu.victoryScreen = win.GetComponent<CanvasGroup>();
        Place(Img("Glow", win, "S_Glow", new Color(1f, 0.75f, 0.4f, 0.12f)), C, C, new Vector2(0f, 180f), new Vector2(1400f, 600f));
        Place(Txt("Title", win, "YOU SURVIVED", 96f, Warm, TextAlignmentOptions.Center, semibold, 26f), C, C, new Vector2(0f, 190f), new Vector2(1600f, 120f));
        Place(Txt("Line", win, "Help arrived with the dawn.", 28f, Cream, TextAlignmentOptions.Center, regular, 4f), C, C, new Vector2(0f, 95f), new Vector2(1400f, 40f));
        menu.victoryStats = Place(Txt("Stats", win, "", 26f, Cream, TextAlignmentOptions.Center, regular, 6f), C, C, new Vector2(0f, -5f), new Vector2(1400f, 90f));
        menu.victoryAgainButton = TextButton("PlayAgain", win, "PLAY AGAIN", C, new Vector2(0f, -150f), 34f, Warm, TextAlignmentOptions.Center);
        menu.victoryMenuButton = TextButton("MainMenu", win, "MAIN MENU", C, new Vector2(0f, -230f), 28f, Cream, TextAlignmentOptions.Center);

        // Defeat.
        var lose = Screen("DefeatScreen", menusRect, new Color(0.03f, 0f, 0.005f, 0.8f));
        menu.defeatScreen = lose.GetComponent<CanvasGroup>();
        Place(Txt("Title", lose, "CONSUMED BY THE DARK", 72f, new Color(0.9f, 0.34f, 0.27f), TextAlignmentOptions.Center, semibold, 16f), C, C, new Vector2(0f, 190f), new Vector2(1700f, 100f));
        menu.defeatCause = Place(Txt("Cause", lose, "", 28f, Cream, TextAlignmentOptions.Center, regular, 2f), C, C, new Vector2(0f, 100f), new Vector2(1500f, 40f));
        menu.defeatStats = Place(Txt("Stats", lose, "", 24f, Dim, TextAlignmentOptions.Center, regular, 6f), C, C, new Vector2(0f, 0f), new Vector2(1400f, 90f));
        menu.defeatAgainButton = TextButton("TryAgain", lose, "TRY AGAIN", C, new Vector2(0f, -150f), 34f, Warm, TextAlignmentOptions.Center);
        menu.defeatMenuButton = TextButton("MainMenu", lose, "MAIN MENU", C, new Vector2(0f, -230f), 28f, Cream, TextAlignmentOptions.Center);

        return menu;
    }

    static Transform Screen(string name, Transform parent, Color bg)
    {
        var r = Rect(name, parent);
        Stretch(r);
        var g = r.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        var img = Img("Backdrop", r, null, bg, true);
        Stretch(img.rectTransform);
        return r;
    }

    static Button TextButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        bool centred = align == TextAlignmentOptions.Center;
        var r = Place(Rect(name, parent), anchor, centred ? C : L, pos, new Vector2(460f, size + 28f));
        var hit = Img("Hit", r, null, new Color(1f, 1f, 1f, 0f), true);
        Stretch(hit.rectTransform);
        var t = Txt("Label", r, label, size, Color.white, align, semibold, 14f);
        Stretch(t.rectTransform);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        var colors = b.colors;
        colors.normalColor = new Color(color.r, color.g, color.b, 0.82f);
        colors.highlightedColor = color;
        colors.selectedColor = color;
        colors.pressedColor = new Color(color.r, color.g, color.b, 0.55f);
        colors.fadeDuration = 0.12f;
        b.colors = colors;
        var nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
        return b;
    }

    // ------------------------------------------------------------------ helpers

    static readonly Vector2 TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f), T = new Vector2(0.5f, 1f), B = new Vector2(0.5f, 0f),
                            BL = new Vector2(0f, 0f), BR = new Vector2(1f, 0f), C = new Vector2(0.5f, 0.5f), L = new Vector2(0f, 0.5f);

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static T Place<T>(T c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) where T : Component
    {
        var r = c as RectTransform ?? (RectTransform)c.transform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return c;
    }

    static Image Img(string name, Transform parent, string sprite, Color color, bool raycast = false)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        if (sprite != null) img.sprite = EmberArt.LoadSprite(sprite);
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font, float spacing)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
