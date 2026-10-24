using UnityEditor;
using UnityEngine;

// Baustellen-Prefabs für Level 3 (Mozzarella Blvd), gebaut aus Grundformen im Lane-Rahmen wie die Stadt-Prefabs
// (lokal +X = Laufrichtung, -Z = Kameraseite, Fassaden bei Z = 3). Menü: Tools > Jump and Run > City > Build Construction Prefabs.
//  - "Scissor Lift": gelbe Scherenbühne. Die Plattform ist eine MovingPlatform (fährt von "moving platform start" nach
//    "moving platform end", Standard 4 m hoch), ScissorLift stellt Sockel und Scherenarme dazu. Oberkante = Wurzel + 0.5.
//  - "Scaffold": Gerüst-Etage vor der Fassade, Bretter über der Laufbahn begehbar (Oberkante = Wurzel + 0.5),
//    Stangen reichen 12 m nach unten (stehen im Gehweg oder in der Grube)
public static class ConstructionSetup
{
    private const int GroundLayer = 3;
    private const float FacadeZ = 3f;
    private const string MaterialFolder = "Assets/Materials/City";

    private const float DeckLength = 3f;
    private const float DeckDepth = 1.6f;
    private const int ScissorStages = 4;
    private const float ArmLength = 2.4f;
    private const float ChassisHeight = 0.4f;

    private static Material yellow, darkYellow, iron, orange, wood, steel;

    [MenuItem("Tools/Jump and Run/City/Build Construction Prefabs")]
    public static void BuildAll()
    {
        CreateMaterials();
        BuildScissorLift();
        BuildScaffold();
        AssetDatabase.SaveAssets();
        Debug.Log("ConstructionSetup: Scissor Lift und Scaffold gespeichert.");
    }

    private static Material Lit(string name, Color color, float smoothness = 0.2f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static void CreateMaterials()
    {
        yellow = Lit("Gondola Yellow", new Color(0.95f, 0.76f, 0.16f));
        darkYellow = Lit("Lift Yellow Dark", new Color(0.78f, 0.58f, 0.1f));
        iron = Lit("Iron", new Color(0.14f, 0.14f, 0.16f), 0.5f);
        orange = Lit("Barrier Orange", new Color(0.98f, 0.45f, 0.08f));
        wood = Lit("Scaffold Plank", new Color(0.72f, 0.55f, 0.32f));
        steel = Lit("Scaffold Steel", new Color(0.62f, 0.64f, 0.66f), 0.5f);
    }

    private static void BuildScissorLift()
    {
        var root = new GameObject("Scissor Lift");
        root.layer = GroundLayer;
        Transform t = root.transform;

        // Plattform: Oberkante 0.5 über der Wurzel, Geländer vorne und hinten (ohne Collider), Warnstreifen an den Enden
        PrimitiveKit.Box("Deck", t, new Vector3(0f, 0.35f, 0f), new Vector3(DeckLength, 0.3f, DeckDepth), yellow);
        foreach (float x in new[] { -DeckLength / 2f + 0.1f, DeckLength / 2f - 0.1f })
        {
            PrimitiveKit.Box("Warning Stripe", t, new Vector3(x, 0.505f, 0f), new Vector3(0.2f, 0.01f, DeckDepth), orange);
        }
        foreach (float z in new[] { -DeckDepth / 2f + 0.04f, DeckDepth / 2f - 0.04f })
        {
            PrimitiveKit.Box("Rail", t, new Vector3(0f, 1.5f, z), new Vector3(DeckLength, 0.06f, 0.06f), yellow);
            foreach (float x in new[] { -1.45f, 0f, 1.45f })
            {
                PrimitiveKit.Box("Rail Post", t, new Vector3(x, 1f, z), new Vector3(0.06f, 1f, 0.06f), iron);
            }
        }

        // Sockel mit Rädern: wird zur Laufzeit auf den Boden gestellt (ScissorLift)
        Transform chassis = PrimitiveKit.Pivot("Chassis", t, new Vector3(0f, -4f, 0f));
        PrimitiveKit.Box("Body", chassis, Vector3.zero, new Vector3(2.6f, ChassisHeight, DeckDepth - 0.2f), darkYellow);
        foreach (float x in new[] { -0.95f, 0.95f })
        {
            foreach (float z in new[] { -0.75f, 0.75f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, chassis, new Vector3(x, -0.08f, z), new Vector3(0.36f, 0.06f, 0.36f), iron)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // Scherenarme: pro Stufe ein X, vorne und hinten je zwei gekreuzte Arme (lokal X = Länge des Arms)
        var stages = new Transform[ScissorStages];
        var rising = new Transform[ScissorStages * 2];
        var falling = new Transform[ScissorStages * 2];
        for (int i = 0; i < ScissorStages; i++)
        {
            stages[i] = PrimitiveKit.Pivot("Stage " + (i + 1), t, new Vector3(0f, -3f + i, 0f));
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -0.6f : 0.6f;
                rising[i * 2 + side] = PrimitiveKit.Box("Arm Rising", stages[i], new Vector3(0f, 0f, z), new Vector3(ArmLength, 0.12f, 0.08f), iron).transform;
                falling[i * 2 + side] = PrimitiveKit.Box("Arm Falling", stages[i], new Vector3(0f, 0f, z * 1.15f), new Vector3(ArmLength, 0.12f, 0.08f), iron).transform;
            }
        }

        Transform start = PrimitiveKit.Pivot("moving platform start", t, Vector3.zero);
        Transform end = PrimitiveKit.Pivot("moving platform end", t, new Vector3(0f, 4f, 0f));
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.35f, 0f), new Vector3(DeckLength, 0.3f, DeckDepth));
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var mover = new SerializedObject(root.AddComponent<MovingPlatform>());
        mover.FindProperty("pointA").objectReferenceValue = start;
        mover.FindProperty("pointB").objectReferenceValue = end;
        mover.FindProperty("speed").floatValue = 1.5f;
        mover.FindProperty("waitTime").floatValue = 1f;
        mover.ApplyModifiedPropertiesWithoutUndo();

        var lift = root.AddComponent<ScissorLift>();
        var serialized = new SerializedObject(lift);
        serialized.FindProperty("chassis").objectReferenceValue = chassis;
        PrimitiveKit.SetReferences(serialized, "stages", stages);
        PrimitiveKit.SetReferences(serialized, "risingArms", rising);
        PrimitiveKit.SetReferences(serialized, "fallingArms", falling);
        serialized.FindProperty("armLength").floatValue = ArmLength;
        serialized.FindProperty("chassisHeight").floatValue = ChassisHeight;
        serialized.FindProperty("deckBottom").floatValue = 0.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        lift.Arrange();

        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Scissor Lift.prefab");
    }

