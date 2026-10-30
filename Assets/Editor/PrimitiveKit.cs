using TMPro;
using UnityEditor;
using UnityEngine;

// Gemeinsame Bausteine für die Editor-Tools, die Prefabs aus Grundformen bauen (Stadt, Level).
// Alles ohne Collider, außer man fügt ihn ausdrücklich hinzu: Optik und Physik bleiben getrennt.
public static class PrimitiveKit
{
    public const string LitShader = "Universal Render Pipeline/Lit";
    public const string UnlitShader = "Universal Render Pipeline/Unlit";
    // Standard-Partikelmaterial von URP (Rauch, Funken, Dampf)
    public const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    public static Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        return pivot;
    }

    public static Renderer Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    public static Renderer Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        return Part(name, PrimitiveType.Cube, parent, center, size, material);
    }

    // Stehender Zylinder mit Mitte localPosition. Der Unity-Zylinder ist 2 m hoch und 1 m breit
    public static Renderer Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
    {
        return Part(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), material);
    }

    // Stange von a nach b (lokal): Zylinder in die Mitte, Länge = Abstand, gedreht von "oben" auf die Richtung a→b
    public static Renderer Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        Renderer rod = Cylinder(name, parent, (a + b) / 2f, radius, Vector3.Distance(a, b), material);
        rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        return rod;
    }

    // Quad zeigt mit der sichtbaren Seite nach -Z, also zur Kamera. Gut für Fenster und Schilder
    public static Renderer Quad(string name, Transform parent, Vector3 center, Vector2 size, Material material)
    {
        return Part(name, PrimitiveType.Quad, parent, center, new Vector3(size.x, size.y, 1f), material);
    }

    public static BoxCollider AddBoxCollider(GameObject target, Vector3 center, Vector3 size, bool isTrigger = false)
    {
        var box = target.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        box.isTrigger = isTrigger;
        return box;
    }

    // 3D-Text (TextMeshPro, nicht UI), lesbar von der Kamera aus. Auto-Size passt die Schrift ins Rechteck
    public static TextMeshPro Text(string name, Transform parent, Vector3 localPosition, Vector2 size, string content, Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        var text = textObject.AddComponent<TextMeshPro>();
        text.text = content;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.5f;
        text.fontSizeMax = 40f;
        return text;
    }

    // Vorhandenes Material laden, sonst neu anlegen. smoothness null = die Glätte bleibt beim Standardwert des Shaders
    public static Material Material(string path, string shaderName, Color color, float? smoothness = 0.2f)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find(shaderName));
        material.SetColor("_BaseColor", color);
        if (smoothness.HasValue && material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness.Value);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Halbdurchsichtiges URP-Lit-Material (Transparent-Modus, Alpha aus color.a), z. B. Bauzaun-Gitter oder nasser Boden.
    // Im Code muss man alles setzen, was der Inspector beim Umschalten auf "Transparent" sonst selbst erledigt.
    // smoothness null = Standardwert des Shaders
    public static Material TransparentMaterial(string path, Color color, float? smoothness = null)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        material = new Material(Shader.Find(LitShader));
        material.SetColor("_BaseColor", color);
        if (smoothness.HasValue)
        {
            material.SetFloat("_Smoothness", smoothness.Value);
        }
        material.SetFloat("_Surface", 1f);   // Transparent
        material.SetFloat("_Blend", 0f);     // Alpha
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public static void SetLayerRecursive(GameObject target, int layer)
    {
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    public static void SetReferences(SerializedObject serialized, string property, params Object[] values)
    {
        SerializedProperty array = serialized.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    // Speichert ein frisch gebautes Objekt als Prefab und räumt es aus der Szene. Vorhandene Prefabs werden ersetzt
    // (gleiche GUID, Instanzen in den Levels bleiben verbunden)
    public static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
