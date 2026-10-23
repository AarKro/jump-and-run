using TMPro;
using UnityEditor;
using UnityEngine;

// Macht aus dem roten Enemy-Prefab den Hund: benennt Enemy.prefab in Dog.prefab um (GUID bleibt, alle Instanzen
// in den Levels werden automatisch zu Hunden), ersetzt die Kugel-Optik durch einen Hund aus Grundformen
// und hängt das Dog-Skript an. Physik (SphereCollider r = 0.5, kinematischer Rigidbody) bleibt unverändert.
// Menü: Tools > Jump and Run > Build Dog Prefab. Mehrfach ausführbar, vorhandene Optik wird ersetzt.
public static class DogSetup
{
    private const string OldPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string DogPrefabPath = "Assets/Prefabs/Dog.prefab";
    private const string DogMaterialPath = "Assets/Materials/Dog.mat";
    private const string EyeMaterialPath = "Assets/Materials/Dog Eye.mat";
    private const string BubbleMaterialPath = "Assets/Materials/Speech Bubble.mat";
    private const string StarMaterialPath = "Assets/Materials/Dizzy Star.mat";
    private const float ChargeSpeed = 6f;

    [MenuItem("Tools/Jump and Run/Build Dog Prefab")]
    public static void BuildDog()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath) == null)
        {
            // MoveAsset benennt um und behält die GUID, Referenzen in Szenen bleiben also gültig
            string error = AssetDatabase.MoveAsset(OldPrefabPath, DogPrefabPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("DogSetup: Umbenennen fehlgeschlagen: " + error);
                return;
            }
        }

        Material dogMaterial = GetOrCreateMaterial(DogMaterialPath, "Universal Render Pipeline/Lit", Color.white);
        Material eyeMaterial = GetOrCreateMaterial(EyeMaterialPath, "Universal Render Pipeline/Lit", new Color(0.05f, 0.05f, 0.05f));
        // Unlit: Sprechblase und Sternchen sollen unabhängig vom Licht gleich hell sein
        Material bubbleMaterial = GetOrCreateMaterial(BubbleMaterialPath, "Universal Render Pipeline/Unlit", Color.white);
        Material starMaterial = GetOrCreateMaterial(StarMaterialPath, "Universal Render Pipeline/Unlit", new Color(1f, 0.85f, 0.1f));

        GameObject root = PrefabUtility.LoadPrefabContents(DogPrefabPath);
        try
        {
            root.name = "Dog";
            foreach (string oldName in new[] { "Body", "Woof Bubble", "Dizzy Stars" })
            {
                Transform old = root.transform.Find(oldName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            // Die alte rote Kugel-Optik am Root entfernen, der SphereCollider bleibt
            // (Beim zweiten Durchlauf sind sie schon weg)
            if (root.TryGetComponent(out MeshRenderer oldRenderer))
            {
                Object.DestroyImmediate(oldRenderer);
            }
            if (root.TryGetComponent(out MeshFilter oldFilter))
            {
                Object.DestroyImmediate(oldFilter);
            }

            var parts = BuildBody(root.transform, dogMaterial, eyeMaterial);
            GameObject bubble = BuildWoofBubble(root.transform, bubbleMaterial);
            Transform stars = BuildDizzyStars(root.transform, starMaterial);
            // Im Prefab aus, sonst sieht man Blase und Sterne im Edit Mode dauerhaft
            bubble.SetActive(false);
            stars.gameObject.SetActive(false);

            Dog dog = root.GetComponent<Dog>();
            if (dog == null)
            {
                dog = root.AddComponent<Dog>();
            }

            var serializedDog = new SerializedObject(dog);
            serializedDog.FindProperty("body").objectReferenceValue = parts.body;
            serializedDog.FindProperty("tail").objectReferenceValue = parts.tail;
            SetArray(serializedDog.FindProperty("legs"), parts.legs);
            SetArray(serializedDog.FindProperty("furRenderers"), parts.fur);
            SetArray(serializedDog.FindProperty("earRenderers"), parts.ears);
            serializedDog.FindProperty("woofBubble").objectReferenceValue = bubble;
            serializedDog.FindProperty("dizzyStars").objectReferenceValue = stars;
            serializedDog.ApplyModifiedPropertiesWithoutUndo();

            // Nur die Optik spiegeln, und beim Losrennen 6 m/s (Spieler läuft 7, man kann also entkommen)
            var serializedPatrol = new SerializedObject(root.GetComponent<EnemyPlatformPatrol>());
            serializedPatrol.FindProperty("flipTarget").objectReferenceValue = parts.body;
            serializedPatrol.FindProperty("chaseSpeed").floatValue = ChargeSpeed;
            serializedPatrol.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DogPrefabPath);
            Debug.Log("DogSetup: Dog-Prefab gebaut.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private struct BodyParts
    {
        public Transform body;
        public Transform tail;
        public Transform[] legs;
        public Renderer[] fur;
        public Renderer[] ears;
    }

    // Der Hund schaut nach +X. Mitte = Mitte des SphereColliders (r = 0.5), die Pfoten enden bei y = -0.5
    private static BodyParts BuildBody(Transform root, Material dogMaterial, Material eyeMaterial)
    {
        Transform body = CreatePivot("Body", root, Vector3.zero);

        Renderer torso = CreatePart("Torso", PrimitiveType.Cube, body, new Vector3(0f, -0.05f, 0f), new Vector3(0.8f, 0.4f, 0.4f), dogMaterial);
        Renderer head = CreatePart("Head", PrimitiveType.Sphere, body, new Vector3(0.48f, 0.2f, 0f), Vector3.one * 0.38f, dogMaterial);
        Renderer snout = CreatePart("Snout", PrimitiveType.Cube, body, new Vector3(0.68f, 0.14f, 0f), new Vector3(0.2f, 0.14f, 0.2f), dogMaterial);
        Renderer nose = CreatePart("Nose", PrimitiveType.Sphere, body, new Vector3(0.79f, 0.17f, 0f), Vector3.one * 0.08f, eyeMaterial);
        // Augen auf beiden Seiten, das vordere (-Z) sieht die Kamera
        CreatePart("Eye Front", PrimitiveType.Sphere, body, new Vector3(0.6f, 0.27f, -0.15f), Vector3.one * 0.07f, eyeMaterial);
        CreatePart("Eye Back", PrimitiveType.Sphere, body, new Vector3(0.6f, 0.27f, 0.15f), Vector3.one * 0.07f, eyeMaterial);

        // Schlappohren seitlich am Kopf
        Renderer earFront = CreatePart("Ear Front", PrimitiveType.Cube, body, new Vector3(0.42f, 0.24f, -0.19f), new Vector3(0.12f, 0.22f, 0.04f), dogMaterial);
        Renderer earBack = CreatePart("Ear Back", PrimitiveType.Cube, body, new Vector3(0.42f, 0.24f, 0.19f), new Vector3(0.12f, 0.22f, 0.04f), dogMaterial);

        // Beine mit Drehpunkt an der Hüfte, damit sie vor und zurück schwingen
        var legs = new Transform[4];
        var legRenderers = new Renderer[4];
        Vector3[] hips =
        {
            new Vector3(0.28f, -0.2f, -0.12f), new Vector3(0.28f, -0.2f, 0.12f),
            new Vector3(-0.28f, -0.2f, 0.12f), new Vector3(-0.28f, -0.2f, -0.12f)
        };
        for (int i = 0; i < 4; i++)
        {
            legs[i] = CreatePivot("Leg Pivot " + (i + 1), body, hips[i]);
            legRenderers[i] = CreatePart("Leg", PrimitiveType.Cube, legs[i], new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.3f, 0.1f), dogMaterial);
        }

        // Schwanz schräg nach oben, wedelt um Z (in der Bildebene, damit man es von vorne sieht)
        Transform tail = CreatePivot("Tail Pivot", body, new Vector3(-0.4f, 0.05f, 0f));
        GameObject tailMesh = CreatePart("Tail", PrimitiveType.Capsule, tail, new Vector3(-0.08f, 0.1f, 0f), new Vector3(0.08f, 0.14f, 0.08f), dogMaterial).gameObject;
        tailMesh.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);

        return new BodyParts
        {
            body = body,
            tail = tail,
            legs = legs,
            fur = new[] { torso, head, snout, legRenderers[0], legRenderers[1], legRenderers[2], legRenderers[3], tailMesh.GetComponent<Renderer>() },
            ears = new[] { earFront, earBack }
        };
    }

    // Sprechblase am Root (nicht an Body), damit sie beim Umdrehen des Hundes nicht gespiegelt wird
    private static GameObject BuildWoofBubble(Transform root, Material material)
    {
        Transform bubble = CreatePivot("Woof Bubble", root, new Vector3(0f, 1.05f, -0.1f));
        // Quad zeigt mit der sichtbaren Seite nach -Z, also zur Kamera
        CreatePart("Background", PrimitiveType.Quad, bubble, Vector3.zero, new Vector3(1.1f, 0.45f, 1f), material);
        CreatePart("Pointer", PrimitiveType.Quad, bubble, new Vector3(0.15f, -0.25f, 0f), Vector3.one * 0.16f, material)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(bubble, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var text = textObject.AddComponent<TextMeshPro>();
        text.text = "WOOF!";
        text.color = new Color(0.15f, 0.1f, 0.05f);
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        // Auto-Size: Die Schrift passt sich der Blasengröße an, statt eine Weltgröße raten zu müssen
        text.rectTransform.sizeDelta = new Vector2(1f, 0.38f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.5f;
        text.fontSizeMax = 10f;

        return bubble.gameObject;
    }

    private static Transform BuildDizzyStars(Transform root, Material material)
    {
        Transform stars = CreatePivot("Dizzy Stars", root, new Vector3(0.45f, 0.6f, 0f));
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Cos(angle) * 0.28f, 0f, Mathf.Sin(angle) * 0.28f);
            CreatePart("Star " + (i + 1), PrimitiveType.Sphere, stars, position, Vector3.one * 0.1f, material);
        }
        return stars;
    }

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static Transform CreatePivot(string name, Transform parent, Vector3 localPosition)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        return pivot;
    }

    private static Renderer CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        // Kein Collider: Der SphereCollider am Root ist der einzige, sonst würde die Optik mit dem Spieler kollidieren
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
