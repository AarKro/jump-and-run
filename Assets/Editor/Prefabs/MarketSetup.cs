using UnityEditor;
using UnityEngine;

// Baut die Prefabs für den Markt auf der Marinara Promenade (Level 4) aus Grundformen: Marktstände (Essen, Fisch,
// Blumen, Flohmarkt), einen federnden Stand, Passanten, eine Gruppe, einen Handkarren und Kistenstapel.
// Menü: Tools > Jump and Run > City > Build Market Prefabs. Mehrfach ausführbar, Prefabs werden ersetzt.
//
// Konvention wie bei den Stadt-Prefabs: Die Wurzel liegt auf der Laufbahn (lokal +X = Laufrichtung, +Z = Hausseite).
// Feste Dinge (Stände, Gruppe, Kisten): Wurzel auf der Mitte der 1 m dicken Bodenplatte, Oberfläche bei +0.5.
// Bewegte Dinge mit GroundPatrol (Passant, Karren): Wurzel 1 m darüber, Füße bei -0.5 (wie Hund und Skater).
// Die Stände sind reine Kulisse ohne Collider und stehen hinter der Bahn (bis 8 m, dort sind die Fassaden).
public static class MarketSetup
{
    public const string PrefabFolder = "Assets/Prefabs/Market";
    private const string MaterialFolder = "Assets/Materials/City";
    private const int GroundLayer = 3;
    private const float Surface = 0.5f;

    // Stand: Tischvorderkante 3 m hinter der Bahn, Tiefe des ganzen Stands gut 2 m
    private const float StallFrontZ = 3f;
    private const float StallWidth = 3f;

    private static Material wood, darkWood, cloth, pole, white, terracotta, leaf, ice, silver, bag, iron;

    [MenuItem("Tools/Jump and Run/City/Build Market Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Market");
        CreateMaterials();
        BuildStall("Market Stall Food", Lit("Market Red", new Color(0.82f, 0.16f, 0.12f)), FoodGoods);
        BuildStall("Market Stall Fish", Lit("Market Blue", new Color(0.2f, 0.4f, 0.72f)), FishGoods);
        BuildStall("Market Stall Flowers", Lit("Market Green", new Color(0.22f, 0.55f, 0.3f)), FlowerGoods);
        BuildStall("Market Stall Flea", Lit("Market Brown", new Color(0.5f, 0.32f, 0.18f)), FleaGoods);
        BuildSouvenirStall();
        BuildBouncyStall();
        BuildPedestrian();
        BuildGroup();
        BuildHandCart();
        BuildCrates("Crate Stack Low", 1);
        BuildCrates("Crate Stack High", 2);
        BuildJugglers();
        BuildStroller();
        BuildHarborLamp();
        BuildBench();
        BuildMusician();
        BuildIceCreamCart();
        BuildBalloonSeller();
        BuildLemonadeStand();
        BuildKicker();
        BuildBollard();
        BuildSkaterKid();
        BuildSkateSpot();
        BuildShopPoster();
        AssetDatabase.SaveAssets();
        Debug.Log("MarketSetup: Markt-Prefabs gebaut.");
    }

    private static Material Lit(string name, Color color, float smoothness = 0.2f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static void CreateMaterials()
    {
        wood = Lit("Crate Wood", new Color(0.72f, 0.55f, 0.33f));
        darkWood = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Door Wood.mat");
        cloth = Lit("Market Cloth", new Color(0.93f, 0.9f, 0.82f));
        pole = Lit("Market Pole", new Color(0.3f, 0.3f, 0.32f), 0.4f);
        white = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Awning White.mat");
        iron = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Iron.mat");
        terracotta = Lit("Terracotta", new Color(0.78f, 0.42f, 0.25f));
        leaf = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Leaves.mat");
        ice = Lit("Fish Ice", new Color(0.85f, 0.93f, 0.97f), 0.7f);
        silver = Lit("Fish Silver", new Color(0.65f, 0.7f, 0.74f), 0.8f);
        bag = Lit("Paper Bag", new Color(0.8f, 0.66f, 0.45f));
    }

    // ---------- Personen ----------

    // Figur wie der Kunde, mit Haaren, als CrowdFigure (Farben und Animation zur Laufzeit). Füße bei localPosition
    public static CrowdFigure Person(Transform parent, string name, Vector3 localPosition, float yaw, float gesture = 1f, float armsForward = 0f)
    {
        ParkSetup.Figure f = ParkSetup.BuildFigure(parent, name, localPosition, yaw);
        Material figureMaterial = f.head.sharedMaterial;
        Renderer hair = PrimitiveKit.Part("Hair", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.42f, 0.04f), new Vector3(0.47f, 0.34f, 0.47f), figureMaterial);
        var figure = f.root.gameObject.AddComponent<CrowdFigure>();
        var serialized = new SerializedObject(figure);
        serialized.FindProperty("rig").objectReferenceValue = f.rig;
        serialized.FindProperty("leftArm").objectReferenceValue = f.leftArm;
        serialized.FindProperty("rightArm").objectReferenceValue = f.rightArm;
        serialized.FindProperty("gesture").floatValue = gesture;
        serialized.FindProperty("armsForward").floatValue = armsForward;
        PrimitiveKit.SetReferences(serialized, "shirtRenderers", f.body);
        PrimitiveKit.SetReferences(serialized, "skinRenderers", f.head, f.leftArmMesh, f.rightArmMesh);
        PrimitiveKit.SetReferences(serialized, "hairRenderers", hair);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return figure;
    }

