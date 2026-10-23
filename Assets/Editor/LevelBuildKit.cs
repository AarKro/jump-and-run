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

    public enum PowerUpKind { Speed = 0, Jump = 1, TimeBonus = 2 }

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

    public static GameObject PowerUp(float u, float y, PowerUpKind kind, bool respawn, Transform parent)
    {
        GameObject powerUp = PlaceAsset("Assets/Prefabs/PowerUp.prefab", u, y, parent);
        var serialized = new SerializedObject(powerUp.GetComponent<PowerUp>());
        serialized.FindProperty("type").enumValueIndex = (int)kind;
        serialized.FindProperty("respawnOnLevelReset").boolValue = respawn;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        powerUp.name = $"PowerUp {kind}";
        return powerUp;
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
    public static void Cutout(Transform link, string name, float x0, float x1, float z0, float z1)
    {
        Transform cutout = new GameObject("Cutout " + name).transform;
        cutout.SetParent(link, false);
        cutout.position = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
        cutout.localScale = new Vector3(Mathf.Abs(x1 - x0), 1f, Mathf.Abs(z1 - z0));
        cutout.gameObject.AddComponent<CityCutout>();
    }

    // Spieler und Startpunkt an die Tür der Pizzeria (steht in der Stadt), Wärme-Zeit, Ziel an die Stelle goalU mit eigener
    // Hausnummer, KillZone unsichtbar knapp über dem Baugruben-Boden, Kamerafahrt, Himmel und Nebel wie in der Stadt
    // Intro: Lieferadresse (wird eingeblendet) und Startposition der Kamera relativ zum Kunden (Welt-Achsen)
    public static void SetupEssentials(float goalU, string houseNumber, float keepWarmTime, Vector3 killZoneCenter, Vector3 killZoneSize,
        string deliveryAddress, Vector3 introOverviewOffset)
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
