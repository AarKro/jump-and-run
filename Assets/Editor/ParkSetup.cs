using TMPro;
using UnityEditor;
using UnityEngine;

// Prefabs für den Pesto Park (Level 3), aus Grundformen im Lane-Rahmen (lokal +X = Laufrichtung, -Z = Kameraseite).
// Wurzel wie bei allen Prefabs auf der Mitte einer 1 m dicken Platte, Boden also bei +0.5 (die Parkwege liegen 4 cm höher,
// das sieht man nicht). Menü: Tools > Jump and Run > City > Build Park Prefabs.
//  - "Frisbee Players": zwei Leute 8 m auseinander auf der Bahn (feste Hindernisse, man kann auf ihnen stehen, "HEY!"),
//    dazwischen die Frisbee (Trigger mit KillZone), gesteuert von FrisbeePlayers
public static class ParkSetup
{
    private const int GroundLayer = 3;
    private const float Surface = 0.5f;
    private const string MaterialFolder = "Assets/Materials/City";
    public const string PrefabFolder = "Assets/Prefabs/Park";

    private static Material figure, frisbeeRed, bubbleWhite;

    [MenuItem("Tools/Jump and Run/City/Build Park Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Park");
        CreateMaterials();
        BuildFrisbeePlayers();
        AssetDatabase.SaveAssets();
        Debug.Log("ParkSetup: Park-Prefabs gespeichert.");
    }

    private static void CreateMaterials()
    {
        // Weiß, die Farben kommen per MaterialPropertyBlock (wie beim Kunden)
        figure = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Customer.mat");
        frisbeeRed = PrimitiveKit.Material($"{MaterialFolder}/Frisbee.mat", PrimitiveKit.LitShader, new Color(0.95f, 0.2f, 0.15f), 0.6f);
        bubbleWhite = PrimitiveKit.Material($"{MaterialFolder}/Sign White.mat", PrimitiveKit.LitShader, new Color(0.97f, 0.96f, 0.92f));
    }

    // ---------- Frisbee ----------

    private static void BuildFrisbeePlayers()
    {
        var root = new GameObject("Frisbee Players");
        Transform t = root.transform;
        const float apart = 4f;

        // Beide schauen schräg zueinander und zur Kamera (die Figur schaut bei Drehung 0 nach -Z, zur Kamera).
        // Geworfen wird mit dem Arm auf der Kameraseite: bei A der linke, bei B der rechte
        Figure a = BuildFigure(t, "Player A", new Vector3(-apart, Surface, 0f), -45f);
        Figure b = BuildFigure(t, "Player B", new Vector3(apart, Surface, 0f), 45f);
        Hitbox(t, "Hitbox A", -apart);
        Hitbox(t, "Hitbox B", apart);

        var frisbeeObject = new GameObject("Frisbee");
        frisbeeObject.transform.SetParent(t, false);
        frisbeeObject.transform.localPosition = new Vector3(-3.5f, 1.7f, 0f);
        var body = frisbeeObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var trigger = frisbeeObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.3f;
        frisbeeObject.AddComponent<KillZone>();
        Transform disc = PrimitiveKit.Pivot("Disc", frisbeeObject.transform, Vector3.zero);
        PrimitiveKit.Part("Plate", PrimitiveType.Cylinder, disc, Vector3.zero, new Vector3(0.5f, 0.025f, 0.5f), frisbeeRed);
        PrimitiveKit.Part("Rim", PrimitiveType.Cylinder, disc, new Vector3(0f, -0.02f, 0f), new Vector3(0.52f, 0.02f, 0.52f), bubbleWhite);

        var serialized = new SerializedObject(root.AddComponent<FrisbeePlayers>());
        serialized.FindProperty("frisbee").objectReferenceValue = body;
        serialized.FindProperty("disc").objectReferenceValue = disc;
        serialized.FindProperty("armA").objectReferenceValue = a.leftArm;
        serialized.FindProperty("armB").objectReferenceValue = b.rightArm;
        PrimitiveKit.SetReferences(serialized, "shirtRenderersA", a.body);
        PrimitiveKit.SetReferences(serialized, "shirtRenderersB", b.body);
        PrimitiveKit.SetReferences(serialized, "skinRenderersA", a.head, a.leftArmMesh, a.rightArmMesh);
        PrimitiveKit.SetReferences(serialized, "skinRenderersB", b.head, b.leftArmMesh, b.rightArmMesh);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Frisbee Players.prefab");
    }

