using TMPro;
using UnityEditor;
using UnityEngine;

// Prefabs für den Pesto Park (Level 3), aus Grundformen im Lane-Rahmen (lokal +X = Laufrichtung, -Z = Kameraseite).
// Wurzel wie bei allen Prefabs auf der Mitte einer 1 m dicken Platte, Boden also bei +0.5 (die Parkwege liegen 4 cm höher,
// das sieht man nicht). Menü: Tools > Jump and Run > City > Build Park Prefabs.
//  - "Frisbee Players": zwei Leute 8 m auseinander auf der Bahn (feste Hindernisse, man kann auf ihnen stehen, "HEY!"),
//    dazwischen die Frisbee (Trigger mit KillZone), gesteuert von FrisbeePlayers
//  - "Frisbee Players Across": dieselben zwei, aber vor und hinter der Bahn; die Frisbee fliegt quer über den Weg
//    (Timing wie bei den Autos, die Leute stehen nicht im Weg)
//  - "Duck Family": weiße Entenmutter (GroundPatrol + EnemyDamage + Duck) und fünf gelbe Küken (Duckling), die ihre Spur
//    mit Verzögerung nachlaufen. Wurzel 1 m über der Plattform-Mitte wie beim Hund
//  - "Skater": Rollschuhfahrer (GroundPatrol ohne Sicht + Skater) für den Brunnenring, fährt hin und her. Draufspringen
//    = abfedern, er rudert und fährt weiter. Wurzel 1 m über der Plattform-Mitte wie beim Hund
//  - "Picnic Goal": Ziel von Level 3 ohne Haus: Picknickdecke mit Korb, der Kunde (Customer, sitzt) und eine Freundin
//    (PicnicFriend). Level 3 setzt es statt des Goal-Prefabs mit Kundenhaus ein (Name bleibt "Goal")
public static class ParkSetup
{
    private const int GroundLayer = 3;
    private const float Surface = 0.5f;
    private const string MaterialFolder = "Assets/Materials/City";
    public const string PrefabFolder = "Assets/Prefabs/Park";

    private static Material figure, frisbeeRed, bubbleWhite, duckWhite, duckOrange, ducklingYellow, eyeBlack;
    public static readonly float[] DucklingSpacing = { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f };
    public const float DuckPatrolSpeed = 2.5f;

    [MenuItem("Tools/Jump and Run/City/Build Park Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Park");
        CreateMaterials();
        BuildFrisbeePlayers(false);
        BuildFrisbeePlayers(true);
        BuildDuckFamily();
        BuildSkater();
        BuildPicnicGoal();
        AssetDatabase.SaveAssets();
        Debug.Log("ParkSetup: Park-Prefabs gespeichert.");
    }

    private static void CreateMaterials()
    {
        // Weiß, die Farben kommen per MaterialPropertyBlock (wie beim Kunden)
        figure = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Customer.mat");
        frisbeeRed = PrimitiveKit.Material($"{MaterialFolder}/Frisbee.mat", PrimitiveKit.LitShader, new Color(0.95f, 0.2f, 0.15f), 0.6f);
        bubbleWhite = PrimitiveKit.Material($"{MaterialFolder}/Sign White.mat", PrimitiveKit.LitShader, new Color(0.97f, 0.96f, 0.92f));
        duckWhite = PrimitiveKit.Material($"{MaterialFolder}/Duck White.mat", PrimitiveKit.LitShader, new Color(0.96f, 0.96f, 0.93f), 0.3f);
        duckOrange = PrimitiveKit.Material($"{MaterialFolder}/Duck Orange.mat", PrimitiveKit.LitShader, new Color(1f, 0.55f, 0.1f));
        ducklingYellow = PrimitiveKit.Material($"{MaterialFolder}/Duckling Yellow.mat", PrimitiveKit.LitShader, new Color(1f, 0.85f, 0.25f));
        eyeBlack = PrimitiveKit.Material($"{MaterialFolder}/Iron.mat", PrimitiveKit.LitShader, new Color(0.14f, 0.14f, 0.16f), 0.5f);
    }

    // ---------- Frisbee ----------

