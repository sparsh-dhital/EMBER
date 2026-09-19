using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static EmberCharacterBuilder;

// Builds the reusable prefabs: the vampire, the fuel can, the radio part and the sacred locket.
public static class EmberPrefabBuilder
{
    public const string VampirePath = "Assets/Prefabs/Enemies/Vampire.prefab";
    public const string FuelPath = "Assets/Prefabs/Pickups/FuelCan.prefab";
    public const string RadioPartPath = "Assets/Prefabs/Pickups/RadioPart.prefab";
    public const string LocketPath = "Assets/Prefabs/Pickups/SacredLocket.prefab";

    [MenuItem("EMBER/Build/3. Prefabs (vampire, pickups)")]
    public static void BuildAll()
    {
        EmberSceneBuilder.EnsureLayers();
        EmberArt.EnsureFolders();
        BuildVampire();
        BuildFuelCan();
        BuildRadioPart();
        BuildLocket();
        AssetDatabase.SaveAssets();
        Debug.Log("EMBER: prefabs built.");
    }

    static void Save(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ vampire

    public static void BuildVampire()
    {
        Material skin = EmberArt.Load("VampireSkin"), face = EmberArt.Load("VampireFace"),
                 cloak = EmberArt.Load("VampireCloak"), eye = EmberArt.Load("VampireEye");

        var root = new GameObject("Vampire");
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 2f;
        agent.speed = 3f;
        agent.acceleration = 14f;
        agent.angularSpeed = 540f;
        agent.stoppingDistance = 0.4f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.autoBraking = true;

        var anim = root.AddComponent<VampireAnimator>();
        var ai = root.AddComponent<VampireAI>();
        ai.visual = anim;

        var model = Bone("Model", root.transform, Vector3.zero);
        var hips = Bone("Hips", model, new Vector3(0f, 1.02f, 0f));
        Part("Waist", PrimitiveType.Cube, hips, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.2f, 0.2f), cloak);

        var torso = Bone("Torso", hips, new Vector3(0f, 0.08f, 0f));
        Part("Ribs", PrimitiveType.Capsule, torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f, 0.32f, 0.22f), skin);
        Part("Mantle", PrimitiveType.Cube, torso, new Vector3(0f, 0.55f, -0.02f), new Vector3(0.56f, 0.1f, 0.24f), cloak);
        // The tall collar gives the vampire an instantly readable silhouette.
        Part("CollarL", PrimitiveType.Cube, torso, new Vector3(-0.13f, 0.74f, -0.09f), new Vector3(0.2f, 0.34f, 0.03f), cloak, new Vector3(-10f, 20f, 18f));
        Part("CollarR", PrimitiveType.Cube, torso, new Vector3(0.13f, 0.74f, -0.09f), new Vector3(0.2f, 0.34f, 0.03f), cloak, new Vector3(-10f, -20f, -18f));

        var head = Bone("Head", torso, new Vector3(0f, 0.66f, 0.03f));
        Part("Skull", PrimitiveType.Sphere, head, new Vector3(0f, 0.13f, 0.02f), new Vector3(0.2f, 0.28f, 0.22f), face);
        Part("Brow", PrimitiveType.Cube, head, new Vector3(0f, 0.17f, 0.1f), new Vector3(0.17f, 0.035f, 0.05f), skin, new Vector3(12f, 0f, 0f));
        Part("Jaw", PrimitiveType.Cube, head, new Vector3(0f, 0.03f, 0.07f), new Vector3(0.11f, 0.06f, 0.09f), skin);
        Part("EarL", PrimitiveType.Cube, head, new Vector3(-0.11f, 0.17f, -0.01f), new Vector3(0.025f, 0.16f, 0.07f), face, new Vector3(0f, 0f, 28f));
        Part("EarR", PrimitiveType.Cube, head, new Vector3(0.11f, 0.17f, -0.01f), new Vector3(0.025f, 0.16f, 0.07f), face, new Vector3(0f, 0f, -28f));
        var eyeL = Part("EyeL", PrimitiveType.Sphere, head, new Vector3(-0.048f, 0.14f, 0.115f), new Vector3(0.045f, 0.025f, 0.02f), eye);
        var eyeR = Part("EyeR", PrimitiveType.Sphere, head, new Vector3(0.048f, 0.14f, 0.115f), new Vector3(0.045f, 0.025f, 0.02f), eye);
        EmberFX.GlowSprite(head, new Vector3(0f, 0.14f, 0.13f), new Color(1f, 0.12f, 0.08f, 0.5f), 0.32f);

