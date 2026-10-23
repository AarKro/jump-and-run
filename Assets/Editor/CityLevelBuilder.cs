using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Baut Level 1 als Stadt komplett neu auf (aus den Prefabs von CityPropsSetup).
// Menü: Tools > Jump and Run > City > Rebuild Level 1 (City).
// ACHTUNG: Löscht alles im Level außer Kamera, Licht, Player, GameManager, HUD, KillZone, Goal und StartPoint.
// Danach ist es ein normales, von Hand bearbeitbares Level. Erneut ausführen überschreibt Handänderungen.
//
// Ablauf (Gehweg-Oberkante y = 0.5, Sprung ~3.2 m hoch / ~5.5 m weit):
//  -18..20   Start: Pizzeria (Tür bei x = -8), Balkon-Kletterei mit Speed-Boost oben
//   20..23.5 Baugrube (3.5 m)
//  23.5..80.5 Hund zwischen zwei Containern, Querstraße bei x = 50, Café-Checkpoint bei x = 61,
//             federnde Markise bei x = 71 → hoher Balkon mit Wärme-Bonus
//  80.5..96  lange Baugrube: Fensterputzer-Gondel hoch, dann über zwei Balkone
//   96..148  zwei Hunde (Bank/Container als Wände), Balkon mit Sprung-Boost, Kunde bei x = 140
public static class CityLevelBuilder
{
    private const string ScenePath = "Assets/Scenes/levels/level01.unity";
    private const float KeepWarmTime = 75f;

    private static readonly HashSet<string> Keep = new HashSet<string>
    {
        "Main Camera", "Directional Light", "Player", "GameManager", "HUD", "KillZone", "Goal", "StartPoint"
    };

    private static Transform groundParent, buildingParent, propParent, gameplayParent;

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

        groundParent = new GameObject("City Ground").transform;
        buildingParent = new GameObject("City Buildings").transform;
        propParent = new GameObject("City Props").transform;
        gameplayParent = new GameObject("City Gameplay").transform;