    // across = false: beide auf der Bahn (lokal X), across = true: vor (Kameraseite, -Z) und hinter der Bahn (+Z)
    private static void BuildFrisbeePlayers(bool across)
    {
        var root = new GameObject(across ? "Frisbee Players Across" : "Frisbee Players");
        Transform t = root.transform;
        Figure a, b;
        Vector3 handA, handB;
        if (across)
        {
            // A steht vorne und dreht der Kamera halb den Rücken zu (schaut zu B), B steht hinten und schaut zu A und zur Kamera
            const float apart = 3.5f;
            a = BuildFigure(t, "Player A", new Vector3(0.3f, Surface, -apart), 160f);
            b = BuildFigure(t, "Player B", new Vector3(-0.3f, Surface, apart), 20f);
            handA = new Vector3(0.2f, 1.7f, -apart + 0.5f);
            handB = new Vector3(-0.2f, 1.7f, apart - 0.5f);
        }
        else
        {
            // Beide schauen schräg zueinander und zur Kamera (die Figur schaut bei Drehung 0 nach -Z, zur Kamera).
            // Geworfen wird mit dem Arm auf der Kameraseite: bei A der linke, bei B der rechte
            const float apart = 4f;
            a = BuildFigure(t, "Player A", new Vector3(-apart, Surface, 0f), -45f);
            b = BuildFigure(t, "Player B", new Vector3(apart, Surface, 0f), 45f);
            Hitbox(t, "Hitbox A", -apart);
            Hitbox(t, "Hitbox B", apart);
            handA = new Vector3(-3.5f, 1.7f, 0f);
            handB = new Vector3(3.5f, 1.7f, 0f);
        }

        var frisbeeObject = new GameObject("Frisbee");
        frisbeeObject.transform.SetParent(t, false);
        frisbeeObject.transform.localPosition = handA;
        var body = frisbeeObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var trigger = frisbeeObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.3f;
        frisbeeObject.AddComponent<KillZone>();
        Transform disc = PrimitiveKit.Pivot("Disc", frisbeeObject.transform, Vector3.zero);
        PrimitiveKit.Part("Plate", PrimitiveType.Cylinder, disc, Vector3.zero, new Vector3(0.5f, 0.025f, 0.5f), frisbeeRed);
        PrimitiveKit.Part("Rim", PrimitiveType.Cylinder, disc, new Vector3(0f, -0.02f, 0f), new Vector3(0.52f, 0.02f, 0.52f), bubbleWhite);

        var serialized = new SerializedObject(root.AddComponent<FrisbeePlayers>());
        serialized.FindProperty("frisbee").objectReferenceValue = body;
        serialized.FindProperty("handA").vector3Value = handA;
        serialized.FindProperty("handB").vector3Value = handB;
        serialized.FindProperty("disc").objectReferenceValue = disc;
        serialized.FindProperty("armA").objectReferenceValue = across ? a.rightArm : a.leftArm;
        serialized.FindProperty("armB").objectReferenceValue = b.rightArm;
        PrimitiveKit.SetReferences(serialized, "shirtRenderersA", a.body);
        PrimitiveKit.SetReferences(serialized, "shirtRenderersB", b.body);
        PrimitiveKit.SetReferences(serialized, "skinRenderersA", a.head, a.leftArmMesh, a.rightArmMesh);
        PrimitiveKit.SetReferences(serialized, "skinRenderersB", b.head, b.leftArmMesh, b.rightArmMesh);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/{root.name}.prefab");
    }

