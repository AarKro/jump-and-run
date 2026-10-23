using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Baut Level 1 komplett neu auf (aus den Prefabs von CityPropsSetup). Häuser, Pizzeria, Café und Kiosk stehen in der
// gemeinsamen Stadt-Szene (CityBuilder), die das Level über "City Link" (LevelCity) dazulädt. Das Level bringt nur seine
// Laufbahn, eigenen Boden (Gehwege mit Baugruben, Eckplatz, Querstraße), Spiel-Elemente und Deko mit und schneidet
// die Stadt dort mit CityCutouts aus.
// Menü: Tools > Jump and Run > City > Rebuild Level 1 (City).
// ACHTUNG: Löscht alles im Level außer Kamera, Player, GameManager, HUD, KillZone, Goal und StartPoint.
// Danach ist es ein normales, von Hand bearbeitbares Level. Erneut ausführen überschreibt Handänderungen.
//
// Alle Positionen sind Lauf-Koordinaten u entlang der LanePath (auf dem ersten Stück ist u = Welt-X).
// Nach dem Café biegt die Straße in einem 90°-Bogen in die Tiefe ab (Linkskurve nach +Z).
// Ablauf (Gehweg-Oberkante y = 0.5, Sprung ~3.2 m hoch / ~5.5 m weit):
//  -18..20    Start: Pizzeria (Tür bei u = -8), Balkon-Kletterei mit Speed-Boost oben
//   20..23.5  Baugrube (3.5 m)
//  23.5..64   Hund zwischen zwei Containern, Querstraße bei u = 50, Café-Checkpoint bei u = 61
//   64..70.3  Kurve (Radius 4 m) über einen Eckplatz, die Seitenwand des Cafés bildet die Hausecke
//  71.5..86.5 federnde Markise bei u = 77 → hoher Balkon mit Wärme-Bonus, 24/7-Kiosk mit Mikrowelle bei u = 80
//  86.5..102  lange Baugrube: Fensterputzer-Gondel hoch, dann über zwei Balkone
//  102..154   zwei Hunde (Bank/Container als Wände), Balkon mit Sprung-Boost, Kunde bei u = 146
public static class CityLevelBuilder
{
    private const string ScenePath = "Assets/Scenes/levels/level01.unity";
    private const float KeepWarmTime = 75f;

    // Kein "Directional Light": Die Sonne kommt aus der Stadt-Szene (sonst wäre alles doppelt beleuchtet)
    private static readonly HashSet<string> Keep = new HashSet<string>
    {
        "Main Camera", "Player", "GameManager", "HUD", "KillZone", "Goal", "StartPoint"
    };

    private static Transform groundParent, buildingParent, propParent, gameplayParent;
    private static LanePath lane;

    // Kurve: beginnt direkt hinter dem Café (u = 64), Linkskurve um 90° mit 4 m Radius
    private const float CornerU = 64f;
    private const float CornerRadius = 4f;
    private static float CornerLength => Mathf.PI / 2f * CornerRadius;

    // Querstraße: Mitte bei u = 50, in einer 10 m breiten Lücke der Häuserzeile (siehe StreetSetup.OuterHalfWidth)
    private const int StreetU = 50;

