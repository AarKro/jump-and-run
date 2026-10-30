using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gemeinsame Bausteine für die Level-Generatoren (CityLevelBuilder = Level 1, Level2Builder, ...).
// Alle Positionen sind Lauf-Koordinaten u entlang der LanePath des Levels; Prefabs werden in den Lane-Rahmen gedreht
// (lokal +X = Laufrichtung, +Z = Hausseite). Der Ablauf eines Generators:
// OpenAndClear → BuildLane → Boden/Spiel-Elemente/Deko mit Place & Co. → CityLink + Cutouts → SetupEssentials → Save.
public static class LevelBuildKit
{
    // Bleibt beim Neubau stehen. Kein "Directional Light": Die Sonne kommt aus der Stadt-Szene (sonst doppelt hell)
    public static readonly HashSet<string> Keep = new HashSet<string>
    {
        "Main Camera", "Player", "GameManager", "HUD", "KillZone", "Goal", "StartPoint"
    };

    public static LanePath Lane { get; private set; }

    // Szene öffnen und alles löschen, was nicht in Keep steht
    public static Scene OpenAndClear(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!Keep.Contains(root.name))
            {
                Object.DestroyImmediate(root);
            }
        }
        return scene;
    }

    // Neue Level-Szene als Kopie der Vorlage (bringt die festen Objekte mit), in den Build Settings direkt hinter afterPath.
    // owner = Name des Generators für die Fehlermeldung
    public static void EnsureScene(string scenePath, string templatePath, string afterPath, string owner)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            if (!AssetDatabase.CopyAsset(templatePath, scenePath))
            {
                throw new System.Exception($"{owner}: Konnte {templatePath} nicht nach {scenePath} kopieren");
            }
        }
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(entry => entry.path == scenePath))
        {
            return;
        }
        int after = scenes.FindIndex(entry => entry.path == afterPath);
        scenes.Insert(after + 1, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Ziel ohne Kundenhaus: das Goal-Prefab (aus der kopierten Szene) gegen ein anderes Ziel-Prefab tauschen. Es heißt weiter
    // "Goal", damit OpenAndClear und SetupEssentials es finden. Ist es schon das richtige, bleibt es stehen
    public static void UseGoal(string prefabPath)
    {
        GameObject old = GameObject.Find("Goal");
        if (old != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old) == prefabPath)
        {
            return;
        }
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        var goal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        goal.name = "Goal";
    }

    public static LanePath BuildLane(Vector3 start, float heading, float startU, List<LanePath.Piece> pieces)
    {
        var laneObject = new GameObject("Lane Path");
        Lane = laneObject.AddComponent<LanePath>();
        Lane.Configure(start, heading, startU, pieces);
        return Lane;
    }

    public static LanePath.Piece Straight(float length) => new LanePath.Piece { type = LanePath.PieceType.Straight, length = length };

    // Viertelkreis: positiv = Linkskurve, negativ = Rechtskurve
    public static LanePath.Piece Arc(float radius, float degrees) =>
        new LanePath.Piece { type = LanePath.PieceType.Arc, length = Mathf.Abs(degrees) * Mathf.Deg2Rad * radius, turnDegrees = degrees };

    public static Transform Group(string name) => new GameObject(name).transform;

    public static Quaternion Frame(float u) => Quaternion.LookRotation(Vector3.Cross(Lane.TangentAt(u), Vector3.up), Vector3.up);

    public static GameObject Place(string cityPrefab, float u, float y, Transform parent)
    {
        return PlaceAsset($"{CityPropsSetup.PrefabFolder}/{cityPrefab}.prefab", u, y, parent);
    }

    // Prefab mit gestreckter Wurzel (Gehweg, Baugrube): Länge entlang der Laufrichtung
    public static GameObject Stretched(string cityPrefab, float u, float y, float length, Transform parent)
    {
        GameObject instance = Place(cityPrefab, u, y, parent);
        instance.transform.localScale = new Vector3(length, 1f, 1f);
        return instance;
    }

    // Auf die Bahn stellen: Position an der Stelle u, gedreht in den Lane-Rahmen
    public static GameObject PlaceAsset(string path, float u, float y, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new System.Exception($"LevelBuildKit: Prefab fehlt: {path}");
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(Lane.PointAt(u) + Vector3.up * y, Frame(u));
        return instance;
    }

    // Chili (Wärme-Bonus): taucht nach dem Tod nicht wieder auf (Prefab-Standard), sonst könnte man Wärme farmen
    public static GameObject HeatPickup(float u, float y, Transform parent)
    {
        return PlaceAsset("Assets/Prefabs/Heat Pickup.prefab", u, y, parent);
    }

    // Hund-Wurzel 1 m über der Gehweg-Mitte: Box-Unterkante liegt dann genau auf der Oberkante.
    // startDirection +1 = schaut in +u, -1 = in -u
    public static GameObject Dog(float u, float startDirection, Transform parent)
    {
        GameObject dog = PlaceAsset("Assets/Prefabs/Dog.prefab", u, 1, parent);
        var serialized = new SerializedObject(dog.GetComponent<GroundPatrol>());
        serialized.FindProperty("startDirection").floatValue = startDirection;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return dog;
    }

    // Unsichtbarer Laufboden (Layer Ground) entlang der Bahn von u0 bis u1, Oberkante top. Für Stellen, an denen die Stadt
    // keinen Collider hat (Fahrbahnen, Hafen). Auf Geraden ein Stück, in Bögen etwa 1 m lange Sehnen; 4 m breit, 1 m dick
    public static void WalkGround(string name, float u0, float u1, float top, Transform parent)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        bool straight = Vector3.Angle(Lane.TangentAt(u0), Lane.TangentAt(u1)) < 0.1f
            && Vector3.Angle(Lane.TangentAt(u0), Lane.TangentAt((u0 + u1) / 2f)) < 0.1f;
        int steps = straight ? 1 : Mathf.Max(1, Mathf.CeilToInt(u1 - u0));
        for (int i = 0; i < steps; i++)
        {
            Vector3 p0 = Lane.PointAt(Mathf.Lerp(u0, u1, i / (float)steps));
            Vector3 p1 = Lane.PointAt(Mathf.Lerp(u0, u1, (i + 1) / (float)steps));
            var piece = new GameObject("Walk " + (i + 1)) { layer = 3 };
            piece.transform.SetParent(group, false);
            piece.transform.SetPositionAndRotation((p0 + p1) / 2f + Vector3.up * (top - 0.5f),
                Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized, Vector3.up));
            // Etwas länger als die Sehne, damit zwischen den Stücken im Bogen keine Kerbe entsteht
            PrimitiveKit.AddBoxCollider(piece, Vector3.zero, new Vector3(Vector3.Distance(p0, p1) + 0.1f, 1f, 4f));
        }
    }

    // Erhöhter Überweg über eine 12 m breite Avenue mit zwei Spuren (Prefab "Avenue Crossing"). spawnZ/despawnZ: wo die Autos
    // auftauchen und verschwinden, lokal Z im Lane-Rahmen (+ = hinter der Bahn, - = Kameraseite); ohne despawnZ gilt der Wert im Prefab
    public static GameObject AvenueCrossing(float u, float spawnZ, Transform parent, float? despawnZ = null)
    {
        GameObject crossing = PlaceAsset("Assets/Prefabs/Avenue Crossing.prefab", u, 0, parent);
        foreach (TrafficLane trafficLane in crossing.GetComponentsInChildren<TrafficLane>(true))
        {
            var serialized = new SerializedObject(trafficLane);
            serialized.FindProperty("spawnZ").floatValue = spawnZ;
            if (despawnZ.HasValue)
            {
                serialized.FindProperty("despawnZ").floatValue = despawnZ.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return crossing;
    }

    // Baugrube auf der Margherita Avenue (Ringstraße, u = Welt-X), Lücke von/bis in Welt-X (Level 2 und 4). Das Prefab ist
    // 1 m länger und steckt unter den Gehwegkanten; der Cutout schneidet Gehweg, Straße und alles darunter in der Lücke aus,
    // nur bis zur Strandkante (z -12, davor liegt der Strand). Seitenwände 0.1 m in der Lücke (genau auf der Schnittkante
    // flackerten sie mit dem Stadt-Boden): unter dem Gehweg bis zu dessen Oberkante, unter der Straße bis zu deren Oberkante
    public static void RingPit(Transform link, float fromX, float toX, Transform parent)
    {
        float center = (fromX + toX) / 2f;
        Stretched("Construction Pit", center, 0, toX - fromX + 1f, parent);
        Place("Barrier", Mathf.Round(toX + 1f), 0, parent);
        Place("Barrier", Mathf.Round(fromX - 1f), 0, parent);
        Cutout(link, $"Pit {center:F0}", fromX, toX, -12f, 3f);
        Material foundation = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Foundation.mat");
        foreach (float x in new[] { fromX + 0.1f, toX - 0.1f })
        {
            PrimitiveKit.Box("Pit Wall Walk", parent, new Vector3(x, (-4f + 0.49f) / 2f, 0.5f), new Vector3(0.2f, 4.49f, 5f), foundation);
            PrimitiveKit.Box("Pit Wall Road", parent, new Vector3(x, (-4f + 0.29f) / 2f, -7f), new Vector3(0.2f, 4.29f, 10f), foundation);
        }
    }

    // 0.2 m dicke Wand quer zur Bahn an der Stelle u, von lokal z0 bis z1, von der Grubensohle (-4) bis top (Level 3 und 4)
    public static void LaneBox(string name, float u, float z0, float z1, float top, Material material, Transform parent)
    {
        Renderer box = PrimitiveKit.Box(name, parent, Vector3.zero, new Vector3(0.2f, top + 4f, z1 - z0), material);
        Quaternion frame = Frame(u);
        box.transform.SetPositionAndRotation(Lane.PointAt(u) + frame * new Vector3(0f, (top - 4f) / 2f, (z0 + z1) / 2f), frame);
    }

    // Straßensperre quer über eine Avenue an der Stelle u (nur Optik, vor der Laufbahn, Level 3 und 4): rot-weiße Baken und ein
    // Schild. curbLocalZ = Bordstein vor der Laufbahn, roadEndLocalZ = gegenüberliegender Bordstein (Lane-Rahmen)
    public static void RoadClosure(string name, float u, float curbLocalZ, float roadEndLocalZ, Transform parent)
    {
        Quaternion frame = Frame(u);
        Transform closure = new GameObject(name).transform;
        closure.SetParent(parent, false);
        closure.SetPositionAndRotation(Lane.PointAt(u), frame);

        Material orange = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Barrier Orange.mat");
        Material white = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Awning White.mat");
        Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Iron.mat");
        const float road = CityLayout.RoadTop;
        // Absperrgitter in einer Reihe quer über die Fahrbahn, schräg gestellt: Gerade quer zur Straße sähe die Kamera
        // von der Seite nur ihre Kanten. Dazwischen Pylonen
        for (float localZ = curbLocalZ - 1.5f; localZ > roadEndLocalZ; localZ -= 2.5f)
        {
            Transform panel = PrimitiveKit.Pivot("Barrier Panel", closure, new Vector3(0f, road, localZ));
            panel.localRotation = Quaternion.Euler(0f, 40f, 0f);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                PrimitiveKit.Box("Post", panel, new Vector3(x, 0.5f, 0f), new Vector3(0.08f, 1f, 0.08f), iron);
            }
            for (int i = 0; i < 4; i++)
            {
                PrimitiveKit.Box("Stripe", panel, new Vector3(-0.45f + i * 0.3f, 0.85f, 0f), new Vector3(0.3f, 0.25f, 0.04f), i % 2 == 0 ? orange : white);
            }
            PrimitiveKit.Part("Cone", PrimitiveType.Cylinder, closure, new Vector3(0f, road + 0.25f, localZ - 1.25f), new Vector3(0.3f, 0.25f, 0.3f), orange);
        }
        // Schild an der Bordsteinkante, Schrift zur Kamera
        PrimitiveKit.Box("Sign Post", closure, new Vector3(0f, road + 0.9f, curbLocalZ - 0.4f), new Vector3(0.08f, 1.8f, 0.08f), iron);
        PrimitiveKit.Box("Sign", closure, new Vector3(0f, road + 1.9f, curbLocalZ - 0.45f), new Vector3(1.6f, 0.6f, 0.04f), white);
        var text = PrimitiveKit.Text("Sign Text", closure, new Vector3(0f, road + 1.9f, curbLocalZ - 0.48f), new Vector2(1.5f, 0.5f), "ROAD CLOSED", new Color(0.8f, 0.13f, 0.1f));
        text.fontSizeMax = 4f;
    }

    // Bach auf der Strandpromenade (Level 2 und 4): kommt unter der Häuserreihe hervor und fließt über den Strand ins Meer.
    // Die Lücke im Plattenweg ist so breit wie eine Baugrube; wer hineinfällt, landet im Wasser (Respawn). Der Cutout nimmt
    // Weg, Kante, Sand und alles darunter (auch das Meer) heraus, das Wasser füllt das Loch wieder auf
    public static void BeachCreek(Transform link, float fromX, float toX, Transform parent)
    {
        float center = (fromX + toX) / 2f;
        float width = toX - fromX;
        const float front = CityLayout.SandFrontZ, back = CityLayout.PromenadeBackZ;
        const float waterTop = 0.05f;
        Cutout(link, $"Creek {center:F0}", fromX, toX, front, back);

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Sea.mat");
        Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Curb.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Interior Dark.mat");

        // Wasser knapp unter dem Sand: Zum Strand hin sieht es aus, als flösse der Bach einfach hinaus
        PrimitiveKit.Box("Creek Water", parent, new Vector3(center, (waterTop - 3f) / 2f, (front + back) / 2f), new Vector3(width, waterTop + 3f, back - front), water);
        // Ufermauern unter dem Plattenweg, 0.1 m in der Lücke (lägen sie genau auf der Schnittkante, flackerten beide)
        foreach (float x in new[] { fromX + 0.1f, toX - 0.1f })
        {
            PrimitiveKit.Box("Creek Wall", parent, new Vector3(x, 0f, (CityLayout.PromenadeFrontZ + back) / 2f), new Vector3(0.2f, 1f, back - CityLayout.PromenadeFrontZ), stone);
        }
        // Auslass unter der Häuserreihe: dunkle Öffnung mit Steinrahmen. 3 cm vor der Schnittkante (z = 3): Dort endet der
        // Stadt-Boden unter den Häusern, eine Fläche genau auf dieser Ebene flackert mit ihm (Z-Fighting)
        PrimitiveKit.Box("Culvert", parent, new Vector3(center, 0.27f, back - 0.03f), new Vector3(width - 0.8f, 0.44f, 0.04f), dark);
        PrimitiveKit.Box("Culvert Lintel", parent, new Vector3(center, 0.53f, back - 0.05f), new Vector3(width - 0.4f, 0.12f, 0.12f), stone);

        // Rinnsal den Abhang hinunter bis ins Meer, knapp über der Schräge
        CityLayout.BeachSlope(out Vector3 surfaceCenter, out Quaternion rotation, out float length, out float waterlineZ);
        float stripLength = length / 2f - waterlineZ + 0.3f;
        Vector3 local = new Vector3(0f, 0.02f, length / 2f - stripLength / 2f);
        surfaceCenter.x = center;
        Renderer stream = PrimitiveKit.Box("Creek Stream", parent, surfaceCenter + rotation * local, new Vector3(width - 0.8f, 0.02f, stripLength), water);
        stream.transform.rotation = rotation;

        // Ins Wasser fallen = Respawn: Auslöser knapp über der Wasseroberfläche, nur unter dem Plattenweg
        var trap = new GameObject("Creek Kill");
        trap.transform.SetParent(parent, false);
        trap.transform.position = new Vector3(center, waterTop - 0.5f, (CityLayout.PromenadeFrontZ + back) / 2f);
        PrimitiveKit.AddBoxCollider(trap, Vector3.zero, new Vector3(width, 1.1f, back - CityLayout.PromenadeFrontZ), isTrigger: true);
        trap.AddComponent<KillZone>();
    }

    // Möwe über einem Strand-Stück: greift nur an, solange der Spieler im Revier (u von/bis) ist; firstDelay staffelt mehrere
    public static GameObject Seagull(float u, float firstDelay, float territoryFrom, float territoryTo, Transform parent)
    {
        GameObject gull = PlaceAsset("Assets/Prefabs/Seagull.prefab", u, 10, parent);
        var serialized = new SerializedObject(gull.GetComponent<Seagull>());
        serialized.FindProperty("territoryFrom").floatValue = territoryFrom;
        serialized.FindProperty("territoryTo").floatValue = territoryTo;
        serialized.FindProperty("firstDelay").floatValue = firstDelay;
        serialized.FindProperty("cooldown").floatValue = 5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return gull;
    }

    // Hund auf der Strandpromenade: Draufgesprungen läuft er zum Meer, von der Promenade 0.3 m hinunter auf den Sand
    public static GameObject BeachDog(float u, Transform parent)
    {
        GameObject dog = Dog(u, 1f, parent);
        var settings = new SerializedObject(dog.GetComponent<Dog>());
        settings.FindProperty("curbDrop").floatValue = CityLayout.Surface - CityLayout.SandTop;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return dog;
    }

    // "City Link": LevelCity lädt die Stadt und richtet die festen Gebäude ein (Start-Tür, benutzte Checkpoints/Mikrowelle)
    public static Transform CityLink(string startLandmark, params string[] usedLandmarks)
    {
        Transform link = new GameObject("City Link").transform;
        var serialized = new SerializedObject(link.gameObject.AddComponent<LevelCity>());
        serialized.FindProperty("startLandmark").stringValue = startLandmark;
        SerializedProperty used = serialized.FindProperty("usedLandmarks");
        used.arraySize = usedLandmarks.Length;
        for (int i = 0; i < usedLandmarks.Length; i++)
        {
            used.GetArrayElementAtIndex(i).stringValue = usedLandmarks[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return link;
    }

    // Rechteck in Welt-X/Z (von, bis), in dem die Stadt für dieses Level verschwindet (Boden wird ausgeschnitten)
    // treesOnly: nur Bäume und Büsche; keepGround: alles außer dem Boden (siehe CityCutout)
    public static void Cutout(Transform link, string name, float x0, float x1, float z0, float z1, bool treesOnly = false, bool keepGround = false)
    {
        Transform cutout = new GameObject("Cutout " + name).transform;
        cutout.SetParent(link, false);
        cutout.position = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
        cutout.localScale = new Vector3(Mathf.Abs(x1 - x0), 1f, Mathf.Abs(z1 - z0));
        var component = cutout.gameObject.AddComponent<CityCutout>();
        if (treesOnly || keepGround)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty("treesOnly").boolValue = treesOnly;
            serialized.FindProperty("keepGround").boolValue = keepGround;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Spieler und Startpunkt an die Tür der Pizzeria (steht in der Stadt), Wärme-Zeit, Ziel an die Stelle goalU mit eigener
    // Hausnummer, KillZone unsichtbar knapp über dem Baugruben-Boden, Kamerafahrt, Himmel und Nebel wie in der Stadt
    // Intro: Lieferadresse (wird eingeblendet, deliveryNote = optionale kleinere zweite Zeile) und Startposition der Kamera relativ zum Kunden (Welt-Achsen)
    public static void SetupEssentials(float goalU, string houseNumber, float keepWarmTime, Vector3 killZoneCenter, Vector3 killZoneSize,
        string deliveryAddress, string deliveryNote, Vector3 introOverviewOffset)
    {
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
        serialized.FindProperty("levelTime").floatValue = keepWarmTime;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null)
        {
            if (mainCamera.GetComponent<IntroFlyover>() == null)
            {
                mainCamera.AddComponent<IntroFlyover>();
            }
            var follow = new SerializedObject(mainCamera.GetComponent<ThirdPersonCamera>());
            follow.FindProperty("lookHeight").floatValue = 2.86f;
            follow.ApplyModifiedPropertiesWithoutUndo();
            var intro = new SerializedObject(mainCamera.GetComponent<IntroFlyover>());
            intro.FindProperty("deliveryAddress").stringValue = deliveryAddress;
            intro.FindProperty("deliveryNote").stringValue = deliveryNote;
            intro.FindProperty("overviewOffset").vector3Value = introOverviewOffset;
            intro.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject goal = GameObject.Find("Goal");
        if (goal != null)
        {
            goal.transform.SetPositionAndRotation(Lane.PointAt(goalU), Frame(goalU));
            // Jeder Kunde wohnt woanders: Hausnummer am Kundenhaus (Teil des Goal-Prefabs) pro Level überschreiben
            foreach (TMPro.TextMeshPro text in goal.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                if (text.name == "Number")
                {
                    var number = new SerializedObject(text);
                    number.FindProperty("m_text").stringValue = houseNumber;
                    number.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        GameObject killZone = GameObject.Find("KillZone");
        if (killZone != null)
        {
            killZone.transform.SetPositionAndRotation(killZoneCenter, Quaternion.identity);
            killZone.transform.localScale = killZoneSize;
            if (killZone.TryGetComponent(out MeshRenderer killRenderer))
            {
                killRenderer.enabled = false;
            }
        }

        // Gleicher Himmel und Nebel wie die Stadt, dann ist egal, welche der beiden Szenen gerade aktiv ist
        CityBuilder.ApplyAtmosphere();
    }

    public static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