    // Fester Körper einer Person (Layer Ground: Man kann darauf stehen, Hunde/Enten drehen davor um), ungedreht,
    // mit "HEY!"-Blase, wenn man auf dem Kopf landet
    private static void Hitbox(Transform parent, string name, float x)
    {
        var hitbox = new GameObject(name);
        hitbox.layer = GroundLayer;
        hitbox.transform.SetParent(parent, false);
        hitbox.transform.localPosition = new Vector3(x, Surface, 0f);
        PrimitiveKit.AddBoxCollider(hitbox, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 1.6f, 0.7f));
        GameObject bubble = SpeechBubble(parent, new Vector3(x + 0.4f, Surface + 2.4f, -0.2f), "HEY!");
        var hey = new SerializedObject(hitbox.AddComponent<HeyBubble>());
        hey.FindProperty("bubble").objectReferenceValue = bubble;
        hey.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sprechblase wie beim Hund: weißes Feld mit Spitze, dunkler Text, schaut zur Kamera. Startet ausgeschaltet
    public static GameObject SpeechBubble(Transform parent, Vector3 localPosition, string content)
    {
        CreateMaterials();
        Transform bubble = PrimitiveKit.Pivot(content + " Bubble", parent, localPosition);
        PrimitiveKit.Quad("Background", bubble, Vector3.zero, new Vector2(1.1f, 0.45f), bubbleWhite);
        PrimitiveKit.Quad("Pointer", bubble, new Vector3(-0.15f, -0.25f, 0f), new Vector2(0.16f, 0.16f), bubbleWhite)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        TextMeshPro text = PrimitiveKit.Text("Text", bubble, new Vector3(0f, 0f, -0.01f), new Vector2(1f, 0.38f), content, new Color(0.15f, 0.1f, 0.05f));
        text.fontSizeMax = 4f;
        bubble.gameObject.SetActive(false);
        return bubble.gameObject;
    }

    // ---------- Enten ----------

