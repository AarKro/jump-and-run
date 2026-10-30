using UnityEditor;
using UnityEngine;

// Leben auf der Piazza Pisa (vorher eine leere Fläche mit dem Turm): Touristen, die den schiefen Turm "stützen" und dabei
// fotografiert werden, Selfies, eine Reisegruppe mit Fähnchen, ein Café mit Terrasse, Verkaufsstände, Bänke, Blumen,
// Kübel mit Bäumen und Spaziergänger. Alles reine Kulisse ohne Collider, also auch in allen Leveln sichtbar, ohne zu stören.
//
// Was frei bleiben muss (Level 2 endet hier):
// - der Streifen um die Laufbahn (z 37, x -236 … -185), dort laufen Spieler und Hund zwischen den Bänken
// - das Kundenhaus von Level 2 (x -232 … -224, z 40 … 48, kommt aus dem Goal-Prefab des Levels)
// - der Sockel des Turms (Radius 10 um CityLayout.LeaningTower)
// Die Turm-Kamera von Level 2 steht 35 m südlich der Bahn und schaut nach oben, sie sieht den Boden erst ab der Bahn:
// Das Gewusel steht deshalb hinter der Bahn. Der Streifen davor (z 11 … 30) bekommt nur Niedriges (Beete, Bänke, Leute)
// für den Überflug im Intro und die normale Kamera.
public static partial class CityBuilder
{
    private const float PiazzaTop = Surface + 0.02f;
    private const float PlinthTop = Surface + 0.6f;

    private static void BuildPiazzaLife(Transform parent)
    {
        Transform life = new GameObject("Piazza Life").transform;
        life.SetParent(parent, false);
        // Eigener Zufall: Die Häuser und der Turm (Zufall des Blocks) bleiben genau wie vorher
        var random = new System.Random(4711);
        BuildTowerTourists(life, random);
        BuildTourGroup(life, random);
        BuildCafeTerrace(life, random);
        BuildPiazzaVendors(life, random);
        BuildPiazzaGreenery(life, random);
        BuildPiazzaWalkers(life, random);
        BuildSightseers(life, random);
    }

    // ---------- Touristen am Turm ----------

    private static void BuildTowerTourists(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Tower Tourists");
        Vector2 tower = CityLayout.LeaningTower;

        // Auf dem Sockel an der Ostseite (der Turm kippt nach Osten): zwei stemmen sich mit beiden Händen gegen ihn
        foreach (float angle in new[] { -25f, 8f })
        {
            Vector2 at = tower + 6.3f * new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CrowdFigure pusher = Tourist(group, "Tower Pusher", at, FaceTowards(at, tower), random, 0f, PlinthTop);
            Pose(pusher, new Vector3(105f, 0f, 12f), new Vector3(105f, 0f, -12f), 14f);
        }
        // Ihr Fotograf vor dem Sockel, daneben eine Freundin, die lacht und winkt
        Photographer(group, new Vector2(-186.6f, 48.1f), tower + new Vector2(6f, -0.5f), random);
        Tourist(group, "Friend", new Vector2(-187.2f, 51.6f), FaceTowards(new Vector2(-187.2f, 51.6f), tower), random, 1f);

        // Westlich: das klassische Foto mit Perspektive. Drei stehen mit dem Turm im Rücken und "halten" ihn hoch,
        // der Fotograf steht weiter westlich und zielt auf sie
        Vector2 cameraSpot = new Vector2(-222.5f, 59.5f);
        CrowdFigure both = Tourist(group, "Poser Both Hands", new Vector2(-215f, 59.5f), FaceTowards(new Vector2(-215f, 59.5f), cameraSpot), random, 0f);
        Pose(both, new Vector3(160f, 0f, -20f), new Vector3(160f, 0f, 20f));
        CrowdFigure right = Tourist(group, "Poser Right", new Vector2(-215.4f, 57f), FaceTowards(new Vector2(-215.4f, 57f), cameraSpot), random, 0f);
        Pose(right, null, new Vector3(0f, 0f, 140f));
        CrowdFigure left = Tourist(group, "Poser Left", new Vector2(-214.6f, 62f), FaceTowards(new Vector2(-214.6f, 62f), cameraSpot), random, 0f);
        Pose(left, new Vector3(0f, 0f, -140f), null);
        Photographer(group, cameraSpot, new Vector2(-215f, 59.5f), random);

        // Selfies mit dem Turm im Rücken (schauen nach Süden, also zur Turm-Kamera)
        Selfie(group, new Vector2(-208f, 64.5f), 0f, random);
        Selfie(group, new Vector2(-190.5f, 60f), 25f, random);
        Selfie(group, new Vector2(-219f, 51.5f), -15f, random);
    }

