using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Baut die gemeinsame Stadt als eigene Szene (Assets/Scenes/City.unity) komplett aus Code, nach CityLayout.
// Menü: Tools > Jump and Run > City > Rebuild City Scene. Erneut ausführen ersetzt alles in der Szene.
// Die Level-Szenen werden später additiv darübergeladen (Route, Gegner, Kunde, Absperrungen).
//
// Absichtlich OHNE Prefab-Instanzen: PrefabGridEnforcer würde jede Prefab-Wurzel aufs Raster bzw. auf die Laufbahn
// des gerade offenen Levels schieben. Hier gibt es nur Grundformen und erzeugte Meshes.
// Die Stadt-Szene besitzt Sonne, Himmel und Nebel: Bei mehreren geladenen Szenen gelten die Render-Einstellungen
// der aktiven Szene, also wird später die Stadt die aktive Szene.
public static partial class CityBuilder
{
    public const string ScenePath = "Assets/Scenes/City.unity";
    private const string MaterialFolder = "Assets/Materials/City";
    private const int GroundLayer = 3;

    private const float W = CityLayout.West, E = CityLayout.East, N = CityLayout.North;
    private const float Surface = CityLayout.Surface, RoadTop = CityLayout.RoadTop;

    private static Transform root;
    private static Material facade;

