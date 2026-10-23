using UnityEditor;
using UnityEngine;

// Baut das Prefab "Street Crossing": Querstraße mit 2 Spuren als Auflage auf eine Plattform.
// Kein eigener Boden-Collider, die Plattform darunter bleibt der Boden. Der Asphalt läuft weit nach hinten (+Z)
// und vorne (-Z), damit man die Autos von weit hinten kommen sieht.
// Menü: Tools > Jump and Run > Build Street Crossing Prefab. Mehrfach ausführbar, das Prefab wird ersetzt.
public static class StreetSetup
{
    private const string PrefabPath = "Assets/Prefabs/Street Crossing.prefab";
    private const string AsphaltMaterialPath = "Assets/Materials/Asphalt.mat";
    private const string MarkingMaterialPath = "Assets/Materials/Road Marking.mat";
    private const string CarMaterialPath = "Assets/Materials/Car.mat";
    private const string WheelMaterialPath = "Assets/Materials/Wheel.mat";
    private const string HeadlightMaterialPath = "Assets/Materials/Headlight.mat";

    private const float LaneWidth = 3f;
    private const float RoadStartZ = -16f;
    private const float RoadEndZ = 42f;
    // Wurzel = Mitte einer 1 m dicken Plattform, wie bei den Plattform-Prefabs. Der PrefabGridEnforcer rastet
    // Prefab-Wurzeln auf ganze Meter ein; die Plattformmitten liegen auf ganzen Metern, ihre Oberkanten 0.5 darüber
    private const float SurfaceHeight = 0.5f;

    [MenuItem("Tools/Jump and Run/Build Street Crossing Prefab")]
    public static void BuildStreet()
    {
        Material asphalt = GetOrCreateMaterial(AsphaltMaterialPath, "Universal Render Pipeline/Lit", new Color(0.18f, 0.18f, 0.2f));
        Material marking = GetOrCreateMaterial(MarkingMaterialPath, "Universal Render Pipeline/Lit", new Color(0.95f, 0.95f, 0.95f));
        Material paint = GetOrCreateMaterial(CarMaterialPath, "Universal Render Pipeline/Lit", Color.white);
        Material wheel = GetOrCreateMaterial(WheelMaterialPath, "Universal Render Pipeline/Lit", new Color(0.08f, 0.08f, 0.08f));
        // Unlit: Scheinwerfer leuchten unabhängig vom Licht, so fallen entgegenkommende Autos früh auf
        Material headlight = GetOrCreateMaterial(HeadlightMaterialPath, "Universal Render Pipeline/Unlit", new Color(1f, 0.95f, 0.6f));

        var root = new GameObject("Street Crossing");
        try
        {
            BuildRoad(root.transform, asphalt, marking);

            // Zwei Spuren nebeneinander in X, beide fahren von hinten nach vorne, die zweite um ein halbes Intervall versetzt
            BuildLane("Lane 1", root.transform, -LaneWidth / 2f, 0f, paint, wheel, headlight);
            BuildLane("Lane 2", root.transform, LaneWidth / 2f, 1.25f, paint, wheel, headlight);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"StreetSetup: Prefab gespeichert unter {PrefabPath}.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildRoad(Transform root, Material asphalt, Material marking)
    {
        Transform road = new GameObject("Road").transform;
        road.SetParent(root, false);
        road.localPosition = new Vector3(0f, SurfaceHeight, 0f);
        float length = RoadEndZ - RoadStartZ;
        float centerZ = (RoadStartZ + RoadEndZ) / 2f;
        float width = LaneWidth * 2f + 0.4f;

        // Knapp über der Plattform, sonst flackern die Flächen gegeneinander (Z-Fighting)
        CreatePart("Asphalt", PrimitiveType.Cube, road, new Vector3(0f, 0.01f, centerZ), new Vector3(width, 0.02f, length), asphalt);
        CreatePart("Center Line", PrimitiveType.Cube, road, new Vector3(0f, 0.022f, centerZ), new Vector3(0.12f, 0.005f, length), marking);

        // Zebrastreifen über die Laufbahn des Spielers (Z = 0): Balken in Fahrtrichtung, nebeneinander in X
        Transform zebra = new GameObject("Zebra Crossing").transform;
        zebra.SetParent(road, false);
        int stripes = 8;
        for (int i = 0; i < stripes; i++)
        {
            float x = -width / 2f + 0.5f + i * ((width - 1f) / (stripes - 1));
            CreatePart("Stripe " + (i + 1), PrimitiveType.Cube, zebra, new Vector3(x, 0.024f, 0f), new Vector3(0.4f, 0.005f, 2.4f), marking);
        }
    }

    private static void BuildLane(string name, Transform root, float x, float startOffset, Material paint, Material wheel, Material headlight)
    {
        Transform lane = new GameObject(name).transform;
        lane.SetParent(root, false);
        lane.localPosition = new Vector3(x, SurfaceHeight, 0f);

        Car template = BuildCar(lane, paint, wheel, headlight);
        template.name = "Car Template";
        template.gameObject.SetActive(false);

        var trafficLane = lane.gameObject.AddComponent<TrafficLane>();
        var serialized = new SerializedObject(trafficLane);
        serialized.FindProperty("carTemplate").objectReferenceValue = template;
        serialized.FindProperty("startOffset").floatValue = startOffset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Auto fährt Richtung -Z (zur Kamera), vorne ist also -Z. Unterkante der Reifen auf y = 0
    private static Car BuildCar(Transform parent, Material paint, Material wheel, Material headlight)
    {
        var carObject = new GameObject("Car");
        carObject.transform.SetParent(parent, false);

        Renderer body = CreatePart("Body", PrimitiveType.Cube, carObject.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.7f, 4f), paint);
        Renderer cabin = CreatePart("Cabin", PrimitiveType.Cube, carObject.transform, new Vector3(0f, 1.2f, 0.3f), new Vector3(1.6f, 0.55f, 2f), paint);

        foreach (float wheelX in new[] { -0.9f, 0.9f })
        {
            foreach (float wheelZ in new[] { -1.3f, 1.3f })
            {
                // Zylinder steht hochkant (Achse Y), um Z gekippt zeigt die Achse nach X wie bei einem Rad
                CreatePart("Wheel", PrimitiveType.Cylinder, carObject.transform, new Vector3(wheelX, 0.3f, wheelZ), new Vector3(0.6f, 0.12f, 0.6f), wheel)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        CreatePart("Headlight L", PrimitiveType.Cube, carObject.transform, new Vector3(-0.6f, 0.65f, -2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);
        CreatePart("Headlight R", PrimitiveType.Cube, carObject.transform, new Vector3(0.6f, 0.65f, -2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);

        // Ein Trigger für das ganze Auto. Bewegte Collider brauchen einen (kinematischen) Rigidbody,
        // sonst behandelt die Physik sie als statisch und muss bei jeder Bewegung neu rechnen
        var trigger = carObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.8f, 0f);
        trigger.size = new Vector3(1.8f, 1.4f, 4f);

        var rigidbody = carObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        var car = carObject.AddComponent<Car>();
        var serialized = new SerializedObject(car);
        SerializedProperty renderers = serialized.FindProperty("paintRenderers");
        renderers.arraySize = 2;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = body;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = cabin;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return car;
    }

    private static Renderer CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        // Nur Optik: Die Plattform ist der Boden, und am Auto zählt nur der eine Trigger
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static Material GetOrCreateMaterial(string path, string shaderName, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find(shaderName));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
