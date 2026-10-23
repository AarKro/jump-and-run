using TMPro;
using UnityEditor;
using UnityEngine;

// Baut das Prefab "Seagull" (Möwe, Signatur-Gefahr von Level 2) aus Grundformen.
// Menü: Tools > Jump and Run > Build Seagull Prefab. Mehrfach ausführbar, das Prefab wird ersetzt.
// Aufbau: Wurzel mit Seagull, Trigger-Kugel und kinematischem Rigidbody (bewegte Trigger brauchen einen Rigidbody).
// Darunter "Model" (schaut in Flugrichtung, Schnabel = +Z), die Sprechblase "SKREE!" und der Schatten am Boden.
public static class SeagullSetup
{
    private const string PrefabPath = "Assets/Prefabs/Seagull.prefab";
    private const string MaterialFolder = "Assets/Materials";
    private const string BubbleMaterialPath = "Assets/Materials/Speech Bubble.mat";

    [MenuItem("Tools/Jump and Run/Build Seagull Prefab")]
    public static void Build()
    {
        Material white = PrimitiveKit.Material($"{MaterialFolder}/Gull White.mat", PrimitiveKit.LitShader, new Color(0.96f, 0.96f, 0.95f));
        Material gray = PrimitiveKit.Material($"{MaterialFolder}/Gull Gray.mat", PrimitiveKit.LitShader, new Color(0.62f, 0.66f, 0.7f));
        Material black = PrimitiveKit.Material($"{MaterialFolder}/Gull Black.mat", PrimitiveKit.LitShader, new Color(0.08f, 0.08f, 0.1f));
        Material beak = PrimitiveKit.Material($"{MaterialFolder}/Gull Beak.mat", PrimitiveKit.LitShader, new Color(0.98f, 0.62f, 0.12f));
        // Schatten: unbeleuchtet dunkel, damit er auch im Hausschatten auffällt
        Material shadowMaterial = PrimitiveKit.Material($"{MaterialFolder}/Gull Shadow.mat", PrimitiveKit.UnlitShader, new Color(0.12f, 0.12f, 0.14f));
        Material bubble = AssetDatabase.LoadAssetAtPath<Material>(BubbleMaterialPath);

        var root = new GameObject("Seagull");
        Transform t = root.transform;

        Transform model = PrimitiveKit.Pivot("Model", t, Vector3.zero);
        Renderer body = PrimitiveKit.Part("Body", PrimitiveType.Capsule, model, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), white);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, model, new Vector3(0f, 0.08f, 0.38f), Vector3.one * 0.26f, white);
        PrimitiveKit.Box("Beak", model, new Vector3(0f, 0.05f, 0.56f), new Vector3(0.06f, 0.05f, 0.18f), beak);
        PrimitiveKit.Part("Eye L", PrimitiveType.Sphere, model, new Vector3(-0.1f, 0.13f, 0.44f), Vector3.one * 0.05f, black);
        PrimitiveKit.Part("Eye R", PrimitiveType.Sphere, model, new Vector3(0.1f, 0.13f, 0.44f), Vector3.one * 0.05f, black);
        PrimitiveKit.Box("Tail", model, new Vector3(0f, 0.02f, -0.45f), new Vector3(0.22f, 0.04f, 0.22f), gray);
        Transform wingLeft = Wing("Wing L", model, -1f, gray, black);
        Transform wingRight = Wing("Wing R", model, 1f, gray, black);

        GameObject skree = BuildBubble(t, bubble);

        Transform shadow = PrimitiveKit.Pivot("Shadow", t, Vector3.zero);
        PrimitiveKit.Part("Disc", PrimitiveType.Cylinder, shadow, Vector3.zero, Vector3.one, shadowMaterial);
        shadow.gameObject.SetActive(false);

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.45f;
        trigger.enabled = false;
        var body3D = root.AddComponent<Rigidbody>();
        body3D.isKinematic = true;
        body3D.useGravity = false;

        var gull = root.AddComponent<Seagull>();
        var serialized = new SerializedObject(gull);
        serialized.FindProperty("model").objectReferenceValue = model;
        serialized.FindProperty("wingLeft").objectReferenceValue = wingLeft;
        serialized.FindProperty("wingRight").objectReferenceValue = wingRight;
        serialized.FindProperty("skreeBubble").objectReferenceValue = skree;
        serialized.FindProperty("shadow").objectReferenceValue = shadow;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"SeagullSetup: Prefab gespeichert unter {PrefabPath}.");
    }

    // Flügel hängt an einem Drehpunkt an der Schulter (Schlag = Drehung um die Längsachse Z), schwarze Spitze außen
    private static Transform Wing(string name, Transform model, float side, Material gray, Material black)
    {
        Transform pivot = PrimitiveKit.Pivot(name, model, new Vector3(side * 0.12f, 0.06f, 0.02f));
        PrimitiveKit.Box("Feathers", pivot, new Vector3(side * 0.32f, 0f, 0f), new Vector3(0.62f, 0.03f, 0.3f), gray);
        PrimitiveKit.Box("Tip", pivot, new Vector3(side * 0.7f, 0f, -0.03f), new Vector3(0.16f, 0.03f, 0.22f), black);
        return pivot;
    }

    // Wie die "WOOF!"-Blase des Hundes: Quad (sichtbare Seite -Z) mit TextMeshPro-Text
    private static GameObject BuildBubble(Transform root, Material material)
    {
        Transform bubble = PrimitiveKit.Pivot("Skree Bubble", root, new Vector3(0f, 0.75f, 0f));
        PrimitiveKit.Quad("Background", bubble, Vector3.zero, new Vector2(1.2f, 0.45f), material);
        PrimitiveKit.Quad("Pointer", bubble, new Vector3(-0.15f, -0.25f, 0f), Vector2.one * 0.16f, material)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        TextMeshPro text = PrimitiveKit.Text("Text", bubble, new Vector3(0f, 0f, -0.01f), new Vector2(1.1f, 0.38f), "SKREE!", new Color(0.15f, 0.1f, 0.05f));
        text.fontSizeMax = 10f;
        bubble.gameObject.SetActive(false);
        return bubble.gameObject;
    }
}