    [MenuItem("Tools/Jump and Run/City/Rebuild City Scene")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Stadt neu bauen",
                $"Ersetzt den kompletten Inhalt von {ScenePath}. Fortfahren?", "Neu bauen", "Abbrechen"))
        {
            return;
        }
        Build();
    }

    // Ohne Dialog, für den Aufruf über Skripte (MCP). Öffnet die Stadt additiv, damit die gerade offene Szene unberührt bleibt
    public static void Build()
    {
        Scene previousActive = SceneManager.GetActiveScene();
        // War die Stadt schon offen (z.B. automatisch neben Level 1), bleibt sie danach offen
        bool wasOpen = SceneManager.GetSceneByPath(ScenePath).isLoaded;
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        // Neue Objekte landen in der aktiven Szene, und RenderSettings gehören auch der aktiven Szene
        SceneManager.SetActiveScene(scene);

        foreach (GameObject old in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(old);
        }

        PrimitiveKit.EnsureFolder("Assets/Materials", "City");
        facade = FacadeMaterial();
        root = new GameObject("City").transform;
        root.gameObject.AddComponent<CityRoot>();

        CreateSun(root);
        ApplyAtmosphere();
        BuildGround();
        BuildStreets();
        BuildBlocks();
        BuildBeach();
        Transform landmarks = Group("Landmarks");
        PlaceLandmarks(landmarks);
        BuildFoundersCircle(landmarks);
        BuildKaiju(Group("Kaiju"));
        BuildSurroundings();
        BuildStreetSigns();

        EditorSceneManager.SaveScene(scene, ScenePath);
        if (previousActive.IsValid() && previousActive != scene)
        {
            SceneManager.SetActiveScene(previousActive);
            if (!wasOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"CityBuilder: Stadt gebaut ({ScenePath}).");
    }

    // ---------- Licht, Himmel, Nebel ----------

    // Die Sonne gehört der Stadt. Level, die die Stadt laden, haben deshalb kein eigenes Directional Light (sonst doppelt hell)
    private static void CreateSun(Transform parent)
    {
        // Wie die Sonne in Level 1
        var sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(parent, false);
        sunObject.transform.rotation = Quaternion.Euler(50f, 330f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.957f, 0.839f);
        sun.intensity = 1f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
    }

    // Himmel, Umgebungslicht und Nebel der aktiven Szene. Auch die Level mit Stadt bekommen genau diese Werte
    // (CityLevelBuilder), damit es egal ist, welche der beiden Szenen gerade die aktive ist
    public static void ApplyAtmosphere()
    {
        // Die ganze Stadt soll sichtbar bleiben: Dunst erst ab 150 m, ganz weg erst bei 900 m (Kamera-Far-Clip ist 1000 m)
        var fogColor = new Color(0.74f, 0.81f, 0.9f);

        // Eigener Himmel statt Default-Skybox: Dessen untere Hälfte ist grau und schaut hinter dem Far Clip (1000 m) unter dem
        // Horizont hervor. Hier hat der "Boden" des Himmels die Nebelfarbe, so geht das Meer nahtlos in den Dunst über
        string skyPath = $"{MaterialFolder}/City Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_GroundColor", fogColor);
            AssetDatabase.CreateAsset(sky, skyPath);
        }

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Skybox;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = 150f;
        RenderSettings.fogEndDistance = 900f;
        DynamicGI.UpdateEnvironment();
    }

    // ---------- Boden und Straßen ----------

    private static void BuildGround()
    {
        Transform ground = Group("Ground");
        // Ein Straßenbelag unter der ganzen Stadt samt Ringstraße (Oberkante 0.3). Darauf liegen die Gehweg-Platten der Blöcke,
        // was dazwischen frei bleibt, ist Fahrbahn. Bis y = -3 hinunter, damit der Rand zum Strand/Wasser massiv aussieht
        // CityGround: Level können hier Löcher hineinschneiden (CityCutout)
        // Vor der Strandpromenade fehlt die Fahrbahn: dort liegen Sand und Wasser (CityBuilder.Beach)
        foreach (Renderer piece in SlabAround("Road Base", ground, W - 12f, -12f, E + 12f, N + 12f, -3f, RoadTop, Mat("Road"), BeachHole(-12f)))
        {
            piece.gameObject.AddComponent<CityGround>();
        }
    }

    private static void BuildStreets()
    {
        Transform streets = Group("Streets");
        Material line = Mat("Road Line");
        foreach (CityLayout.Street street in CityLayout.Streets)
        {
            if (street.Type == CityLayout.Side.Narrow)
            {
                continue;
            }
            // Gelbe Mittellinie wie in Level 1
            float y = RoadTop + 0.005f;
            Renderer centerLine = street.Vertical
                ? Slab(street.Name + " Line", streets, street.At - 0.075f, street.From, street.At + 0.075f, street.To, RoadTop, y, line)
                : Slab(street.Name + " Line", streets, street.From, street.At - 0.075f, street.To, street.At + 0.075f, RoadTop, y, line);
            centerLine.gameObject.AddComponent<CityGround>();
        }
    }

    // ---------- Blöcke ----------

    private static void BuildBlocks()
    {
        Transform blocks = Group("Blocks");
        Material sidewalk = Mat("Sidewalk");

        for (int i = 0; i < CityLayout.Blocks.Length; i++)
        {
            CityLayout.Block block = CityLayout.Blocks[i];
            Transform parent = new GameObject($"Block {i + 1} ({block.Kind})").transform;
            parent.SetParent(blocks, false);

            // Begehbare Gehweg-Platte über den ganzen Block, 1 m dick wie die Gehweg-Prefabs (Mitte y = 0)
            Rect walk = block.Sidewalk;
            // An der Strandpromenade beginnt der Gehweg erst an den Fassaden, davor liegt der Plattenweg
            if (walk.xMin >= CityLayout.BeachFromX - 0.1f && walk.xMax <= CityLayout.BeachToX + 0.1f && walk.yMin < CityLayout.PromenadeBackZ)
            {
                walk.yMin = CityLayout.PromenadeBackZ;
            }
            GameObject slab = Slab("Sidewalk", parent, walk.xMin, walk.yMin, walk.xMax, walk.yMax, Surface - 1f, Surface, sidewalk).gameObject;
            slab.layer = GroundLayer;
            slab.AddComponent<BoxCollider>();
            slab.AddComponent<CityGround>();

            var random = new System.Random(1000 + i * 77);
            switch (block.Kind)
            {
                case CityLayout.BlockKind.Houses:
                    foreach (Rect lot in Lots(block.Lots, random, false))
                    {
                        House(parent, lot, Pick(random, 8f, 10f, 11f, 13f, 14f, 18f));
                    }
                    // Haus mit fester Größe, wo Level 1 sein Kundenhaus hineinsetzt (schneidet dieses Haus aus)
                    if (block.Lots.Overlaps(CityLayout.CustomerHouseLot))
                    {
                        House(parent, CityLayout.CustomerHouseLot, 10f).name = "House (Customer Lot)";
                    }
                    break;
                case CityLayout.BlockKind.Downtown:
                    // Rund um die Olive Lane etwas höher: gibt der Stadt eine Skyline
                    foreach (Rect lot in Lots(block.Lots, random, false))
                    {
                        House(parent, lot, Pick(random, 14f, 18f, 22f, 26f, 30f, 34f));
                    }
                    break;
                case CityLayout.BlockKind.Piazza:
                    BuildPiazza(parent, block, random);
                    break;
                case CityLayout.BlockKind.Park:
                    BuildPark(parent, block, random);
                    break;
                case CityLayout.BlockKind.Ruins:
                    BuildRuins(parent, block, random);
                    break;
            }
        }
    }

    // Grundstücke entlang der Blockränder, 8 m tief wie die Häuser in Level 1, 8–14 m breit. Innen bleibt ein Hof frei.
    // Ist der Block zu schmal für zwei Reihen, wird er mit einer Reihe aufgefüllt. skipEast: Ostseite frei lassen (Piazza).
    // Reservierte Grundstücke (feste Gebäude, Kundenhaus) werden ausgespart, die Reihe läuft davor und dahinter weiter
    private static List<Rect> Lots(Rect area, System.Random random, bool skipEast)
    {
        const float depth = 8f;
        var lots = new List<Rect>();
        var reserved = new List<Rect>(CityLayout.LandmarkLots) { CityLayout.CustomerHouseLot };

        void Run(float from, float to, System.Action<float, float> add)
        {
            float position = from;
            while (to - position > 0.5f)
            {
                float width = 8f + (float)random.NextDouble() * 6f;
                if (to - (position + width) < 6f)
                {
                    width = to - position;
                }
                add(position, position + width);
                position += width;
            }
        }

        // Eine Häuserreihe (band) entlang X oder Z, unterbrochen von reservierten Grundstücken
        void Row(Rect band, bool alongX)
        {
            var blocked = new List<(float from, float to)>();
            foreach (Rect lot in reserved)
            {
                if (lot.Overlaps(band))
                {
                    blocked.Add(alongX ? (lot.xMin, lot.xMax) : (lot.yMin, lot.yMax));
                }
            }
            blocked.Sort((a, b) => a.from.CompareTo(b.from));

            System.Action<float, float> add = alongX
                ? (a, b) => lots.Add(Rect.MinMaxRect(a, band.yMin, b, band.yMax))
                : (a, b) => lots.Add(Rect.MinMaxRect(band.xMin, a, band.xMax, b));
            float start = alongX ? band.xMin : band.yMin;
            float end = alongX ? band.xMax : band.yMax;
            foreach (var gap in blocked)
            {
                Run(start, Mathf.Min(gap.from, end), add);
                start = Mathf.Max(start, gap.to);
            }
            Run(start, end, add);
        }

        if (area.width < 2f * depth + 3f || area.height < 2f * depth + 3f)
        {
            Row(area, area.width >= area.height);
            return lots;
        }

        Row(Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, area.yMin + depth), true);
        Row(Rect.MinMaxRect(area.xMin, area.yMax - depth, area.xMax, area.yMax), true);
        Row(Rect.MinMaxRect(area.xMin, area.yMin + depth, area.xMin + depth, area.yMax - depth), false);
        if (!skipEast)
        {
            Row(Rect.MinMaxRect(area.xMax - depth, area.yMin + depth, area.xMax, area.yMax - depth), false);
        }
        return lots;
    }

    // Feste Gebäude mit Spiel-Funktion als Prefab-Instanzen. Die Stadt-Szene ist vom PrefabGridEnforcer ausgenommen,
    // sonst würde er sie auf die Laufbahn des offenen Levels schieben
    private static void PlaceLandmarks(Transform parent)
    {
        foreach (var landmark in CityLayout.Landmarks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CityPropsSetup.PrefabFolder}/{landmark.prefab}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(landmark.position, Quaternion.Euler(0f, landmark.yaw, 0f));
            var serialized = new SerializedObject(instance.AddComponent<CityLandmark>());
            serialized.FindProperty("id").stringValue = landmark.prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Die Stadt muss in den Build Settings stehen, sonst kann LevelCity sie nicht per Name laden
    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(entry => entry.path == ScenePath))
        {
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Ein Haus = ein Würfel mit dem Fassaden-Shader (Fenster, Farbe, Dach kommen aus dem Shader)
    private static Renderer House(Transform parent, Rect lot, float height)
    {
        return PrimitiveKit.Box("House", parent, new Vector3(lot.center.x, Surface + height / 2f, lot.center.y), new Vector3(lot.width, height, lot.height), facade);
    }

    // ---------- Hilfen ----------

    private static Transform Group(string name)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(root, false);
        return group;
    }

    // Quader zwischen zwei Ecken (X/Z) und zwei Höhen
    private static Renderer Slab(string name, Transform parent, float x0, float z0, float x1, float z1, float bottom, float top, Material material)
    {
        var center = new Vector3((x0 + x1) / 2f, (bottom + top) / 2f, (z0 + z1) / 2f);
        var size = new Vector3(Mathf.Abs(x1 - x0), top - bottom, Mathf.Abs(z1 - z0));
        return PrimitiveKit.Box(name, parent, center, size, material);
    }

    // Quader mit rechteckigem Loch (X/Z): bis zu vier Teilstücke drumherum (West und Ost in voller Tiefe, dazwischen Süd und Nord)
    private static List<Renderer> SlabAround(string name, Transform parent, float x0, float z0, float x1, float z1, float bottom, float top,
        Material material, Rect hole)
    {
        var pieces = new List<Renderer>();
        void Piece(float a0, float b0, float a1, float b1)
        {
            if (a1 - a0 > 0.01f && b1 - b0 > 0.01f)
            {
                pieces.Add(Slab(name, parent, a0, b0, a1, b1, bottom, top, material));
            }
        }

        float hx0 = Mathf.Max(x0, hole.xMin), hx1 = Mathf.Min(x1, hole.xMax);
        float hz0 = Mathf.Max(z0, hole.yMin), hz1 = Mathf.Min(z1, hole.yMax);
        if (hx0 >= hx1 || hz0 >= hz1)
        {
            Piece(x0, z0, x1, z1);
            return pieces;
        }
        Piece(x0, z0, hx0, z1);
        Piece(hx1, z0, x1, z1);
        Piece(hx0, z0, hx1, hz0);
        Piece(hx0, hz1, hx1, z1);
        return pieces;
    }

    // Bereich vor der Strandpromenade, von südlich (fromZ) bis zur Steinkante: dort fehlen Fahrbahn, Wiese und der alte breite Strand
    private static Rect BeachHole(float fromZ) => Rect.MinMaxRect(CityLayout.BeachFromX, fromZ - Far, CityLayout.BeachToX, CityLayout.PromenadeFrontZ);

    private static float Pick(System.Random random, params float[] values) => values[random.Next(values.Length)];

    // Vorhandene Stadt-Materialien (aus CityPropsSetup) laden, neue mit der Standardfarbe anlegen
    private static Material Mat(string name) => Mat(name, Color.magenta);

    private static Material Mat(string name, Color color, float smoothness = 0.15f)
    {
        return PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);
    }

    private static Material Glow(string name, Color color)
    {
        return PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.UnlitShader, color);
    }

    private static Material FacadeMaterial()
    {
        string path = $"{MaterialFolder}/Facade.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("JumpAndRun/CityFacade"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
}