    // Fester Körper einer Person (Layer Ground: Man kann darauf stehen, Hunde/Enten drehen davor um), ungedreht,
    // mit "HEY!"-Blase, wenn man auf dem Kopf landet
    private static void Hitbox(Transform parent, string name, float x)
    {
        var hitbox = new GameObject(name);
        hitbox.layer = GroundLayer;
        hitbox.transform.SetParent(parent, false);
        hitbox.transform.localPosition = new Vector3(x, Surface, 0f);
        PrimitiveKit.AddBoxCollider(hitbox, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 1.6f, 0.7f));
        GameObject bubble = SpeechBubble(parent, new Vector3(x + 0.4f, Surface + 2.4f, -0.2f), "HEY!");
        var hey = new SerializedObject(hitbox.AddComponent<HeyBubble>());
        hey.FindProperty("bubble").objectReferenceValue = bubble;
        hey.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sprechblase wie beim Hund: weißes Feld mit Spitze, dunkler Text, schaut zur Kamera. Startet ausgeschaltet
    public static GameObject SpeechBubble(Transform parent, Vector3 localPosition, string content)
    {
        CreateMaterials();
        Transform bubble = PrimitiveKit.Pivot(content + " Bubble", parent, localPosition);
        PrimitiveKit.Quad("Background", bubble, Vector3.zero, new Vector2(1.1f, 0.45f), bubbleWhite);
        PrimitiveKit.Quad("Pointer", bubble, new Vector3(-0.15f, -0.25f, 0f), new Vector2(0.16f, 0.16f), bubbleWhite)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        TextMeshPro text = PrimitiveKit.Text("Text", bubble, new Vector3(0f, 0f, -0.01f), new Vector2(1f, 0.38f), content, new Color(0.15f, 0.1f, 0.05f));
        text.fontSizeMax = 4f;
        bubble.gameObject.SetActive(false);
        return bubble.gameObject;
    }

    // ---------- Figur (wie der Kunde: Kapsel-Körper, Kugel-Kopf, Arme mit Drehpunkt an der Schulter) ----------

    public struct Figure
    {
        public Transform root, rig, leftArm, rightArm;
        public Renderer body, head, leftArmMesh, rightArmMesh;
    }

    // Füße bei localPosition, schaut bei yaw 0 zur Kamera (-Z)
    public static Figure BuildFigure(Transform parent, string name, Vector3 localPosition, float yaw)
    {
        CreateMaterials();
        var f = new Figure();
        f.root = PrimitiveKit.Pivot(name, parent, localPosition);
        f.root.localRotation = Quaternion.Euler(0f, yaw, 0f);
        f.rig = PrimitiveKit.Pivot("Rig", f.root, Vector3.zero);
        // Unity-Kapsel ist 2 hoch und 1 breit: Skalierung 0.6/0.55 ergibt 1.1 m Körper mit 0.3 m Radius
        f.body = PrimitiveKit.Part("Body", PrimitiveType.Capsule, f.rig, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), figure);
        f.head = PrimitiveKit.Part("Head", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.45f, figure);
        f.leftArm = PrimitiveKit.Pivot("Arm Pivot L", f.rig, new Vector3(-0.33f, 0.95f, 0f));
        f.leftArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, f.leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), figure);
        f.rightArm = PrimitiveKit.Pivot("Arm Pivot R", f.rig, new Vector3(0.33f, 0.95f, 0f));
        f.rightArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, f.rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), figure);
        return f;
    }
}