    [MenuItem("Tools/Jump and Run/City/Rebuild Level 1 (City)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Level 1 neu bauen",
                "Löscht alle Plattformen, Gegner, Power-Ups und Deko in Level 1 und baut die Stadt neu auf. Fortfahren?",
                "Neu bauen", "Abbrechen"))
        {
            return;
        }
        Build();
    }

    // Ohne Dialog, für den Aufruf über Skripte (MCP)
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!Keep.Contains(root.name))
            {
                Object.DestroyImmediate(root);
            }
        }

        lane = BuildLane();
        groundParent = new GameObject("City Ground").transform;
        buildingParent = new GameObject("City Buildings").transform;
        propParent = new GameObject("City Props").transform;
        gameplayParent = new GameObject("City Gameplay").transform;

        BuildGround();
        BuildClouds();
        BuildGameplay();
        BuildProps();
        SetupCityLink();
        SetupEssentials();
        // Gleicher Himmel und Nebel wie die Stadt, dann ist egal, welche der beiden Szenen gerade aktiv ist
        CityBuilder.ApplyAtmosphere();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CityLevelBuilder: Level 1 als Stadt gebaut.");
    }

    // ---------- Laufbahn ----------

    private static LanePath BuildLane()
    {
        var laneObject = new GameObject("Lane Path");
        var path = laneObject.AddComponent<LanePath>();
        var pieces = new List<LanePath.Piece>
        {
            new LanePath.Piece { type = LanePath.PieceType.Straight, length = CornerU + 40f },
            new LanePath.Piece { type = LanePath.PieceType.Arc, length = CornerLength, turnDegrees = 90f },
            new LanePath.Piece { type = LanePath.PieceType.Straight, length = 110f }
        };
        path.Configure(new Vector3(-40f, 0f, 0f), 0f, -40f, pieces);
        return path;
    }

    // ---------- Boden ----------

    private static void BuildGround()
    {
        // Mitte und Länge so gewählt, dass die Mitte auf ganzen Metern liegt (PrefabGridEnforcer)
        Sidewalk(1, 38);     // -18 .. 20
        Sidewalk(44, 41);    // 23.5 .. 64.5 (bis in die Kurve, der Eckplatz übernimmt)
        Sidewalk(79, 15);    // 71.5 .. 86.5 (nach der Kurve)
        Sidewalk(128, 52);   // 102 .. 154

        // Baugruben je 1 m breiter als die Lücke, damit sie unter den Gehweg-Kanten verschwinden
        Stretched("Construction Pit", 22, 0, 5, groundParent);   // Lücke 20 .. 23.5
        Stretched("Construction Pit", 94, 0, 17, groundParent);  // Lücke 86.5 .. 102
        Place("Barrier", 19, 0, propParent);
        Place("Barrier", 25, 0, propParent);
        Place("Barrier", 85, 0, propParent);
        Place("Barrier", 103, 0, propParent);

        BuildCornerPlaza();
    }

    // Eckplatz unter der Kurve: Gehwegfläche (begehbar) über den ganzen Viertelkreis-Ring (Radius 1 bis 6 um den
    // Kurvenmittelpunkt) und eine große Asphaltfläche außen herum, wo sich die beiden Straßen treffen.
    // Kein Prefab, also auch kein Einrasten durch den PrefabGridEnforcer
    private static void BuildCornerPlaza()
    {
        const string materials = "Assets/Materials/City";
        Material concrete = AssetDatabase.LoadAssetAtPath<Material>($"{materials}/Sidewalk.mat");
        Material asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{materials}/Road.mat");
        Material foundation = AssetDatabase.LoadAssetAtPath<Material>($"{materials}/Foundation.mat");
        Material curb = AssetDatabase.LoadAssetAtPath<Material>($"{materials}/Curb.mat");

        Transform plaza = new GameObject("Corner Plaza").transform;
        plaza.SetParent(groundParent, false);
        // Kurvenmittelpunkt bei (CornerU, 0, CornerRadius). Gehweg reicht 3 m nach innen und 2 m nach außen
        float x0 = CornerU;
        float x1 = CornerU + CornerRadius + 2f;
        float z0 = -2f;
        float z1 = CornerRadius + 2f;
        var slabCenter = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
        var slabSize = new Vector3(x1 - x0, 1f, z1 - z0);
        GameObject slab = PrimitiveKit.Box("Sidewalk", plaza, slabCenter, slabSize, concrete).gameObject;
        slab.layer = 3;
        PrimitiveKit.AddBoxCollider(slab, Vector3.zero, Vector3.one);
        PrimitiveKit.Box("Sidewalk Foundation", plaza, slabCenter + Vector3.down * 3.25f, new Vector3(slabSize.x, 5.5f, slabSize.z), foundation);
        PrimitiveKit.Box("Curb Front", plaza, new Vector3(slabCenter.x, 0.38f, z0 + 0.1f), new Vector3(slabSize.x, 0.25f, 0.25f), curb);
        PrimitiveKit.Box("Curb Side", plaza, new Vector3(x1 - 0.1f, 0.38f, slabCenter.z), new Vector3(0.25f, 0.25f, slabSize.z), curb);

        // Asphalt außen herum: vorne die Straße von vor der Kurve, rechts die Straße nach der Kurve
        var roadCenter = new Vector3(CornerU + 9f, 0.15f, -4f);
        var roadSize = new Vector3(18f, 0.3f, 20f);
        PrimitiveKit.Box("Road", plaza, roadCenter, roadSize, asphalt);
        PrimitiveKit.Box("Road Foundation", plaza, new Vector3(roadCenter.x, -3.1f, roadCenter.z), new Vector3(roadSize.x, 6.2f, roadSize.z), foundation);
    }

    private static void Sidewalk(int centerU, float length)
    {
        Stretched("Sidewalk", centerU, 0, length, groundParent);
    }

    // ---------- Wolken ----------

    // Häuser, Pizzeria, Café und Kiosk kommen jetzt aus der Stadt-Szene (CityBuilder). Hier nur noch die Wolken
    private static void BuildClouds()
    {
        (int u, int y)[] clouds = { (-10, 28), (25, 33), (55, 26), (100, 34), (130, 29), (165, 31) };
        foreach (var cloud in clouds)
        {
            Place("Cloud", cloud.u, cloud.y, buildingParent);
        }
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Start: zwei Balkone als Kletterpartie (je 3 m höher), oben ein Speed-Boost
        Place("Balcony", 9, 3, gameplayParent);   // Oberkante 3.5
        Place("Balcony", 13, 6, gameplayParent);  // Oberkante 6.5
        PowerUp(13, 8, PowerUpKind.Speed, true);
        Place("Bench", 3, 0, gameplayParent);

        // Hund 1 zwischen zwei Containern (an denen er umdreht), schaut dem Spieler entgegen
        Place("Dumpster", 30, 0, gameplayParent);
        Dog(37, -1f);
        Place("Dumpster", 44, 0, gameplayParent);

        // Querstraße mit Verkehr
        PlaceAsset("Assets/Prefabs/Street Crossing.prefab", StreetU, 0, gameplayParent);

        // Nach der Kurve: Markise (Oberkante 2.5) katapultiert ~5 m hoch → Balkon (Oberkante 6.5) mit Wärme-Bonus
        Place("Bouncy Awning", 77, 2, gameplayParent);
        Place("Balcony", 81, 6, gameplayParent);
        PowerUp(81, 8, PowerUpKind.TimeBonus, false);

        // Lange Baugrube: Gondel bündig am Gehweg-Ende, fährt auf Balkon-Höhe, dann zwei Balkone (je 2 m Abstand)
        Place("Window Washer Gondola", 88, 0, gameplayParent);   // 86.5 .. 89.5, fährt bis Oberkante 6.5
        Place("Balcony", 93, 6, gameplayParent);                 // 91.5 .. 94.5
        Place("Balcony", 98, 6, gameplayParent);                 // 96.5 .. 99.5, dann runter auf den Gehweg bei 102

        // Schlussstück: Hund 2 zwischen Bank und Container, Balkon mit Sprung-Boost, Hund 3 zwischen zwei Containern
        Place("Bench", 107, 0, gameplayParent);
        Dog(112, -1f);
        Place("Dumpster", 118, 0, gameplayParent);
        Place("Balcony", 122, 3, gameplayParent);
        PowerUp(122, 5, PowerUpKind.Jump, true);
        Dog(129, 1f);
        Place("Dumpster", 136, 0, gameplayParent);
    }

    private enum PowerUpKind { Speed = 0, Jump = 1, TimeBonus = 2 }

    private static void PowerUp(int u, int y, PowerUpKind kind, bool respawn)
    {
        GameObject powerUp = PlaceAsset("Assets/Prefabs/PowerUp.prefab", u, y, gameplayParent);
        var serialized = new SerializedObject(powerUp.GetComponent<PowerUp>());
        serialized.FindProperty("type").enumValueIndex = (int)kind;
        serialized.FindProperty("respawnOnLevelReset").boolValue = respawn;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        powerUp.name = $"PowerUp {kind}";
    }

    // Hund-Wurzel 1 m über der Gehweg-Mitte: Box-Unterkante liegt dann genau auf der Oberkante
    private static void Dog(int u, float startDirection)
    {
        GameObject dog = PlaceAsset("Assets/Prefabs/Dog.prefab", u, 1, gameplayParent);
        var serialized = new SerializedObject(dog.GetComponent<GroundPatrol>());
        serialized.FindProperty("startDirection").floatValue = startDirection;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Deko ----------

    private static void BuildProps()
    {
        // Nicht in Baugruben, nicht auf der Querstraße, nicht vor Türen, nicht in der Kurve
        // und nicht unter Balkonen/Markise (Laterne und Baumkrone würden sonst durch die Platte stechen)
        var blocked = new List<(float from, float to)>
        {
            (19, 25), (46, 54), (84, 104), (-10, -6), (59, 63), (63, 73), (142, 150),
            (6.5f, 15.5f), (74.5f, 83.5f), (119.5f, 124.5f)
        };
        bool Free(int u) => blocked.TrueForAll(zone => u < zone.from || u > zone.to);

        for (int u = -16; u < 152; u += 12)
        {
            if (Free(u)) Place("Street Lamp", u, 0, propParent);
            if (Free(u + 6)) Place("Tree", u + 6, 0, propParent);
        }
        foreach (int u in new[] { 15, 57, 110, 140 })
        {
            if (Free(u)) Place("Hydrant", u, 0, propParent);
        }
        foreach (int u in new[] { -2, 33, 74, 126 })
        {
            if (Free(u)) Place("Trash Can", u, 0, propParent);
        }
    }

    // ---------- Verbindung zur Stadt ----------

    // LevelCity lädt die Stadt dazu und richtet Pizzeria (Start), Café (Checkpoint) und Kiosk (Mikrowelle) ein.
    // Die CityCutouts schneiden die Stadt dort aus, wo Level 1 eigenen Boden hat (Gehwege mit Baugruben, Eckplatz,
    // Querstraße mit Zebrastreifen) und wo sein Kundenhaus (Teil des Goal-Prefabs) steht
    private static void SetupCityLink()
    {
        Transform link = new GameObject("City Link").transform;
        link.gameObject.AddComponent<LevelCity>();

        // Rechteck in Welt-X/Z (von, bis)
        void Cutout(string name, float x0, float x1, float z0, float z1)
        {
            Transform cutout = new GameObject("Cutout " + name).transform;
            cutout.SetParent(link, false);
            cutout.position = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            cutout.localScale = new Vector3(x1 - x0, 1f, z1 - z0);
            cutout.gameObject.AddComponent<CityCutout>();
        }
        // Gehweg + Straße davor (Sidewalk-Prefab reicht von Z -14 bis 3) ab dem ersten Gehwegstück bei u = -18
        Cutout("Main Street", -18f, 66f, -14f, 3f);
        // Nach der Kurve bis zum Ende des letzten Gehwegstücks (u = 154 → z 87.7), Straße bis 14 m davor
        Cutout("After Bend", 64f, 82f, -14f, 88f);
        // Querstraße (Street Crossing): eigene Fahrbahn und Gehwege bis z 104
        Cutout("Cross Street", 45f, 55f, -14f, 104f);
        Rect customer = CityLayout.CustomerHouseLot;
        Cutout("Customer House", customer.xMin, customer.xMax + 1f, customer.yMin - 0.2f, customer.yMax + 0.2f);
    }

    // ---------- Grundausstattung ----------

    private static void SetupEssentials()
    {
        // Die Pizzeria steht in der Stadt: Ihre Tür-Position aus dem Prefab plus ihrem Platz in der Stadt berechnen
        var landmark = System.Array.Find(CityLayout.Landmarks, entry => entry.prefab == "Pizzeria");
        var pizzeriaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityPropsSetup.PrefabFolder}/Pizzeria.prefab");
        Vector3 doorOutside = landmark.position + Quaternion.Euler(0f, landmark.yaw, 0f) * pizzeriaPrefab.GetComponent<SpawnDoor>().OutsidePosition;

        GameObject startPoint = GameObject.Find("StartPoint");
        if (startPoint != null)
        {
            startPoint.transform.position = doorOutside;
        }

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = doorOutside;
        }

        // Die Start-Tür setzt LevelCity zur Laufzeit (liegt in der Stadt-Szene, das kann man nicht im Inspector verknüpfen)
        var gameManager = Object.FindAnyObjectByType<GameManager>();
        var serialized = new SerializedObject(gameManager);
        serialized.FindProperty("startDoor").objectReferenceValue = null;
        serialized.FindProperty("levelTime").floatValue = KeepWarmTime;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Kamerafahrt zum Levelstart (Kunde → Route → Pizzeria)
        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null && mainCamera.GetComponent<IntroFlyover>() == null)
        {
            mainCamera.AddComponent<IntroFlyover>();
        }

        GameObject goal = GameObject.Find("Goal");
        if (goal != null)
        {
            // Kundenhaus nach der Kurve, also auch in den Lane-Rahmen drehen
            const float goalU = 146f;
            goal.transform.SetPositionAndRotation(lane.PointAt(goalU), Frame(goalU));
        }

        GameObject killZone = GameObject.Find("KillZone");
        if (killZone != null)
        {
            // Knapp über dem Baugruben-Boden (y = -4): Der Tod passiert noch sichtbar in der Grube.
            // Groß genug für beide Straßen (vor der Kurve entlang X, danach entlang Z)
            killZone.transform.SetPositionAndRotation(new Vector3(25f, -3f, 70f), Quaternion.identity);
            killZone.transform.localScale = new Vector3(140f, 1f, 190f);
            // Unsichtbar: Man soll in die Baugrube fallen, nicht auf eine rote Fläche schauen
            if (killZone.TryGetComponent(out MeshRenderer killRenderer))
            {
                killRenderer.enabled = false;
            }
        }
    }

    // ---------- Hilfen ----------

    private static Quaternion Frame(float u) => Quaternion.LookRotation(Vector3.Cross(lane.TangentAt(u), Vector3.up), Vector3.up);

    private static GameObject Place(string cityPrefab, int u, int y, Transform parent)
    {
        return PlaceAsset($"{CityPropsSetup.PrefabFolder}/{cityPrefab}.prefab", u, y, parent);
    }

    private static GameObject Stretched(string cityPrefab, int u, int y, float length, Transform parent)
    {
        GameObject instance = Place(cityPrefab, u, y, parent);
        instance.transform.localScale = new Vector3(length, 1f, 1f);
        return instance;
    }

    // Auf die Bahn stellen: Position an der Stelle u, gedreht in den Lane-Rahmen (lokal +X = Laufrichtung)
    private static GameObject PlaceAsset(string path, int u, int y, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new System.Exception($"CityLevelBuilder: Prefab fehlt: {path} (erst Build City Prefabs ausführen)");
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(lane.PointAt(u) + Vector3.up * y, Frame(u));
        return instance;
    }
}
