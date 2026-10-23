using TMPro;
using UnityEditor;
using UnityEngine;

// Baut alle Stadt-Prefabs (Assets/Prefabs/City) aus Grundformen: Gehweg, Baugrube, Gebäude, Pizzeria, Café,
// Balkon, federnde Markise, Bank, Container, Gondel, Bäume, Laternen, Hydrant, Mülleimer, Skyline, Wolken.
// Menü: Tools > Jump and Run > City > Build City Prefabs. Mehrfach ausführbar, Prefabs werden ersetzt.
//
// Konvention (wegen PrefabGridEnforcer, der Prefab-Wurzeln auf ganze Meter und Z = 0 einrastet):
// Jede Wurzel liegt bei Z = 0 auf der Mitte einer 1 m dicken Bodenplatte, deren Oberkante bei +0.5 liegt.
// Inhalte, die woanders stehen sollen (Fassaden hinten bei Z = 3, Skyline bei Z = 30), sind im Prefab verschoben.
public static class CityPropsSetup
{
    public const string PrefabFolder = "Assets/Prefabs/City";
    private const string MaterialFolder = "Assets/Materials/City";
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    private const int GroundLayer = 3;

    // Oberkante des Gehwegs (relativ zur Wurzel) und Vorderkante der Hausfassaden
    public const float Surface = 0.5f;
    public const float FacadeZ = 3f;

    // Gebäude-Varianten: Name, Breite, Höhe (über dem Gehweg), Fassadenfarbe
    public static readonly (string name, float width, float height, Color color)[] BuildingTypes =
    {
        ("Building Brick", 6f, 10f, new Color(0.62f, 0.3f, 0.22f)),
        ("Building Beige", 8f, 14f, new Color(0.86f, 0.77f, 0.6f)),
        ("Building Gray", 6f, 18f, new Color(0.56f, 0.57f, 0.6f)),
        ("Building Teal", 10f, 8f, new Color(0.36f, 0.6f, 0.6f)),
        ("Building Yellow", 8f, 11f, new Color(0.9f, 0.76f, 0.42f)),
        ("Building Pink", 6f, 13f, new Color(0.86f, 0.58f, 0.58f))
    };

    private static Material concrete, curb, foundation, asphalt, roadLine, pit, iron, windowDark, windowLit,
        doorWood, interior, signWhite, red, white, green, cream, darkGreen, beige, brick, barrierOrange,
        trunk, leaves, leavesLight, lampGlow, hydrantRed, binGray, skyline, skylineLight, cloud, gondolaYellow, sill;

    [MenuItem("Tools/Jump and Run/City/Build City Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "City");
        PrimitiveKit.EnsureFolder("Assets/Materials", "City");
        CreateMaterials();

        BuildSidewalk();
        BuildConstructionPit();
        BuildBarrier();
        foreach (var type in BuildingTypes)
        {
            BuildBuilding(type.name, type.width, type.height, type.color);
        }
        BuildPizzeria();
        BuildCoffeeShop();
        BuildKiosk();
        BuildBalcony();
        BuildAwning();
        BuildBench();
        BuildDumpster();
        BuildGondola();
        BuildTree();
        BuildStreetLamp();
        BuildHydrant();
        BuildTrashCan();
        BuildSkyline();
        BuildCloud();

        AssetDatabase.SaveAssets();
        Debug.Log("CityPropsSetup: Stadt-Prefabs gebaut.");
    }

    // Öffentlich, damit andere Tools (CustomerSetup) Gebäudeteile mit denselben Materialien bauen können
    public static void CreateMaterials()
    {
        Material Lit(string name, Color color, float smoothness = 0.15f) =>
            PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);
        // Unlit für alles, was "leuchtet" (erleuchtete Fenster, Laternen, Neon): unabhängig vom Licht immer hell
        Material Unlit(string name, Color color) =>
            PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.UnlitShader, color);