    // Passant: läuft mit GroundPatrol in seinem Abschnitt hin und her (Grenzen setzt der Level-Generator), bleibt an den
    // Enden kurz stehen. Kinematischer Rigidbody + Box auf der Wurzel (Layer Default: kein Boden, Hunde/Passanten laufen
    // nicht dagegen). Die Figur steht im Profil unter "Body", das GroundPatrol zum Umdrehen spiegelt
    private static void BuildPedestrian()
    {
        const float feet = -0.5f;
        var root = new GameObject("Pedestrian");
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, feet + 0.8f, 0f), new Vector3(0.6f, 1.6f, 0.6f));
        AddKinematicBody(root);

        Transform body = PrimitiveKit.Pivot("Body", t, Vector3.zero);
        CrowdFigure figure = Person(body, "Figure", new Vector3(0f, feet, 0f), -90f);
        // Einkaufstüte in der rechten Hand (hängt am Arm, schwingt also mit)
        Transform rightArm = figure.transform.Find("Rig/Arm Pivot R");
        PrimitiveKit.Box("Bag", rightArm, new Vector3(0f, -0.7f, 0f), new Vector3(0.12f, 0.3f, 0.28f), bag);

        GameObject hey = ParkSetup.SpeechBubble(t, new Vector3(0.4f, feet + 2.2f, -0.3f), "HEY!");

        var patrol = new SerializedObject(root.AddComponent<GroundPatrol>());
        patrol.FindProperty("patrolSpeed").floatValue = 1.2f;
        patrol.FindProperty("chaseSpeed").floatValue = 1.2f;
        patrol.FindProperty("acceleration").floatValue = 4f;
        patrol.FindProperty("deceleration").floatValue = 8f;
        patrol.FindProperty("turnDuration").floatValue = 0.45f;
        patrol.FindProperty("sightRange").floatValue = 0f;
        patrol.FindProperty("usePatrolLimits").boolValue = true;
        patrol.FindProperty("patrolLimits").vector2Value = new Vector2(-4f, 4f);
        patrol.FindProperty("waitAtLimits").floatValue = 1f;
        patrol.FindProperty("flipTarget").objectReferenceValue = body;
        patrol.ApplyModifiedPropertiesWithoutUndo();

        AddObstacle(root, bounceOnTop: true, passable: true, hey, null, figure);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Pedestrian.prefab");
    }

    // Drei Leute, die auf der Bahn stehen und sich unterhalten. Man muss drüber (sie ducken sich, "WHOA!")
    private static void BuildGroup()
    {
        var root = new GameObject("Market Group");
        Transform t = root.transform;
        CrowdFigure a = Person(t, "Person A", new Vector3(-0.6f, Surface, -0.1f), -70f);
        CrowdFigure b = Person(t, "Person B", new Vector3(0.6f, Surface, -0.05f), 70f);
        CrowdFigure c = Person(t, "Person C", new Vector3(0f, Surface, 0.35f), 0f);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 0.8f, 0.1f), new Vector3(1.9f, 1.6f, 1f));
        GameObject hey = ParkSetup.SpeechBubble(t, new Vector3(0.5f, Surface + 2.2f, -0.4f), "HEY!");
        GameObject whoa = ParkSetup.SpeechBubble(t, new Vector3(-0.4f, Surface + 2.2f, -0.4f), "WHOA!");
        AddObstacle(root, bounceOnTop: true, passable: true, hey, whoa, a, b, c);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Market Group.prefab");
    }

    // Händler schiebt einen Karren mit Obstkisten langsam hin und her. Die Ladefläche ist eine Plattform (Layer Ground),
    // seitlich angefahren fällt man vom Board
    private static void BuildHandCart()
    {
        const float feet = -0.5f;
        var root = new GameObject("Hand Cart");
        root.layer = GroundLayer;
        Transform t = root.transform;
        // Box über Karren und Händler, Oberkante = Ladefläche (feet + 1.1)
        PrimitiveKit.AddBoxCollider(root, new Vector3(-0.2f, feet + 0.55f, 0f), new Vector3(2.6f, 1.1f, 1f));
        AddKinematicBody(root);

        // Karren vorne (lokal +X der Figur = Fahrtrichtung), Händler dahinter
        Transform body = PrimitiveKit.Pivot("Body", t, Vector3.zero);
        PrimitiveKit.Box("Cart Bed", body, new Vector3(0.35f, feet + 0.85f, 0f), new Vector3(1.5f, 0.12f, 0.9f), wood);
        PrimitiveKit.Box("Cart Side Front", body, new Vector3(0.35f, feet + 0.98f, -0.43f), new Vector3(1.5f, 0.25f, 0.04f), darkWood);
        PrimitiveKit.Box("Cart Side Back", body, new Vector3(0.35f, feet + 0.98f, 0.43f), new Vector3(1.5f, 0.25f, 0.04f), darkWood);
        foreach (float z in new[] { -0.5f, 0.5f })
        {
            Renderer wheel = PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, body, new Vector3(0.35f, feet + 0.4f, z), new Vector3(0.8f, 0.04f, 0.8f), darkWood);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveKit.Box("Handle", body, new Vector3(-0.65f, feet + 1.0f, z * 0.8f), new Vector3(0.7f, 0.05f, 0.05f), darkWood);
        }
        PrimitiveKit.Box("Leg", body, new Vector3(1.0f, feet + 0.4f, 0f), new Vector3(0.06f, 0.8f, 0.06f), darkWood);
        // Ladung: zwei flache Kisten mit Orangen und Äpfeln
        Material orange = Lit("Fruit Orange", new Color(1f, 0.55f, 0.1f));
        Material apple = Lit("Fruit Apple", new Color(0.75f, 0.12f, 0.1f));
        FruitCrate(body, new Vector3(0f, feet + 0.91f, 0f), orange);
        FruitCrate(body, new Vector3(0.7f, feet + 0.91f, 0f), apple);

        CrowdFigure vendor = Person(body, "Vendor", new Vector3(-1.1f, feet, 0f), -90f, 0f, 70f);
        GameObject hey = ParkSetup.SpeechBubble(t, new Vector3(-0.6f, feet + 2.2f, -0.4f), "HEY!");

        var patrol = new SerializedObject(root.AddComponent<GroundPatrol>());
        patrol.FindProperty("patrolSpeed").floatValue = 0.9f;
        patrol.FindProperty("chaseSpeed").floatValue = 0.9f;
        patrol.FindProperty("acceleration").floatValue = 2f;
        patrol.FindProperty("deceleration").floatValue = 4f;
        patrol.FindProperty("turnDuration").floatValue = 0.8f;
        patrol.FindProperty("sightRange").floatValue = 0f;
        patrol.FindProperty("usePatrolLimits").boolValue = true;
        patrol.FindProperty("patrolLimits").vector2Value = new Vector2(-5f, 5f);
        patrol.FindProperty("waitAtLimits").floatValue = 1.5f;
        patrol.FindProperty("flipTarget").objectReferenceValue = body;
        patrol.ApplyModifiedPropertiesWithoutUndo();

        AddObstacle(root, bounceOnTop: false, passable: true, hey, null, vendor);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Hand Cart.prefab");
    }

    // Flache Holzkiste mit einer Lage Obst
    private static void FruitCrate(Transform parent, Vector3 bottomCenter, Material fruit)
    {
        Transform crate = PrimitiveKit.Pivot("Fruit Crate", parent, bottomCenter);
        PrimitiveKit.Box("Crate", crate, new Vector3(0f, 0.08f, 0f), new Vector3(0.6f, 0.16f, 0.8f), wood);
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 4; z++)
            {
                PrimitiveKit.Part("Fruit", PrimitiveType.Sphere, crate, new Vector3(-0.18f + x * 0.18f, 0.18f, -0.28f + z * 0.18f), Vector3.one * 0.16f, fruit);
            }
        }
    }

    // Holzkisten-Stapel auf der Bahn: levels = 1 (0.8 m hoch) oder 2 (1.6 m). Plattform, seitlich = vom Board fallen
    private static void BuildCrates(string name, int levels)
    {
        const float crateHeight = 0.8f;
        var root = new GameObject(name);
        Transform t = root.transform;
        Material[] fruits =
        {
            Lit("Fruit Tomato", new Color(0.9f, 0.18f, 0.12f)), Lit("Fruit Lemon", new Color(1f, 0.88f, 0.25f)),
        };
        for (int level = 0; level < levels; level++)
        {
            float y = Surface + level * crateHeight;
            PrimitiveKit.Box("Crate", t, new Vector3(0f, y + crateHeight / 2f, 0f), new Vector3(1.1f, crateHeight - 0.02f, 0.9f), wood);
            // Dunkle Latten außen, damit man die einzelnen Kisten erkennt
            PrimitiveKit.Box("Slat", t, new Vector3(0f, y + 0.2f, -0.46f), new Vector3(1.12f, 0.08f, 0.02f), darkWood);
            PrimitiveKit.Box("Slat", t, new Vector3(0f, y + crateHeight - 0.2f, -0.46f), new Vector3(1.12f, 0.08f, 0.02f), darkWood);
        }
        // Obenauf eine Lage Obst (Optik, ragt 10 cm über den Collider)
        FruitCrate(t, new Vector3(0f, Surface + levels * crateHeight - 0.08f, 0f), fruits[levels - 1]);

        GameObject box = new GameObject("Hitbox") { layer = GroundLayer };
        box.transform.SetParent(t, false);
        PrimitiveKit.AddBoxCollider(box, new Vector3(0f, Surface + levels * crateHeight / 2f, 0f), new Vector3(1.1f, levels * crateHeight, 0.9f));
        var obstacle = new SerializedObject(box.AddComponent<CrowdObstacle>());
        obstacle.FindProperty("bounceOnTop").boolValue = false;
        obstacle.FindProperty("passableAfterCrash").boolValue = false;
        obstacle.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/{name}.prefab");
    }

    private static void AddKinematicBody(GameObject root)
    {
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.None;
    }

    private static void AddObstacle(GameObject target, bool bounceOnTop, bool passable, GameObject hey, GameObject whoa, params CrowdFigure[] duckers)
    {
        var obstacle = new SerializedObject(target.AddComponent<CrowdObstacle>());
        obstacle.FindProperty("bounceOnTop").boolValue = bounceOnTop;
        obstacle.FindProperty("passableAfterCrash").boolValue = passable;
        obstacle.FindProperty("heyBubble").objectReferenceValue = hey;
        obstacle.FindProperty("duckBubble").objectReferenceValue = whoa;
        PrimitiveKit.SetReferences(obstacle, "duckers", duckers);
        obstacle.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Jongleure (Hindernis quer über die Bahn) ----------

    // Einer steht vor der Bahn (Kameraseite, dreht ihr halb den Rücken zu), einer dahinter. Zwei Keulen fliegen flach
    // auf Brusthöhe hin und her. Treffer = vom Board fallen. Um den vorderen stehen ein paar Zuschauer
    private static void BuildJugglers()
    {
        Material clubWhite = white;
        Material clubRed = Lit("Club Red", new Color(0.85f, 0.15f, 0.2f), 0.5f);
        var root = new GameObject("Jugglers Across");
        Transform t = root.transform;
        const float frontZ = -3.2f, backZ = 2.6f, handHeight = 1.15f;
        CrowdFigure a = Person(t, "Juggler A", new Vector3(0.2f, Surface, frontZ), 170f, 0f);
        CrowdFigure b = Person(t, "Juggler B", new Vector3(-0.2f, Surface, backZ), 10f, 0f);
        // Narrenkappen in Rot, damit man die beiden als Artisten erkennt
        foreach (CrowdFigure juggler in new[] { a, b })
        {
            Transform rig = juggler.transform.Find("Rig");
            PrimitiveKit.Part("Jester Hat", PrimitiveType.Capsule, rig, new Vector3(0f, 1.62f, 0f), new Vector3(0.4f, 0.22f, 0.4f), clubRed);
        }
        Vector3 handA = new Vector3(0.2f, Surface + handHeight, frontZ + 0.45f);
        Vector3 handB = new Vector3(-0.2f, Surface + handHeight, backZ - 0.45f);

        var clubs = new Rigidbody[2];
        var visuals = new Transform[2];
        for (int i = 0; i < clubs.Length; i++)
        {
            var club = new GameObject("Club " + (i + 1));
            club.transform.SetParent(t, false);
            club.transform.localPosition = handA;
            var body = club.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var trigger = club.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.3f;
            club.AddComponent<CrashTrigger>();
            Transform visual = PrimitiveKit.Pivot("Spin", club.transform, Vector3.zero);
            PrimitiveKit.Part("Handle", PrimitiveType.Capsule, visual, new Vector3(0f, -0.12f, 0f), new Vector3(0.05f, 0.12f, 0.05f), clubWhite);
            PrimitiveKit.Part("Body", PrimitiveType.Capsule, visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.13f, 0.16f, 0.13f), clubWhite);
            PrimitiveKit.Part("Stripe", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.025f, 0.14f), clubRed);
            clubs[i] = body;
            visuals[i] = visual;
        }

        // Zuschauer um den vorderen Jongleur, schauen zu ihm (und damit Richtung Bahn)
        Person(t, "Spectator A", new Vector3(-1.4f, Surface, frontZ - 1.2f), 135f, 1f);
        Person(t, "Spectator B", new Vector3(1.6f, Surface, frontZ - 1.0f), 215f, 1f);
        Person(t, "Spectator C", new Vector3(0.4f, Surface, frontZ - 2.3f), 180f, 1f);

        var serialized = new SerializedObject(root.AddComponent<Jugglers>());
        PrimitiveKit.SetReferences(serialized, "clubs", clubs);
        PrimitiveKit.SetReferences(serialized, "clubVisuals", visuals);
        serialized.FindProperty("handA").vector3Value = handA;
        serialized.FindProperty("handB").vector3Value = handB;
        serialized.FindProperty("armA").objectReferenceValue = a.transform.Find("Rig/Arm Pivot R");
        serialized.FindProperty("armB").objectReferenceValue = b.transform.Find("Rig/Arm Pivot R");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Die Wurf-Arme bewegt Jugglers, nicht CrowdFigure: dort Gesten aus, Arme vorne
        foreach (CrowdFigure juggler in new[] { a, b })
        {
            var figure = new SerializedObject(juggler);
            figure.FindProperty("armsForward").floatValue = 70f;
            figure.ApplyModifiedPropertiesWithoutUndo();
        }
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Jugglers Across.prefab");
    }

    // ---------- Kulisse vor und hinter der Bahn ----------

    // Spaziergänger ohne Collider. Die Wurzel wandert auf der Bahn, die Figur steht seitlich versetzt (setzt der Level-
    // Generator: vorne am Wasser negativ, hinten zwischen den Ständen positiv)
    private static void BuildStroller()
    {
        var root = new GameObject("Stroller");
        CrowdFigure figure = Person(root.transform, "Figure", new Vector3(0f, Surface, -5f), -90f);
        var serialized = new SerializedObject(root.AddComponent<Stroller>());
        serialized.FindProperty("figure").objectReferenceValue = figure.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Stroller.prefab");
    }

    // Alte Hafenlaterne am Wasser-Rand des sichtbaren Bereichs (lokal z -6.5: so weit vorne, dass sie den Spieler nicht verdeckt)
    private static void BuildHarborLamp()
    {
        Material glow = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Lamp Glow.mat");
        var root = new GameObject("Harbor Lamp");
        Transform t = root.transform;
        const float z = -6.5f;
        PrimitiveKit.Part("Foot", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.2f, z), new Vector3(0.3f, 0.2f, 0.3f), iron);
        PrimitiveKit.Part("Pole", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 1.9f, z), new Vector3(0.12f, 1.7f, 0.12f), iron);
        PrimitiveKit.Box("Arm", t, new Vector3(0f, Surface + 3.5f, z), new Vector3(1.1f, 0.06f, 0.06f), iron);
        foreach (float x in new[] { -0.55f, 0.55f })
        {
            PrimitiveKit.Box("Lantern", t, new Vector3(x, Surface + 3.25f, z), new Vector3(0.26f, 0.38f, 0.26f), glow);
            PrimitiveKit.Box("Lantern Cap", t, new Vector3(x, Surface + 3.48f, z), new Vector3(0.34f, 0.08f, 0.34f), iron);
        }
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Harbor Lamp.prefab");
    }

    // Bank mit Blick aufs Wasser (Sitzende schauen also zur Kamera), zwei Leute darauf
    private static void BuildBench()
    {
        var root = new GameObject("Promenade Bench");
        Transform t = root.transform;
        const float z = -5.6f, seat = 0.45f;
        PrimitiveKit.Box("Seat", t, new Vector3(0f, Surface + seat, z), new Vector3(2f, 0.08f, 0.5f), darkWood);
        PrimitiveKit.Box("Back", t, new Vector3(0f, Surface + seat + 0.35f, z + 0.25f), new Vector3(2f, 0.4f, 0.06f), darkWood);
        foreach (float x in new[] { -0.85f, 0.85f })
        {
            PrimitiveKit.Box("Leg", t, new Vector3(x, Surface + seat / 2f, z), new Vector3(0.08f, seat, 0.45f), iron);
        }
        // Die Pillen-Figur hat keine Beine: Füße auf der Sitzfläche sieht aus wie sitzen
        Person(t, "Sitter A", new Vector3(-0.45f, Surface + seat + 0.04f, z), 0f, 0.6f);
        Person(t, "Sitter B", new Vector3(0.5f, Surface + seat + 0.04f, z), -15f, 0.6f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Promenade Bench.prefab");
    }

    // Akkordeonspieler im Profil, Zuschauer im Halbkreis neben ihm (alle in derselben Tiefe vor der Bahn)
    private static void BuildMusician()
    {
        Material bellows = Lit("Accordion Red", new Color(0.75f, 0.1f, 0.12f), 0.5f);
        Material keys = white;
        var root = new GameObject("Street Musician");
        Transform t = root.transform;
        const float z = -4.8f;
        CrowdFigure musician = Person(t, "Musician", new Vector3(-1.2f, Surface, z), -70f, 0f, 60f);
        Transform rig = musician.transform.Find("Rig");
        // Akkordeon vor der Brust, der Balg geht auf und zu (Squeeze)
        Transform accordion = PrimitiveKit.Pivot("Accordion", rig, new Vector3(0f, 0.75f, -0.38f));
        PrimitiveKit.Box("Keys", accordion, new Vector3(-0.22f, 0f, 0f), new Vector3(0.08f, 0.38f, 0.28f), keys);
        PrimitiveKit.Box("Bellows", accordion, Vector3.zero, new Vector3(0.36f, 0.34f, 0.26f), bellows);
        PrimitiveKit.Box("Buttons", accordion, new Vector3(0.22f, 0f, 0f), new Vector3(0.08f, 0.38f, 0.28f), darkWood);
        accordion.gameObject.AddComponent<Squeeze>();
        // Hut für Münzen vor ihm
        PrimitiveKit.Part("Hat", PrimitiveType.Cylinder, t, new Vector3(-0.6f, Surface + 0.05f, z - 0.3f), new Vector3(0.35f, 0.05f, 0.35f), Lit("Hat Black", new Color(0.12f, 0.12f, 0.14f)));
        // Zuschauer schauen zu ihm (nach -X), wiegen sich und klatschen
        Person(t, "Listener A", new Vector3(0.4f, Surface, z + 0.6f), 110f, 1f);
        Person(t, "Listener B", new Vector3(0.7f, Surface, z - 0.5f), 75f, 1f);
        Person(t, "Listener C", new Vector3(1.5f, Surface, z + 0.1f), 95f, 1f);
        Person(t, "Listener D", new Vector3(0.1f, Surface, z - 1.4f), 45f, 1f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Street Musician.prefab");
    }

    // Eiswagen: weiß-rosa Kasten mit Schirm, Eisverkäufer dahinter (zur Kamera), zwei Kunden
    private static void BuildIceCreamCart()
    {
        Material pink = Lit("Ice Cream Pink", new Color(0.97f, 0.62f, 0.75f));
        Material mint = Lit("Ice Cream Mint", new Color(0.6f, 0.9f, 0.75f));
        var root = new GameObject("Ice Cream Cart");
        Transform t = root.transform;
        const float z = -5.4f;
        PrimitiveKit.Box("Cart", t, new Vector3(0f, Surface + 0.55f, z), new Vector3(1.4f, 0.8f, 0.8f), white);
        PrimitiveKit.Box("Cart Stripe", t, new Vector3(0f, Surface + 0.55f, z - 0.41f), new Vector3(1.4f, 0.2f, 0.02f), pink);
        foreach (float x in new[] { -0.5f, 0.5f })
        {
            Renderer wheel = PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, t, new Vector3(x, Surface + 0.18f, z - 0.42f), new Vector3(0.36f, 0.03f, 0.36f), iron);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        // Drei Eiskugeln-Attrappen oben drauf und ein Sonnenschirm
        Material[] scoops = { pink, mint, Lit("Ice Cream Vanilla", new Color(0.98f, 0.92f, 0.7f)) };
        for (int i = 0; i < 3; i++)
        {
            PrimitiveKit.Part("Scoop", PrimitiveType.Sphere, t, new Vector3(-0.4f + i * 0.4f, Surface + 1.05f, z), Vector3.one * 0.25f, scoops[i]);
        }
        PrimitiveKit.Box("Umbrella Pole", t, new Vector3(0.55f, Surface + 1.6f, z + 0.3f), new Vector3(0.05f, 2.2f, 0.05f), iron);
        PrimitiveKit.Part("Umbrella", PrimitiveType.Sphere, t, new Vector3(0.55f, Surface + 2.65f, z + 0.3f), new Vector3(1.8f, 0.35f, 1.8f), pink);
        Person(t, "Ice Cream Vendor", new Vector3(0f, Surface, z + 0.7f), 0f, 0.6f);
        Person(t, "Ice Cream Kid", new Vector3(-1.2f, Surface, z - 0.3f), 60f, 1f).transform.localScale = Vector3.one * 0.7f;
        Person(t, "Ice Cream Customer", new Vector3(1.3f, Surface, z - 0.4f), -60f, 0.8f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Ice Cream Cart.prefab");
    }

    // Luftballonverkäufer mit einem Strauß bunter Ballons (wiegen sich im Wind, Squeeze an den Ballons)
    private static void BuildBalloonSeller()
    {
        Color[] colors =
        {
            new Color(0.95f, 0.2f, 0.25f), new Color(0.2f, 0.5f, 0.95f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 0.4f),
            new Color(0.75f, 0.35f, 0.9f), new Color(1f, 0.55f, 0.15f), new Color(0.95f, 0.5f, 0.75f),
        };
        var root = new GameObject("Balloon Seller");
        Transform t = root.transform;
        const float z = -5.6f;
        CrowdFigure seller = Person(t, "Balloon Seller", new Vector3(0f, Surface, z), 20f, 0.3f);
        // Strauß hängt an der rechten Hand, schwankt als Ganzes
        Transform bunch = PrimitiveKit.Pivot("Balloons", t, new Vector3(0.45f, Surface + 1.0f, z));
        Material line = Lit("Balloon String", new Color(0.9f, 0.9f, 0.9f));
        for (int i = 0; i < colors.Length; i++)
        {
            float angle = i / (float)colors.Length * Mathf.PI * 2f;
            Vector3 top = new Vector3(Mathf.Cos(angle) * 0.35f, 1.9f + (i % 3) * 0.25f, Mathf.Sin(angle) * 0.3f);
            Renderer str = PrimitiveKit.Box("String", bunch, top / 2f, new Vector3(0.01f, top.magnitude, 0.01f), line);
            str.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top.normalized);
            PrimitiveKit.Part("Balloon", PrimitiveType.Sphere, bunch, top + Vector3.up * 0.2f, new Vector3(0.38f, 0.46f, 0.38f), Lit("Balloon " + (i + 1), colors[i], 0.6f));
        }
        var sway = new SerializedObject(bunch.gameObject.AddComponent<Squeeze>());
        sway.FindProperty("mode").enumValueIndex = (int)Squeeze.Mode.Sway;
        sway.FindProperty("speed").floatValue = 0.35f;
        sway.FindProperty("amount").floatValue = 6f;
        sway.ApplyModifiedPropertiesWithoutUndo();
        Person(t, "Balloon Kid", new Vector3(1.0f, Surface, z + 0.2f), -50f, 1f).transform.localScale = Vector3.one * 0.7f;
        Person(t, "Balloon Parent", new Vector3(1.6f, Surface, z - 0.3f), -40f, 0.4f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Balloon Seller.prefab");
    }

    // Kleiner Limonadenstand: Holzkiste als Theke, gelbes Schild, ein Kind verkauft, ein Kunde trinkt
    private static void BuildLemonadeStand()
    {
        Material lemon = Lit("Fruit Lemon", new Color(1f, 0.88f, 0.25f));
        var root = new GameObject("Lemonade Stand");
        Transform t = root.transform;
        const float z = -5.4f;
        PrimitiveKit.Box("Counter", t, new Vector3(0f, Surface + 0.45f, z), new Vector3(1.2f, 0.9f, 0.6f), wood);
        PrimitiveKit.Box("Sign Board", t, new Vector3(0f, Surface + 0.55f, z - 0.31f), new Vector3(1.0f, 0.35f, 0.02f), lemon);
        var text = PrimitiveKit.Text("Sign Text", t, new Vector3(0f, Surface + 0.55f, z - 0.33f), new Vector2(0.95f, 0.3f), "LEMONADE", new Color(0.55f, 0.35f, 0.05f));
        text.fontSizeMax = 3f;
        PrimitiveKit.Part("Jug", PrimitiveType.Cylinder, t, new Vector3(-0.3f, Surface + 1.05f, z), new Vector3(0.18f, 0.15f, 0.18f), lemon);
        for (int i = 0; i < 3; i++)
        {
            PrimitiveKit.Part("Cup", PrimitiveType.Cylinder, t, new Vector3(0.05f + i * 0.15f, Surface + 0.96f, z), new Vector3(0.08f, 0.06f, 0.08f), white);
        }
        Person(t, "Lemonade Kid", new Vector3(0f, Surface, z + 0.7f), 0f, 1f).transform.localScale = Vector3.one * 0.72f;
        Person(t, "Lemonade Customer", new Vector3(1.1f, Surface, z - 0.3f), -50f, 0.6f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Lemonade Stand.prefab");
    }

    // ---------- Skate-Strecke (Oregano, Crust Street) ----------

    // Holzrampe vor einer Baugrube: 4 m lang, steigt in Laufrichtung (lokal -X) auf 0.6 m an, die Kante liegt bei lokal x -2
    // (= Grubenkante). Begehbar (Layer Ground), an der Kante ein Trigger mit KickerRamp
    private static void BuildKicker()
    {
        Material plank = Lit("Ramp Plank", new Color(0.66f, 0.48f, 0.28f));
        Material stripe = Lit("Hazard Yellow", new Color(1f, 0.82f, 0.1f));
        var root = new GameObject("Kicker Ramp");
        Transform t = root.transform;
        const float length = 4f, height = 0.6f;
        float angle = Mathf.Atan2(height, length) * Mathf.Rad2Deg;
        float slope = Mathf.Sqrt(length * length + height * height);
        // Sichtbar ist ein einziger Keil (eigenes Mesh): schräge Oberseite, dreieckige Seiten, senkrechte Rückwand an der
        // Kante. Vorher waren die Seiten rechteckige Klötze, die im flachen Teil durch die Schräge ragten
        var body = new GameObject("Wedge");
        body.transform.SetParent(t, false);
        body.transform.localPosition = new Vector3(0f, Surface, 0f);
        body.AddComponent<MeshFilter>().sharedMesh = KickerMesh(length, height, 2f);
        body.AddComponent<MeshRenderer>().sharedMaterial = plank;
        // Collider: schräge Platte, um Z gedreht (negativer Winkel hebt das -X-Ende an). Ihre Oberseite liegt auf der des Keils
        var deck = new GameObject("Deck") { layer = GroundLayer };
        deck.transform.SetParent(t, false);
        deck.transform.localPosition = new Vector3(0f, Surface + height / 2f - 0.1f, 0f);
        deck.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        PrimitiveKit.Box("Lip Stripe", deck.transform, new Vector3(-slope / 2f + 0.2f, 0.105f, 0f), new Vector3(0.4f, 0.01f, 2f), stripe);
        PrimitiveKit.AddBoxCollider(deck, Vector3.zero, new Vector3(slope, 0.2f, 2f));
        // Auslöser an der Kante: der Spieler fährt die Schräge hinauf und hinein
        var lip = new GameObject("Launch Trigger");
        lip.transform.SetParent(t, false);
        // Schmal und genau an der Kante: Löste er früher aus, flöge er von weiter hinten ab und käme zu kurz
        PrimitiveKit.AddBoxCollider(lip, new Vector3(-length / 2f + 0.25f, Surface + height + 0.8f, 0f), new Vector3(0.5f, 1.6f, 2f), true);
        lip.AddComponent<KickerRamp>();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Kicker Ramp.prefab");
    }

    // Keil (Dreiecksprisma), Ursprung unten in der Mitte: flaches Ende bei x = +length/2, hohes bei -length/2, Breite in Z.
    // Jede Fläche bekommt eigene Ecken, damit RecalculateNormals harte Kanten macht. Gespeichert als Asset (ein Prefab kann
    // kein Mesh referenzieren, das nur im Speicher existiert); beim erneuten Bauen wird dasselbe Asset überschrieben
    private static Mesh KickerMesh(float length, float height, float width)
    {
        PrimitiveKit.EnsureFolder("Assets", "Meshes");
        const string path = "Assets/Meshes/Kicker Ramp.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Kicker Ramp" };
            AssetDatabase.CreateAsset(mesh, path);
        }

        float x = length / 2f, z = width / 2f;
        Vector3 lowFront = new Vector3(x, 0f, -z), lowBack = new Vector3(x, 0f, z);
        Vector3 topFront = new Vector3(-x, height, -z), topBack = new Vector3(-x, height, z);
        Vector3 footFront = new Vector3(-x, 0f, -z), footBack = new Vector3(-x, 0f, z);

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        // Unity zeigt die Seite, von der aus die Ecken im Uhrzeigersinn liegen; die Normale ist dann Cross(b - a, c - a).
        // Zeigt sie nicht nach außen, werden zwei Ecken getauscht
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                (b, c) = (c, b);
            }
            foreach (Vector3 corner in new[] { a, b, c })
            {
                triangles.Add(vertices.Count);
                vertices.Add(corner);
            }
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Triangle(a, b, c, outward);
            Triangle(a, c, d, outward);
        }
        Quad(lowFront, lowBack, topBack, topFront, new Vector3(height, length, 0f));   // Schräge
        Quad(topFront, topBack, footBack, footFront, Vector3.left);                     // Rückwand an der Kante
        Quad(lowFront, footFront, footBack, lowBack, Vector3.down);                     // Unterseite
        Triangle(lowFront, topFront, footFront, Vector3.back);                          // Seite zur Kamera
        Triangle(lowBack, topBack, footBack, Vector3.forward);                          // Seite zum Park

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // Poller auf der Bahn zum Drüberspringen (0.9 m). Auf dem Board seitlich angefahren = vom Board fallen (wie auf dem Markt)
    private static void BuildBollard()
    {
        Material red = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Pizzeria Red.mat");
        var root = new GameObject("Street Bollard") { layer = GroundLayer };
        Transform t = root.transform;
        PrimitiveKit.Part("Post", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.45f, 0f), new Vector3(0.28f, 0.45f, 0.28f), iron);
        PrimitiveKit.Part("Band", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.72f, 0f), new Vector3(0.3f, 0.06f, 0.3f), red);
        PrimitiveKit.Part("Cap", PrimitiveType.Sphere, t, new Vector3(0f, Surface + 0.9f, 0f), new Vector3(0.28f, 0.1f, 0.28f), iron);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 0.45f, 0f), new Vector3(0.4f, 0.9f, 0.4f));
        var obstacle = new SerializedObject(root.AddComponent<CrowdObstacle>());
        obstacle.FindProperty("bounceOnTop").boolValue = false;
        obstacle.FindProperty("passableAfterCrash").boolValue = false;
        obstacle.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Street Bollard.prefab");
    }

    // ---------- Oregano Street vor dem Board-Shop: Kinder auf Skateboards ----------

    // Skateboard aus Grundformen (Deck, Rollen) unter den Füßen, lokal entlang X. Gibt den Drehpunkt zurück (Boden)
    private static Transform KidBoard(Transform parent, Vector3 at, Material deck)
    {
        Transform board = PrimitiveKit.Pivot("Board", parent, at);
        PrimitiveKit.Box("Deck", board, new Vector3(0f, 0.09f, 0f), new Vector3(0.62f, 0.03f, 0.2f), deck);
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            foreach (float z in new[] { -0.08f, 0.08f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, board, new Vector3(x, 0.035f, z), new Vector3(0.06f, 0.02f, 0.06f), white)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        return board;
    }

    // Kind auf dem Skateboard, rollt mit GroundPatrol auf dem Gehweg hin und her (Grenzen setzt der Level-Generator).
    // Wie der Rollschuhfahrer in Level 3 (Skater): seitlich = Respawn, von oben = abfedern, das Kind rudert ("WHOA!")
    private static void BuildSkaterKid()
    {
        const float feet = -0.5f, boardHeight = 0.11f, scale = 0.78f;
        var root = new GameObject("Skater Kid");
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, feet + 0.7f, 0f), new Vector3(0.7f, 1.4f, 0.6f));
        AddKinematicBody(root);

        Transform body = PrimitiveKit.Pivot("Body", t, Vector3.zero);
        KidBoard(body, new Vector3(0f, feet, 0f), Lit("Kid Board Green", new Color(0.3f, 0.75f, 0.35f)));
        ParkSetup.Figure f = ParkSetup.BuildFigure(body, "Figure", new Vector3(0f, feet + boardHeight, 0f), -90f);
        f.root.localScale = Vector3.one * scale;
        Material figureMaterial = f.head.sharedMaterial;
        Renderer helmet = PrimitiveKit.Part("Helmet", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.45f, 0.02f), new Vector3(0.5f, 0.36f, 0.52f), figureMaterial);
        // Basecap verkehrt herum (Schirm hinten) statt Visier: Kind, kein Rollschuh-Profi
        Renderer cap = PrimitiveKit.Part("Cap Brim", PrimitiveType.Cylinder, f.rig, new Vector3(0f, 1.45f, 0.24f), new Vector3(0.28f, 0.012f, 0.22f), figureMaterial);
        GameObject whoa = ParkSetup.SpeechBubble(t, new Vector3(0.4f, feet + 1.9f, -0.3f), "WHOA!");

        var patrol = new SerializedObject(root.AddComponent<GroundPatrol>());
        patrol.FindProperty("patrolSpeed").floatValue = 3.5f;
        patrol.FindProperty("chaseSpeed").floatValue = 3.5f;
        patrol.FindProperty("acceleration").floatValue = 6f;
        patrol.FindProperty("deceleration").floatValue = 8f;
        patrol.FindProperty("turnDuration").floatValue = 0.4f;
        patrol.FindProperty("sightRange").floatValue = 0f;
        patrol.FindProperty("usePatrolLimits").boolValue = true;
        patrol.FindProperty("waitAtLimits").floatValue = 0.3f;
        patrol.FindProperty("flipTarget").objectReferenceValue = body;
        patrol.ApplyModifiedPropertiesWithoutUndo();

        var skater = new SerializedObject(root.AddComponent<Skater>());
        skater.FindProperty("rig").objectReferenceValue = f.rig;
        skater.FindProperty("leftArm").objectReferenceValue = f.leftArm;
        skater.FindProperty("rightArm").objectReferenceValue = f.rightArm;
        skater.FindProperty("whoaBubble").objectReferenceValue = whoa;
        skater.FindProperty("fullSpeed").floatValue = 3.5f;
        skater.FindProperty("strideLength").floatValue = 2.5f;
        skater.FindProperty("shirtColor").colorValue = new Color(0.95f, 0.45f, 0.15f);
        skater.FindProperty("helmetColor").colorValue = new Color(0.2f, 0.35f, 0.75f);
        PrimitiveKit.SetReferences(skater, "shirtRenderers", f.body);
        PrimitiveKit.SetReferences(skater, "skinRenderers", f.head, f.leftArmMesh, f.rightArmMesh);
        PrimitiveKit.SetReferences(skater, "helmetRenderers", helmet, cap);
        skater.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Skater Kid.prefab");
    }

    // Skate-Spot auf einem leeren Grundstück hinter der Bahn (lokal z 3 … 17, x -7 … 7; der Level-Generator schneidet die
    // Häuser dort aus): Betonboden, niedriger Zaun zur Straße mit Lücke, Quarterpipe am +X-Ende, Rail, bemalte Rückwand,
    // drei Kinder (SkateSpotKid: Quarterpipe, Ollies, Zuschauer). Reine Kulisse ohne Collider
    private static void BuildSkateSpot()
    {
        Material concrete = Lit("Skate Concrete", new Color(0.7f, 0.7f, 0.68f));
        Material ramp = Lit("Ramp Wood", new Color(0.62f, 0.45f, 0.28f));
        Material rail = Lit("Rail Steel", new Color(0.75f, 0.77f, 0.8f), 0.7f);
        Color[] paint = { new Color(0.95f, 0.3f, 0.5f), new Color(0.2f, 0.75f, 0.85f), new Color(1f, 0.82f, 0.15f), new Color(0.5f, 0.35f, 0.85f), new Color(0.3f, 0.8f, 0.4f) };
        var root = new GameObject("Skate Spot");
        Transform t = root.transform;
        const float z0 = 3.05f, z1 = 17f, x0 = -7f, x1 = 7f;
        PrimitiveKit.Box("Floor", t, new Vector3(0f, Surface - 0.2f + 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.4f, z1 - z0), concrete);

        // Zaun zur Straße: Pfosten und oberer Holm, mit einer Lücke in der Mitte (Einfahrt)
        for (float x = x0 + 0.1f; x <= x1; x += 2f)
        {
            if (Mathf.Abs(x) < 1.5f) continue;
            PrimitiveKit.Box("Fence Post", t, new Vector3(x, Surface + 0.6f, z0 + 0.1f), new Vector3(0.06f, 1.2f, 0.06f), iron);
        }
        PrimitiveKit.Box("Fence Rail", t, new Vector3((x0 - 1.4f) / 2f, Surface + 1.15f, z0 + 0.1f), new Vector3(-1.4f - x0, 0.05f, 0.05f), iron);
        PrimitiveKit.Box("Fence Rail", t, new Vector3((x1 + 1.4f) / 2f, Surface + 1.15f, z0 + 0.1f), new Vector3(x1 - 1.4f, 0.05f, 0.05f), iron);

        // Bemalte Rückwand (Graffiti als bunte Flächen)
        PrimitiveKit.Box("Back Wall", t, new Vector3(0f, Surface + 1.6f, z1 - 0.2f), new Vector3(x1 - x0, 3.2f, 0.3f), concrete);
        for (int i = 0; i < 7; i++)
        {
            Renderer tag = PrimitiveKit.Box("Graffiti", t, new Vector3(x0 + 1f + i * 2f, Surface + 1.2f + (i % 3) * 0.5f, z1 - 0.37f),
                new Vector3(1.6f, 0.7f + (i % 2) * 0.4f, 0.02f), Lit("Graffiti " + (i % paint.Length + 1), paint[i % paint.Length]));
            tag.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 8f : -6f));
        }

        // Quarterpipe am +X-Ende: Viertelkreis aus schmalen Segmenten, darüber ein Deck mit Kante
        const float radius = 2.4f, rampX = 3.6f, rampZ = 9f, rampWidth = 5f;
        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * 80f / segments * Mathf.Deg2Rad, a1 = (i + 1) * 80f / segments * Mathf.Deg2Rad;
            Vector3 p0 = new Vector3(rampX + Mathf.Sin(a0) * radius, Surface + radius - Mathf.Cos(a0) * radius, rampZ);
            Vector3 p1 = new Vector3(rampX + Mathf.Sin(a1) * radius, Surface + radius - Mathf.Cos(a1) * radius, rampZ);
            Renderer segment = PrimitiveKit.Box("Ramp Segment", t, (p0 + p1) / 2f, new Vector3(Vector3.Distance(p0, p1) + 0.05f, 0.08f, rampWidth), ramp);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg);
        }
        float top = Surface + radius - Mathf.Cos(80f * Mathf.Deg2Rad) * radius;
        PrimitiveKit.Box("Ramp Back", t, new Vector3(rampX + radius + 0.4f, (Surface + top) / 2f, rampZ), new Vector3(0.8f, top - Surface, rampWidth), ramp);
        PrimitiveKit.Box("Ramp Deck", t, new Vector3(rampX + radius + 0.4f, top + 0.04f, rampZ), new Vector3(1f, 0.08f, rampWidth), ramp);
        PrimitiveKit.Box("Coping", t, new Vector3(rampX + Mathf.Sin(80f * Mathf.Deg2Rad) * radius, top, rampZ), new Vector3(0.08f, 0.08f, rampWidth), rail);

        // Rail auf zwei Füßen, schräg im Bild
        PrimitiveKit.Box("Rail", t, new Vector3(-2.5f, Surface + 0.45f, 13f), new Vector3(4f, 0.06f, 0.06f), rail);
        foreach (float x in new[] { -4.2f, -0.8f })
        {
            PrimitiveKit.Box("Rail Foot", t, new Vector3(x, Surface + 0.22f, 13f), new Vector3(0.06f, 0.45f, 0.06f), rail);
        }

        SpotKid(t, "Ramp Kid", new Vector3(0f, Surface, rampZ - 1f), SkateSpotKid.Trick.QuarterPipe, new Color(0.2f, 0.6f, 0.9f), rampX, radius);
        SpotKid(t, "Ollie Kid", new Vector3(-3f, Surface, 11f), SkateSpotKid.Trick.Ollies, new Color(0.95f, 0.8f, 0.2f), rampX, radius);
        SpotKid(t, "Watching Kid", new Vector3(rampX + radius + 0.5f, top + 0.08f, rampZ + 1.2f), SkateSpotKid.Trick.Watching, new Color(0.9f, 0.3f, 0.35f), rampX, radius);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Skate Spot.prefab");
    }

    // Ein Kind im Skate-Spot: Wurzel = Fußpunkt (SkateSpotKid bewegt sie), darin Board und Figur (0.75 groß)
    private static void SpotKid(Transform parent, string name, Vector3 at, SkateSpotKid.Trick trick, Color shirt, float rampX, float radius)
    {
        Transform kid = PrimitiveKit.Pivot(name, parent, at);
        Transform board = trick == SkateSpotKid.Trick.Watching ? null : KidBoard(kid, Vector3.zero, Lit("Kid Board " + name, shirt * 0.8f));
        ParkSetup.Figure f = ParkSetup.BuildFigure(kid, "Figure", new Vector3(0f, trick == SkateSpotKid.Trick.Watching ? 0f : 0.11f, 0f), 0f);
        f.root.localScale = Vector3.one * 0.75f;
        // Eigene Materialien statt MaterialPropertyBlock: Ein im Editor gesetzter Block wird nicht im Prefab gespeichert (die
        // Figuren mit eigenem Skript setzen ihn zur Laufzeit, diese Kinder haben keins dafür)
        f.body.sharedMaterial = Lit("Kid Shirt " + name, shirt);
        Material skin = Lit("Kid Skin", new Color(0.92f, 0.74f, 0.6f));
        f.head.sharedMaterial = skin;
        f.leftArmMesh.sharedMaterial = skin;
        f.rightArmMesh.sharedMaterial = skin;
        var script = new SerializedObject(kid.gameObject.AddComponent<SkateSpotKid>());
        script.FindProperty("trick").enumValueIndex = (int)trick;
        script.FindProperty("rig").objectReferenceValue = f.root;
        script.FindProperty("leftArm").objectReferenceValue = f.leftArm;
        script.FindProperty("rightArm").objectReferenceValue = f.rightArm;
        script.FindProperty("board").objectReferenceValue = board;
        // Weg relativ zur Startposition des Kinds: Rampenfuß bei rampX (Spot-Koordinaten) minus Startposition
        script.FindProperty("rampStartX").floatValue = rampX - at.x;
        script.FindProperty("flatFromX").floatValue = -4f - at.x;
        script.FindProperty("rampRadius").floatValue = radius;
        script.ApplyModifiedPropertiesWithoutUndo();
    }

    // Plakat an einer Hausfassade (lokal z 2.96, knapp davor): "OLLIE'S BOARD SHOP" mit Pfeil in Laufrichtung (lokal -X)
    private static void BuildShopPoster()
    {
        Material orange = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Board Shop Orange.mat");
        Material teal = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Board Shop Teal.mat");
        var root = new GameObject("Shop Poster");
        Transform t = root.transform;
        const float z = 2.95f, y = Surface + 2.1f;
        PrimitiveKit.Box("Poster", t, new Vector3(0f, y, z), new Vector3(2.2f, 1.4f, 0.03f), teal);
        PrimitiveKit.Box("Frame", t, new Vector3(0f, y, z + 0.01f), new Vector3(2.35f, 1.55f, 0.02f), white);
        var title = PrimitiveKit.Text("Title", t, new Vector3(0.15f, y + 0.25f, z - 0.03f), new Vector2(1.7f, 0.5f), "OLLIE'S", new Color(1f, 0.6f, 0.15f));
        title.fontSizeMax = 6f;
        var subtitle = PrimitiveKit.Text("Subtitle", t, new Vector3(0.15f, y - 0.2f, z - 0.03f), new Vector2(1.7f, 0.35f), "BOARD SHOP  AHEAD", Color.white);
        subtitle.fontSizeMax = 3f;
        // Pfeil nach -X (Laufrichtung): Schaft und zwei schräge Spitzen
        PrimitiveKit.Box("Arrow Shaft", t, new Vector3(-0.75f, y - 0.5f, z - 0.03f), new Vector3(0.5f, 0.08f, 0.01f), orange);
        foreach (float angle in new[] { 40f, -40f })
        {
            Renderer tip = PrimitiveKit.Box("Arrow Tip", t, new Vector3(-0.93f, y - 0.5f + (angle > 0f ? 0.07f : -0.07f), z - 0.035f), new Vector3(0.22f, 0.07f, 0.01f), orange);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Shop Poster.prefab");
    }

    // ---------- Marktstände (Kulisse hinter der Bahn) ----------

    // Tisch mit Tuch, vier Stangen, gestreifte Markise, Händler dahinter, ein, zwei Kunden davor. goods legt die Ware auf
    // den Tisch (Tischplatte bei y, Mitte bei z)
    private static void BuildStall(string name, Material stripe, System.Action<Transform, float, float> goods)
    {
        var root = new GameObject(name);
        StallFrame(root.transform, stripe, StallFrontZ - 0.3f, 2.3f, 2.8f);
        goods(root.transform, Surface + 0.9f, StallFrontZ + 0.45f);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/{name}.prefab");
    }

    // Gerüst eines Stands. awningFrontZ: Vorderkante der Markise, frontY/backY: Höhe vorne/hinten (über dem Boden)
    private static void StallFrame(Transform t, Material stripe, float awningFrontZ, float frontY, float backY)
    {
        const float tableHeight = 0.9f, tableDepth = 0.9f, backZ = StallFrontZ + 2.2f;
        float tableZ = StallFrontZ + tableDepth / 2f;
        PrimitiveKit.Box("Table", t, new Vector3(0f, Surface + tableHeight - 0.03f, tableZ), new Vector3(StallWidth - 0.2f, 0.06f, tableDepth), darkWood);
        PrimitiveKit.Box("Table Cloth", t, new Vector3(0f, Surface + tableHeight / 2f, StallFrontZ - 0.01f), new Vector3(StallWidth - 0.2f, tableHeight - 0.06f, 0.02f), cloth);
        PrimitiveKit.Box("Cloth Stripe", t, new Vector3(0f, Surface + tableHeight - 0.15f, StallFrontZ - 0.025f), new Vector3(StallWidth - 0.2f, 0.12f, 0.01f), stripe);

        foreach (float x in new[] { -StallWidth / 2f, StallWidth / 2f })
        {
            PrimitiveKit.Box("Pole Front", t, new Vector3(x, Surface + frontY / 2f, StallFrontZ - 0.1f), new Vector3(0.06f, frontY, 0.06f), pole);
            PrimitiveKit.Box("Pole Back", t, new Vector3(x, Surface + backY / 2f, backZ), new Vector3(0.06f, backY, 0.06f), pole);
        }

        // Markise: Streifen quer zur Bahn, von hinten oben nach vorne unten geneigt, vorne ein Volant
        float depth = backZ - awningFrontZ;
        float slope = Mathf.Atan2(backY - frontY, depth) * Mathf.Rad2Deg;
        Transform awning = PrimitiveKit.Pivot("Awning", t, new Vector3(0f, Surface + (frontY + backY) / 2f + 0.05f, (awningFrontZ + backZ) / 2f));
        awning.localRotation = Quaternion.Euler(-slope, 0f, 0f);
        float length = Mathf.Sqrt(depth * depth + (backY - frontY) * (backY - frontY)) + 0.2f;
        int stripes = Mathf.RoundToInt((StallWidth + 0.4f) / 0.4f);
        for (int i = 0; i < stripes; i++)
        {
            float x = -(StallWidth + 0.4f) / 2f + 0.2f + i * 0.4f;
            Material m = i % 2 == 0 ? stripe : white;
            PrimitiveKit.Box("Stripe", awning, new Vector3(x, 0f, 0f), new Vector3(0.4f, 0.06f, length), m);
            PrimitiveKit.Box("Valance", awning, new Vector3(x, -0.13f, -length / 2f), new Vector3(0.4f, 0.26f, 0.03f), m);
        }

        // Händler hinter dem Tisch (schaut zur Kamera), zwei Kunden davor (schauen auf den Stand, halb von der Kamera weg)
        Person(t, "Vendor", new Vector3(0.3f, Surface, StallFrontZ + 1.4f), 0f);
        Person(t, "Shopper A", new Vector3(-0.7f, Surface, StallFrontZ - 0.8f), 160f);
        Person(t, "Shopper B", new Vector3(0.9f, Surface, StallFrontZ - 1.1f), 200f, 0.5f);
        Person(t, "Shopper C", new Vector3(0.1f, Surface, StallFrontZ - 0.6f), 175f, 0.3f);
        Person(t, "Shopper D", new Vector3(-1.5f, Surface, StallFrontZ - 1.3f), 120f, 0.8f);
    }

    private static void FoodGoods(Transform t, float top, float z)
    {
        Material tomato = Lit("Fruit Tomato", new Color(0.9f, 0.18f, 0.12f));
        Material lemon = Lit("Fruit Lemon", new Color(1f, 0.88f, 0.25f));
        Material cheese = Lit("Cheese", new Color(0.98f, 0.85f, 0.45f));
        Material salami = Lit("Salami", new Color(0.55f, 0.18f, 0.15f));
        // Tomaten-Pyramide, Zitronen, Käselaibe, Basilikum-Töpfe
        for (int row = 0; row < 3; row++)
        {
            for (int i = 0; i < 4 - row; i++)
            {
                PrimitiveKit.Part("Tomato", PrimitiveType.Sphere, t, new Vector3(-1.1f + i * 0.17f + row * 0.085f, top + 0.08f + row * 0.13f, z - 0.1f), Vector3.one * 0.16f, tomato);
                PrimitiveKit.Part("Tomato", PrimitiveType.Sphere, t, new Vector3(-1.1f + i * 0.17f + row * 0.085f, top + 0.08f + row * 0.13f, z + 0.08f), Vector3.one * 0.16f, tomato);
            }
        }
        for (int i = 0; i < 5; i++)
        {
            PrimitiveKit.Part("Lemon", PrimitiveType.Sphere, t, new Vector3(-0.3f + (i % 3) * 0.15f, top + 0.07f + (i / 3) * 0.12f, z), new Vector3(0.14f, 0.12f, 0.12f), lemon);
        }
        PrimitiveKit.Part("Cheese Wheel", PrimitiveType.Cylinder, t, new Vector3(0.45f, top + 0.08f, z), new Vector3(0.45f, 0.08f, 0.45f), cheese);
        PrimitiveKit.Part("Cheese Wheel", PrimitiveType.Cylinder, t, new Vector3(0.45f, top + 0.24f, z), new Vector3(0.42f, 0.08f, 0.42f), cheese);
        foreach (float x in new[] { 0.95f, 1.2f })
        {
            PrimitiveKit.Part("Basil Pot", PrimitiveType.Cylinder, t, new Vector3(x, top + 0.09f, z + 0.15f), new Vector3(0.16f, 0.09f, 0.16f), terracotta);
            PrimitiveKit.Part("Basil", PrimitiveType.Sphere, t, new Vector3(x, top + 0.25f, z + 0.15f), Vector3.one * 0.24f, leaf);
        }
        // Salamis hängen vorne an der Markise
        for (int i = 0; i < 5; i++)
        {
            PrimitiveKit.Part("Salami", PrimitiveType.Capsule, t, new Vector3(-0.9f + i * 0.45f, Surface + 1.85f, StallFrontZ - 0.05f), new Vector3(0.09f, 0.18f, 0.09f), salami);
        }
    }

    private static void FishGoods(Transform t, float top, float z)
    {
        Material orange = Lit("Fish Salmon", new Color(0.98f, 0.55f, 0.4f), 0.6f);
        Material mussel = Lit("Mussel", new Color(0.12f, 0.12f, 0.18f), 0.7f);
        PrimitiveKit.Box("Ice", t, new Vector3(-0.35f, top + 0.05f, z), new Vector3(2f, 0.1f, 0.8f), ice);
        for (int i = 0; i < 6; i++)
        {
            for (int row = 0; row < 2; row++)
            {
                Renderer fish = PrimitiveKit.Part("Fish", PrimitiveType.Capsule, t, new Vector3(-1.1f + i * 0.3f, top + 0.13f, z - 0.18f + row * 0.36f),
                    new Vector3(0.12f, 0.16f, 0.07f), (i + row) % 4 == 0 ? orange : silver);
                fish.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        PrimitiveKit.Box("Mussel Crate", t, new Vector3(1.05f, top + 0.1f, z), new Vector3(0.6f, 0.2f, 0.6f), wood);
        for (int i = 0; i < 9; i++)
        {
            PrimitiveKit.Part("Mussel", PrimitiveType.Sphere, t, new Vector3(0.88f + (i % 3) * 0.17f, top + 0.22f, z - 0.17f + (i / 3) * 0.17f), new Vector3(0.13f, 0.07f, 0.09f), mussel);
        }
    }

    private static void FlowerGoods(Transform t, float top, float z)
    {
        Color[] blooms =
        {
            new Color(0.95f, 0.35f, 0.55f), new Color(1f, 0.85f, 0.2f), new Color(0.85f, 0.15f, 0.2f), new Color(0.6f, 0.4f, 0.85f), new Color(0.97f, 0.97f, 0.95f),
        };
        Material bucket = Lit("Flower Bucket", new Color(0.2f, 0.32f, 0.3f), 0.5f);
        for (int i = 0; i < 5; i++)
        {
            float x = -1.1f + i * 0.55f;
            Material bloom = Lit("Bloom " + (i + 1), blooms[i]);
            PrimitiveKit.Part("Bucket", PrimitiveType.Cylinder, t, new Vector3(x, top + 0.15f, z), new Vector3(0.32f, 0.15f, 0.32f), bucket);
            PrimitiveKit.Part("Stems", PrimitiveType.Cylinder, t, new Vector3(x, top + 0.42f, z), new Vector3(0.2f, 0.15f, 0.2f), leaf);
            for (int b = 0; b < 5; b++)
            {
                float angle = b * 72f * Mathf.Deg2Rad;
                PrimitiveKit.Part("Bloom", PrimitiveType.Sphere, t, new Vector3(x + Mathf.Cos(angle) * 0.1f, top + 0.6f + (b % 2) * 0.06f, z + Mathf.Sin(angle) * 0.1f), Vector3.one * 0.13f, bloom);
            }
        }
    }

    private static void FleaGoods(Transform t, float top, float z)
    {
        Color[] covers = { new Color(0.7f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.6f), new Color(0.3f, 0.5f, 0.3f), new Color(0.85f, 0.7f, 0.3f) };
        // Bücherstapel
        for (int stack = 0; stack < 2; stack++)
        {
            for (int i = 0; i < 4; i++)
            {
                PrimitiveKit.Box("Book", t, new Vector3(-1.05f + stack * 0.45f, top + 0.04f + i * 0.08f, z), new Vector3(0.32f - (i % 2) * 0.04f, 0.07f, 0.24f),
                    Lit("Book " + ((i + stack) % 4 + 1), covers[(i + stack) % 4]));
            }
        }
        // Stehlampe mit Schirm, Vase, Bilderrahmen, altes Radio
        PrimitiveKit.Part("Lamp Foot", PrimitiveType.Cylinder, t, new Vector3(0.05f, top + 0.02f, z), new Vector3(0.18f, 0.02f, 0.18f), iron);
        PrimitiveKit.Box("Lamp Stem", t, new Vector3(0.05f, top + 0.25f, z), new Vector3(0.03f, 0.46f, 0.03f), iron);
        PrimitiveKit.Part("Lamp Shade", PrimitiveType.Cylinder, t, new Vector3(0.05f, top + 0.52f, z), new Vector3(0.3f, 0.1f, 0.3f), Lit("Lamp Shade", new Color(0.95f, 0.8f, 0.55f)));
        PrimitiveKit.Part("Vase", PrimitiveType.Capsule, t, new Vector3(0.45f, top + 0.15f, z + 0.1f), new Vector3(0.16f, 0.15f, 0.16f), Lit("Vase Blue", new Color(0.25f, 0.45f, 0.75f), 0.7f));
        Transform frame = PrimitiveKit.Pivot("Picture Frame", t, new Vector3(0.8f, top + 0.22f, z + 0.2f));
        frame.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        PrimitiveKit.Box("Frame", frame, Vector3.zero, new Vector3(0.4f, 0.32f, 0.03f), Lit("Gold Frame", new Color(0.8f, 0.62f, 0.25f), 0.6f));
        PrimitiveKit.Box("Painting", frame, new Vector3(0f, 0f, -0.02f), new Vector3(0.32f, 0.24f, 0.01f), Lit("Painting", new Color(0.35f, 0.55f, 0.7f)));
        PrimitiveKit.Box("Radio", t, new Vector3(1.15f, top + 0.12f, z), new Vector3(0.36f, 0.24f, 0.18f), darkWood);
        PrimitiveKit.Part("Speaker", PrimitiveType.Cylinder, t, new Vector3(1.1f, top + 0.12f, z - 0.095f), new Vector3(0.14f, 0.005f, 0.14f), cloth)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    // Souvenirstand für die Piazza Pisa (steht in der Stadt, nicht auf dem Markt): Mini-Pizzatürme, Postkarten, Kappen.
    // Eigener Menüpunkt, damit man ihn bauen kann, ohne die übrigen Markt-Prefabs neu zu erzeugen
    [MenuItem("Tools/Jump and Run/City/Build Souvenir Stall Prefab")]
    public static void BuildSouvenirStall()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Market");
        CreateMaterials();
        BuildStall("Market Stall Souvenir", Lit("Market Yellow", new Color(0.95f, 0.75f, 0.15f)), SouvenirGoods);
        AssetDatabase.SaveAssets();
    }

    private static void SouvenirGoods(Transform t, float top, float z)
    {
        Material dough = Lit("Pizza Crust", new Color(0.85f, 0.62f, 0.33f));
        Material cheese = Lit("Pizza Cheese", new Color(0.98f, 0.82f, 0.36f), 0.3f);
        Material marble = Lit("Marble", new Color(0.93f, 0.91f, 0.86f), 0.3f);
        // Drei Mini-Türme: Teig-Säule mit Käse-Scheiben, schief wie das Original
        for (int i = 0; i < 3; i++)
        {
            Transform tower = PrimitiveKit.Pivot("Mini Tower", t, new Vector3(-1.05f + i * 0.35f, top, z));
            tower.localRotation = Quaternion.Euler(0f, 0f, -6f);
            PrimitiveKit.Cylinder("Base", tower, new Vector3(0f, 0.02f, 0f), 0.09f, 0.04f, marble);
            PrimitiveKit.Cylinder("Core", tower, new Vector3(0f, 0.22f, 0f), 0.05f, 0.4f, dough);
            for (int tier = 0; tier < 4; tier++)
            {
                PrimitiveKit.Cylinder("Tier", tower, new Vector3(0f, 0.1f + tier * 0.1f, 0f), 0.08f, 0.025f, cheese);
            }
        }
        // Postkartenständer: drei Reihen bunter Karten auf einer schrägen Tafel
        Color[] cards = { new Color(0.3f, 0.6f, 0.9f), new Color(0.95f, 0.7f, 0.3f), new Color(0.9f, 0.4f, 0.4f), new Color(0.4f, 0.75f, 0.5f) };
        Transform rack = PrimitiveKit.Pivot("Postcard Rack", t, new Vector3(0.1f, top + 0.3f, z + 0.15f));
        rack.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        PrimitiveKit.Box("Board", rack, Vector3.zero, new Vector3(0.7f, 0.55f, 0.03f), darkWood);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                PrimitiveKit.Box("Postcard", rack, new Vector3(-0.22f + col * 0.22f, 0.17f - row * 0.17f, -0.02f), new Vector3(0.18f, 0.13f, 0.01f),
                    Lit("Postcard " + ((row + col) % 4 + 1), cards[(row + col) % 4]));
            }
        }
        // Kappen in den Pizzeria-Farben
        Material red = Lit("Market Red", new Color(0.82f, 0.16f, 0.12f));
        for (int i = 0; i < 3; i++)
        {
            Vector3 at = new Vector3(0.7f + i * 0.25f, top + 0.06f, z - (i % 2) * 0.15f);
            PrimitiveKit.Part("Cap", PrimitiveType.Sphere, t, at, new Vector3(0.2f, 0.12f, 0.2f), i == 1 ? white : red);
            PrimitiveKit.Box("Brim", t, at + new Vector3(0f, -0.04f, -0.12f), new Vector3(0.16f, 0.02f, 0.1f), i == 1 ? red : white);
        }
    }

    // Stand, dessen Markise weit über die Bahn ragt: ein Trampolin (BounceAwning). Unter der Markise fährt man durch
    // (Unterkante vorne ~2.1 m über dem Weg), von oben federt man ~5 m hoch. Die Markise hängt vorne frei (Ausleger von
    // den hinteren Stangen), es steht also nichts auf der Bahn
    private static void BuildBouncyStall()
    {
        Material stripe = Lit("Market Orange", new Color(0.98f, 0.55f, 0.15f));
        var root = new GameObject("Bouncy Market Stall");
        Transform t = root.transform;
        StallFrame(t, Lit("Market Red", new Color(0.82f, 0.16f, 0.12f)), StallFrontZ - 0.3f, 2.3f, 2.8f);
        FoodGoods(t, Surface + 0.9f, StallFrontZ + 0.45f);

        // Ausleger-Markise über der Bahn: von lokal z +2.6 bis -1.6, Oberkante vorne 2.4, hinten 2.7 über dem Weg
        const float frontZ = -1.6f, backZ = 2.6f, frontTop = 2.4f, backTop = 2.7f;
        float depth = backZ - frontZ;
        float slope = Mathf.Atan2(backTop - frontTop, depth) * Mathf.Rad2Deg;
        var canopy = new GameObject("Bouncy Canopy") { layer = GroundLayer };
        canopy.transform.SetParent(t, false);
        canopy.transform.localPosition = new Vector3(0f, Surface + (frontTop + backTop) / 2f - 0.05f, (frontZ + backZ) / 2f);
        canopy.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
        float length = Mathf.Sqrt(depth * depth + (backTop - frontTop) * (backTop - frontTop));
        Transform fabric = PrimitiveKit.Pivot("Canvas", canopy.transform, Vector3.zero);
        int stripes = 8;
        for (int i = 0; i < stripes; i++)
        {
            float x = -1.6f + 0.2f + i * 0.4f;
            Material m = i % 2 == 0 ? stripe : white;
            PrimitiveKit.Box("Stripe", fabric, new Vector3(x, 0f, 0f), new Vector3(0.4f, 0.1f, length), m);
            PrimitiveKit.Box("Valance", fabric, new Vector3(x, -0.15f, -length / 2f), new Vector3(0.4f, 0.3f, 0.03f), m);
        }
        PrimitiveKit.AddBoxCollider(canopy, Vector3.zero, new Vector3(3.2f, 0.1f, length));
        // Ausleger: zwei schräge Streben von den hinteren Stangen nach vorne
        foreach (float x in new[] { -1.55f, 1.55f })
        {
            Vector3 from = new Vector3(x, Surface + 2.8f, StallFrontZ + 2.2f);
            Vector3 to = new Vector3(x, Surface + frontTop - 0.1f, frontZ + 0.2f);
            Renderer strut = PrimitiveKit.Box("Strut", t, (from + to) / 2f, new Vector3(0.06f, 0.06f, Vector3.Distance(from, to)), pole);
            strut.transform.localRotation = Quaternion.LookRotation(to - from);
        }
        var bounce = new SerializedObject(canopy.AddComponent<BounceAwning>());
        bounce.FindProperty("canvas").objectReferenceValue = fabric;
        bounce.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Bouncy Market Stall.prefab");
    }
}
