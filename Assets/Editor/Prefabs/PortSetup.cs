using UnityEditor;
using UnityEngine;

// Baut die Prefabs für den Containerhafen (Level 4): Containerstapel zum Klettern, Kran-Container (bewegte Plattform an
// einem Kranseil) und das Ziel mit den Bauarbeitern auf dem Stahlträger.
// Menü: Tools > Jump and Run > City > Build Port Prefabs. Mehrfach ausführbar, Prefabs werden ersetzt.
//
// Konvention wie bei den Stadt-Prefabs: Wurzel auf der Laufbahn (lokal +X = Laufrichtung, +Z = hinten), auf der Mitte
// einer 1 m dicken Bodenplatte (Oberfläche +0.5). Der Hafenboden liegt bei 0.6: Container stehen 10 cm "im" Boden, das
// sieht man nicht. Ein Container: 6 m lang (entlang der Bahn), 2.6 m hoch, 2.44 m tief.
public static class PortSetup
{
    public const string PrefabFolder = "Assets/Prefabs/Port";
    private const string MaterialFolder = "Assets/Materials/City";
    private const int GroundLayer = 3;
    private const float Surface = 0.5f;
    public const float ContainerLength = 6f;
    public const float ContainerHeight = 2.6f;
    public const float ContainerDepth = 2.44f;

    private static Material[] containerColors;
    private static Material rib, steel, cable, hookYellow, beamRed, hatYellow, hatWhite, reflective, lunchBox;

    [MenuItem("Tools/Jump and Run/City/Build Port Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Port");
        CreateMaterials();
        for (int levels = 1; levels <= 3; levels++)
        {
            BuildStack(levels);
        }
        BuildCraneContainer();
        BuildCrateStep("Crate Step Low", 1);
        BuildCrateStep("Crate Step High", 2);
        BuildCrewGoal();
        AssetDatabase.SaveAssets();
        Debug.Log("PortSetup: Hafen-Prefabs gebaut.");
    }

    private static Material Lit(string name, Color color, float smoothness = 0.25f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static void CreateMaterials()
    {
        containerColors = new[]
        {
            Lit("Port Container Red", new Color(0.72f, 0.18f, 0.14f)), Lit("Port Container Blue", new Color(0.16f, 0.32f, 0.6f)),
            Lit("Port Container Green", new Color(0.18f, 0.5f, 0.32f)), Lit("Port Container Orange", new Color(0.92f, 0.5f, 0.12f)),
            Lit("Port Container Teal", new Color(0.16f, 0.55f, 0.58f)),
        };
        rib = Lit("Container Rib", new Color(0.12f, 0.12f, 0.13f));
        steel = Lit("Crane Steel", new Color(0.3f, 0.32f, 0.35f), 0.5f);
        cable = Lit("Crane Cable", new Color(0.1f, 0.1f, 0.11f), 0.3f);
        hookYellow = Lit("Crane Yellow", new Color(0.95f, 0.75f, 0.15f), 0.4f);
        beamRed = Lit("Steel Beam Primer", new Color(0.62f, 0.22f, 0.14f), 0.3f);
        hatYellow = Lit("Hard Hat Yellow", new Color(1f, 0.82f, 0.1f), 0.5f);
        hatWhite = Lit("Hard Hat White", new Color(0.96f, 0.96f, 0.94f), 0.5f);
        reflective = Lit("Reflective Stripe", new Color(0.85f, 0.88f, 0.9f), 0.9f);
        lunchBox = Lit("Lunch Box", new Color(0.2f, 0.45f, 0.7f), 0.5f);
    }

    // Ein Container (Optik): Körper, senkrechte Rippen auf der Kamera- und Rückseite, Türen am +X-Ende. Unterkante bei y
    private static void Container(Transform parent, float y, Material color, string name = "Container")
    {
        Transform c = PrimitiveKit.Pivot(name, parent, new Vector3(0f, y, 0f));
        PrimitiveKit.Box("Body", c, new Vector3(0f, ContainerHeight / 2f, 0f), new Vector3(ContainerLength, ContainerHeight - 0.02f, ContainerDepth), color);
        for (float x = -ContainerLength / 2f + 0.4f; x < ContainerLength / 2f - 0.2f; x += 0.6f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box("Rib", c, new Vector3(x, ContainerHeight / 2f, side * (ContainerDepth / 2f + 0.01f)), new Vector3(0.12f, ContainerHeight - 0.25f, 0.02f), color);
            }
        }
        // Dunkle Kanten oben und unten, damit man gestapelte Container auseinanderhält. Etwas unter der Oberseite bzw. über
        // der Unterseite des Körpers: Lägen ihre Flächen genau auf denen des Körpers, flackerten beide (Z-Fighting), am
        // auffälligsten auf dem Dach, auf dem man steht
        PrimitiveKit.Box("Bottom Rail", c, new Vector3(0f, 0.1f, 0f), new Vector3(ContainerLength + 0.02f, 0.14f, ContainerDepth + 0.04f), rib);
        PrimitiveKit.Box("Top Rail", c, new Vector3(0f, ContainerHeight - 0.1f, 0f), new Vector3(ContainerLength + 0.02f, 0.14f, ContainerDepth + 0.04f), rib);
        // Türen am Ende: zwei Flügel mit Verriegelungsstangen
        foreach (float z in new[] { -0.6f, 0.6f })
        {
            PrimitiveKit.Box("Door Bar", c, new Vector3(ContainerLength / 2f + 0.02f, ContainerHeight / 2f, z), new Vector3(0.04f, ContainerHeight - 0.4f, 0.06f), rib);
        }
    }

