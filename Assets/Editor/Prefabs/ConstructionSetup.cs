using TMPro;
using UnityEditor;
using UnityEngine;

// Baustellen-Prefabs für Level 3 (Mozzarella Blvd), gebaut aus Grundformen im Lane-Rahmen wie die Stadt-Prefabs
// (lokal +X = Laufrichtung, -Z = Kameraseite, Fassaden bei Z = 3). Menü: Tools > Jump and Run > City > Build Construction Prefabs.
//  - "Scissor Lift": gelbe Scherenbühne. Die Plattform ist eine MovingPlatform (fährt von "moving platform start" nach
//    "moving platform end", Standard 4 m hoch), ScissorLift stellt Sockel und Scherenarme dazu. Oberkante = Wurzel + 0.5.
//  - "Scaffold": Gerüst-Etage vor der Fassade, Bretter über der Laufbahn begehbar (Oberkante = Wurzel + 0.5),
//    Stangen reichen 12 m nach unten (stehen im Gehweg oder in der Grube)
//  - "Site Office": Checkpoint von Level 3. Baugrundstück (8 m entlang der Bahn, 10.8 m tief, ersetzt ein Stadthaus per
//    Cutout, bringt deshalb eigenen Boden mit), Bauzaun mit Tor, zwei gestapelte Bürocontainer von "Crust & Sons
//    Construction", Tür mit SpawnDoor, Rundumleuchte auf dem Dach (SiteOfficeEffects)
public static class ConstructionSetup
{
    private const int GroundLayer = 3;
    private const float FacadeZ = 3f;
    private const string MaterialFolder = "Assets/Materials/City";

    private const float DeckLength = 3f;
    private const float DeckDepth = 1.6f;
    private const int ScissorStages = 4;
    private const float ArmLength = 2.4f;
    private const float ChassisHeight = 0.4f;

    private static Material yellow, darkYellow, iron, orange, wood, steel;
    private static Material container, siteBlue, gravel, fenceMesh, beaconOff, beaconOn, windowDark, windowLit, interior, signWhite;

    [MenuItem("Tools/Jump and Run/City/Build Construction Prefabs")]
    public static void BuildAll()
    {
        CreateMaterials();
        BuildScissorLift();
        BuildScaffold();
        BuildSiteOffice();
        AssetDatabase.SaveAssets();
        Debug.Log("ConstructionSetup: Scissor Lift, Scaffold und Site Office gespeichert.");
    }