        Transform[] ua = new Transform[2], fa = new Transform[2], th = new Transform[2], sh = new Transform[2], cape = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            string side = i == 0 ? "L" : "R";
            ua[i] = Bone("UpperArm" + side, torso, new Vector3(0.27f * s, 0.52f, 0f));
            Part("Arm", PrimitiveType.Capsule, ua[i], new Vector3(0f, -0.19f, 0f), new Vector3(0.09f, 0.2f, 0.09f), skin);
            fa[i] = Bone("Forearm" + side, ua[i], new Vector3(0f, -0.38f, 0f));
            Part("Forearm", PrimitiveType.Capsule, fa[i], new Vector3(0f, -0.18f, 0f), new Vector3(0.075f, 0.19f, 0.075f), skin);
            for (int f = 0; f < 3; f++)
                Part("Claw" + f, PrimitiveType.Cube, fa[i], new Vector3((f - 1) * 0.025f, -0.43f, 0.01f), new Vector3(0.014f, 0.13f, 0.014f), face, new Vector3(-12f, 0f, (f - 1) * 10f));

            cape[i] = Bone("Cape" + side, torso, new Vector3(0.15f * s, 0.56f, -0.13f));
            Part("CapePanel", PrimitiveType.Cube, cape[i], new Vector3(0.02f * s, -0.58f, 0f), new Vector3(0.3f, 1.18f, 0.025f), cloak);

