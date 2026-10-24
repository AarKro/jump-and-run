using UnityEditor;
using UnityEngine;

// Baut das Modell des Pizzaboten ins Player-Prefab: so groß wie der Kunde (~1.6 m). Uniform in den Farben der Pizzeria:
// rotes Polo mit weißem Brustlogo, rote Kappe mit weißem Schirm, dunkle Hose, weiße Turnschuhe.
// Hitbox: Kapsel statt der alten Kugel, die nach oben wächst. Die Füße bleiben bei y = -0.5 (wie die Unterkante der Kugel),
// dadurch stimmen Bodenprüfung, Spawn-Höhen und Stomps unverändert.
// Das Modell schaut bei Yaw 0 zur Kamera (-Z), wie der Kunde. DeliveryGuy dreht und animiert es zur Laufzeit.
// Der Karton hängt am "Carton Hold"-Punkt vor dem Bauch (PizzaCarton.holdPoint), die Arme halten ihn von unten.
// Menü: Tools > Jump and Run > Build Player Model. Mehrfach ausführbar, das alte Modell wird ersetzt.
public static class PlayerModelSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string MaterialFolder = "Assets/Materials/Player";

    [MenuItem("Tools/Jump and Run/Build Player Model")]
    public static void Build()
    {
        PrimitiveKit.EnsureFolder("Assets/Materials", "Player");
        // Gleiches Rot wie das Pizzeria-Schild: Er gehört sichtbar zum Laden
        Material red = Mat("Uniform Red", new Color(0.8f, 0.13f, 0.1f), 0.3f);
        Material white = Mat("Uniform White", new Color(0.97f, 0.97f, 0.95f), 0.3f);
        Material pants = Mat("Pants", new Color(0.16f, 0.18f, 0.27f), 0.2f);
        Material skin = Mat("Skin", new Color(0.96f, 0.78f, 0.62f), 0.3f);
        Material hair = Mat("Hair", new Color(0.3f, 0.18f, 0.1f), 0.2f);
        Material eyes = Mat("Eyes", new Color(0.08f, 0.08f, 0.1f), 0.8f);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // Die alte Platzhalter-Kugel (Mesh direkt an der Wurzel) fliegt raus, der Collider bleibt
            if (root.TryGetComponent(out MeshRenderer sphere))
            {
                Object.DestroyImmediate(sphere);
            }
            if (root.TryGetComponent(out MeshFilter sphereMesh))
            {
                Object.DestroyImmediate(sphereMesh);
            }
            // Kapsel: 1.6 m hoch, Radius 0.5 wie die Kugel, von y -0.5 (Füße) bis 1.1 (Kappe)
            if (root.TryGetComponent(out SphereCollider sphereCollider))
            {
                Object.DestroyImmediate(sphereCollider);
            }
            if (!root.TryGetComponent(out CapsuleCollider capsule))
            {
                capsule = root.AddComponent<CapsuleCollider>();
            }
            capsule.direction = 1;
            capsule.radius = 0.5f;
            capsule.height = 1.6f;
            capsule.center = new Vector3(0f, 0.3f, 0f);

            Transform old = root.transform.Find("Model");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Transform model = PrimitiveKit.Pivot("Model", root.transform, Vector3.zero);
            Transform rig = PrimitiveKit.Pivot("Rig", model, Vector3.zero);

            // Beine: Drehpunkt an der Hüfte, Hosenbein darunter, Schuh ganz unten (Sohle bei y = -0.5)
            Transform legLeft = Leg("Leg Pivot L", rig, -0.12f, pants, white);
            Transform legRight = Leg("Leg Pivot R", rig, 0.12f, pants, white);

            PrimitiveKit.Part("Hips", PrimitiveType.Sphere, rig, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.2f, 0.36f), pants);
            // Unity-Kapsel: Skalierung (0.5, 0.3, 0.4) = 0.6 m hoch, vorne/hinten etwas flacher als breit
            PrimitiveKit.Part("Torso", PrimitiveType.Capsule, rig, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.3f, 0.4f), red);
            PrimitiveKit.Box("Chest Logo", rig, new Vector3(0.1f, 0.4f, -0.2f), new Vector3(0.13f, 0.1f, 0.02f), white);
            PrimitiveKit.Part("Collar", PrimitiveType.Cylinder, rig, new Vector3(0f, 0.6f, 0f), new Vector3(0.26f, 0.02f, 0.24f), white);

            // Kopf: Haare hinten und an den Seiten, Augen und Nase vorne (-Z) zeigen die Blickrichtung
            PrimitiveKit.Part("Head", PrimitiveType.Sphere, rig, new Vector3(0f, 0.84f, 0f), Vector3.one * 0.44f, skin);
            PrimitiveKit.Part("Hair", PrimitiveType.Sphere, rig, new Vector3(0f, 0.86f, 0.04f), new Vector3(0.45f, 0.4f, 0.44f), hair);
            PrimitiveKit.Part("Eye L", PrimitiveType.Sphere, rig, new Vector3(-0.08f, 0.86f, -0.2f), new Vector3(0.05f, 0.07f, 0.03f), eyes);
            PrimitiveKit.Part("Eye R", PrimitiveType.Sphere, rig, new Vector3(0.08f, 0.86f, -0.2f), new Vector3(0.05f, 0.07f, 0.03f), eyes);
            PrimitiveKit.Part("Nose", PrimitiveType.Sphere, rig, new Vector3(0f, 0.81f, -0.22f), Vector3.one * 0.06f, skin);

            // Kappe: abgeflachte Kugel über dem Kopf, weißer Schirm nach vorne (steckt ein Stück in der Kappe, sonst
            // sieht man eine Lücke), Knopf oben
            PrimitiveKit.Part("Cap", PrimitiveType.Sphere, rig, new Vector3(0f, 0.97f, 0f), new Vector3(0.47f, 0.3f, 0.47f), red);
            PrimitiveKit.Part("Cap Brim", PrimitiveType.Cylinder, rig, new Vector3(0f, 0.94f, -0.17f), new Vector3(0.3f, 0.012f, 0.24f), white);
            PrimitiveKit.Part("Cap Button", PrimitiveType.Sphere, rig, new Vector3(0f, 1.12f, 0f), Vector3.one * 0.05f, white);

            // Arme: Drehpunkt an der Schulter, roter Ärmel, Unterarm und Hand
            Transform armLeft = Arm("Arm Pivot L", rig, -0.3f, red, skin);
            Transform armRight = Arm("Arm Pivot R", rig, 0.3f, red, skin);

            // Haltepunkt des Kartons: vor dem Bauch, die Hände (bei 70° nach vorne, ~0.4 m vor dem Körper) liegen unter
            // seiner vorderen Kante. Hängt am Rig, wippt also beim Laufen mit
            Transform hold = PrimitiveKit.Pivot("Carton Hold", rig, new Vector3(0f, 0.48f, -0.6f));

            PizzaCarton carton = root.GetComponentInChildren<PizzaCarton>(true);
            var cartonSettings = new SerializedObject(carton);
            cartonSettings.FindProperty("holdPoint").objectReferenceValue = hold;
            cartonSettings.ApplyModifiedPropertiesWithoutUndo();

            DeliveryGuy guy = root.GetComponent<DeliveryGuy>();
            if (guy == null)
            {
                guy = root.AddComponent<DeliveryGuy>();
            }
            var serialized = new SerializedObject(guy);
            serialized.FindProperty("model").objectReferenceValue = model;
            serialized.FindProperty("rig").objectReferenceValue = rig;
            serialized.FindProperty("legLeft").objectReferenceValue = legLeft;
            serialized.FindProperty("legRight").objectReferenceValue = legRight;
            serialized.FindProperty("armLeft").objectReferenceValue = armLeft;
            serialized.FindProperty("armRight").objectReferenceValue = armRight;
            serialized.FindProperty("carton").objectReferenceValue = carton;
            // Ganz ins Profil drehen (90°), nicht nur zu drei Vierteln
            serialized.FindProperty("facingYaw").floatValue = 90f;
            // Schrittlänge und Wippen passend zu den 0.5 m langen Beinen
            serialized.FindProperty("strideLength").floatValue = 1.8f;
            serialized.FindProperty("bobHeight").floatValue = 0.06f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("PlayerModelSetup: Pizzabote ins Player-Prefab gebaut.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform Leg(string name, Transform parent, float x, Material pants, Material shoe)
    {
        Transform pivot = PrimitiveKit.Pivot(name, parent, new Vector3(x, 0f, 0f));
        PrimitiveKit.Part("Pant Leg", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.22f, 0.16f), pants);
        PrimitiveKit.Box("Shoe", pivot, new Vector3(0f, -0.46f, -0.04f), new Vector3(0.15f, 0.08f, 0.26f), shoe);
        return pivot;
    }

    private static Transform Arm(string name, Transform parent, float x, Material sleeve, Material skin)
    {
        Transform pivot = PrimitiveKit.Pivot(name, parent, new Vector3(x, 0.52f, 0f));
        PrimitiveKit.Part("Sleeve", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.09f, 0f), new Vector3(0.15f, 0.11f, 0.15f), sleeve);
        PrimitiveKit.Part("Forearm", PrimitiveType.Capsule, pivot, new Vector3(0f, -0.27f, 0f), new Vector3(0.12f, 0.13f, 0.12f), skin);
        PrimitiveKit.Part("Hand", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.43f, 0f), Vector3.one * 0.13f, skin);
        return pivot;
    }

    private static Material Mat(string name, Color color, float smoothness)
    {
        return PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);
    }
}