    private static Material Lit(string name, Color color, float smoothness = 0.2f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static void CreateMaterials()
    {
        yellow = Lit("Gondola Yellow", new Color(0.95f, 0.76f, 0.16f));
        darkYellow = Lit("Lift Yellow Dark", new Color(0.78f, 0.58f, 0.1f));
        iron = Lit("Iron", new Color(0.14f, 0.14f, 0.16f), 0.5f);
        orange = Lit("Barrier Orange", new Color(0.98f, 0.45f, 0.08f));
        wood = Lit("Scaffold Plank", new Color(0.72f, 0.55f, 0.32f));
        steel = Lit("Scaffold Steel", new Color(0.62f, 0.64f, 0.66f), 0.5f);
        container = Lit("Site Container", new Color(0.88f, 0.9f, 0.9f));
        siteBlue = Lit("Site Blue", new Color(0.15f, 0.3f, 0.6f));
        gravel = Lit("Gravel", new Color(0.52f, 0.49f, 0.44f));
        beaconOff = Lit("Beacon Off", new Color(0.55f, 0.32f, 0.1f), 0.6f);
        beaconOn = PrimitiveKit.Material($"{MaterialFolder}/Beacon On.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.62f, 0.1f));
        windowDark = Lit("Window Glass", new Color(0.28f, 0.38f, 0.52f), 0.8f);
        windowLit = PrimitiveKit.Material($"{MaterialFolder}/Window Lit.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.86f, 0.55f));
        interior = PrimitiveKit.Material($"{MaterialFolder}/Interior Dark.mat", PrimitiveKit.UnlitShader, new Color(0.07f, 0.05f, 0.04f));
        signWhite = Lit("Sign White", new Color(0.97f, 0.96f, 0.92f));
        // Halbdurchsichtig: Man sieht den Container hinter dem Gitter des Bauzauns
        fenceMesh = PrimitiveKit.TransparentMaterial($"{MaterialFolder}/Fence Mesh.mat", new Color(0.75f, 0.77f, 0.78f, 0.35f));
    }

    private static void BuildScissorLift()
    {
        var root = new GameObject("Scissor Lift");
        root.layer = GroundLayer;
        Transform t = root.transform;

        // Plattform: Oberkante 0.5 über der Wurzel, Geländer vorne und hinten (ohne Collider), Warnstreifen an den Enden
        PrimitiveKit.Box("Deck", t, new Vector3(0f, 0.35f, 0f), new Vector3(DeckLength, 0.3f, DeckDepth), yellow);
        foreach (float x in new[] { -DeckLength / 2f + 0.1f, DeckLength / 2f - 0.1f })
        {
            PrimitiveKit.Box("Warning Stripe", t, new Vector3(x, 0.505f, 0f), new Vector3(0.2f, 0.01f, DeckDepth), orange);
        }
        foreach (float z in new[] { -DeckDepth / 2f + 0.04f, DeckDepth / 2f - 0.04f })
        {
            PrimitiveKit.Box("Rail", t, new Vector3(0f, 1.5f, z), new Vector3(DeckLength, 0.06f, 0.06f), yellow);
            foreach (float x in new[] { -1.45f, 0f, 1.45f })
            {
                PrimitiveKit.Box("Rail Post", t, new Vector3(x, 1f, z), new Vector3(0.06f, 1f, 0.06f), iron);
            }
        }

        // Sockel mit Rädern: wird zur Laufzeit auf den Boden gestellt (ScissorLift)
        Transform chassis = PrimitiveKit.Pivot("Chassis", t, new Vector3(0f, -4f, 0f));
        PrimitiveKit.Box("Body", chassis, Vector3.zero, new Vector3(2.6f, ChassisHeight, DeckDepth - 0.2f), darkYellow);
        foreach (float x in new[] { -0.95f, 0.95f })
        {
            foreach (float z in new[] { -0.75f, 0.75f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, chassis, new Vector3(x, -0.08f, z), new Vector3(0.36f, 0.06f, 0.36f), iron)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // Scherenarme: pro Stufe ein X, vorne und hinten je zwei gekreuzte Arme (lokal X = Länge des Arms)
        var stages = new Transform[ScissorStages];
        var rising = new Transform[ScissorStages * 2];
        var falling = new Transform[ScissorStages * 2];
        for (int i = 0; i < ScissorStages; i++)
        {
            stages[i] = PrimitiveKit.Pivot("Stage " + (i + 1), t, new Vector3(0f, -3f + i, 0f));
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -0.6f : 0.6f;
                rising[i * 2 + side] = PrimitiveKit.Box("Arm Rising", stages[i], new Vector3(0f, 0f, z), new Vector3(ArmLength, 0.12f, 0.08f), iron).transform;
                falling[i * 2 + side] = PrimitiveKit.Box("Arm Falling", stages[i], new Vector3(0f, 0f, z * 1.15f), new Vector3(ArmLength, 0.12f, 0.08f), iron).transform;
            }
        }

        Transform start = PrimitiveKit.Pivot("moving platform start", t, Vector3.zero);
        Transform end = PrimitiveKit.Pivot("moving platform end", t, new Vector3(0f, 4f, 0f));
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.35f, 0f), new Vector3(DeckLength, 0.3f, DeckDepth));
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var mover = new SerializedObject(root.AddComponent<MovingPlatform>());
        mover.FindProperty("pointA").objectReferenceValue = start;
        mover.FindProperty("pointB").objectReferenceValue = end;
        mover.FindProperty("speed").floatValue = 1.5f;
        mover.FindProperty("waitTime").floatValue = 1f;
        mover.ApplyModifiedPropertiesWithoutUndo();

        var lift = root.AddComponent<ScissorLift>();
        var serialized = new SerializedObject(lift);
        serialized.FindProperty("chassis").objectReferenceValue = chassis;
        PrimitiveKit.SetReferences(serialized, "stages", stages);
        PrimitiveKit.SetReferences(serialized, "risingArms", rising);
        PrimitiveKit.SetReferences(serialized, "fallingArms", falling);
        serialized.FindProperty("armLength").floatValue = ArmLength;
        serialized.FindProperty("chassisHeight").floatValue = ChassisHeight;
        serialized.FindProperty("deckBottom").floatValue = 0.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        lift.Arrange();

        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Scissor Lift.prefab");
    }

    private static void BuildScaffold()
    {
        var root = new GameObject("Scaffold");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float front = -0.8f;
        float depth = FacadeZ - front;
        float centerZ = (FacadeZ + front) / 2f;

        // Bretter (begehbar), darunter ein Querträger, vorne ein Bordbrett
        for (int i = 0; i < 4; i++)
        {
            float z = front + depth * (i + 0.5f) / 4f;
            PrimitiveKit.Box("Plank " + (i + 1), t, new Vector3(0f, 0.4f, z), new Vector3(3f, 0.2f, depth / 4f - 0.04f), wood);
        }
        PrimitiveKit.Box("Toe Board", t, new Vector3(0f, 0.6f, front + 0.05f), new Vector3(3f, 0.2f, 0.04f), orange);
        PrimitiveKit.Box("Ledger Front", t, new Vector3(0f, 0.22f, front + 0.1f), new Vector3(3f, 0.08f, 0.08f), steel);
        PrimitiveKit.Box("Ledger Back", t, new Vector3(0f, 0.22f, FacadeZ - 0.3f), new Vector3(3f, 0.08f, 0.08f), steel);

        // Stangen an den vier Ecken bis 12 m nach unten (enden im Boden), Geländer hinten und an den Seiten
        foreach (float x in new[] { -1.45f, 1.45f })
        {
            foreach (float z in new[] { front + 0.1f, FacadeZ - 0.3f })
            {
                PrimitiveKit.Part("Standard", PrimitiveType.Cylinder, t, new Vector3(x, -5f, z), new Vector3(0.08f, 6.6f, 0.08f), steel);
            }
            PrimitiveKit.Box("Side Rail", t, new Vector3(x, 1.5f, centerZ), new Vector3(0.05f, 0.05f, depth), steel);
        }
        PrimitiveKit.Box("Back Rail", t, new Vector3(0f, 1.5f, FacadeZ - 0.3f), new Vector3(3f, 0.05f, 0.05f), steel);
        // Diagonale Strebe hinten, damit es nach Gerüst aussieht
        PrimitiveKit.Box("Brace", t, new Vector3(0f, -1.2f, FacadeZ - 0.25f), new Vector3(0.05f, 3.6f, 0.05f), steel)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 40f);

        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.4f, centerZ), new Vector3(3f, 0.2f, depth));
        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Scaffold.prefab");
    }

    // Baugrundstück mit Bürocontainern als Checkpoint. Wurzel auf der Laufbahn (Mitte des Grundstücks entlang u),
    // Grundstück von der Fassadenlinie (lokal z 3) bis z 13.8, 8 m breit (x -4..4). Lokal -X zeigt hier Richtung
    // Crust Street (Nordseite des Grundstücks, bekommt auch einen Zaun)
    private static void BuildSiteOffice()
    {
        const float surface = 0.5f;
        const float halfWidth = 4f;
        const float lotBack = 13.8f;
        const float gateHalf = 1.2f;
        const float fenceHeight = 2f;
        const float boxFront = 5f, boxDepth = 2.6f, boxHeight = 2.6f, boxHalf = 3f;
        const float doorHalf = 0.5f, doorHeight = 2.1f;

        var root = new GameObject("Site Office");
        Transform t = root.transform;

        // Eigener Boden: Der Cutout nimmt das Stadthaus UND den Gehweg darunter weg. Oberkante 5 mm unter dem Gehweg,
        // damit an der Schnittkante nichts flackert
        PrimitiveKit.Box("Gravel", t, new Vector3(0f, (surface - 0.005f - 6f) / 2f, (FacadeZ + lotBack) / 2f),
            new Vector3(halfWidth * 2f, surface - 0.005f + 6f, lotBack - FacadeZ), gravel);

        // Bauzaun vorne (mit Tor in der Mitte) und an der Nordseite: Rahmen aus Rohren, Gitter halbdurchsichtig
        Transform fence = PrimitiveKit.Pivot("Fence", t, Vector3.zero);
        FencePanel(fence, new Vector3(-(halfWidth + gateHalf) / 2f, surface, FacadeZ - 0.1f), halfWidth - gateHalf, fenceHeight, 0f);
        FencePanel(fence, new Vector3((halfWidth + gateHalf) / 2f, surface, FacadeZ - 0.1f), halfWidth - gateHalf, fenceHeight, 0f);
        for (int i = 0; i < 3; i++)
        {
            float length = (lotBack - FacadeZ) / 3f;
            FencePanel(fence, new Vector3(-halfWidth + 0.05f, surface, FacadeZ + length * (i + 0.5f)), length, fenceHeight, 90f);
        }
        // Firmen-Banner am rechten Zaunfeld
        PrimitiveKit.Box("Banner", fence, new Vector3((halfWidth + gateHalf) / 2f, surface + 1.15f, FacadeZ - 0.14f), new Vector3(halfWidth - gateHalf - 0.2f, 0.9f, 0.02f), signWhite);
        PrimitiveKit.Text("Banner Text", fence, new Vector3((halfWidth + gateHalf) / 2f, surface + 1.15f, FacadeZ - 0.16f), new Vector2(halfWidth - gateHalf - 0.4f, 0.8f),
            "CRUST & SONS\n<size=60%>CONSTRUCTION</size>", new Color(0.8f, 0.13f, 0.1f));

        // Torflügel, Drehpunkt am äußeren Pfosten, schwingen nach innen (SiteOfficeEffects)
        Transform gateLeft = PrimitiveKit.Pivot("Gate Left", t, new Vector3(-gateHalf, surface, FacadeZ - 0.1f));
        FencePanel(gateLeft, new Vector3(gateHalf / 2f, 0f, 0f), gateHalf - 0.04f, fenceHeight, 0f);
        Transform gateRight = PrimitiveKit.Pivot("Gate Right", t, new Vector3(gateHalf, surface, FacadeZ - 0.1f));
        FencePanel(gateRight, new Vector3(-gateHalf / 2f, 0f, 0f), gateHalf - 0.04f, fenceHeight, 0f);
        Transform closedSign = PrimitiveKit.Pivot("Closed Sign", gateLeft, new Vector3(gateHalf / 2f, 1.2f, -0.06f));
        PrimitiveKit.Box("Board", closedSign, Vector3.zero, new Vector3(0.9f, 0.34f, 0.03f), signWhite);
        PrimitiveKit.Text("Text", closedSign, new Vector3(0f, 0f, -0.02f), new Vector2(0.8f, 0.28f), "CLOSED", new Color(0.8f, 0.13f, 0.1f));

        // Unterer Container mit Türöffnung: Wände links/rechts, Sturz, dunkler Raum hinter der Tür (dort wartet der Spieler
        // beim Respawn, unsichtbar für die Kamera, und kommt beim Öffnen aus dem Dunkel)
        float boxCenterZ = boxFront + boxDepth / 2f;
        float sideWidth = boxHalf - doorHalf;
        PrimitiveKit.Box("Wall Left", t, new Vector3(-boxHalf + sideWidth / 2f, surface + boxHeight / 2f, boxCenterZ), new Vector3(sideWidth, boxHeight, boxDepth), container);
        PrimitiveKit.Box("Wall Right", t, new Vector3(boxHalf - sideWidth / 2f, surface + boxHeight / 2f, boxCenterZ), new Vector3(sideWidth, boxHeight, boxDepth), container);
        PrimitiveKit.Box("Lintel", t, new Vector3(0f, surface + (doorHeight + boxHeight) / 2f, boxCenterZ), new Vector3(doorHalf * 2f, boxHeight - doorHeight, boxDepth), container);
        PrimitiveKit.Box("Interior", t, new Vector3(0f, surface + doorHeight / 2f, boxFront + 0.2f + (boxDepth - 0.4f) / 2f), new Vector3(doorHalf * 2f, doorHeight, boxDepth - 0.4f), interior);
        Transform hinge = PrimitiveKit.Pivot("Door Hinge", t, new Vector3(-doorHalf, surface, boxFront - 0.04f));
        PrimitiveKit.Box("Door Leaf", hinge, new Vector3(doorHalf, doorHeight / 2f, 0f), new Vector3(doorHalf * 2f, doorHeight, 0.06f), siteBlue);
        PrimitiveKit.Part("Door Handle", PrimitiveType.Sphere, hinge, new Vector3(doorHalf * 2f - 0.15f, doorHeight / 2f, -0.05f), Vector3.one * 0.08f, steel);

        // Oberer Container, etwas versetzt
        float upperY = surface + boxHeight;
        PrimitiveKit.Box("Upper Container", t, new Vector3(0.5f, upperY + boxHeight / 2f, boxCenterZ), new Vector3(boxHalf * 2f, boxHeight, boxDepth), container);
        // Blaue Eckpfosten und Dachkanten an beiden Containern
        foreach (float y in new[] { surface, upperY })
        {
            float x0 = y > surface ? 0.5f : 0f;
            foreach (float x in new[] { x0 - boxHalf + 0.06f, x0 + boxHalf - 0.06f })
            {
                PrimitiveKit.Box("Corner Post", t, new Vector3(x, y + boxHeight / 2f, boxFront - 0.02f), new Vector3(0.14f, boxHeight, 0.06f), siteBlue);
            }
            PrimitiveKit.Box("Roof Edge", t, new Vector3(x0, y + boxHeight - 0.07f, boxFront - 0.02f), new Vector3(boxHalf * 2f, 0.14f, 0.06f), siteBlue);
        }

        // Fenster: aus bis zum Checkpoint, dann hell (SiteOfficeEffects)
        var windows = new System.Collections.Generic.List<Renderer>
        {
            PrimitiveKit.Quad("Window", t, new Vector3(-1.9f, surface + 1.6f, boxFront - 0.01f), new Vector2(1.2f, 0.9f), windowDark),
            PrimitiveKit.Quad("Window", t, new Vector3(1.9f, surface + 1.6f, boxFront - 0.01f), new Vector2(1.2f, 0.9f), windowDark),
            PrimitiveKit.Quad("Window", t, new Vector3(-1.6f, upperY + 1.2f, boxFront - 0.01f), new Vector2(1.2f, 0.9f), windowDark),
            PrimitiveKit.Quad("Window", t, new Vector3(2.6f, upperY + 1.2f, boxFront - 0.01f), new Vector2(1.2f, 0.9f), windowDark),
        };
        PrimitiveKit.Box("Company Sign", t, new Vector3(0.6f, upperY + 1.3f, boxFront - 0.03f), new Vector3(2.4f, 0.9f, 0.04f), siteBlue);
        PrimitiveKit.Text("Company Sign Text", t, new Vector3(0.6f, upperY + 1.3f, boxFront - 0.06f), new Vector2(2.2f, 0.8f),
            "CRUST & SONS\n<size=55%>CONSTRUCTION</size>", new Color(0.97f, 0.96f, 0.92f));

        // Rundumleuchte auf dem Dach: Glas aus/an, darin ein drehender Lichtkegel (Spot Light), der über die Fassaden streicht
        float roof = upperY + boxHeight;
        PrimitiveKit.Part("Beacon Base", PrimitiveType.Cylinder, t, new Vector3(-1.5f, roof + 0.05f, boxCenterZ), new Vector3(0.4f, 0.05f, 0.4f), iron);
        Renderer glass = PrimitiveKit.Part("Beacon Glass", PrimitiveType.Cylinder, t, new Vector3(-1.5f, roof + 0.27f, boxCenterZ), new Vector3(0.32f, 0.18f, 0.32f), beaconOff);
        Transform rotor = PrimitiveKit.Pivot("Beacon Rotor", t, new Vector3(-1.5f, roof + 0.27f, boxCenterZ));
        var lightObject = new GameObject("Beacon Light");
        lightObject.transform.SetParent(rotor, false);
        var spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.62f, 0.15f);
        spot.intensity = 40f;
        spot.range = 14f;
        spot.spotAngle = 35f;
        spot.shadows = LightShadows.None;
        rotor.gameObject.SetActive(false);

        // Ein paar Baustoffe auf dem Kies
        PrimitiveKit.Box("Pallet Stack", t, new Vector3(-2.6f, surface + 0.45f, 10.5f), new Vector3(1.2f, 0.9f, 1f), wood);
        PrimitiveKit.Box("Sand Bags", t, new Vector3(2.4f, surface + 0.3f, 11f), new Vector3(1.4f, 0.6f, 1.2f), darkYellow);

        // Spieler-Mitte drinnen im dunklen Raum, draußen auf der Laufbahn etwas neben dem Tor
        Transform inside = PrimitiveKit.Pivot("Inside Point", t, new Vector3(0f, surface + 0.5f, boxFront + 1.2f));
        Transform outside = PrimitiveKit.Pivot("Outside Point", t, new Vector3(0.5f, surface + 0.5f, 0f));
        var door = root.AddComponent<SpawnDoor>();
        var doorSettings = new SerializedObject(door);
        doorSettings.FindProperty("insidePoint").objectReferenceValue = inside;
        doorSettings.FindProperty("outsidePoint").objectReferenceValue = outside;
        doorSettings.FindProperty("doorLeaf").objectReferenceValue = hinge;
        doorSettings.ApplyModifiedPropertiesWithoutUndo();

        TextMeshPro unlocked = CityPropsSetup.BuildUnlockedText(t, new Vector3(0f, roof + 1.2f, FacadeZ - 0.6f));
        ParticleSystem sparkles = CityPropsSetup.BuildSparkles(t);

        // Auslöser vor dem Tor auf der Laufbahn (wie beim Café)
        PrimitiveKit.AddBoxCollider(root, new Vector3(0.5f, surface + 1.5f, 0f), new Vector3(1.5f, 3f, 3f), true);
        var checkpoint = new SerializedObject(root.AddComponent<Checkpoint>());
        checkpoint.FindProperty("door").objectReferenceValue = door;
        checkpoint.ApplyModifiedPropertiesWithoutUndo();

        var fx = new SerializedObject(root.AddComponent<SiteOfficeEffects>());
        PrimitiveKit.SetReferences(fx, "windows", windows.ToArray());
        fx.FindProperty("windowsClosed").objectReferenceValue = windowDark;
        fx.FindProperty("windowsOpen").objectReferenceValue = windowLit;
        fx.FindProperty("beaconGlass").objectReferenceValue = glass;
        fx.FindProperty("beaconOff").objectReferenceValue = beaconOff;
        fx.FindProperty("beaconOn").objectReferenceValue = beaconOn;
        fx.FindProperty("beaconRotor").objectReferenceValue = rotor;
        fx.FindProperty("gateLeft").objectReferenceValue = gateLeft;
        fx.FindProperty("gateRight").objectReferenceValue = gateRight;
        fx.FindProperty("closedSign").objectReferenceValue = closedSign.gameObject;
        fx.FindProperty("unlockedText").objectReferenceValue = unlocked;
        fx.FindProperty("sparkles").objectReferenceValue = sparkles;
        fx.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Site Office.prefab");
    }

    // Bauzaun-Feld: Rohrrahmen mit halbdurchsichtigem Gitter, Betonfüße. center = Mitte unten, yaw 0 = entlang lokal X
    private static void FencePanel(Transform parent, Vector3 center, float length, float height, float yaw)
    {
        Transform panel = PrimitiveKit.Pivot("Fence Panel", parent, center);
        panel.localRotation = Quaternion.Euler(0f, yaw, 0f);
        PrimitiveKit.Box("Mesh", panel, new Vector3(0f, height / 2f + 0.1f, 0f), new Vector3(length - 0.1f, height - 0.2f, 0.01f), fenceMesh);
        PrimitiveKit.Box("Top", panel, new Vector3(0f, height, 0f), new Vector3(length, 0.05f, 0.05f), steel);
        PrimitiveKit.Box("Bottom", panel, new Vector3(0f, 0.12f, 0f), new Vector3(length, 0.05f, 0.05f), steel);
        foreach (float x in new[] { -length / 2f + 0.03f, length / 2f - 0.03f })
        {
            PrimitiveKit.Box("Post", panel, new Vector3(x, height / 2f, 0f), new Vector3(0.05f, height, 0.05f), steel);
            PrimitiveKit.Box("Foot", panel, new Vector3(x, 0.08f, 0f), new Vector3(0.25f, 0.16f, 0.6f), steel);
        }
    }
}
