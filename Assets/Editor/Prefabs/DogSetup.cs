using TMPro;
using UnityEditor;
using UnityEngine;

// Macht aus dem roten Enemy-Prefab den Hund: benennt Enemy.prefab in Dog.prefab um (GUID bleibt, alle Instanzen
// in den Levels werden automatisch zu Hunden), ersetzt die Kugel-Optik durch einen Hund aus Grundformen
// und hängt Dog + GroundPatrol an. Physik: BoxCollider in Körpergröße, kinematischer Rigidbody.
// Menü: Tools > Jump and Run > Build Dog Prefab. Mehrfach ausführbar, vorhandene Optik wird ersetzt.
public static class DogSetup
{
    private const string OldPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string DogPrefabPath = "Assets/Prefabs/Dog.prefab";
    private const string DogMaterialPath = "Assets/Materials/Dog.mat";
    private const string EyeMaterialPath = "Assets/Materials/Dog Eye.mat";
    private const string BubbleMaterialPath = "Assets/Materials/Speech Bubble.mat";
    private const string StarMaterialPath = "Assets/Materials/Dizzy Star.mat";
    private const float ChargeSpeed = 5f;
    private const float PatrolSpeed = 1.7f;
    // Das Modell (Rumpf -0.4 bis Nasenspitze 0.87) wird um diesen Wert nach hinten verschoben, damit es mittig
    // um die Wurzel liegt. Sonst passt beim Umdrehen (Spiegeln um die Wurzel) die Box nicht mehr zur Optik
    private const float ModelCenterOffset = 0.23f;
    private static readonly Vector3 ColliderCenter = new Vector3(0f, -0.055f, 0f);
    private static readonly Vector3 ColliderSize = new Vector3(1.25f, 0.89f, 0.45f);

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

        Material dogMaterial = PrimitiveKit.Material(DogMaterialPath, PrimitiveKit.LitShader, Color.white, smoothness: null);
        Material eyeMaterial = PrimitiveKit.Material(EyeMaterialPath, PrimitiveKit.LitShader, new Color(0.05f, 0.05f, 0.05f), smoothness: null);
        // Unlit: Sprechblase und Sternchen sollen unabhängig vom Licht gleich hell sein
        Material bubbleMaterial = PrimitiveKit.Material(BubbleMaterialPath, PrimitiveKit.UnlitShader, Color.white, smoothness: null);
        Material starMaterial = PrimitiveKit.Material(StarMaterialPath, PrimitiveKit.UnlitShader, new Color(1f, 0.85f, 0.1f), smoothness: null);

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

            // Kugel-Collider durch eine Box in Körpergröße ersetzen
            if (root.TryGetComponent(out SphereCollider oldSphere))
            {
                Object.DestroyImmediate(oldSphere);
            }
            if (!root.TryGetComponent(out BoxCollider box))
            {
                box = root.AddComponent<BoxCollider>();
            }
            box.center = ColliderCenter;
            box.size = ColliderSize;

            // Keine Constraints: Bewegt wird der kinematische Rigidbody nur von GroundPatrol (MovePosition/MoveRotation).
            // Das alte Enemy-Prefab hatte noch "Freeze Position Z + Rotation" aus der 2D-Zeit. Nach der Kurve läuft die
            // Bahn entlang Z, dort hat das den Hund festgehalten
            if (root.TryGetComponent(out Rigidbody body))
            {
                body.constraints = RigidbodyConstraints.None;
            }

