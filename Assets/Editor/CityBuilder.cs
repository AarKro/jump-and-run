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
        bool existed = File.Exists(ScenePath);
        Scene scene = existed
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

        SetupLighting(root);
        BuildGround();
        BuildStreets();
        BuildBlocks();
        BuildFoundersCircle(Group("Landmarks"));
        BuildKaiju(Group("Kaiju"));
        BuildSurroundings();

        EditorSceneManager.SaveScene(scene, ScenePath);
        if (previousActive.IsValid() && previousActive != scene)
        {
            SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"CityBuilder: Stadt gebaut ({ScenePath}).");
    }

    // ---------- Licht, Himmel, Nebel ----------

    private static void SetupLighting(Transform parent)
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

        RenderSettings.sun = sun;
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
        Slab("Road Base", ground, W - 12f, -12f, E + 12f, N + 12f, -3f, RoadTop, Mat("Road"));
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
            if (street.Vertical)
            {
                Slab(street.Name + " Line", streets, street.At - 0.075f, street.From, street.At + 0.075f, street.To, RoadTop, y, line);
            }
            else
            {
                Slab(street.Name + " Line", streets, street.From, street.At - 0.075f, street.To, street.At + 0.075f, RoadTop, y, line);
            }
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
            GameObject slab = Slab("Sidewalk", parent, walk.xMin, walk.yMin, walk.xMax, walk.yMax, Surface - 1f, Surface, sidewalk).gameObject;
            slab.layer = GroundLayer;
            slab.AddComponent<BoxCollider>();

            var random = new System.Random(1000 + i * 77);
            switch (block.Kind)
            {
                case CityLayout.BlockKind.Houses:
                    foreach (Rect lot in Lots(block.Lots, random, false))
                    {
                        House(parent, lot, Pick(random, 8f, 10f, 11f, 13f, 14f, 18f));
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
    // Ist der Block zu schmal für zwei Reihen, wird er mit einer Reihe aufgefüllt. skipEast: Ostseite frei lassen (Piazza)
    private static List<Rect> Lots(Rect area, System.Random random, bool skipEast)
    {
        const float depth = 8f;
        var lots = new List<Rect>();

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

        if (area.width < 2f * depth + 3f || area.height < 2f * depth + 3f)
        {
            if (area.width >= area.height)
            {
                Run(area.xMin, area.xMax, (a, b) => lots.Add(Rect.MinMaxRect(a, area.yMin, b, area.yMax)));
            }
            else
            {
                Run(area.yMin, area.yMax, (a, b) => lots.Add(Rect.MinMaxRect(area.xMin, a, area.xMax, b)));
            }
            return lots;
        }

        Run(area.xMin, area.xMax, (a, b) => lots.Add(Rect.MinMaxRect(a, area.yMin, b, area.yMin + depth)));
        Run(area.xMin, area.xMax, (a, b) => lots.Add(Rect.MinMaxRect(a, area.yMax - depth, b, area.yMax)));
        Run(area.yMin + depth, area.yMax - depth, (a, b) => lots.Add(Rect.MinMaxRect(area.xMin, a, area.xMin + depth, b)));
        if (!skipEast)
        {
            Run(area.yMin + depth, area.yMax - depth, (a, b) => lots.Add(Rect.MinMaxRect(area.xMax - depth, a, area.xMax, b)));
        }
        return lots;
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