        concrete = Lit("Sidewalk", new Color(0.74f, 0.73f, 0.7f));
        curb = Lit("Curb", new Color(0.56f, 0.56f, 0.56f));
        foundation = Lit("Foundation", new Color(0.36f, 0.34f, 0.32f));
        asphalt = Lit("Road", new Color(0.2f, 0.2f, 0.22f));
        roadLine = Lit("Road Line", new Color(0.95f, 0.85f, 0.4f));
        pit = Lit("Pit", new Color(0.12f, 0.1f, 0.08f));
        iron = Lit("Iron", new Color(0.14f, 0.14f, 0.16f), 0.5f);
        windowDark = Lit("Window Glass", new Color(0.28f, 0.38f, 0.52f), 0.8f);
        windowLit = Unlit("Window Lit", new Color(1f, 0.86f, 0.55f));
        doorWood = Lit("Door Wood", new Color(0.42f, 0.25f, 0.13f));
        interior = Unlit("Interior Dark", new Color(0.07f, 0.05f, 0.04f));
        signWhite = Lit("Sign White", new Color(0.97f, 0.96f, 0.92f));
        red = Lit("Pizzeria Red", new Color(0.8f, 0.13f, 0.1f));
        white = Lit("Awning White", new Color(0.97f, 0.97f, 0.95f));
        green = Lit("Coffee Green", new Color(0.1f, 0.42f, 0.28f));
        darkGreen = Lit("Coffee Dark", new Color(0.12f, 0.24f, 0.18f));
        cream = Lit("Cream", new Color(0.95f, 0.9f, 0.8f));
        beige = Lit("Beige", new Color(0.86f, 0.78f, 0.62f));
        brick = Lit("Brick", new Color(0.6f, 0.28f, 0.2f));
        barrierOrange = Lit("Barrier Orange", new Color(0.98f, 0.45f, 0.08f));
        trunk = Lit("Tree Trunk", new Color(0.38f, 0.26f, 0.16f));
        leaves = Lit("Leaves", new Color(0.24f, 0.55f, 0.25f));
        leavesLight = Lit("Leaves Light", new Color(0.36f, 0.66f, 0.3f));
        lampGlow = Unlit("Lamp Glow", new Color(1f, 0.92f, 0.65f));
        hydrantRed = Lit("Hydrant Red", new Color(0.85f, 0.12f, 0.1f), 0.4f);
        binGray = Lit("Bin", new Color(0.32f, 0.4f, 0.36f));
        // Skyline weit hinten: blasses Blaugrau, verschwimmt zusätzlich im Nebel
        skyline = Lit("Skyline", new Color(0.55f, 0.6f, 0.7f));
        skylineLight = Lit("Skyline Light", new Color(0.72f, 0.77f, 0.85f));
        cloud = Unlit("Cloud", new Color(0.96f, 0.97f, 1f));
        gondolaYellow = Lit("Gondola Yellow", new Color(0.95f, 0.76f, 0.16f));
        sill = Lit("Window Sill", new Color(0.85f, 0.84f, 0.8f));
    }

    private static string PathFor(string name) => $"{PrefabFolder}/{name}.prefab";

    // ---------- Boden ----------

    // 1 m langes Gehwegstück: Wurzel per scale.x auf die gewünschte Länge ziehen. Vorne Bordstein und Straße (nur Optik),
    // nach unten ein Fundament, damit man unter dem Gehweg nicht ins Leere schaut
    private static void BuildSidewalk()
    {
        var root = new GameObject("Sidewalk");
        root.layer = GroundLayer;
        Transform t = root.transform;
        PrimitiveKit.Box("Top", t, new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 5f), concrete);
        PrimitiveKit.Box("Curb", t, new Vector3(0f, 0.38f, -1.9f), new Vector3(1f, 0.25f, 0.25f), curb);
        PrimitiveKit.Box("Foundation", t, new Vector3(0f, -3.25f, 0.5f), new Vector3(1f, 5.5f, 5f), foundation);
        PrimitiveKit.Box("Road", t, new Vector3(0f, 0.15f, -8f), new Vector3(1f, 0.3f, 12f), asphalt);
        PrimitiveKit.Box("Road Line", t, new Vector3(0f, 0.305f, -8f), new Vector3(1f, 0.01f, 0.15f), roadLine);
        PrimitiveKit.Box("Road Foundation", t, new Vector3(0f, -3.1f, -8f), new Vector3(1f, 6.2f, 12f), foundation);
        // Nur die Gehweg-Platte ist begehbar. Collider an der Wurzel, damit er mit scale.x mitwächst
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 5f));
        PrimitiveKit.SavePrefab(root, PathFor("Sidewalk"));
    }

    // Baugrube für die Lücken zwischen Gehwegen. Auch per scale.x auf die Lückenbreite ziehen.
    // Der Boden liegt tief (y = -4), damit man die Fundament-Wände der Gehwege runtergehen sieht und es nach Loch aussieht.
    // Kein Collider: Wer hineinfällt, stirbt in der KillZone knapp darüber (y = -3), also noch sichtbar
    private static void BuildConstructionPit()
    {
        var root = new GameObject("Construction Pit");
        Transform t = root.transform;
        PrimitiveKit.Box("Floor", t, new Vector3(0f, -5.5f, -5.5f), new Vector3(1f, 3f, 17f), pit);
        Material dirt = PrimitiveKit.Material($"{MaterialFolder}/Dirt.mat", PrimitiveKit.LitShader, new Color(0.35f, 0.25f, 0.16f));
        PrimitiveKit.Box("Back Wall", t, new Vector3(0f, -1.75f, FacadeZ - 0.2f), new Vector3(1f, 4.5f, 0.4f), dirt);
        PrimitiveKit.Box("Dirt", t, new Vector3(0f, -3.95f, -5.5f), new Vector3(1f, 0.1f, 17f), dirt);
        PrimitiveKit.SavePrefab(root, PathFor("Construction Pit"));
    }

    // Absperrung an der Kante einer Baugrube, hinter der Laufbahn (blockiert nichts)
    private static void BuildBarrier()
    {
        var root = new GameObject("Barrier");
        Transform t = root.transform;
        float z = 1.6f;
        PrimitiveKit.Box("Post L", t, new Vector3(-0.55f, Surface + 0.5f, z), new Vector3(0.08f, 1f, 0.08f), iron);
        PrimitiveKit.Box("Post R", t, new Vector3(0.55f, Surface + 0.5f, z), new Vector3(0.08f, 1f, 0.08f), iron);
        for (int i = 0; i < 4; i++)
        {
            // Abwechselnd orange und weiß gestreift
            PrimitiveKit.Box("Stripe " + (i + 1), t, new Vector3(-0.375f + i * 0.25f, Surface + 0.85f, z - 0.05f), new Vector3(0.25f, 0.25f, 0.04f), i % 2 == 0 ? barrierOrange : white);
        }
        PrimitiveKit.Part("Cone", PrimitiveType.Cylinder, t, new Vector3(0.9f, Surface + 0.25f, z - 0.6f), new Vector3(0.3f, 0.25f, 0.3f), barrierOrange);
        PrimitiveKit.SavePrefab(root, PathFor("Barrier"));
    }

    // ---------- Gebäude ----------

    private static void BuildBuilding(string name, float width, float height, Color color)
    {
        Material facade = PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, 0.1f);
        Material groundFloor = PrimitiveKit.Material($"{MaterialFolder}/{name} Ground Floor.mat", PrimitiveKit.LitShader, color * 0.7f, 0.1f);

        var root = new GameObject(name);
        Transform t = root.transform;
        float top = Surface + height;
        // Bis tief nach unten, damit man unter dem Gehweg kein Loch sieht
        PrimitiveKit.Box("Body", t, new Vector3(0f, (top - 12f) / 2f, FacadeZ + 4f), new Vector3(width, top + 12f, 8f), facade);
        PrimitiveKit.Box("Ground Floor", t, new Vector3(0f, Surface + 1.6f, FacadeZ - 0.06f), new Vector3(width, 3.2f, 0.12f), groundFloor);
        PrimitiveKit.Quad("Shop Window", t, new Vector3(-width * 0.15f, Surface + 1.5f, FacadeZ - 0.13f), new Vector2(width * 0.5f, 1.7f), Hash(name, 0, 0) % 2 == 0 ? windowLit : windowDark);
        PrimitiveKit.Box("Door", t, new Vector3(width * 0.32f, Surface + 1.1f, FacadeZ - 0.15f), new Vector3(1.1f, 2.2f, 0.06f), doorWood);
        AddWindowGrid(t, name, width, Surface + 4.6f, top - 1.2f);
        PrimitiveKit.Box("Cornice", t, new Vector3(0f, top + 0.2f, FacadeZ + 0.15f), new Vector3(width + 0.3f, 0.4f, 0.6f), sill);
        // Etwas Kram auf dem Dach
        PrimitiveKit.Box("AC Unit", t, new Vector3(width * 0.25f, top + 0.5f, FacadeZ + 2f), new Vector3(1.2f, 0.8f, 1f), curb);
        if (height > 12f)
        {
            PrimitiveKit.Part("Water Tank", PrimitiveType.Cylinder, t, new Vector3(-width * 0.2f, top + 1.2f, FacadeZ + 3f), new Vector3(1.4f, 1.2f, 1.4f), doorWood);
        }
        PrimitiveKit.SavePrefab(root, PathFor(name));
    }

    // Fensterraster: Spalten im Abstand von etwa 2 m, Etagen alle 3 m. Manche Fenster leuchten (pseudo-zufällig, aber fest)
    private static void AddWindowGrid(Transform parent, string seed, float width, float fromY, float toY)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((width - 1f) / 2f));
        float spacing = width / columns;
        int row = 0;
        for (float y = fromY; y + 0.75f <= toY; y += 3f, row++)
        {
            for (int column = 0; column < columns; column++)
            {
                float x = -width / 2f + spacing * (column + 0.5f);
                bool lit = Hash(seed, row, column) % 4 == 0;
                PrimitiveKit.Quad($"Window {row}-{column}", parent, new Vector3(x, y, FacadeZ - 0.01f), new Vector2(1.1f, 1.5f), lit ? windowLit : windowDark);
                PrimitiveKit.Box($"Sill {row}-{column}", parent, new Vector3(x, y - 0.8f, FacadeZ - 0.08f), new Vector3(1.3f, 0.1f, 0.16f), sill);
            }
        }
    }

    private static int Hash(string seed, int a, int b)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in seed)
            {
                h = h * 31 + c;
            }
            h = h * 31 + a * 7919;
            h = h * 31 + b * 104729;
            return Mathf.Abs(h);
        }
    }

    // Gebäude mit echter Türöffnung (für SpawnDoor): Erdgeschoss aus Wandstücken links/rechts/über der Tür,
    // dahinter ein dunkler Raum. Der Spieler startet in diesem Raum und läuft durch die Öffnung heraus
    public static SpawnDoor BuildDoorBuilding(Transform root, float width, float height, Material groundFloor, Material upper)
    {
        const float doorWidth = 1.4f;
        const float doorHeight = 2.2f;
        const float floorHeight = 3.2f;
        float top = Surface + height;
        float sideWidth = width / 2f - doorWidth / 2f;

        PrimitiveKit.Box("Upper Body", root, new Vector3(0f, (Surface + floorHeight + top) / 2f, FacadeZ + 4f), new Vector3(width, top - Surface - floorHeight, 8f), upper);
        PrimitiveKit.Box("Foundation", root, new Vector3(0f, (Surface - 12f) / 2f, FacadeZ + 4f), new Vector3(width, Surface + 12f, 8f), foundation);
        PrimitiveKit.Box("Wall Left", root, new Vector3(-width / 2f + sideWidth / 2f, Surface + floorHeight / 2f, FacadeZ + 4f), new Vector3(sideWidth, floorHeight, 8f), groundFloor);
        PrimitiveKit.Box("Wall Right", root, new Vector3(width / 2f - sideWidth / 2f, Surface + floorHeight / 2f, FacadeZ + 4f), new Vector3(sideWidth, floorHeight, 8f), groundFloor);
        PrimitiveKit.Box("Lintel", root, new Vector3(0f, Surface + doorHeight + (floorHeight - doorHeight) / 2f, FacadeZ + 4f), new Vector3(doorWidth, floorHeight - doorHeight, 8f), groundFloor);
        // Dunkler Raum hinter der Tür, leicht zurückgesetzt, damit die Öffnung Tiefe hat
        PrimitiveKit.Box("Interior", root, new Vector3(0f, Surface + doorHeight / 2f, FacadeZ + 1.4f), new Vector3(doorWidth, doorHeight, 2.4f), interior);
        PrimitiveKit.Box("Back Wall", root, new Vector3(0f, Surface + doorHeight / 2f, FacadeZ + 5.4f), new Vector3(doorWidth, doorHeight, 5.2f), groundFloor);

        // Türblatt: Drehpunkt am linken Scharnier, das Blatt reicht nach rechts. Dreht nach innen auf (+Z)
        Transform hinge = PrimitiveKit.Pivot("Door Hinge", root, new Vector3(-doorWidth / 2f, Surface, FacadeZ - 0.05f));
        PrimitiveKit.Box("Door Leaf", hinge, new Vector3(doorWidth / 2f, doorHeight / 2f, 0f), new Vector3(doorWidth, doorHeight, 0.08f), doorWood);
        PrimitiveKit.Part("Door Knob", PrimitiveType.Sphere, hinge, new Vector3(doorWidth - 0.15f, doorHeight / 2f, -0.06f), Vector3.one * 0.09f, lampGlow);

        // Spieler-Mitte: Gehweg + Kugelradius (0.5). Drinnen im dunklen Raum, draußen auf der Laufbahn etwas rechts der Tür
        Transform inside = PrimitiveKit.Pivot("Inside Point", root, new Vector3(0f, Surface + 0.5f, FacadeZ + 1.3f));
        Transform outside = PrimitiveKit.Pivot("Outside Point", root, new Vector3(0.5f, Surface + 0.5f, 0f));

        var door = root.gameObject.AddComponent<SpawnDoor>();
        var serialized = new SerializedObject(door);
        serialized.FindProperty("insidePoint").objectReferenceValue = inside;
        serialized.FindProperty("outsidePoint").objectReferenceValue = outside;
        serialized.FindProperty("doorLeaf").objectReferenceValue = hinge;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    // Gestreifte Markise über einer Tür (nur Optik). Für die federnde Variante siehe BuildAwning
    private static void AddDecorativeAwning(Transform parent, Vector3 center, float width, Material a, Material b)
    {
        Transform awning = PrimitiveKit.Pivot("Awning", parent, center);
        awning.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        int stripes = Mathf.RoundToInt(width / 0.5f);
        for (int i = 0; i < stripes; i++)
        {
            PrimitiveKit.Box("Stripe " + (i + 1), awning, new Vector3(-width / 2f + 0.25f + i * 0.5f, 0f, 0f), new Vector3(0.5f, 0.1f, 1.6f), i % 2 == 0 ? a : b);
            PrimitiveKit.Box("Valance " + (i + 1), awning, new Vector3(-width / 2f + 0.25f + i * 0.5f, -0.15f, -0.8f), new Vector3(0.5f, 0.3f, 0.04f), i % 2 == 0 ? a : b);
        }
    }

    private static void BuildPizzeria()
    {
        // Gerade Breite, damit die Kanten auf ganzen Metern liegen und Nachbarhäuser bündig anschließen
        const float width = 10f;
        const float height = 8f;
        var root = new GameObject("Pizzeria");
        Transform t = root.transform;
        BuildDoorBuilding(t, width, height, red, cream);

        AddDecorativeAwning(t, new Vector3(0f, Surface + 3.0f, FacadeZ - 0.8f), 4f, red, white);
        PrimitiveKit.Quad("Shop Window L", t, new Vector3(-2.9f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(2.2f, 1.7f), windowLit);
        PrimitiveKit.Quad("Shop Window R", t, new Vector3(2.9f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(2.2f, 1.7f), windowLit);

        // Großes Schild über dem Eingang
        PrimitiveKit.Box("Sign Board", t, new Vector3(0f, Surface + 4.4f, FacadeZ - 0.1f), new Vector3(5.6f, 1.2f, 0.15f), signWhite);
        PrimitiveKit.Text("Sign Text", t, new Vector3(0f, Surface + 4.4f, FacadeZ - 0.2f), new Vector2(5.2f, 1f), "PIZZERIA", new Color(0.8f, 0.13f, 0.1f));
        AddWindowGrid(t, "Pizzeria", width, Surface + 6.2f, Surface + height - 0.6f);
        PrimitiveKit.Box("Cornice", t, new Vector3(0f, Surface + height + 0.2f, FacadeZ + 0.15f), new Vector3(width + 0.3f, 0.4f, 0.6f), red);
        // Schornstein vom Pizzaofen
        PrimitiveKit.Box("Chimney", t, new Vector3(width / 2f - 1.5f, Surface + height + 1.2f, FacadeZ + 2f), new Vector3(0.9f, 2.4f, 0.9f), brick);
        PrimitiveKit.SavePrefab(root, PathFor("Pizzeria"));
    }

    // Café = Checkpoint: Trigger vor der Tür. Beim Erreichen leuchtet das "OPEN"-Schild, danach respawnt man hier
    private static void BuildCoffeeShop()
    {
        const float width = 8f;
        const float height = 9f;
        var root = new GameObject("Coffee Shop");
        Transform t = root.transform;
        SpawnDoor door = BuildDoorBuilding(t, width, height, darkGreen, beige);

        AddDecorativeAwning(t, new Vector3(0f, Surface + 3.0f, FacadeZ - 0.8f), 4f, green, white);
        Renderer windowLeft = PrimitiveKit.Quad("Shop Window L", t, new Vector3(-2.5f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(1.8f, 1.7f), windowDark);
        Renderer windowRight = PrimitiveKit.Quad("Shop Window R", t, new Vector3(2.5f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(1.8f, 1.7f), windowDark);
        PrimitiveKit.Box("Sign Board", t, new Vector3(0f, Surface + 4.4f, FacadeZ - 0.1f), new Vector3(5f, 1.2f, 0.15f), green);
        TextMeshPro signText = PrimitiveKit.Text("Sign Text", t, new Vector3(0f, Surface + 4.4f, FacadeZ - 0.2f), new Vector2(4.6f, 1f), "COFFEE", new Color(0.97f, 0.96f, 0.92f));
        AddWindowGrid(t, "Coffee", width, Surface + 6.2f, Surface + height - 0.6f);
        PrimitiveKit.Box("Cornice", t, new Vector3(0f, Surface + height + 0.2f, FacadeZ + 0.15f), new Vector3(width + 0.3f, 0.4f, 0.6f), green);

        // Neon-Schild im Fenster: aus (grau) bis zum Checkpoint, dann an (pink). TMP-Text ist ohnehin unbeleuchtet,
        // also "leuchtet" er von selbst, die Farbe reicht
        GameObject neonOff = PrimitiveKit.Text("Open Sign Off", t, new Vector3(2.5f, Surface + 1.6f, FacadeZ - 0.05f), new Vector2(1.6f, 0.6f), "OPEN", new Color(0.3f, 0.3f, 0.3f)).gameObject;
        GameObject neonOn = PrimitiveKit.Text("Open Sign On", t, new Vector3(2.5f, Surface + 1.6f, FacadeZ - 0.05f), new Vector2(1.6f, 0.6f), "OPEN", new Color(1f, 0.35f, 0.6f)).gameObject;
        neonOn.SetActive(false);

        // "CLOSED"-Schildchen am Türblatt: hängt am Scharnier, dreht beim Öffnen also mit
        Transform hinge = t.Find("Door Hinge");
        Transform closedSign = PrimitiveKit.Pivot("Closed Sign", hinge, new Vector3(0.7f, 1.55f, -0.06f));
        PrimitiveKit.Box("Board", closedSign, Vector3.zero, new Vector3(0.8f, 0.32f, 0.03f), signWhite);
        PrimitiveKit.Text("Text", closedSign, new Vector3(0f, 0f, -0.02f), new Vector2(0.72f, 0.26f), "CLOSED", new Color(0.8f, 0.13f, 0.1f));

        Transform chalkboard = BuildChalkboard(t, new Vector3(-2.3f, Surface, 1.3f));
        Transform cup = BuildCoffeeCup(t, new Vector3(0.5f, Surface + 3.3f, 0f));
        ParticleSystem sparkles = BuildSparkles(t);

        PrimitiveKit.AddBoxCollider(root, new Vector3(0.5f, Surface + 1.5f, 0f), new Vector3(1.5f, 3f, 3f), true);
        var checkpoint = root.AddComponent<Checkpoint>();
        var serialized = new SerializedObject(checkpoint);
        serialized.FindProperty("door").objectReferenceValue = door;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var effects = root.AddComponent<CoffeeShopEffects>();
        var fx = new SerializedObject(effects);
        PrimitiveKit.SetReferences(fx, "shopWindows", windowLeft, windowRight);
        fx.FindProperty("windowsClosed").objectReferenceValue = windowDark;
        fx.FindProperty("windowsOpen").objectReferenceValue = windowLit;
        fx.FindProperty("signText").objectReferenceValue = signText;
        fx.FindProperty("closedDoorSign").objectReferenceValue = closedSign.gameObject;
        fx.FindProperty("neonOff").objectReferenceValue = neonOff;
        fx.FindProperty("neonOn").objectReferenceValue = neonOn;
        fx.FindProperty("chalkboard").objectReferenceValue = chalkboard;
        fx.FindProperty("cupHint").objectReferenceValue = cup;
        fx.FindProperty("sparkles").objectReferenceValue = sparkles;
        fx.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, PathFor("Coffee Shop"));
    }

    // Gehweg-Tafel: vorne "CLOSED", hinten "OPEN" (um 180° gedreht, damit es nach dem Umklappen richtig herum steht).
    // Gibt den Drehpunkt zurück, den CoffeeShopEffects beim Aktivieren um 180° dreht
    private static Transform BuildChalkboard(Transform parent, Vector3 position)
    {
        Material slate = PrimitiveKit.Material($"{MaterialFolder}/Chalkboard.mat", PrimitiveKit.LitShader, new Color(0.12f, 0.16f, 0.14f));
        Transform stand = PrimitiveKit.Pivot("Chalkboard", parent, position);
        PrimitiveKit.Box("Leg L", stand, new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.06f, 1.1f, 0.06f), doorWood);
        PrimitiveKit.Box("Leg R", stand, new Vector3(0.45f, 0.55f, 0f), new Vector3(0.06f, 1.1f, 0.06f), doorWood);
        Transform board = PrimitiveKit.Pivot("Board Pivot", stand, new Vector3(0f, 0.75f, 0f));
        PrimitiveKit.Box("Frame", board, Vector3.zero, new Vector3(0.9f, 0.75f, 0.06f), doorWood);
        PrimitiveKit.Box("Slate", board, Vector3.zero, new Vector3(0.8f, 0.65f, 0.07f), slate);
        PrimitiveKit.Text("Closed", board, new Vector3(0f, 0f, -0.04f), new Vector2(0.72f, 0.5f), "CLOSED", new Color(0.92f, 0.92f, 0.88f));
        TextMeshPro open = PrimitiveKit.Text("Open", board, new Vector3(0f, 0f, 0.04f), new Vector2(0.72f, 0.5f), "OPEN\n<size=60%>fresh coffee</size>", new Color(1f, 0.85f, 0.35f));
        open.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        return board;
    }

    // Schwebende Kaffeetasse über der Laufbahn als Hinweis "hier ist ein Checkpoint"
    private static Transform BuildCoffeeCup(Transform parent, Vector3 position)
    {
        Material coffee = PrimitiveKit.Material($"{MaterialFolder}/Coffee.mat", PrimitiveKit.LitShader, new Color(0.35f, 0.2f, 0.1f));
        Transform cup = PrimitiveKit.Pivot("Coffee Cup Hint", parent, position);
        PrimitiveKit.Part("Cup", PrimitiveType.Cylinder, cup, Vector3.zero, new Vector3(0.4f, 0.22f, 0.4f), white);
        PrimitiveKit.Part("Coffee", PrimitiveType.Cylinder, cup, new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.02f, 0.34f), coffee);
        PrimitiveKit.Box("Handle", cup, new Vector3(0.24f, 0.02f, 0f), new Vector3(0.1f, 0.22f, 0.06f), white);
        PrimitiveKit.Part("Saucer", PrimitiveType.Cylinder, cup, new Vector3(0f, -0.22f, 0f), new Vector3(0.6f, 0.02f, 0.6f), white);
        return cup;
    }

    // Funken-Ladung beim Aktivieren (nur Emit, keine Dauer-Emission)
    private static ParticleSystem BuildSparkles(Transform parent)
    {
        var sparkleObject = new GameObject("Sparkles");
        sparkleObject.transform.SetParent(parent, false);
        var sparkles = sparkleObject.AddComponent<ParticleSystem>();
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = sparkles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 1f));
        main.gravityModifier = 0.6f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = sparkles.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        sparkleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        return sparkles;
    }

    // Haus des Kunden fürs Ziel (wird von CustomerSetup ins Goal-Prefab gebaut). Hellblau mit weißem Erdgeschoss,
    // echte Türöffnung, Hausnummer, Türlampe, Blumenkästen. Der Kunde kommt durch die Tür heraus
    public static SpawnDoor BuildCustomerHouse(Transform parent)
    {
        CreateMaterials();
        const float width = 8f;
        const float height = 10f;
        Material blue = PrimitiveKit.Material($"{MaterialFolder}/Customer House.mat", PrimitiveKit.LitShader, new Color(0.55f, 0.72f, 0.86f), 0.1f);
        Material porchWhite = PrimitiveKit.Material($"{MaterialFolder}/Customer House Ground.mat", PrimitiveKit.LitShader, new Color(0.93f, 0.92f, 0.88f), 0.1f);

        Transform house = PrimitiveKit.Pivot("Customer House", parent, Vector3.zero);
        SpawnDoor door = BuildDoorBuilding(house, width, height, porchWhite, blue);

        PrimitiveKit.Quad("Window L", house, new Vector3(-2.6f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(1.8f, 1.5f), windowLit);
        PrimitiveKit.Quad("Window R", house, new Vector3(2.6f, Surface + 1.5f, FacadeZ - 0.01f), new Vector2(1.8f, 1.5f), windowLit);
        // Kleines Vordach über der Tür statt Markise
        PrimitiveKit.Box("Porch Roof", house, new Vector3(0f, Surface + 2.85f, FacadeZ - 0.5f), new Vector3(2.2f, 0.12f, 1f), white);
        PrimitiveKit.Box("Porch Post L", house, new Vector3(-1f, Surface + 1.4f, FacadeZ - 0.95f), new Vector3(0.08f, 2.8f, 0.08f), white);
        PrimitiveKit.Box("Porch Post R", house, new Vector3(1f, Surface + 1.4f, FacadeZ - 0.95f), new Vector3(0.08f, 2.8f, 0.08f), white);
        PrimitiveKit.Box("Doorstep", house, new Vector3(0f, Surface + 0.05f, FacadeZ - 0.4f), new Vector3(1.8f, 0.1f, 0.8f), curb);
        PrimitiveKit.Part("Door Lamp", PrimitiveType.Sphere, house, new Vector3(-1.55f, Surface + 2.2f, FacadeZ - 0.1f), Vector3.one * 0.22f, lampGlow);
        PrimitiveKit.Box("Number Plate", house, new Vector3(1.6f, Surface + 2.1f, FacadeZ - 0.04f), new Vector3(0.5f, 0.4f, 0.04f), signWhite);
        PrimitiveKit.Text("Number", house, new Vector3(1.6f, Surface + 2.1f, FacadeZ - 0.07f), new Vector2(0.44f, 0.34f), "42", new Color(0.15f, 0.25f, 0.45f));

        AddWindowGrid(house, "Customer House", width, Surface + 4.6f, Surface + height - 1.2f);
        // Blumenkästen unter der ersten Fensterreihe
        PrimitiveKit.Box("Flower Box L", house, new Vector3(-2f, Surface + 3.7f, FacadeZ - 0.15f), new Vector3(1.3f, 0.2f, 0.25f), leaves);
        PrimitiveKit.Box("Flower Box R", house, new Vector3(2f, Surface + 3.7f, FacadeZ - 0.15f), new Vector3(1.3f, 0.2f, 0.25f), leavesLight);
        PrimitiveKit.Box("Cornice", house, new Vector3(0f, Surface + height + 0.2f, FacadeZ + 0.15f), new Vector3(width + 0.3f, 0.4f, 0.6f), white);
        PrimitiveKit.Box("Chimney", house, new Vector3(-width / 2f + 1.5f, Surface + height + 1f, FacadeZ + 3f), new Vector3(0.8f, 2f, 0.8f), brick);
        return door;
    }

    // 24/7-Kiosk mit Mikrowellen-Station (MicrowaveStation): Theke vor dem Schaufenster, darauf die Mikrowelle.
    // Der Spieler läuft automatisch zur Theke (Z ~1.7), wärmt den Karton auf und läuft zurück auf die Laufbahn
    private static void BuildKiosk()
    {
        const float width = 6f;
        const float height = 9f;
        Material night = PrimitiveKit.Material($"{MaterialFolder}/Kiosk.mat", PrimitiveKit.LitShader, new Color(0.22f, 0.24f, 0.42f), 0.1f);
        Material fluorescent = PrimitiveKit.Material($"{MaterialFolder}/Fluorescent.mat", PrimitiveKit.UnlitShader, new Color(0.88f, 0.95f, 1f));
        Material appliance = PrimitiveKit.Material($"{MaterialFolder}/Appliance.mat", PrimitiveKit.LitShader, new Color(0.85f, 0.86f, 0.87f), 0.5f);
        Material glassOff = PrimitiveKit.Material($"{MaterialFolder}/Microwave Glass.mat", PrimitiveKit.LitShader, new Color(0.08f, 0.09f, 0.1f), 0.9f);
        Material glassOn = PrimitiveKit.Material($"{MaterialFolder}/Microwave Glass On.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.85f, 0.45f));

        var root = new GameObject("Kiosk");
        Transform t = root.transform;
        float top = Surface + height;
        PrimitiveKit.Box("Body", t, new Vector3(0f, (top - 12f) / 2f, FacadeZ + 4f), new Vector3(width, top + 12f, 8f), night);
        PrimitiveKit.Quad("Shop Window", t, new Vector3(0f, Surface + 1.7f, FacadeZ - 0.01f), new Vector2(width - 1f, 2.4f), fluorescent);
        PrimitiveKit.Box("Sign Board", t, new Vector3(0f, Surface + 3.6f, FacadeZ - 0.1f), new Vector3(3.6f, 1f, 0.15f), iron);
        PrimitiveKit.Text("Sign Text", t, new Vector3(0f, Surface + 3.6f, FacadeZ - 0.2f), new Vector2(3.3f, 0.85f), "24/7", new Color(1f, 0.85f, 0.2f));
        AddWindowGrid(t, "Kiosk", width, Surface + 5.6f, top - 1.2f);
        PrimitiveKit.Box("Cornice", t, new Vector3(0f, top + 0.2f, FacadeZ + 0.15f), new Vector3(width + 0.3f, 0.4f, 0.6f), sill);

        // Theke vor dem Schaufenster
        const float counterZ = FacadeZ - 0.4f;
        PrimitiveKit.Box("Counter", t, new Vector3(0.3f, Surface + 0.5f, counterZ), new Vector3(2.6f, 1f, 0.7f), curb);
        PrimitiveKit.Box("Counter Top", t, new Vector3(0.3f, Surface + 1.02f, counterZ), new Vector3(2.7f, 0.05f, 0.8f), doorWood);
        PrimitiveKit.Text("Counter Sign", t, new Vector3(0.3f, Surface + 0.6f, counterZ - 0.36f), new Vector2(2.3f, 0.4f), "WARM-UP STATION", new Color(1f, 0.85f, 0.2f));

        // Mikrowelle auf der Theke: Gehäuse, dunkler Innenraum, Tür vorne (Drehpunkt links), Bedienfeld mit Display rechts
        const float mwX = 0.5f;
        const float mwWidth = 0.95f;
        const float mwHeight = 0.55f;
        const float mwDepth = 0.6f;
        float mwY = Surface + 1.045f + mwHeight / 2f;
        float frontZ = counterZ - mwDepth / 2f;
        PrimitiveKit.Box("Microwave Body", t, new Vector3(mwX, mwY, counterZ), new Vector3(mwWidth, mwHeight, mwDepth), appliance);
        PrimitiveKit.Box("Microwave Interior", t, new Vector3(mwX - 0.12f, mwY, counterZ - 0.02f), new Vector3(0.66f, 0.45f, mwDepth - 0.02f), interior);
        Transform cartonSpot = PrimitiveKit.Pivot("Carton Spot", t, new Vector3(mwX - 0.12f, mwY - 0.15f, counterZ));
        PrimitiveKit.Part("Turntable", PrimitiveType.Cylinder, cartonSpot, new Vector3(0f, -0.06f, 0f), new Vector3(0.5f, 0.01f, 0.5f), glassOff);

        Transform door = PrimitiveKit.Pivot("Microwave Door", t, new Vector3(mwX - mwWidth / 2f + 0.02f, mwY, frontZ - 0.02f));
        PrimitiveKit.Box("Door Frame", door, new Vector3(0.35f, 0f, 0f), new Vector3(0.7f, 0.5f, 0.03f), appliance);
        Renderer glass = PrimitiveKit.Box("Door Glass", door, new Vector3(0.35f, 0f, -0.01f), new Vector3(0.56f, 0.36f, 0.02f), glassOff);
        PrimitiveKit.Box("Control Panel", t, new Vector3(mwX + mwWidth / 2f - 0.13f, mwY, frontZ - 0.01f), new Vector3(0.22f, 0.5f, 0.02f), iron);
        TextMeshPro display = PrimitiveKit.Text("Display", t, new Vector3(mwX + mwWidth / 2f - 0.13f, mwY + 0.12f, frontZ - 0.03f), new Vector2(0.2f, 0.12f), "READY", new Color(0.4f, 1f, 0.45f));

        // "DING!"-Sprechblase über der Mikrowelle (Quad + Text, beide zeigen zur Kamera)
        Transform bubble = PrimitiveKit.Pivot("Ding Bubble", t, new Vector3(mwX, mwY + 0.85f, frontZ - 0.2f));
        PrimitiveKit.Quad("Background", bubble, Vector3.zero, new Vector2(1f, 0.45f), white);
        PrimitiveKit.Quad("Pointer", bubble, new Vector3(-0.1f, -0.26f, 0f), new Vector2(0.16f, 0.16f), white).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        PrimitiveKit.Text("Text", bubble, new Vector3(0f, 0f, -0.01f), new Vector2(0.9f, 0.38f), "DING!", new Color(0.85f, 0.15f, 0.1f));
        bubble.gameObject.SetActive(false);

        // Wege des Spielers (Kugelmitte): vor die Theke, dann zurück auf die Laufbahn
        Transform stand = PrimitiveKit.Pivot("Stand Point", t, new Vector3(-0.5f, Surface + 0.5f, 1.7f));
        Transform exit = PrimitiveKit.Pivot("Exit Point", t, new Vector3(0.6f, Surface + 0.5f, 0f));

        PrimitiveKit.AddBoxCollider(root, new Vector3(-0.8f, Surface + 1.5f, 0f), new Vector3(1.2f, 3f, 3f), true);
        var station = root.AddComponent<MicrowaveStation>();
        var serialized = new SerializedObject(station);
        serialized.FindProperty("standPoint").objectReferenceValue = stand;
        serialized.FindProperty("exitPoint").objectReferenceValue = exit;
        serialized.FindProperty("door").objectReferenceValue = door;
        serialized.FindProperty("cartonSpot").objectReferenceValue = cartonSpot;
        serialized.FindProperty("doorGlass").objectReferenceValue = glass;
        serialized.FindProperty("glassOff").objectReferenceValue = glassOff;
        serialized.FindProperty("glassOn").objectReferenceValue = glassOn;
        serialized.FindProperty("display").objectReferenceValue = display;
        serialized.FindProperty("dingBubble").objectReferenceValue = bubble.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, PathFor("Kiosk"));
    }

    // ---------- Begehbares ----------

    // Balkon an der Fassade, ragt bis über die Laufbahn. Oberkante bei Wurzel + 0.5 (Wurzel = ganze Meter)
    private static void BuildBalcony()
    {
        var root = new GameObject("Balcony");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float depthFront = -0.6f;
        float depth = FacadeZ - depthFront;
        float centerZ = (FacadeZ + depthFront) / 2f;

        PrimitiveKit.Box("Slab", t, new Vector3(0f, 0.35f, centerZ), new Vector3(3f, 0.3f, depth), concrete);
        PrimitiveKit.Box("Rail Front", t, new Vector3(0f, 1.45f, depthFront + 0.05f), new Vector3(3f, 0.06f, 0.06f), iron);
        PrimitiveKit.Box("Rail Left", t, new Vector3(-1.47f, 1.45f, centerZ), new Vector3(0.06f, 0.06f, depth), iron);
        PrimitiveKit.Box("Rail Right", t, new Vector3(1.47f, 1.45f, centerZ), new Vector3(0.06f, 0.06f, depth), iron);
        for (int i = 0; i < 5; i++)
        {
            PrimitiveKit.Box("Post " + (i + 1), t, new Vector3(-1.45f + i * 0.725f, 0.975f, depthFront + 0.05f), new Vector3(0.05f, 0.95f, 0.05f), iron);
        }
        PrimitiveKit.Box("Flower Box", t, new Vector3(0.6f, 1.55f, depthFront + 0.05f), new Vector3(0.9f, 0.18f, 0.2f), leaves);
        PrimitiveKit.Quad("Balcony Door", t, new Vector3(0f, 1.5f, FacadeZ - 0.02f), new Vector2(1.1f, 2f), windowDark);
        PrimitiveKit.Box("Bracket L", t, new Vector3(-1.2f, -0.1f, FacadeZ - 0.3f), new Vector3(0.1f, 0.6f, 0.6f), iron);
        PrimitiveKit.Box("Bracket R", t, new Vector3(1.2f, -0.1f, FacadeZ - 0.3f), new Vector3(0.1f, 0.6f, 0.6f), iron);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.35f, centerZ), new Vector3(3f, 0.3f, depth));
        PrimitiveKit.SavePrefab(root, PathFor("Balcony"));
    }

    // Federnde Markise: wie ein Balkon montiert, katapultiert den Spieler hoch (BounceAwning)
    private static void BuildAwning()
    {
        var root = new GameObject("Bouncy Awning");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float depthFront = -0.6f;
        float depth = FacadeZ - depthFront;
        float centerZ = (FacadeZ + depthFront) / 2f;

        // Der Stoff hat einen eigenen Pivot, damit BounceAwning ihn beim Abfedern stauchen kann
        Transform canvas = PrimitiveKit.Pivot("Canvas", t, new Vector3(0f, 0.35f, centerZ));
        for (int i = 0; i < 6; i++)
        {
            Material stripe = i % 2 == 0 ? red : white;
            PrimitiveKit.Box("Stripe " + (i + 1), canvas, new Vector3(-1.25f + i * 0.5f, 0f, 0f), new Vector3(0.5f, 0.2f, depth), stripe);
            PrimitiveKit.Box("Valance " + (i + 1), canvas, new Vector3(-1.25f + i * 0.5f, -0.25f, -depth / 2f), new Vector3(0.5f, 0.35f, 0.05f), stripe);
        }
        PrimitiveKit.Part("Rod L", PrimitiveType.Cylinder, t, new Vector3(-1.4f, -0.4f, 1.4f), new Vector3(0.05f, 1.2f, 0.05f), iron).transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
        PrimitiveKit.Part("Rod R", PrimitiveType.Cylinder, t, new Vector3(1.4f, -0.4f, 1.4f), new Vector3(0.05f, 1.2f, 0.05f), iron).transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.35f, centerZ), new Vector3(3f, 0.3f, depth));

        var bounce = root.AddComponent<BounceAwning>();
        var serialized = new SerializedObject(bounce);
        serialized.FindProperty("canvas").objectReferenceValue = canvas;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, PathFor("Bouncy Awning"));
    }

    // Parkbank auf der Laufbahn: kleine Stufe (Oberkante 0.55 über dem Gehweg). Hunde drehen hier um
    private static void BuildBench()
    {
        var root = new GameObject("Bench");
        root.layer = GroundLayer;
        Transform t = root.transform;
        PrimitiveKit.Box("Seat", t, new Vector3(0f, Surface + 0.49f, 0f), new Vector3(2f, 0.12f, 0.6f), doorWood);
        PrimitiveKit.Box("Backrest", t, new Vector3(0f, Surface + 0.85f, 0.28f), new Vector3(2f, 0.45f, 0.08f), doorWood);
        PrimitiveKit.Box("Leg L", t, new Vector3(-0.8f, Surface + 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.55f), iron);
        PrimitiveKit.Box("Leg R", t, new Vector3(0.8f, Surface + 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.55f), iron);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 0.275f, 0f), new Vector3(2f, 0.55f, 0.6f));
        PrimitiveKit.SavePrefab(root, PathFor("Bench"));
    }

    // Müllcontainer auf der Laufbahn: Hindernis zum Drüberspringen (Oberkante 1.3 über dem Gehweg), Hunde drehen hier um
    private static void BuildDumpster()
    {
        var root = new GameObject("Dumpster");
        root.layer = GroundLayer;
        Transform t = root.transform;
        Material dumpsterGreen = PrimitiveKit.Material($"{MaterialFolder}/Dumpster.mat", PrimitiveKit.LitShader, new Color(0.2f, 0.45f, 0.26f), 0.3f);
        PrimitiveKit.Box("Body", t, new Vector3(0f, Surface + 0.65f, 0.1f), new Vector3(2.2f, 1.3f, 1.3f), dumpsterGreen);
        PrimitiveKit.Box("Lid", t, new Vector3(0f, Surface + 1.33f, 0.1f), new Vector3(2.3f, 0.08f, 1.4f), iron);
        PrimitiveKit.Part("Wheel L", PrimitiveType.Cylinder, t, new Vector3(-0.8f, Surface + 0.08f, -0.5f), new Vector3(0.16f, 0.04f, 0.16f), iron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        PrimitiveKit.Part("Wheel R", PrimitiveType.Cylinder, t, new Vector3(0.8f, Surface + 0.08f, -0.5f), new Vector3(0.16f, 0.04f, 0.16f), iron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 0.65f, 0.1f), new Vector3(2.2f, 1.3f, 1.3f));
        PrimitiveKit.SavePrefab(root, PathFor("Dumpster"));
    }

    // Fensterputzer-Gondel als Aufzug: fährt 6 m nach oben und wieder runter (MovingPlatform)
    private static void BuildGondola()
    {
        var root = new GameObject("Window Washer Gondola");
        root.layer = GroundLayer;
        Transform t = root.transform;
        PrimitiveKit.Box("Floor", t, new Vector3(0f, 0.35f, 0f), new Vector3(3f, 0.3f, 1.4f), gondolaYellow);
        PrimitiveKit.Box("Rail Front", t, new Vector3(0f, 1.3f, -0.67f), new Vector3(3f, 0.08f, 0.06f), gondolaYellow);
        PrimitiveKit.Box("Rail Back", t, new Vector3(0f, 1.3f, 0.67f), new Vector3(3f, 0.08f, 0.06f), gondolaYellow);
        foreach (float x in new[] { -1.45f, 0f, 1.45f })
        {
            PrimitiveKit.Box("Post Front", t, new Vector3(x, 0.9f, -0.67f), new Vector3(0.06f, 0.8f, 0.06f), iron);
            PrimitiveKit.Box("Post Back", t, new Vector3(x, 0.9f, 0.67f), new Vector3(0.06f, 0.8f, 0.06f), iron);
        }
        // Seile nach oben, fahren mit (lang genug, dass man das Ende nie sieht)
        PrimitiveKit.Part("Rope L", PrimitiveType.Cylinder, t, new Vector3(-1.3f, 21.3f, 0f), new Vector3(0.04f, 20f, 0.04f), iron);
        PrimitiveKit.Part("Rope R", PrimitiveType.Cylinder, t, new Vector3(1.3f, 21.3f, 0f), new Vector3(0.04f, 20f, 0.04f), iron);
        PrimitiveKit.Box("Bucket", t, new Vector3(0.9f, 0.7f, 0.2f), new Vector3(0.35f, 0.4f, 0.35f), binGray);

        Transform start = PrimitiveKit.Pivot("moving platform start", t, Vector3.zero);
        Transform end = PrimitiveKit.Pivot("moving platform end", t, new Vector3(0f, 6f, 0f));
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.35f, 0f), new Vector3(3f, 0.3f, 1.4f));
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var mover = root.AddComponent<MovingPlatform>();
        var serialized = new SerializedObject(mover);
        serialized.FindProperty("pointA").objectReferenceValue = start;
        serialized.FindProperty("pointB").objectReferenceValue = end;
        serialized.FindProperty("speed").floatValue = 2f;
        serialized.FindProperty("waitTime").floatValue = 1.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, PathFor("Window Washer Gondola"));
    }

    // ---------- Deko (ohne Collider, hinter der Laufbahn) ----------

    private static void BuildTree()
    {
        var root = new GameObject("Tree");
        Transform t = root.transform;
        const float z = 1.9f;
        PrimitiveKit.Box("Tree Pit", t, new Vector3(0f, Surface + 0.005f, z), new Vector3(1.2f, 0.01f, 1.2f), foundation);
        PrimitiveKit.Part("Trunk", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 1.4f, z), new Vector3(0.3f, 1.4f, 0.3f), trunk);
        PrimitiveKit.Part("Crown", PrimitiveType.Sphere, t, new Vector3(0f, Surface + 3.7f, z), Vector3.one * 2.3f, leaves);
        PrimitiveKit.Part("Crown L", PrimitiveType.Sphere, t, new Vector3(-0.8f, Surface + 3.1f, z - 0.2f), Vector3.one * 1.6f, leavesLight);
        PrimitiveKit.Part("Crown R", PrimitiveType.Sphere, t, new Vector3(0.8f, Surface + 3.3f, z + 0.2f), Vector3.one * 1.7f, leavesLight);
        PrimitiveKit.SavePrefab(root, PathFor("Tree"));
    }

    private static void BuildStreetLamp()
    {
        var root = new GameObject("Street Lamp");
        Transform t = root.transform;
        const float z = 2.4f;
        PrimitiveKit.Part("Pole", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 2.3f, z), new Vector3(0.12f, 2.3f, 0.12f), iron);
        PrimitiveKit.Box("Arm", t, new Vector3(0f, Surface + 4.55f, z - 0.45f), new Vector3(0.08f, 0.08f, 0.9f), iron);
        PrimitiveKit.Box("Head", t, new Vector3(0f, Surface + 4.5f, z - 0.9f), new Vector3(0.35f, 0.18f, 0.5f), iron);
        PrimitiveKit.Part("Glow", PrimitiveType.Sphere, t, new Vector3(0f, Surface + 4.38f, z - 0.9f), new Vector3(0.28f, 0.14f, 0.36f), lampGlow);
        PrimitiveKit.SavePrefab(root, PathFor("Street Lamp"));
    }

    private static void BuildHydrant()
    {
        var root = new GameObject("Hydrant");
        Transform t = root.transform;
        const float z = 1.6f;
        PrimitiveKit.Part("Body", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.35f, z), new Vector3(0.32f, 0.35f, 0.32f), hydrantRed);
        PrimitiveKit.Part("Cap", PrimitiveType.Sphere, t, new Vector3(0f, Surface + 0.72f, z), Vector3.one * 0.3f, hydrantRed);
        PrimitiveKit.Part("Nozzle", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.45f, z - 0.2f), new Vector3(0.14f, 0.1f, 0.14f), hydrantRed).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        PrimitiveKit.SavePrefab(root, PathFor("Hydrant"));
    }

    private static void BuildTrashCan()
    {
        var root = new GameObject("Trash Can");
        Transform t = root.transform;
        const float z = 2.2f;
        PrimitiveKit.Part("Bin", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.45f, z), new Vector3(0.55f, 0.45f, 0.55f), binGray);
        PrimitiveKit.Part("Lid", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.93f, z), new Vector3(0.6f, 0.04f, 0.6f), iron);
        PrimitiveKit.SavePrefab(root, PathFor("Trash Can"));
    }

    // Hochhäuser weit hinten (Z 30-40), etwa 40 m breit. Der Nebel macht sie blass, das gibt Tiefe
    private static void BuildSkyline()
    {
        var root = new GameObject("Skyline");
        Transform t = root.transform;
        (float x, float width, float height, float z)[] towers =
        {
            (-15f, 12f, 26f, 32f), (-3f, 9f, 36f, 36f), (8f, 13f, 22f, 30f), (17f, 8f, 31f, 38f)
        };
        foreach (var tower in towers)
        {
            PrimitiveKit.Box("Tower", t, new Vector3(tower.x, (tower.height - 12f) / 2f, tower.z), new Vector3(tower.width, tower.height + 12f, 8f), skyline);
            // Ein paar helle Fensterbänder für Struktur
            for (float y = 4f; y < tower.height - 2f; y += 4f)
            {
                PrimitiveKit.Box("Band", t, new Vector3(tower.x, y, tower.z - 4.01f), new Vector3(tower.width * 0.8f, 0.6f, 0.02f), skylineLight);
            }
        }
        PrimitiveKit.SavePrefab(root, PathFor("Skyline"));
    }

    private static void BuildCloud()
    {
        var root = new GameObject("Cloud");
        Transform t = root.transform;
        const float z = 70f;
        PrimitiveKit.Part("Puff", PrimitiveType.Sphere, t, new Vector3(0f, 0f, z), new Vector3(9f, 3.5f, 4f), cloud);
        PrimitiveKit.Part("Puff L", PrimitiveType.Sphere, t, new Vector3(-4.5f, -0.6f, z), new Vector3(6f, 2.6f, 3.5f), cloud);
        PrimitiveKit.Part("Puff R", PrimitiveType.Sphere, t, new Vector3(4.2f, -0.4f, z), new Vector3(7f, 3f, 3.5f), cloud);
        PrimitiveKit.Part("Puff Top", PrimitiveType.Sphere, t, new Vector3(1f, 1.4f, z), new Vector3(5f, 2.8f, 3f), cloud);
        PrimitiveKit.SavePrefab(root, PathFor("Cloud"));
    }

    // ---------- Spieler ----------

    // Hängt PlayerLife (Tod + Respawn aus der Tür) und die Rauchwolke für die Todesanimation an den Player
    [MenuItem("Tools/Jump and Run/City/Add Death Animation to Player Prefab")]
    public static void AddPlayerLife()
    {
        const string playerPath = "Assets/Prefabs/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            Transform old = root.transform.Find("Poof");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var poofObject = new GameObject("Poof");
            poofObject.transform.SetParent(root.transform, false);
            var poof = poofObject.AddComponent<ParticleSystem>();
            // Nur auf Abruf (Emit), nicht automatisch
            poof.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = poof.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.gravityModifier = -0.1f;
            main.maxParticles = 40;
            // World: Die Wolke bleibt, wo der Spieler gestorben ist, auch wenn er schon an der Tür wieder auftaucht
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = poof.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = poof.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            ParticleSystem.SizeOverLifetimeModule size = poof.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = poof.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            poofObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);

            if (!root.TryGetComponent(out PlayerLife life))
            {
                life = root.AddComponent<PlayerLife>();
            }
            var serialized = new SerializedObject(life);
            serialized.FindProperty("poof").objectReferenceValue = poof;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            Debug.Log("CityPropsSetup: PlayerLife + Rauchwolke am Player-Prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