            // Komponenten gelöschter Skripte entfernen (z.B. die alte Wegpunkt-Patrouille)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if (!root.TryGetComponent(out GroundPatrol patrol))
            {
                patrol = root.AddComponent<GroundPatrol>();
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
            PrimitiveKit.SetReferences(serializedDog, "legs", parts.legs);
            PrimitiveKit.SetReferences(serializedDog, "furRenderers", parts.fur);
            PrimitiveKit.SetReferences(serializedDog, "earRenderers", parts.ears);
            serializedDog.FindProperty("woofBubble").objectReferenceValue = bubble;
            serializedDog.FindProperty("dizzyStars").objectReferenceValue = stars;
            serializedDog.ApplyModifiedPropertiesWithoutUndo();

            // Nur die Optik spiegeln. Beim Losrennen 5 m/s: deutlich langsamer als der Spieler (7), sonst läuft er beim
            // Drüberspringen unter einem durch und man landet fast immer auf seinem Kopf
            var serializedPatrol = new SerializedObject(patrol);
            serializedPatrol.FindProperty("flipTarget").objectReferenceValue = parts.body;
            serializedPatrol.FindProperty("chaseSpeed").floatValue = ChargeSpeed;
            serializedPatrol.FindProperty("patrolSpeed").floatValue = PatrolSpeed;
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
        Transform body = PrimitiveKit.Pivot("Body", root, Vector3.zero);
        // Body wird gespiegelt, Model darin nur verschoben, damit der Hund mittig um die Wurzel liegt
        Transform model = PrimitiveKit.Pivot("Model", body, new Vector3(-ModelCenterOffset, 0f, 0f));

        Renderer torso = PrimitiveKit.Part("Torso", PrimitiveType.Cube, model, new Vector3(0f, -0.05f, 0f), new Vector3(0.8f, 0.4f, 0.4f), dogMaterial);
        Renderer head = PrimitiveKit.Part("Head", PrimitiveType.Sphere, model, new Vector3(0.48f, 0.2f, 0f), Vector3.one * 0.38f, dogMaterial);
        Renderer snout = PrimitiveKit.Part("Snout", PrimitiveType.Cube, model, new Vector3(0.68f, 0.14f, 0f), new Vector3(0.2f, 0.14f, 0.2f), dogMaterial);
        Renderer nose = PrimitiveKit.Part("Nose", PrimitiveType.Sphere, model, new Vector3(0.79f, 0.17f, 0f), Vector3.one * 0.08f, eyeMaterial);
        // Augen auf beiden Seiten, das vordere (-Z) sieht die Kamera
        PrimitiveKit.Part("Eye Front", PrimitiveType.Sphere, model, new Vector3(0.6f, 0.27f, -0.15f), Vector3.one * 0.07f, eyeMaterial);
        PrimitiveKit.Part("Eye Back", PrimitiveType.Sphere, model, new Vector3(0.6f, 0.27f, 0.15f), Vector3.one * 0.07f, eyeMaterial);

        // Schlappohren seitlich am Kopf
        Renderer earFront = PrimitiveKit.Part("Ear Front", PrimitiveType.Cube, model, new Vector3(0.42f, 0.24f, -0.19f), new Vector3(0.12f, 0.22f, 0.04f), dogMaterial);
        Renderer earBack = PrimitiveKit.Part("Ear Back", PrimitiveType.Cube, model, new Vector3(0.42f, 0.24f, 0.19f), new Vector3(0.12f, 0.22f, 0.04f), dogMaterial);

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
            legs[i] = PrimitiveKit.Pivot("Leg Pivot " + (i + 1), model, hips[i]);
            legRenderers[i] = PrimitiveKit.Part("Leg", PrimitiveType.Cube, legs[i], new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.3f, 0.1f), dogMaterial);
        }

        // Schwanz schräg nach oben, wedelt um Z (in der Bildebene, damit man es von vorne sieht)
        Transform tail = PrimitiveKit.Pivot("Tail Pivot", model, new Vector3(-0.4f, 0.05f, 0f));
        GameObject tailMesh = PrimitiveKit.Part("Tail", PrimitiveType.Capsule, tail, new Vector3(-0.08f, 0.1f, 0f), new Vector3(0.08f, 0.14f, 0.08f), dogMaterial).gameObject;
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
        Transform bubble = PrimitiveKit.Pivot("Woof Bubble", root, new Vector3(0f, 1.05f, -0.1f));
        // Quad zeigt mit der sichtbaren Seite nach -Z, also zur Kamera
        PrimitiveKit.Part("Background", PrimitiveType.Quad, bubble, Vector3.zero, new Vector3(1.1f, 0.45f, 1f), material);
        PrimitiveKit.Part("Pointer", PrimitiveType.Quad, bubble, new Vector3(0.15f, -0.25f, 0f), Vector3.one * 0.16f, material)
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
        // Dreht sich zur Kamera, auch wenn die von der anderen Seite der Bahn schaut
        bubble.gameObject.AddComponent<FaceCamera>();

        return bubble.gameObject;
    }

    private static Transform BuildDizzyStars(Transform root, Material material)
    {
        // Über der Körpermitte: Die Sterne hängen am ungespiegelten Root, der Kopf wechselt beim Umdrehen die Seite
        Transform stars = PrimitiveKit.Pivot("Dizzy Stars", root, new Vector3(0f, 0.6f, 0f));
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Cos(angle) * 0.28f, 0f, Mathf.Sin(angle) * 0.28f);
            PrimitiveKit.Part("Star " + (i + 1), PrimitiveType.Sphere, stars, position, Vector3.one * 0.1f, material);
        }
        return stars;
    }
}