        BuildGround();
        GameObject pizzeria = BuildBuildings();
        BuildGameplay();
        BuildProps();
        SetupEssentials(pizzeria);
        SetupAtmosphere();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CityLevelBuilder: Level 1 als Stadt gebaut.");
    }

    // ---------- Boden ----------

    private static void BuildGround()
    {
        // Mitte und Länge so gewählt, dass die Mitte auf ganzen Metern liegt (PrefabGridEnforcer)
        Sidewalk(1, 38);     // -18 .. 20
        Sidewalk(52, 57);    // 23.5 .. 80.5
        Sidewalk(122, 52);   // 96 .. 148

        // Baugruben je 1 m breiter als die Lücke, damit sie unter den Gehweg-Kanten verschwinden
        Stretched("Construction Pit", 22, 0, 5, groundParent);   // Lücke 20 .. 23.5
        Stretched("Construction Pit", 88, 0, 17, groundParent);  // Lücke 80.5 .. 96
        Place("Barrier", 19, 0, propParent);
        Place("Barrier", 25, 0, propParent);
        Place("Barrier", 79, 0, propParent);
        Place("Barrier", 97, 0, propParent);
    }

    private static void Sidewalk(int centerX, float length)
    {
        Stretched("Sidewalk", centerX, 0, length, groundParent);
    }

    // ---------- Gebäude ----------

    private static GameObject BuildBuildings()
    {
        GameObject pizzeria = Place("Pizzeria", -8, 0, buildingParent);       // -13 .. -3
        Place("Coffee Shop", 61, 0, buildingParent);                          // 57 .. 65

        // Bereiche ohne normale Häuser: Pizzeria, Querstraße (Autos fahren dort von hinten durch), Café, Kunde
        // Hinter dem Kunden stehen normale Häuser, seine Haustür wirkt dann wie ein Eingang
        var reserved = new List<(float from, float to)> { (-13, -3), (45, 55), (57, 65) };

        string[] order = { "Building Brick", "Building Beige", "Building Gray", "Building Teal", "Building Yellow", "Building Pink" };
        var widths = new Dictionary<string, float>();
        foreach (var type in CityPropsSetup.BuildingTypes)
        {
            widths[type.name] = type.width;
        }

        int next = 0;
        float cursor = -27f;
        while (cursor < 150f)
        {
            string name = order[next % order.Length];
            float width = widths[name];
            float from = cursor;
            float to = cursor + width;

            int blocked = reserved.FindIndex(zone => from < zone.to && to > zone.from);
            if (blocked >= 0)
            {
                // Passt nicht davor: hinter dem reservierten Bereich weitermachen (die Lücke wird eine Gasse)
                cursor = reserved[blocked].to;
                continue;
            }

            Place(name, Mathf.RoundToInt(from + width / 2f), 0, buildingParent);
            cursor = to;
            next++;
        }

        // Skyline-Gruppen sind je 42 m breit. Zwischen 43 und 56 bleibt eine Lücke für die Querstraße
        foreach (int x in new[] { -20, 22, 77, 119, 161 })
        {
            Place("Skyline", x, 0, buildingParent);
        }
        (int x, int y)[] clouds = { (-10, 28), (25, 33), (60, 26), (95, 34), (130, 29), (165, 31) };
        foreach (var cloud in clouds)
        {
            Place("Cloud", cloud.x, cloud.y, buildingParent);
        }
        return pizzeria;
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
        PlaceAsset("Assets/Prefabs/Street Crossing.prefab", 50, 0, gameplayParent);

        // Markise (Oberkante 2.5) katapultiert ~5 m hoch → Balkon (Oberkante 6.5) mit Wärme-Bonus.
        // Ohne Markise zu hoch (6 m über dem Gehweg)
        Place("Bouncy Awning", 71, 2, gameplayParent);
        Place("Balcony", 75, 6, gameplayParent);
        PowerUp(75, 8, PowerUpKind.TimeBonus, false);

        // Lange Baugrube: Gondel bündig am Gehweg-Ende, fährt auf Balkon-Höhe, dann zwei Balkone (je 2 m Abstand)
        Place("Window Washer Gondola", 82, 0, gameplayParent);   // 80.5 .. 83.5, fährt bis Oberkante 6.5
        Place("Balcony", 87, 6, gameplayParent);                 // 85.5 .. 88.5
        Place("Balcony", 92, 6, gameplayParent);                 // 90.5 .. 93.5, dann runter auf den Gehweg bei 96

        // Schlussstück: Hund 2 zwischen Bank und Container, Balkon mit Sprung-Boost, Hund 3 zwischen zwei Containern
        Place("Bench", 101, 0, gameplayParent);
        Dog(106, -1f);
        Place("Dumpster", 112, 0, gameplayParent);
        Place("Balcony", 116, 3, gameplayParent);
        PowerUp(116, 5, PowerUpKind.Jump, true);
        Dog(123, 1f);
        Place("Dumpster", 130, 0, gameplayParent);
    }

    private enum PowerUpKind { Speed = 0, Jump = 1, TimeBonus = 2 }

    private static void PowerUp(int x, int y, PowerUpKind kind, bool respawn)
    {
        GameObject powerUp = PlaceAsset("Assets/Prefabs/PowerUp.prefab", x, y, gameplayParent);
        var serialized = new SerializedObject(powerUp.GetComponent<PowerUp>());
        serialized.FindProperty("type").enumValueIndex = (int)kind;
        serialized.FindProperty("respawnOnLevelReset").boolValue = respawn;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        powerUp.name = $"PowerUp {kind}";
    }

    // Hund-Wurzel 1 m über der Gehweg-Mitte: Box-Unterkante liegt dann genau auf der Oberkante
    private static void Dog(int x, float startDirection)
    {
        GameObject dog = PlaceAsset("Assets/Prefabs/Dog.prefab", x, 1, gameplayParent);
        var serialized = new SerializedObject(dog.GetComponent<GroundPatrol>());
        serialized.FindProperty("startDirection").floatValue = startDirection;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Deko ----------

    private static void BuildProps()
    {
        // Nicht in Baugruben, nicht auf der Querstraße, nicht vor Türen und nicht unter Balkonen/Markise
        // (Laterne und Baumkrone würden sonst durch die Platte stechen)
        var blocked = new List<(float from, float to)>
        {
            (19, 25), (46, 54), (79, 97), (-10, -6), (59, 63), (138, 143),
            (6.5f, 15.5f), (68.5f, 77.5f), (113.5f, 118.5f)
        };
        bool Free(int x) => blocked.TrueForAll(zone => x < zone.from || x > zone.to);

        for (int x = -16; x < 146; x += 12)
        {
            if (Free(x)) Place("Street Lamp", x, 0, propParent);
            if (Free(x + 6)) Place("Tree", x + 6, 0, propParent);
        }
        foreach (int x in new[] { 15, 57, 104, 134 })
        {
            if (Free(x)) Place("Hydrant", x, 0, propParent);
        }
        foreach (int x in new[] { -2, 33, 68, 118 })
        {
            if (Free(x)) Place("Trash Can", x, 0, propParent);
        }
    }

    // ---------- Grundausstattung ----------

    private static void SetupEssentials(GameObject pizzeria)
    {
        SpawnDoor door = pizzeria.GetComponent<SpawnDoor>();

        GameObject startPoint = GameObject.Find("StartPoint");
        if (startPoint != null)
        {
            startPoint.transform.position = door.OutsidePosition;
        }

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = door.OutsidePosition;
        }

        var gameManager = Object.FindAnyObjectByType<GameManager>();
        var serialized = new SerializedObject(gameManager);
        serialized.FindProperty("startDoor").objectReferenceValue = door;
        serialized.FindProperty("levelTime").floatValue = KeepWarmTime;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject goal = GameObject.Find("Goal");
        if (goal != null)
        {
            goal.transform.position = new Vector3(140f, 0f, 0f);
        }

        GameObject killZone = GameObject.Find("KillZone");
        if (killZone != null)
        {
            // Knapp über dem Baugruben-Boden (y = -4): Der Tod passiert noch sichtbar in der Grube
            killZone.transform.position = new Vector3(65f, -3f, 0f);
            killZone.transform.localScale = new Vector3(210f, 1f, 40f);
            // Unsichtbar: Man soll in die Baugrube fallen, nicht auf eine rote Fläche schauen
            if (killZone.TryGetComponent(out MeshRenderer killRenderer))
            {
                killRenderer.enabled = false;
            }
        }
    }

    // Leichter Dunst: Skyline und Wolken werden blasser, das gibt Tiefe
    private static void SetupAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.81f, 0.9f);
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = 130f;
    }

    // ---------- Hilfen ----------

    private static GameObject Place(string cityPrefab, int x, int y, Transform parent)
    {
        return PlaceAsset($"{CityPropsSetup.PrefabFolder}/{cityPrefab}.prefab", x, y, parent);
    }

    private static GameObject Stretched(string cityPrefab, int x, int y, float length, Transform parent)
    {
        GameObject instance = Place(cityPrefab, x, y, parent);
        instance.transform.localScale = new Vector3(length, 1f, 1f);
        return instance;
    }

    private static GameObject PlaceAsset(string path, int x, int y, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new System.Exception($"CityLevelBuilder: Prefab fehlt: {path} (erst Build City Prefabs ausführen)");
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        instance.transform.position = new Vector3(x, y, 0f);
        return instance;
    }
}