            th[i] = Bone("Thigh" + side, hips, new Vector3(0.11f * s, -0.05f, 0f));
            Part("Thigh", PrimitiveType.Capsule, th[i], new Vector3(0f, -0.23f, 0f), new Vector3(0.12f, 0.25f, 0.12f), cloak);
            sh[i] = Bone("Shin" + side, th[i], new Vector3(0f, -0.47f, 0f));
            Part("Shin", PrimitiveType.Capsule, sh[i], new Vector3(0f, -0.22f, 0f), new Vector3(0.1f, 0.24f, 0.1f), skin);
            Part("Foot", PrimitiveType.Cube, sh[i], new Vector3(0f, -0.47f, 0.06f), new Vector3(0.09f, 0.06f, 0.24f), cloak);
        }

        anim.model = model;
        anim.hips = hips;
        anim.torso = torso;
        anim.head = head;
        anim.upperArmL = ua[0]; anim.upperArmR = ua[1];
        anim.forearmL = fa[0]; anim.forearmR = fa[1];
        anim.thighL = th[0]; anim.thighR = th[1];
        anim.shinL = sh[0]; anim.shinR = sh[1];
        anim.capeL = cape[0]; anim.capeR = cape[1];
        anim.eyes = new[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() };

        // A solid body the player can't walk through (kinematic: the NavMeshAgent moves it, physics never pushes it).
        var body = root.AddComponent<CapsuleCollider>();
        body.radius = 0.3f;
        body.height = 1.9f;
        body.center = new Vector3(0f, 0.95f, 0f);
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;

        SetLayerRecursive(root, LayerMask.NameToLayer(EmberLayers.Enemy));

        // What the sword tests against: slightly larger than the body so a swing that visibly connects counts.
        var hitbox = new GameObject("Hitbox");
        hitbox.transform.SetParent(root.transform, false);
        hitbox.layer = LayerMask.NameToLayer(EmberLayers.EnemyHitbox);
        var hc = hitbox.AddComponent<CapsuleCollider>();
        hc.isTrigger = true;
        hc.radius = 0.42f;
        hc.height = 2f;
        hc.center = new Vector3(0f, 1.05f, 0f);
        Save(root, VampirePath);
    }

    // ------------------------------------------------------------------ fuel can

    public static void BuildFuelCan()
    {
        Material can = EmberArt.Load("FuelCan"), label = EmberArt.Load("FuelLabel"), metal = EmberArt.Load("Metal");

        var root = new GameObject("FuelCan");
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.75f;
        col.center = new Vector3(0f, 0.5f, 0f);
        var pickup = root.AddComponent<FuelPickup>();

        var visual = Bone("Visual", root.transform, new Vector3(0f, 0.08f, 0f));
        Part("Body", PrimitiveType.Cube, visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.28f, 0.36f, 0.14f), can);
        Part("Label", PrimitiveType.Cube, visual, new Vector3(0f, 0.24f, 0f), new Vector3(0.285f, 0.07f, 0.145f), label);
        Part("HandleL", PrimitiveType.Cube, visual, new Vector3(-0.05f, 0.47f, 0f), new Vector3(0.025f, 0.08f, 0.03f), can);
        Part("HandleR", PrimitiveType.Cube, visual, new Vector3(0.05f, 0.47f, 0f), new Vector3(0.025f, 0.08f, 0.03f), can);
        Part("HandleTop", PrimitiveType.Cube, visual, new Vector3(0f, 0.51f, 0f), new Vector3(0.13f, 0.025f, 0.035f), can);
        Part("Spout", PrimitiveType.Cylinder, visual, new Vector3(0.1f, 0.47f, 0f), new Vector3(0.05f, 0.05f, 0.05f), metal, new Vector3(0f, 0f, -35f));
        Part("Cap", PrimitiveType.Cylinder, visual, new Vector3(0.13f, 0.51f, 0f), new Vector3(0.06f, 0.012f, 0.06f), metal, new Vector3(0f, 0f, -35f));
        EmberFX.GlowSprite(visual, new Vector3(0f, 0.27f, 0f), new Color(1f, 0.6f, 0.2f, 0.28f), 1.4f);
        var motes = EmberFX.Motes(visual, new Vector3(0f, 0.3f, 0f), new Color(1f, 0.65f, 0.25f), 3f, 0.2f, 0.025f, 10);
        var m = motes.main; m.playOnAwake = true;

        pickup.visual = visual;
        pickup.pickupBurst = EmberFX.Burst(root.transform, new Vector3(0f, 0.4f, 0f), new Color(1f, 0.65f, 0.25f), 22, 2.2f, 0.06f, "PickupBurst");

        SetLayerRecursive(root, LayerMask.NameToLayer("Interactable"));
        Save(root, FuelPath);
    }

    // ------------------------------------------------------------------ radio part

    public static void BuildRadioPart()
    {
        Material panel = EmberArt.Load("RadioPanel"), metal = EmberArt.Load("Metal"), led = EmberArt.Load("RadioLED");

        var root = new GameObject("RadioPart");
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.8f, 0.8f, 0.8f);
        col.center = new Vector3(0f, 0.3f, 0f);
        var part = root.AddComponent<RadioPart>();

        var visual = Bone("Visual", root.transform, Vector3.zero);
        Part("Case", PrimitiveType.Cube, visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.34f, 0.2f, 0.24f), panel);
        Part("Faceplate", PrimitiveType.Cube, visual, new Vector3(0f, 0.11f, 0.122f), new Vector3(0.3f, 0.16f, 0.01f), metal);
        Part("Dial", PrimitiveType.Cylinder, visual, new Vector3(-0.07f, 0.11f, 0.13f), new Vector3(0.07f, 0.012f, 0.07f), metal, new Vector3(90f, 0f, 0f));
        Part("Grille", PrimitiveType.Cube, visual, new Vector3(0.07f, 0.11f, 0.13f), new Vector3(0.1f, 0.1f, 0.008f), panel);
        Part("Antenna", PrimitiveType.Cylinder, visual, new Vector3(0.12f, 0.36f, -0.06f), new Vector3(0.012f, 0.2f, 0.012f), metal, new Vector3(0f, 0f, -12f));
        Part("Handle", PrimitiveType.Cube, visual, new Vector3(0f, 0.22f, 0f), new Vector3(0.2f, 0.02f, 0.04f), metal);
        var ledT = Part("LED", PrimitiveType.Sphere, visual, new Vector3(-0.12f, 0.18f, 0.125f), new Vector3(0.025f, 0.025f, 0.02f), led);

        part.indicator = ledT.GetComponent<Renderer>();
        part.visual = visual.gameObject;
        part.pickupBurst = EmberFX.Burst(root.transform, new Vector3(0f, 0.2f, 0f), new Color(0.5f, 1f, 0.8f), 18, 1.6f, 0.04f, "PickupBurst");

        SetLayerRecursive(root, LayerMask.NameToLayer("Interactable"));
        Save(root, RadioPartPath);
    }

    // ------------------------------------------------------------------ locket

    public static void BuildLocket()
    {
        Material gold = EmberArt.Load("Gold");

        var root = new GameObject("SacredLocket");
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.7f;
        col.center = new Vector3(0f, 0.2f, 0f);
        var locket = root.AddComponent<LocketPickup>();

        var visual = Bone("Visual", root.transform, new Vector3(0f, 0.18f, 0f));
        Part("Pendant", PrimitiveType.Sphere, visual, Vector3.zero, new Vector3(0.1f, 0.13f, 0.035f), gold);
        Part("CrossV", PrimitiveType.Cube, visual, new Vector3(0f, 0.005f, 0.018f), new Vector3(0.012f, 0.06f, 0.008f), gold);
        Part("CrossH", PrimitiveType.Cube, visual, new Vector3(0f, 0.017f, 0.018f), new Vector3(0.036f, 0.012f, 0.008f), gold);
        Part("Bail", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.075f, 0f), new Vector3(0.02f, 0.008f, 0.02f), gold, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI;
            Part("Chain" + i, PrimitiveType.Sphere, visual, new Vector3(Mathf.Cos(a) * 0.08f, 0.08f + Mathf.Sin(a) * 0.1f, 0f), Vector3.one * 0.012f, gold);
        }
        EmberFX.GlowSprite(visual, Vector3.zero, new Color(1f, 0.85f, 0.5f, 0.4f), 0.9f);
        var shimmer = EmberFX.Motes(visual, Vector3.zero, new Color(1f, 0.9f, 0.6f), 6f, 0.25f, 0.025f, 14, "Shimmer");
        var sm = shimmer.main; sm.playOnAwake = true;

        locket.visual = visual.gameObject;
        locket.shimmer = shimmer;
        locket.pickupBurst = EmberFX.Burst(root.transform, new Vector3(0f, 0.2f, 0f), new Color(1f, 0.85f, 0.5f), 30, 2f, 0.05f, "PickupBurst");

        SetLayerRecursive(root, LayerMask.NameToLayer("Interactable"));
        Save(root, LocketPath);
    }
}