    // Stapel aus 1–3 Containern auf der Bahn, ein Collider über alles (Layer Ground)
    private static void BuildStack(int levels)
    {
        var root = new GameObject($"Container Stack {levels}") { layer = GroundLayer };
        for (int level = 0; level < levels; level++)
        {
            Container(root.transform, Surface + level * ContainerHeight, containerColors[(levels * 2 + level * 3) % containerColors.Length]);
        }
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + levels * ContainerHeight / 2f, 0f), new Vector3(ContainerLength, levels * ContainerHeight, ContainerDepth));
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Container Stack {levels}.prefab");
    }

    // Holzkisten-Stapel als Stufe (2 m lang, levels × 1.3 m hoch, begehbar): steht dort, wo man zwischen die Container auf den
    // Boden fallen kann, damit man wieder hinaufkommt (ein Container ist mit 2.6 m bzw. gestapelt 5.2 m zu hoch für einen Sprung)
    private static void BuildCrateStep(string name, int levels)
    {
        Material wood = PrimitiveKit.Material($"{MaterialFolder}/Crate Wood.mat", PrimitiveKit.LitShader, new Color(0.72f, 0.55f, 0.33f));
        Material dark = PrimitiveKit.Material($"{MaterialFolder}/Door Wood.mat", PrimitiveKit.LitShader, new Color(0.42f, 0.25f, 0.13f));
        const float crate = 1.3f, length = 2f;
        var root = new GameObject(name) { layer = GroundLayer };
        Transform t = root.transform;
        for (int level = 0; level < levels; level++)
        {
            float y = Surface + level * crate;
            // Zwei Kisten nebeneinander, die obere Lage etwas versetzt (sieht gestapelt aus)
            for (int i = 0; i < 2; i++)
            {
                float x = -length / 4f + i * length / 2f + (level % 2 == 0 ? 0f : 0.05f);
                PrimitiveKit.Box("Crate", t, new Vector3(x, y + crate / 2f, 0f), new Vector3(length / 2f - 0.04f, crate - 0.04f, 1.4f), wood);
                PrimitiveKit.Box("Slat", t, new Vector3(x, y + 0.3f, -0.71f), new Vector3(length / 2f - 0.02f, 0.1f, 0.02f), dark);
                PrimitiveKit.Box("Slat", t, new Vector3(x, y + crate - 0.3f, -0.71f), new Vector3(length / 2f - 0.02f, 0.1f, 0.02f), dark);
            }
        }
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + levels * crate / 2f, 0f), new Vector3(length, levels * crate, 1.4f));
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/{name}.prefab");
    }

    // Container am Kranseil: MovingPlatform (fährt von "moving platform start" nach "moving platform end", setzt der
    // Level-Generator: waagerecht = Fähre, senkrecht = Aufzug). Oberkante = Wurzel + 0.5 (wie die Hebebühne).
    // Obendrauf ein Spreader (Greifrahmen), vier kurze Seile zum Haken, CraneCable streckt das Hauptseil zur Laufkatze
    private static void BuildCraneContainer()
    {
        var root = new GameObject("Crane Container") { layer = GroundLayer };
        Transform t = root.transform;
        float bottom = Surface - ContainerHeight;
        Container(t, bottom, containerColors[3]);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface - ContainerHeight / 2f, 0f), new Vector3(ContainerLength, ContainerHeight, ContainerDepth));

        // Spreader knapp über dem Dach (Oberkante bleibt begehbar: er ist schmaler als der Container und hat keinen Collider)
        PrimitiveKit.Box("Spreader", t, new Vector3(0f, Surface + 0.12f, 0f), new Vector3(ContainerLength - 0.4f, 0.2f, 0.5f), hookYellow);
        Vector3 hookPoint = new Vector3(0f, Surface + 1.8f, 0f);
        foreach (float x in new[] { -2.4f, 2.4f })
        {
            Vector3 from = new Vector3(x, Surface + 0.2f, 0f);
            Renderer sling = PrimitiveKit.Box("Sling", t, (from + hookPoint) / 2f, new Vector3(0.05f, Vector3.Distance(from, hookPoint), 0.05f), cable);
            sling.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (hookPoint - from).normalized);
        }
        Transform hook = PrimitiveKit.Pivot("Hook", t, hookPoint);
        PrimitiveKit.Box("Hook Block", hook, new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.4f, 0.35f), hookYellow);

        // Hauptseil und Laufkatze: Position und Länge setzt CraneCable jeden Frame (topY setzt der Level-Generator)
        Transform mainCable = PrimitiveKit.Box("Main Cable", t, hookPoint + Vector3.up * 2f, new Vector3(0.07f, 1f, 0.07f), cable).transform;
        Transform trolley = PrimitiveKit.Pivot("Trolley", t, hookPoint + Vector3.up * 4f);
        PrimitiveKit.Box("Trolley Body", trolley, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.7f, 1.2f), hookYellow);
        var cables = new SerializedObject(root.AddComponent<CraneCable>());
        cables.FindProperty("hook").objectReferenceValue = hook;
        cables.FindProperty("cable").objectReferenceValue = mainCable;
        cables.FindProperty("trolley").objectReferenceValue = trolley;
        cables.ApplyModifiedPropertiesWithoutUndo();

        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        Transform start = PrimitiveKit.Pivot("moving platform start", t, Vector3.zero);
        Transform end = PrimitiveKit.Pivot("moving platform end", t, Vector3.right * 8f);
        var platform = new SerializedObject(root.AddComponent<MovingPlatform>());
        platform.FindProperty("pointA").objectReferenceValue = start;
        platform.FindProperty("pointB").objectReferenceValue = end;
        platform.FindProperty("speed").floatValue = 2.5f;
        platform.FindProperty("waitTime").floatValue = 1f;
        platform.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Crane Container.prefab");
    }

    // ---------- Ziel: Bauarbeiter auf dem Stahlträger ----------

    // Wurzel auf der Bahn mit LevelGoal und Trigger (der Spieler läuft unter dem Träger durch). Der Träger hängt 2 m über dem
    // Kai, etwas hinter der Bahn, an zwei Seilen, die zu einem Haken weit oben laufen. Fünf Arbeiter sitzen darauf (schauen zur
    // Kamera, Beine baumeln): der Vorarbeiter in der Mitte (weißer Helm) ist der Kunde, die anderen jubeln mit
    private static void BuildCrewGoal()
    {
        var root = new GameObject("Crew Goal");
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 1.5f, 0f), new Vector3(2f, 3f, 3f), true);
        root.AddComponent<LevelGoal>();

        const float beamZ = 0.7f, beamBottom = 2.0f, beamLength = 7f;
        float beamTop = Surface + beamBottom + 0.35f;
        // I-Träger: oberer und unterer Flansch, Steg dazwischen
        PrimitiveKit.Box("Beam Top Flange", t, new Vector3(0f, beamTop - 0.03f, beamZ), new Vector3(beamLength, 0.06f, 0.36f), beamRed);
        PrimitiveKit.Box("Beam Web", t, new Vector3(0f, beamTop - 0.175f, beamZ), new Vector3(beamLength, 0.23f, 0.06f), beamRed);
        PrimitiveKit.Box("Beam Bottom Flange", t, new Vector3(0f, beamTop - 0.32f, beamZ), new Vector3(beamLength, 0.06f, 0.36f), beamRed);
        // Seile von beiden Enden zum Haken, von dort ein Seil hinauf aus dem Bild
        Vector3 hook = new Vector3(0f, beamTop + 6f, beamZ);
        foreach (float x in new[] { -beamLength / 2f + 0.4f, beamLength / 2f - 0.4f })
        {
            Vector3 from = new Vector3(x, beamTop, beamZ);
            Renderer sling = PrimitiveKit.Box("Sling", t, (from + hook) / 2f, new Vector3(0.05f, Vector3.Distance(from, hook), 0.05f), cable);
            sling.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (hook - from).normalized);
        }
        PrimitiveKit.Box("Hook Block", t, hook + Vector3.up * 0.2f, new Vector3(0.5f, 0.5f, 0.4f), hookYellow);
        PrimitiveKit.Box("Hoist Cable", t, hook + Vector3.up * 20f, new Vector3(0.08f, 40f, 0.08f), cable);
        // Brotdose und Thermoskanne neben dem letzten Arbeiter
        PrimitiveKit.Box("Lunch Box", t, new Vector3(beamLength / 2f - 0.3f, beamTop + 0.09f, beamZ), new Vector3(0.3f, 0.18f, 0.2f), lunchBox);
        PrimitiveKit.Part("Thermos", PrimitiveType.Cylinder, t, new Vector3(-beamLength / 2f + 0.25f, beamTop + 0.15f, beamZ), new Vector3(0.1f, 0.15f, 0.1f), steel);

        float[] seats = { -2.6f, -1.3f, 0f, 1.3f, 2.6f };
        Color[] skins = { new Color(0.96f, 0.78f, 0.62f), new Color(0.7f, 0.5f, 0.35f), new Color(0.88f, 0.68f, 0.52f), new Color(0.5f, 0.34f, 0.24f), new Color(0.98f, 0.86f, 0.75f) };
        for (int i = 0; i < seats.Length; i++)
        {
            bool foreman = i == 2;
            ParkSetup.Figure f = ParkSetup.BuildFigure(t, foreman ? "Foreman" : $"Worker {i + 1}", new Vector3(seats[i], beamTop, beamZ - 0.05f), (i - 2) * 6f);
            // Helm (halbe Kugel mit Krempe) und zwei Reflexstreifen auf der Weste
            PrimitiveKit.Part("Hard Hat", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.47f, 0f), new Vector3(0.5f, 0.34f, 0.5f), foreman ? hatWhite : hatYellow);
            PrimitiveKit.Part("Hat Brim", PrimitiveType.Cylinder, f.rig, new Vector3(0f, 1.43f, -0.02f), new Vector3(0.58f, 0.012f, 0.58f), foreman ? hatWhite : hatYellow);
            PrimitiveKit.Part("Vest Stripe", PrimitiveType.Cylinder, f.rig, new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.035f, 0.62f), reflective);
            PrimitiveKit.Part("Vest Stripe", PrimitiveType.Cylinder, f.rig, new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.035f, 0.62f), reflective);
            // Beine: Drehpunkt an der Sitzkante vorne, Hosenbein nach unten, Stiefel
            Transform[] legs = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.13f : 0.13f;
                legs[side] = PrimitiveKit.Pivot(side == 0 ? "Leg L" : "Leg R", f.rig, new Vector3(x, 0.12f, -0.2f));
                PrimitiveKit.Part("Pant Leg", PrimitiveType.Capsule, legs[side], new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.25f, 0.16f), steel);
                PrimitiveKit.Box("Boot", legs[side], new Vector3(0f, -0.52f, -0.05f), new Vector3(0.16f, 0.12f, 0.28f), PrimitiveKit.Material($"{MaterialFolder}/Door Wood.mat", PrimitiveKit.LitShader, new Color(0.42f, 0.25f, 0.13f)));
            }
            Color vest = foreman ? new Color(1f, 0.45f, 0.08f) : (i % 2 == 0 ? new Color(1f, 0.55f, 0.1f) : new Color(0.85f, 0.95f, 0.15f));
            if (foreman)
            {
                var customer = new SerializedObject(f.root.gameObject.AddComponent<Customer>());
                customer.FindProperty("rig").objectReferenceValue = f.rig;
                customer.FindProperty("leftArm").objectReferenceValue = f.leftArm;
                customer.FindProperty("rightArm").objectReferenceValue = f.rightArm;
                customer.FindProperty("seatedOnBeam").boolValue = true;
                customer.FindProperty("sitDrop").floatValue = 0f;
                customer.FindProperty("leftLeg").objectReferenceValue = legs[0];
                customer.FindProperty("rightLeg").objectReferenceValue = legs[1];
                customer.FindProperty("comeOutDistance").floatValue = 10f;
                customer.FindProperty("shirtColor").colorValue = vest;
                customer.FindProperty("skinColor").colorValue = skins[i];
                PrimitiveKit.SetReferences(customer, "shirtRenderers", f.body);
                PrimitiveKit.SetReferences(customer, "skinRenderers", f.head, f.leftArmMesh, f.rightArmMesh);
                customer.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var worker = new SerializedObject(f.root.gameObject.AddComponent<BeamWorker>());
                worker.FindProperty("rig").objectReferenceValue = f.rig;
                worker.FindProperty("leftArm").objectReferenceValue = f.leftArm;
                worker.FindProperty("rightArm").objectReferenceValue = f.rightArm;
                worker.FindProperty("leftLeg").objectReferenceValue = legs[0];
                worker.FindProperty("rightLeg").objectReferenceValue = legs[1];
                worker.FindProperty("phase").floatValue = i * 1.7f;
                worker.FindProperty("vestColor").colorValue = vest;
                worker.FindProperty("skinColor").colorValue = skins[i];
                PrimitiveKit.SetReferences(worker, "vestRenderers", f.body);
                PrimitiveKit.SetReferences(worker, "skinRenderers", f.head, f.leftArmMesh, f.rightArmMesh);
                worker.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Crew Goal.prefab");
    }
}