    private static void Photographer(Transform parent, Vector2 at, Vector2 target, System.Random random)
    {
        CrowdFigure photographer = Tourist(parent, "Photographer", at, FaceTowards(at, target), random, 0f, PiazzaTop, accessories: false);
        Pose(photographer, new Vector3(120f, 0f, 28f), new Vector3(120f, 0f, -28f), 6f);
        Transform rig = photographer.transform.Find("Rig");
        Material black = Mat("Camera Black", new Color(0.1f, 0.1f, 0.12f), 0.6f);
        PrimitiveKit.Box("Camera", rig, new Vector3(0f, 1.3f, -0.4f), new Vector3(0.24f, 0.15f, 0.1f), black);
        PrimitiveKit.Rod("Lens", rig, new Vector3(0f, 1.3f, -0.45f), new Vector3(0f, 1.3f, -0.55f), 0.045f, black);
    }

    private static void Selfie(Transform parent, Vector2 at, float yaw, System.Random random)
    {
        CrowdFigure figure = Tourist(parent, "Selfie", at, yaw, random, 0f);
        Pose(figure, null, new Vector3(150f, 0f, -12f));
        // Handy in der Hand, Bildschirm zum Gesicht (die Hand liegt im Arm-Pivot bei -0.56 nach unten)
        Transform arm = figure.transform.Find("Rig/Arm Pivot R");
        Renderer phone = PrimitiveKit.Box("Phone", arm, new Vector3(0f, -0.62f, 0f), new Vector3(0.09f, 0.03f, 0.16f), Mat("Phone", new Color(0.12f, 0.12f, 0.15f), 0.8f));
        phone.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
    }

    // ---------- Reisegruppe ----------