    private static void BuildScaffold()
    {
        var root = new GameObject("Scaffold");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float front = -0.8f;
        float depth = FacadeZ - front;
        float centerZ = (FacadeZ + front) / 2f;

        // Bretter (begehbar), darunter ein Querträger, vorne ein Bordbrett
        for (int i = 0; i < 4; i++)
        {
            float z = front + depth * (i + 0.5f) / 4f;
            PrimitiveKit.Box("Plank " + (i + 1), t, new Vector3(0f, 0.4f, z), new Vector3(3f, 0.2f, depth / 4f - 0.04f), wood);
        }
        PrimitiveKit.Box("Toe Board", t, new Vector3(0f, 0.6f, front + 0.05f), new Vector3(3f, 0.2f, 0.04f), orange);
        PrimitiveKit.Box("Ledger Front", t, new Vector3(0f, 0.22f, front + 0.1f), new Vector3(3f, 0.08f, 0.08f), steel);
        PrimitiveKit.Box("Ledger Back", t, new Vector3(0f, 0.22f, FacadeZ - 0.3f), new Vector3(3f, 0.08f, 0.08f), steel);

        // Stangen an den vier Ecken bis 12 m nach unten (enden im Boden), Geländer hinten und an den Seiten
        foreach (float x in new[] { -1.45f, 1.45f })
        {
            foreach (float z in new[] { front + 0.1f, FacadeZ - 0.3f })
            {
                PrimitiveKit.Part("Standard", PrimitiveType.Cylinder, t, new Vector3(x, -5f, z), new Vector3(0.08f, 6.6f, 0.08f), steel);
            }
            PrimitiveKit.Box("Side Rail", t, new Vector3(x, 1.5f, centerZ), new Vector3(0.05f, 0.05f, depth), steel);
        }
        PrimitiveKit.Box("Back Rail", t, new Vector3(0f, 1.5f, FacadeZ - 0.3f), new Vector3(3f, 0.05f, 0.05f), steel);
        // Diagonale Strebe hinten, damit es nach Gerüst aussieht
        PrimitiveKit.Box("Brace", t, new Vector3(0f, -1.2f, FacadeZ - 0.25f), new Vector3(0.05f, 3.6f, 0.05f), steel)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 40f);

        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, 0.4f, centerZ), new Vector3(3f, 0.2f, depth));
        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Scaffold.prefab");
    }
}