    private static void BuildDuckFamily()
    {
        var root = new GameObject("Duck Family");
        Transform t = root.transform;

        // Mutter: Wurzel 1 m über der Plattform-Mitte, Füße also bei -0.46 (Parkweg 0.54). Optik schaut nach lokal +X
        var motherObject = new GameObject("Mother Duck");
        motherObject.transform.SetParent(t, false);
        PrimitiveKit.AddBoxCollider(motherObject, new Vector3(0f, -0.2f, 0f), new Vector3(0.8f, 0.6f, 0.45f));
        var motherBody = motherObject.AddComponent<Rigidbody>();
        motherBody.isKinematic = true;
        motherBody.useGravity = false;
        motherBody.interpolation = RigidbodyInterpolation.Interpolate;

        Transform body = PrimitiveKit.Pivot("Body", motherObject.transform, Vector3.zero);
        Transform waddle = PrimitiveKit.Pivot("Waddle", body, new Vector3(0f, -0.45f, 0f));
        PrimitiveKit.Part("Belly", PrimitiveType.Sphere, waddle, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.4f, 0.45f), duckWhite);
        PrimitiveKit.Part("Tail", PrimitiveType.Sphere, waddle, new Vector3(-0.36f, 0.4f, 0f), new Vector3(0.25f, 0.15f, 0.2f), duckWhite)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
        Transform neck = PrimitiveKit.Pivot("Neck", waddle, new Vector3(0.24f, 0.42f, 0f));
        PrimitiveKit.Part("Neck Mesh", PrimitiveType.Capsule, neck, new Vector3(0f, 0.12f, 0f), new Vector3(0.15f, 0.15f, 0.15f), duckWhite);
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, neck, new Vector3(0.04f, 0.3f, 0f), Vector3.one * 0.26f, duckWhite);
        PrimitiveKit.Box("Beak", neck, new Vector3(0.2f, 0.28f, 0f), new Vector3(0.18f, 0.06f, 0.13f), duckOrange);
        foreach (float z in new[] { -0.11f, 0.11f })
        {
            PrimitiveKit.Part("Eye", PrimitiveType.Sphere, neck, new Vector3(0.1f, 0.35f, z), Vector3.one * 0.05f, eyeBlack);
        }
        // Flügel: flache Platte, die von der Schulter seitlich absteht (lokal ±Z). Duck klappt sie in Ruhe nach unten an den
        // Körper (Drehung um X) und schlägt sie im Flug auf und ab
        Transform wingLeft = PrimitiveKit.Pivot("Wing L", waddle, new Vector3(-0.02f, 0.42f, 0.2f));
        PrimitiveKit.Part("Wing", PrimitiveType.Sphere, wingLeft, new Vector3(-0.06f, 0f, 0.2f), new Vector3(0.42f, 0.05f, 0.4f), duckWhite);
        Transform wingRight = PrimitiveKit.Pivot("Wing R", waddle, new Vector3(-0.02f, 0.42f, -0.2f));
        PrimitiveKit.Part("Wing", PrimitiveType.Sphere, wingRight, new Vector3(-0.06f, 0f, -0.2f), new Vector3(0.42f, 0.05f, 0.4f), duckWhite);
        // In Ruhe angelegt (sonst stünden sie im Editor waagerecht ab)
        wingLeft.localRotation = Quaternion.Euler(Duck.WingFoldAngle, 0f, 0f);
        wingRight.localRotation = Quaternion.Euler(-Duck.WingFoldAngle, 0f, 0f);
        Transform[] legs = DuckLegs(waddle, 0.1f, 0.12f, 1f);

        GameObject quack = SpeechBubble(motherObject.transform, new Vector3(0.4f, 0.75f, -0.3f), "QUACK!");
        ParticleSystem feathers = CityPropsSetup.BuildSparkles(t);
        feathers.name = "Feathers";
        ParticleSystem.MainModule main = feathers.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.9f, 0.9f, 0.85f));
        main.gravityModifier = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);

        var patrol = new SerializedObject(motherObject.AddComponent<GroundPatrol>());
        patrol.FindProperty("patrolSpeed").floatValue = DuckPatrolSpeed;
        patrol.FindProperty("chaseSpeed").floatValue = 6f;
        patrol.FindProperty("sightRange").floatValue = 4f;
        patrol.FindProperty("loseSightTime").floatValue = 0.8f;
        patrol.FindProperty("flapOverGaps").boolValue = true;
        patrol.FindProperty("maxFlapGap").floatValue = 4f;
        patrol.FindProperty("flipTarget").objectReferenceValue = body;
        patrol.ApplyModifiedPropertiesWithoutUndo();
        motherObject.AddComponent<EnemyDamage>();
        var duck = motherObject.AddComponent<Duck>();

        // Küken hinter der Mutter (Startrichtung +u), der Level-Generator setzt sie passend zur Blickrichtung
        var ducklings = new Duckling[DucklingSpacing.Length];
        for (int i = 0; i < ducklings.Length; i++)
        {
            ducklings[i] = BuildDuckling(t, i + 1, -DucklingSpacing[i], duck, DucklingSpacing[i]);
        }

        var serialized = new SerializedObject(duck);
        serialized.FindProperty("body").objectReferenceValue = body;
        serialized.FindProperty("waddle").objectReferenceValue = waddle;
        serialized.FindProperty("neck").objectReferenceValue = neck;
        serialized.FindProperty("wingLeft").objectReferenceValue = wingLeft;
        serialized.FindProperty("wingRight").objectReferenceValue = wingRight;
        PrimitiveKit.SetReferences(serialized, "legs", legs);
        serialized.FindProperty("quackBubble").objectReferenceValue = quack;
        serialized.FindProperty("feathers").objectReferenceValue = feathers;
        PrimitiveKit.SetReferences(serialized, "ducklings", ducklings);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Duck Family.prefab");
    }

    // Küken: gleiche Bauweise wie die Mutter, kleiner und gelb. Eigene Wurzel mit Rigidbody, Collider, EnemyDamage
    private static Duckling BuildDuckling(Transform parent, int number, float x, Duck mother, float spacing)
    {
        var ducklingObject = new GameObject("Duckling " + number);
        ducklingObject.transform.SetParent(parent, false);
        ducklingObject.transform.localPosition = new Vector3(x, 0f, 0f);
        PrimitiveKit.AddBoxCollider(ducklingObject, new Vector3(0f, -0.34f, 0f), new Vector3(0.4f, 0.32f, 0.3f));
        var rigidbody = ducklingObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        Transform body = PrimitiveKit.Pivot("Body", ducklingObject.transform, new Vector3(0f, -0.46f, 0f));
        PrimitiveKit.Part("Belly", PrimitiveType.Sphere, body, new Vector3(0f, 0.15f, 0f), new Vector3(0.34f, 0.22f, 0.24f), ducklingYellow);
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, body, new Vector3(0.12f, 0.3f, 0f), Vector3.one * 0.17f, ducklingYellow);
        PrimitiveKit.Box("Beak", body, new Vector3(0.22f, 0.29f, 0f), new Vector3(0.09f, 0.03f, 0.07f), duckOrange);
        foreach (float z in new[] { -0.06f, 0.06f })
        {
            PrimitiveKit.Part("Eye", PrimitiveType.Sphere, body, new Vector3(0.17f, 0.33f, z), Vector3.one * 0.03f, eyeBlack);
        }
        Transform[] legs = DuckLegs(body, 0.05f, 0.05f, 0.45f);

        ducklingObject.AddComponent<EnemyDamage>();
        var duckling = ducklingObject.AddComponent<Duckling>();
        var serialized = new SerializedObject(duckling);
        serialized.FindProperty("mother").objectReferenceValue = mother;
        serialized.FindProperty("spacing").floatValue = spacing;
        serialized.FindProperty("body").objectReferenceValue = body;
        PrimitiveKit.SetReferences(serialized, "legs", legs);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return duckling;
    }

    // Zwei orange Beine mit Füßen, Drehpunkt an der Hüfte (schwingen um lokal Z). hipY über den Füßen
    private static Transform[] DuckLegs(Transform parent, float sideZ, float hipY, float scale)
    {
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float z = i == 0 ? -sideZ : sideZ;
            legs[i] = PrimitiveKit.Pivot("Leg", parent, new Vector3(0.02f, hipY, z));
            PrimitiveKit.Part("Shin", PrimitiveType.Cylinder, legs[i], new Vector3(0f, -hipY / 2f, 0f), new Vector3(0.04f * scale, hipY / 2f, 0.04f * scale), duckOrange);
            PrimitiveKit.Box("Foot", legs[i], new Vector3(0.04f * scale, -hipY + 0.01f, 0f), new Vector3(0.13f * scale, 0.02f, 0.09f * scale), duckOrange);
        }
        return legs;
    }

    // ---------- Skater ----------

    private static void BuildSkater()
    {
        // Füße bei -0.46 (Parkweg 0.54, Wurzel 1 m über der Plattform-Mitte), die Figur steht 0.14 höher auf den Rollschuhen
        const float feet = -0.46f, skateHeight = 0.14f;
        var root = new GameObject("Skater");
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, feet + 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.6f));
        var rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // Körper (wird zum Umdrehen gespiegelt), darin die Figur im Profil: Sie schaut bei Drehung 0 nach -Z, -90° = nach +X
        Transform body = PrimitiveKit.Pivot("Body", t, Vector3.zero);
        Figure f = BuildFigure(body, "Figure", new Vector3(0f, feet + skateHeight, 0f), -90f);
        Renderer helmet = PrimitiveKit.Part("Helmet", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.45f, 0.02f), new Vector3(0.5f, 0.36f, 0.52f), figure);
        Renderer visor = PrimitiveKit.Part("Visor", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.42f, -0.22f), new Vector3(0.36f, 0.06f, 0.16f), figure);

        // Rollschuhe unter der Figur (nicht am Rig, damit sie beim Vorlehnen auf dem Boden bleiben)
        var skates = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            skates[i] = PrimitiveKit.Pivot(i == 0 ? "Skate L" : "Skate R", f.root, new Vector3(side * 0.13f, -skateHeight, 0f));
            PrimitiveKit.Box("Boot", skates[i], new Vector3(0f, 0.1f, 0f), new Vector3(0.15f, 0.13f, 0.3f), eyeBlack);
            foreach (float z in new[] { -0.11f, -0.04f, 0.04f, 0.11f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, skates[i], new Vector3(0f, 0.035f, z), new Vector3(0.07f, 0.015f, 0.07f), ducklingYellow)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        // Sprechblase an der Wurzel (nicht am gespiegelten Körper), zur Kamera
        GameObject whoa = SpeechBubble(t, new Vector3(0.4f, feet + 2.25f, -0.3f), "WHOA!");

        var patrol = new SerializedObject(root.AddComponent<GroundPatrol>());
        patrol.FindProperty("patrolSpeed").floatValue = 6f;
        patrol.FindProperty("chaseSpeed").floatValue = 6f;
        patrol.FindProperty("acceleration").floatValue = 8f;
        patrol.FindProperty("deceleration").floatValue = 10f;
        patrol.FindProperty("turnDuration").floatValue = 0.35f;
        // Sieht niemanden: fährt nur seine Runde
        patrol.FindProperty("sightRange").floatValue = 0f;
        patrol.FindProperty("usePatrolLimits").boolValue = true;
        patrol.FindProperty("flipTarget").objectReferenceValue = body;
        patrol.ApplyModifiedPropertiesWithoutUndo();

        var skater = new SerializedObject(root.AddComponent<Skater>());
        skater.FindProperty("rig").objectReferenceValue = f.rig;
        skater.FindProperty("leftArm").objectReferenceValue = f.leftArm;
        skater.FindProperty("rightArm").objectReferenceValue = f.rightArm;
        skater.FindProperty("leftSkate").objectReferenceValue = skates[0];
        skater.FindProperty("rightSkate").objectReferenceValue = skates[1];
        skater.FindProperty("whoaBubble").objectReferenceValue = whoa;
        PrimitiveKit.SetReferences(skater, "shirtRenderers", f.body);
        PrimitiveKit.SetReferences(skater, "skinRenderers", f.head, f.leftArmMesh, f.rightArmMesh);
        PrimitiveKit.SetReferences(skater, "helmetRenderers", helmet, visor);
        skater.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Skater.prefab");
    }

    // ---------- Picknick (Ziel von Level 3) ----------

    // Wurzel auf der Laufbahn (wie das Goal-Prefab): Trigger davor, Decke dahinter (lokal +Z, Teichseite)
    private static void BuildPicnicGoal()
    {
        Material red = PrimitiveKit.Material($"{MaterialFolder}/Blanket Red.mat", PrimitiveKit.LitShader, new Color(0.85f, 0.18f, 0.16f));
        Material wicker = PrimitiveKit.Material($"{MaterialFolder}/Wicker.mat", PrimitiveKit.LitShader, new Color(0.7f, 0.5f, 0.28f));
        Material juice = PrimitiveKit.Material($"{MaterialFolder}/Juice.mat", PrimitiveKit.LitShader, new Color(1f, 0.6f, 0.1f), 0.6f);

        var root = new GameObject("Picnic Goal");
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + 1.5f, 0f), new Vector3(2f, 3f, 3f), true);
        root.AddComponent<LevelGoal>();

        // Karierte Decke aus 6 x 4 Feldern
        Transform blanket = PrimitiveKit.Pivot("Blanket", t, new Vector3(0.8f, Surface + 0.045f, 1.9f));
        const float tile = 0.4f;
        for (int x = 0; x < 6; x++)
        {
            for (int z = 0; z < 4; z++)
            {
                PrimitiveKit.Box("Tile", blanket, new Vector3((x - 2.5f) * tile, 0f, (z - 1.5f) * tile), new Vector3(tile, 0.01f, tile), (x + z) % 2 == 0 ? red : bubbleWhite);
            }
        }
        // Korb mit Deckel und Henkel, Teller, Saftflasche
        PrimitiveKit.Box("Basket", t, new Vector3(-0.6f, Surface + 0.2f, 2.3f), new Vector3(0.6f, 0.35f, 0.4f), wicker);
        PrimitiveKit.Box("Basket Lid", t, new Vector3(-0.6f, Surface + 0.39f, 2.3f), new Vector3(0.64f, 0.04f, 0.44f), wicker);
        PrimitiveKit.Box("Handle", t, new Vector3(-0.6f, Surface + 0.55f, 2.3f), new Vector3(0.45f, 0.04f, 0.04f), wicker);
        foreach (float x in new[] { -0.82f, -0.38f })
        {
            PrimitiveKit.Box("Handle Side", t, new Vector3(x, Surface + 0.47f, 2.3f), new Vector3(0.04f, 0.17f, 0.04f), wicker);
        }
        PrimitiveKit.Part("Plate", PrimitiveType.Cylinder, t, new Vector3(1.0f, Surface + 0.06f, 1.4f), new Vector3(0.35f, 0.01f, 0.35f), bubbleWhite);
        PrimitiveKit.Part("Plate", PrimitiveType.Cylinder, t, new Vector3(1.5f, Surface + 0.06f, 2.6f), new Vector3(0.35f, 0.01f, 0.35f), bubbleWhite);
        PrimitiveKit.Part("Juice", PrimitiveType.Cylinder, t, new Vector3(0.2f, Surface + 0.2f, 2.5f), new Vector3(0.12f, 0.15f, 0.12f), juice);

        // Kunde sitzt vorne auf der Decke, schaut zur Kamera; die Freundin daneben, etwas zu ihm gedreht
        Figure customerFigure = BuildFigure(t, "Customer", new Vector3(0.3f, Surface, 1.5f), 0f);
        var customer = new SerializedObject(customerFigure.root.gameObject.AddComponent<Customer>());
        customer.FindProperty("rig").objectReferenceValue = customerFigure.rig;
        customer.FindProperty("leftArm").objectReferenceValue = customerFigure.leftArm;
        customer.FindProperty("rightArm").objectReferenceValue = customerFigure.rightArm;
        customer.FindProperty("sitsOnBlanket").boolValue = true;
        customer.FindProperty("shirtColor").colorValue = new Color(0.2f, 0.62f, 0.3f);
        PrimitiveKit.SetReferences(customer, "shirtRenderers", customerFigure.body);
        PrimitiveKit.SetReferences(customer, "skinRenderers", customerFigure.head, customerFigure.leftArmMesh, customerFigure.rightArmMesh);
        customer.ApplyModifiedPropertiesWithoutUndo();

        Figure friendFigure = BuildFigure(t, "Friend", new Vector3(1.7f, Surface, 2.2f), -25f);
        var friend = new SerializedObject(friendFigure.root.gameObject.AddComponent<PicnicFriend>());
        friend.FindProperty("rig").objectReferenceValue = friendFigure.rig;
        friend.FindProperty("leftArm").objectReferenceValue = friendFigure.leftArm;
        friend.FindProperty("rightArm").objectReferenceValue = friendFigure.rightArm;
        PrimitiveKit.SetReferences(friend, "shirtRenderers", friendFigure.body);
        PrimitiveKit.SetReferences(friend, "skinRenderers", friendFigure.head, friendFigure.leftArmMesh, friendFigure.rightArmMesh);
        friend.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Picnic Goal.prefab");
    }

    // ---------- Figur (wie der Kunde: Kapsel-Körper, Kugel-Kopf, Arme mit Drehpunkt an der Schulter) ----------

    public struct Figure
    {
        public Transform root, rig, leftArm, rightArm;
        public Renderer body, head, leftArmMesh, rightArmMesh;
    }

    // Füße bei localPosition, schaut bei yaw 0 zur Kamera (-Z)
    public static Figure BuildFigure(Transform parent, string name, Vector3 localPosition, float yaw)
    {
        CreateMaterials();
        var f = new Figure();
        f.root = PrimitiveKit.Pivot(name, parent, localPosition);
        f.root.localRotation = Quaternion.Euler(0f, yaw, 0f);
        f.rig = PrimitiveKit.Pivot("Rig", f.root, Vector3.zero);
        // Unity-Kapsel ist 2 hoch und 1 breit: Skalierung 0.6/0.55 ergibt 1.1 m Körper mit 0.3 m Radius
        f.body = PrimitiveKit.Part("Body", PrimitiveType.Capsule, f.rig, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), figure);
        f.head = PrimitiveKit.Part("Head", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.45f, figure);
        f.leftArm = PrimitiveKit.Pivot("Arm Pivot L", f.rig, new Vector3(-0.33f, 0.95f, 0f));
        f.leftArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, f.leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), figure);
        f.rightArm = PrimitiveKit.Pivot("Arm Pivot R", f.rig, new Vector3(0.33f, 0.95f, 0f));
        f.rightArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, f.rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), figure);
        return f;
    }
}