    // Führerin mit Fähnchen läuft eine Runde nördlich des Turms und bleibt an jeder Ecke stehen (dreht sich zur Gruppe
    // um), die Gruppe trottet als Pulk hinterher (PathWalker mit leader)
    private static void BuildTourGroup(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Tour Group");
        Vector3[] loop =
        {
            P(-216f, 68f), P(-196f, 68f), P(-191f, 79f), P(-199f, 89f), P(-215f, 88f), P(-222f, 78f),
        };
        CrowdFigure guide = MarketSetup.Person(group, "Tour Guide", loop[0], 0f, 0f);
        Pose(guide, null, new Vector3(170f, 0f, 8f));
        // Fähnchen an einer Stange in der erhobenen Hand (verlängert den Arm nach oben)
        Transform arm = guide.transform.Find("Rig/Arm Pivot R");
        PrimitiveKit.Rod("Flag Pole", arm, new Vector3(0f, -0.45f, 0f), new Vector3(0f, -1.9f, 0f), 0.02f, Mat("Iron"));
        PrimitiveKit.Box("Flag", arm, new Vector3(0.22f, -1.72f, 0f), new Vector3(0.4f, 0.3f, 0.02f), Mat("Tour Flag", new Color(1f, 0.82f, 0.1f)));
        PathWalker leader = Walker(guide, loop, true, 0.9f, 4f, 0f);
        var guideWalk = new SerializedObject(leader);
        guideWalk.FindProperty("turnBackWhenWaiting").boolValue = true;
        guideWalk.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < 8; i++)
        {
            CrowdFigure member = Tourist(group, $"Tourist {i + 1}", new Vector2(loop[0].x, loop[0].z), 0f, random, 0.4f);
            PathWalker follower = Walker(member, loop, true, 0.9f, 0f, 0f);
            var serialized = new SerializedObject(follower);
            serialized.FindProperty("leader").objectReferenceValue = leader;
            serialized.FindProperty("followDistance").floatValue = 1.5f + i * 0.7f + (float)random.NextDouble() * 0.3f;
            serialized.FindProperty("sideOffset").floatValue = (i % 2 == 0 ? -0.55f : 0.55f) + ((float)random.NextDouble() - 0.5f) * 0.3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------- Café mit Terrasse ----------

    // An der Westseite (Fassade x -239): Markise, Schild, Tür, zwei Reihen Tische (die äußeren mit Sonnenschirm),
    // Gäste mit Pizza, ein Kellner mit Tablett, Pflanzkübel als Rand und eine Tafel am Eingang
    private static void BuildCafeTerrace(Transform parent, System.Random random)
    {
        Transform cafe = Child(parent, "Cafe Terrace");
        const float facadeX = -239f;
        const float doorZ = 74f;
        Material green = Mat("Cafe Green", new Color(0.12f, 0.45f, 0.25f));
        Material white = Mat("Awning White");
        Material darkWood = Mat("Door Wood");

        // Markise: Streifen grün/weiß, von der Fassade schräg nach vorn unten, vorne ein Volant
        Vector2 top = new Vector2(facadeX, Surface + 3.1f), front = new Vector2(-234.8f, Surface + 2.5f);
        float slope = Mathf.Atan2(front.y - top.y, front.x - top.x) * Mathf.Rad2Deg;
        float awningLength = Vector2.Distance(top, front);
        for (int i = 0; i < 16; i++)
        {
            float z = 66.5f + i;
            Material stripe = i % 2 == 0 ? green : white;
            Renderer strip = PrimitiveKit.Box("Awning Stripe", cafe, new Vector3((top.x + front.x) / 2f, (top.y + front.y) / 2f, z), new Vector3(awningLength, 0.06f, 1f), stripe);
            strip.transform.localRotation = Quaternion.Euler(0f, 0f, slope);
            PrimitiveKit.Box("Valance", cafe, new Vector3(front.x - 0.02f, front.y - 0.15f, z), new Vector3(0.03f, 0.3f, 1f), stripe);
        }
        PrimitiveKit.Box("Sign Board", cafe, new Vector3(facadeX - 0.06f, Surface + 4f, doorZ), new Vector3(0.08f, 0.8f, 6f), green);
        var sign = PrimitiveKit.Text("Sign Text", cafe, new Vector3(facadeX - 0.11f, Surface + 4f, doorZ), new Vector2(5.6f, 0.65f), "CAFFE TORRE", new Color(0.98f, 0.94f, 0.82f));
        sign.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        PrimitiveKit.Box("Door", cafe, new Vector3(facadeX - 0.04f, Surface + 1.15f, doorZ), new Vector3(0.06f, 2.3f, 1.5f), darkWood);
        PrimitiveKit.Box("Door Glass", cafe, new Vector3(facadeX - 0.08f, Surface + 1.5f, doorZ), new Vector3(0.02f, 1.1f, 1.1f), Mat("Window Lit"));

        // Tische: innere Reihe unter der Markise, äußere mit Sonnenschirmen. Auf Höhe der Tür bleibt der Gang frei
        Material[] parasols = { Mat("Parasol Red", new Color(0.82f, 0.16f, 0.12f)), white, green };
        int table = 0;
        foreach (float z in new[] { 66.5f, 70.5f, 77.5f, 81.5f })
        {
            foreach (float x in new[] { -236.2f, -231.8f })
            {
                bool outer = x > -234f;
                CafeTable(cafe, new Vector2(x, z), outer ? parasols[table % parasols.Length] : null, random);
                table++;
            }
        }

        // Pflanzkübel als Rand der Terrasse, Lücke am Eingang
        Material pot = Mat("Terracotta", new Color(0.78f, 0.42f, 0.25f));
        foreach ((float from, float to) in new[] { (63.5f, 72.5f), (75.5f, 84.5f) })
        {
            for (float z = from + 0.75f; z < to; z += 1.5f)
            {
                PrimitiveKit.Box("Planter Box", cafe, new Vector3(-229.3f, PiazzaTop + 0.3f, z), new Vector3(0.5f, 0.6f, 1.4f), pot);
                PrimitiveKit.Part("Planter Green", PrimitiveType.Sphere, cafe, new Vector3(-229.3f, PiazzaTop + 0.7f, z), new Vector3(0.55f, 0.45f, 1.3f),
                    random.Next(2) == 0 ? Mat("Leaves") : Mat("Leaves Light"));
            }
        }

        // Aufsteller am Eingang, schräg zur Turm-Kamera
        Transform board = PrimitiveKit.Pivot("Menu Board", cafe, new Vector3(-228.2f, PiazzaTop, 72.8f));
        board.localRotation = Quaternion.Euler(0f, FaceTowards(Vector2.zero, new Vector2(1f, -1.5f)), 0f);
        Transform panel = PrimitiveKit.Pivot("Panel", board, new Vector3(0f, 0.5f, -0.1f));
        panel.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        PrimitiveKit.Box("Frame", panel, Vector3.zero, new Vector3(0.7f, 1f, 0.05f), darkWood);
        PrimitiveKit.Box("Slate", panel, new Vector3(0f, 0f, -0.03f), new Vector3(0.6f, 0.9f, 0.01f), Mat("Chalkboard", new Color(0.12f, 0.16f, 0.14f)));
        PrimitiveKit.Text("Menu Text", panel, new Vector3(0f, 0.05f, -0.04f), new Vector2(0.55f, 0.6f), "PIZZA\nBY THE\nSLICE", new Color(0.95f, 0.95f, 0.9f));

        // Kellner: weißes Hemd, Schürze, Tablett mit Gläsern. Läuft von der Tür in den Gang zwischen den Tischreihen
        CrowdFigure waiter = MarketSetup.Person(cafe, "Waiter", P(-238.3f, doorZ), 0f, 0f, 75f);
        var outfit = new SerializedObject(waiter);
        outfit.FindProperty("fixedOutfit").boolValue = true;
        outfit.FindProperty("outfitShirt").colorValue = new Color(0.97f, 0.97f, 0.95f);
        outfit.FindProperty("outfitHair").colorValue = new Color(0.08f, 0.07f, 0.07f);
        outfit.ApplyModifiedPropertiesWithoutUndo();
        Transform rig = waiter.transform.Find("Rig");
        PrimitiveKit.Box("Apron", rig, new Vector3(0f, 0.38f, -0.29f), new Vector3(0.42f, 0.5f, 0.03f), Mat("Hat Black", new Color(0.12f, 0.12f, 0.14f)));
        PrimitiveKit.Box("Tray", rig, new Vector3(0f, 0.84f, -0.5f), new Vector3(0.45f, 0.03f, 0.32f), Mat("Fish Silver", new Color(0.65f, 0.7f, 0.74f), 0.8f));
        PrimitiveKit.Cylinder("Glass", rig, new Vector3(-0.1f, 0.92f, -0.5f), 0.04f, 0.14f, Mat("Wine Red", new Color(0.55f, 0.08f, 0.12f), 0.6f));
        PrimitiveKit.Cylinder("Glass", rig, new Vector3(0.1f, 0.92f, -0.46f), 0.04f, 0.14f, Mat("Fruit Lemon", new Color(1f, 0.88f, 0.25f)));
        Walker(waiter, new[] { P(-238.3f, doorZ), P(-234f, doorZ), P(-234f, 82f), P(-234f, 66f) }, false, 1.1f, 2.5f, 0f);
    }

    // Runder Tisch mit 2-3 Stühlen, meistens besetzt. Vor jedem Gast ein Teller mit Pizzastück und ein Glas
    private static void CafeTable(Transform parent, Vector2 at, Material parasol, System.Random random)
    {
        Material metal = Mat("Iron");
        Material tableTop = Mat("Cafe Table", new Color(0.92f, 0.9f, 0.85f), 0.4f);
        Material chairWood = Mat("Bridge Wood", new Color(0.55f, 0.38f, 0.22f));
        Material plate = Mat("Awning White");
        Material slice = Mat("Pizza Cheese", new Color(0.98f, 0.82f, 0.36f), 0.3f);
        Material[] drinks = { Mat("Wine Red", new Color(0.55f, 0.08f, 0.12f), 0.6f), Mat("Fruit Lemon", new Color(1f, 0.88f, 0.25f)) };

        Transform table = PrimitiveKit.Pivot("Cafe Table", parent, new Vector3(at.x, PiazzaTop, at.y));
        PrimitiveKit.Cylinder("Foot", table, new Vector3(0f, 0.015f, 0f), 0.22f, 0.03f, metal);
        PrimitiveKit.Cylinder("Post", table, new Vector3(0f, 0.37f, 0f), 0.04f, 0.72f, metal);
        PrimitiveKit.Cylinder("Top", table, new Vector3(0f, 0.75f, 0f), 0.45f, 0.04f, tableTop);
        if (parasol != null)
        {
            PrimitiveKit.Cylinder("Parasol Pole", table, new Vector3(0f, 1.25f, 0f), 0.03f, 2.5f, metal);
            PrimitiveKit.Part("Parasol", PrimitiveType.Sphere, table, new Vector3(0f, 2.45f, 0f), new Vector3(2.8f, 0.45f, 2.8f), parasol);
            PrimitiveKit.Part("Parasol Tip", PrimitiveType.Sphere, table, new Vector3(0f, 2.7f, 0f), Vector3.one * 0.1f, metal);
        }

        int chairs = 2 + random.Next(2);
        float start = (float)random.NextDouble() * 360f;
        for (int c = 0; c < chairs; c++)
        {
            float angle = (start + c * 360f / chairs) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 seat = at + direction * 0.75f;
            float yaw = FaceTowards(seat, at);
            Transform chair = PrimitiveKit.Pivot("Chair", parent, new Vector3(seat.x, PiazzaTop, seat.y));
            chair.localRotation = Quaternion.Euler(0f, yaw, 0f);
            PrimitiveKit.Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), chairWood);
            PrimitiveKit.Box("Back", chair, new Vector3(0f, 0.72f, 0.2f), new Vector3(0.42f, 0.5f, 0.04f), chairWood);
            foreach (Vector2 leg in new[] { new Vector2(-0.17f, -0.17f), new Vector2(0.17f, -0.17f), new Vector2(-0.17f, 0.17f), new Vector2(0.17f, 0.17f) })
            {
                PrimitiveKit.Box("Leg", chair, new Vector3(leg.x, 0.22f, leg.y), new Vector3(0.04f, 0.44f, 0.04f), metal);
            }
            if (random.NextDouble() < 0.2)
            {
                continue;
            }
            // Gast sitzt (die Pillen-Figur hat keine Beine, wie auf den Bänken), gestikuliert beim Reden ein wenig
            Tourist(parent, "Cafe Guest", seat, yaw, random, 0.5f, PiazzaTop + 0.49f, accessories: false);
            var dish = new Vector3(direction.x * 0.25f, 0.775f, direction.y * 0.25f);
            PrimitiveKit.Cylinder("Plate", table, dish, 0.14f, 0.01f, plate);
            Renderer pizza = PrimitiveKit.Box("Pizza Slice", table, dish + new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.015f, 0.12f), slice);
            pizza.transform.localRotation = Quaternion.Euler(0f, 45f + yaw, 0f);
            Vector3 side = new Vector3(-direction.y, 0f, direction.x) * 0.15f;
            PrimitiveKit.Cylinder("Glass", table, new Vector3(direction.x * 0.28f, 0.84f, direction.y * 0.28f) + side, 0.035f, 0.13f, drinks[random.Next(drinks.Length)]);
        }
    }

    // ---------- Stände und Verkäufer ----------

    // Markt-Prefabs (MarketSetup) als Instanzen. Sie sind für die Laufbahn gebaut: Stände stehen bei lokal z 1.7 … 5.2 und
    // schauen nach -Z (Kunden davor), die kleinen Verkäufer stehen bei lokal z ~-5 und schauen zur Kamera (-Z)
    private static void BuildPiazzaVendors(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Vendors");
        // Reihe nördlich der Reisegruppen-Runde, Kunden zur Turm-Kamera
        MarketPrefab(group, "Market Stall Flowers", new Vector2(-226f, 95f), 0f, 0f);
        MarketPrefab(group, "Market Stall Souvenir", new Vector2(-219f, 95f), 0f, 0f);
        MarketPrefab(group, "Market Stall Food", new Vector2(-212f, 95f), 0f, 0f);
        // Zweiter Souvenirstand am Ostrand, Rücken zur Basil Street
        MarketPrefab(group, "Market Stall Souvenir", new Vector2(-191.7f, 100f), 0f, 90f);
        // Kleine Verkäufer nahe der Bahn, zur Kamera gedreht
        MarketPrefab(group, "Ice Cream Cart", new Vector2(-189f, 42.5f), -5.4f, 0f);
        MarketPrefab(group, "Balloon Seller", new Vector2(-213f, 43.5f), -5.6f, 0f);
        MarketPrefab(group, "Street Musician", new Vector2(-226f, 56f), -4.8f, 0f);

        // Grüppchen, die sich unterhalten (innerhalb der Reisegruppen-Runde und vor dem Café)
        ChatGroup(group, new Vector2(-205f, 78f), 3, random);
        ChatGroup(group, new Vector2(-235f, 90f), 2, random);
        ChatGroup(group, new Vector2(-198f, 102f), 3, random);
    }

    private static void ChatGroup(Transform parent, Vector2 center, int count, System.Random random)
    {
        float start = (float)random.NextDouble() * 360f;
        for (int i = 0; i < count; i++)
        {
            float angle = (start + i * 360f / count) * Mathf.Deg2Rad;
            Vector2 at = center + 0.7f * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Tourist(parent, "Chatting", at, FaceTowards(at, center), random, 1f);
        }
    }

    // Prefab so hinstellen, dass sein Inhalt (lokal z = contentZ) bei at liegt
    private static void MarketPrefab(Transform parent, string name, Vector2 at, float contentZ, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MarketSetup.PrefabFolder}/{name}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"CityBuilder: {name} fehlt, erst Tools > Jump and Run > City > Build Market Prefabs ausführen.");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.y) - rotation * new Vector3(0f, 0f, contentZ), rotation);
    }

    // ---------- Grün, Bänke, Laternen ----------

    private static void BuildPiazzaGreenery(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Greenery");
        Vector2 tower = CityLayout.LeaningTower;

        // Beete im Kreis um den Sockel, außer im Süden (dort läuft die Bahn knapp vorbei)
        foreach (float angle in new[] { -160f, 180f, 150f, 120f, 90f, 60f, 30f, 0f, -20f })
        {
            FlowerBed(group, tower + 12f * new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)), 1.1f, random);
        }

        // Vorne (zur Margherita Avenue): große Beete, dazwischen Bänke mit Blick zum Turm, Kübelbäume an den Ecken
        foreach (float x in new[] { -228f, -212f, -196f })
        {
            FlowerBed(group, new Vector2(x, 21f), 2.2f, random);
        }
        PiazzaBench(group, new Vector2(-220f, 21f), north: true, sitters: 2, random);
        PiazzaBench(group, new Vector2(-204f, 21f), north: true, sitters: 1, random);
        foreach (float x in new[] { -236.5f, -188.5f })
        {
            PlanterTree(group, new Vector2(x, 14f), random);
        }

        // Hinten (zur Crust Street): Bänke mit Blick zur Piazza, Kübelbäume dazwischen und an den Ecken
        int sitters = 0;
        foreach (float x in new[] { -230f, -218f, -206f, -194f })
        {
            PiazzaBench(group, new Vector2(x, 107.5f), north: false, sitters: 1 + sitters++ % 2, random);
        }
        foreach (float x in new[] { -236.5f, -224f, -212f, -200f, -188.5f })
        {
            PlanterTree(group, new Vector2(x, 108.5f), random);
        }

        // Laternen am Ostrand (zur Basil Street) und vorne
        foreach (float z in new[] { 64f, 76f, 88f })
        {
            ParkLamp(group, new Vector2(-187.3f, z));
        }
        foreach (float x in new[] { -220f, -204f })
        {
            ParkLamp(group, new Vector2(x, 13f));
        }
    }

    // Parkbank (ohne Collider) mit Leuten darauf. north = Sitzende schauen nach Norden (zum Turm), sonst nach Süden
    private static void PiazzaBench(Transform parent, Vector2 at, bool north, int sitters, System.Random random)
    {
        ParkBench(parent, at, north ? Vector2.down : Vector2.up);
        float yaw = north ? 180f : 0f;
        float[] offsets = sitters == 1 ? new[] { (float)random.NextDouble() - 0.5f } : new[] { -0.45f, 0.5f };
        foreach (float offset in offsets)
        {
            Tourist(parent, "Bench Sitter", at + new Vector2(offset, 0f), yaw, random, 0.6f, PiazzaTop + 0.49f, accessories: false);
        }
    }

    private static void PlanterTree(Transform parent, Vector2 at, System.Random random)
    {
        Transform planter = PrimitiveKit.Pivot("Planter Tree", parent, new Vector3(at.x, PiazzaTop, at.y));
        PrimitiveKit.Box("Pot", planter, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.6f), Mat("Terracotta", new Color(0.78f, 0.42f, 0.25f)));
        PrimitiveKit.Box("Soil", planter, new Vector3(0f, 0.71f, 0f), new Vector3(1.4f, 0.02f, 1.4f), Mat("Soil", new Color(0.3f, 0.2f, 0.13f)));
        Tree(planter, planter.position + Vector3.up * 0.7f, 0.65f, random);
    }

    // ---------- Spaziergänger ----------

    private static void BuildPiazzaWalkers(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Walkers");
        // Vorne zwischen den Beeten (z 15 und 26.5), hinten vor den Bänken und am Ostrand
        Stroll(group, new[] { P(-234f, 15f), P(-190f, 15f) }, 1f, 0f, random, couple: true);
        Stroll(group, new[] { P(-190f, 26.5f), P(-234f, 26.5f) }, 0.85f, 20f, random);
        Stroll(group, new[] { P(-232f, 104f), P(-192f, 104f) }, 1.1f, 12f, random, couple: true);
        Stroll(group, new[] { P(-188.8f, 64f), P(-188.8f, 95f) }, 0.9f, 5f, random);
        Stroll(group, new[] { P(-205f, 92.5f), P(-230f, 92.5f) }, 1f, 30f, random);
        Stroll(group, new[] { P(-200f, 72f), P(-200f, 85f), P(-210f, 85f) }, 0.8f, 8f, random);
    }

    // Spaziergänger hin und her, couple = mit Begleitung daneben (läuft im Gleichschritt mit)
    private static void Stroll(Transform parent, Vector3[] points, float speed, float start, System.Random random, bool couple = false)
    {
        CrowdFigure walker = Tourist(parent, "Stroller", new Vector2(points[0].x, points[0].z), 0f, random, 0.3f);
        PathWalker leader = Walker(walker, points, false, speed, 1.5f + (float)random.NextDouble() * 2f, start);
        if (!couple)
        {
            return;
        }
        CrowdFigure partner = Tourist(parent, "Stroller Partner", new Vector2(points[0].x, points[0].z), 0f, random, 0.3f);
        var serialized = new SerializedObject(Walker(partner, points, false, speed, 0f, start));
        serialized.FindProperty("leader").objectReferenceValue = leader;
        serialized.FindProperty("followDistance").floatValue = 0.1f;
        serialized.FindProperty("sideOffset").floatValue = 0.7f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Schaulustige ----------

    // Verstreute Leute zwischen Bahn und Ständen, die zum Turm hochschauen, auf ihn zeigen oder ihn filmen. Zufällig
    // verteilt, aber nie dort, wo schon etwas steht oder jemand entlangläuft
    private static void BuildSightseers(Transform parent, System.Random random)
    {
        Transform group = Child(parent, "Sightseers");
        Vector2 tower = CityLayout.LeaningTower;
        Vector2[] taken =
        {
            new Vector2(-226f, 56f), new Vector2(-189f, 42.5f), new Vector2(-212.5f, 43.5f), new Vector2(-205f, 78f), new Vector2(-235f, 90f),
            new Vector2(-198f, 102f), new Vector2(-208f, 64.5f), new Vector2(-190.5f, 60f), new Vector2(-219f, 51.5f), new Vector2(-186.9f, 49.8f),
        };
        // Wege der Reisegruppe und der Spaziergänger (dort würde man durch sie hindurchlaufen)
        (Vector2 a, Vector2 b)[] paths =
        {
            (new Vector2(-216f, 68f), new Vector2(-196f, 68f)), (new Vector2(-196f, 68f), new Vector2(-191f, 79f)),
            (new Vector2(-191f, 79f), new Vector2(-199f, 89f)), (new Vector2(-199f, 89f), new Vector2(-215f, 88f)),
            (new Vector2(-215f, 88f), new Vector2(-222f, 78f)), (new Vector2(-222f, 78f), new Vector2(-216f, 68f)),
            (new Vector2(-188.8f, 64f), new Vector2(-188.8f, 95f)), (new Vector2(-205f, 92.5f), new Vector2(-230f, 92.5f)),
            (new Vector2(-200f, 72f), new Vector2(-200f, 85f)), (new Vector2(-200f, 85f), new Vector2(-210f, 85f)),
        };
        var placed = new System.Collections.Generic.List<Vector2>();
        bool Free(Vector2 p)
        {
            bool house = p.x > -234f && p.x < -221f && p.y < 50f;
            bool cafe = p.x < -228f && p.y > 61f && p.y < 87f;
            bool trio = p.x > -224f && p.x < -213f && p.y > 55f && p.y < 64f;
            bool eastLamps = p.x > -190f;
            if (house || cafe || trio || eastLamps || Vector2.Distance(p, tower) < 13.6f)
            {
                return false;
            }
            foreach (Vector2 spot in taken)
            {
                if (Vector2.Distance(p, spot) < 2.8f) return false;
            }
            foreach (Vector2 other in placed)
            {
                if (Vector2.Distance(p, other) < 1.6f) return false;
            }
            foreach (var path in paths)
            {
                Vector2 ab = path.b - path.a;
                float t = Mathf.Clamp01(Vector2.Dot(p - path.a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, path.a + ab * t) < 1.5f) return false;
            }
            return true;
        }

        Material phone = Mat("Phone", new Color(0.12f, 0.12f, 0.15f), 0.8f);
        int count = 0;
        for (int attempt = 0; attempt < 400 && count < 24; attempt++)
        {
            var at = new Vector2(-237f + (float)random.NextDouble() * 47f, 41.5f + (float)random.NextDouble() * 50f);
            if (!Free(at))
            {
                continue;
            }
            placed.Add(at);
            count++;
            // Schaut grob zum Turm (etwas gestreut)
            float yaw = FaceTowards(at, tower) + ((float)random.NextDouble() - 0.5f) * 40f;
            CrowdFigure figure = Tourist(group, "Sightseer", at, yaw, random, 0.3f);
            switch (random.Next(3))
            {
                case 0:
                    // Zeigt hinauf
                    Pose(figure, null, new Vector3(145f, 0f, -5f), -6f);
                    break;
                case 1:
                    // Filmt mit dem Handy vor dem Gesicht
                    Pose(figure, new Vector3(110f, 0f, 22f), new Vector3(110f, 0f, -22f));
                    PrimitiveKit.Box("Phone", figure.transform.Find("Rig"), new Vector3(0f, 1.2f, -0.45f), new Vector3(0.16f, 0.09f, 0.02f), phone);
                    break;
            }
        }
    }

    // ---------- Hilfen ----------

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Vector3 P(float x, float z) => new Vector3(x, PiazzaTop, z);

    // Drehung, mit der eine Pillen-Figur (schaut bei 0 nach -Z) von from nach to schaut
    private static float FaceTowards(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        return Mathf.Atan2(-d.x, -d.y) * Mathf.Rad2Deg;
    }

    // Tourist: Marktfigur, oft mit Sonnenhut, Rucksack oder Kamera um den Hals
    private static CrowdFigure Tourist(Transform parent, string name, Vector2 at, float yaw, System.Random random, float gesture = 1f,
        float y = PiazzaTop, bool accessories = true)
    {
        CrowdFigure figure = MarketSetup.Person(parent, name, new Vector3(at.x, y, at.y), yaw, gesture);
        if (!accessories)
        {
            return figure;
        }
        Transform rig = figure.transform.Find("Rig");
        switch (random.Next(4))
        {
            case 0:
                Material straw = Mat("Sun Hat Straw", new Color(0.93f, 0.82f, 0.55f));
                PrimitiveKit.Cylinder("Hat Brim", rig, new Vector3(0f, 1.56f, 0f), 0.3f, 0.03f, straw);
                PrimitiveKit.Cylinder("Hat Crown", rig, new Vector3(0f, 1.64f, 0f), 0.17f, 0.14f, straw);
                break;
            case 1:
                Color[] packs = { new Color(0.2f, 0.35f, 0.7f), new Color(0.75f, 0.2f, 0.2f), new Color(0.25f, 0.5f, 0.3f) };
                int pack = random.Next(packs.Length);
                PrimitiveKit.Box("Backpack", rig, new Vector3(0f, 0.75f, 0.32f), new Vector3(0.4f, 0.45f, 0.2f), Mat("Backpack " + (pack + 1), packs[pack]));
                break;
            case 2:
                PrimitiveKit.Box("Neck Camera", rig, new Vector3(0f, 0.75f, -0.31f), new Vector3(0.16f, 0.1f, 0.08f), Mat("Camera Black", new Color(0.1f, 0.1f, 0.12f), 0.6f));
                break;
        }
        return figure;
    }

    private static void Pose(CrowdFigure figure, Vector3? left, Vector3? right, float lean = 0f)
    {
        var serialized = new SerializedObject(figure);
        if (left.HasValue)
        {
            serialized.FindProperty("holdLeftArm").boolValue = true;
            serialized.FindProperty("armPoseLeft").vector3Value = left.Value;
        }
        if (right.HasValue)
        {
            serialized.FindProperty("holdRightArm").boolValue = true;
            serialized.FindProperty("armPoseRight").vector3Value = right.Value;
        }
        serialized.FindProperty("lean").floatValue = lean;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PathWalker Walker(CrowdFigure figure, Vector3[] points, bool loop, float speed, float wait, float start)
    {
        var walker = figure.gameObject.AddComponent<PathWalker>();
        var serialized = new SerializedObject(walker);
        SerializedProperty list = serialized.FindProperty("points");
        list.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            list.GetArrayElementAtIndex(i).vector3Value = points[i];
        }
        serialized.FindProperty("loop").boolValue = loop;
        serialized.FindProperty("speed").floatValue = speed;
        serialized.FindProperty("waitTime").floatValue = wait;
        serialized.FindProperty("startDistance").floatValue = start;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return walker;
    }
}
