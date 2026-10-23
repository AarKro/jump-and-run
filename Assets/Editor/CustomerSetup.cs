using UnityEditor;
using UnityEngine;

// Baut den Kunden mit Haustür ins Goal-Prefab (ersetzt die alte Stange "Visual").
// Alle Teile ohne Collider: Der Trigger am Goal bleibt das Einzige, was mit dem Spieler interagiert.
// Menü: Tools > Jump and Run > Build Customer into Goal Prefab. Mehrfach ausführbar, vorhandenes wird ersetzt.
public static class CustomerSetup
{
    private const string GoalPrefabPath = "Assets/Prefabs/Goal.prefab";
    private const string CustomerMaterialPath = "Assets/Materials/Customer.mat";
    private const string WallMaterialPath = "Assets/Materials/House Wall.mat";
    private const string DoorMaterialPath = "Assets/Materials/Door.mat";
    private const string KnobMaterialPath = "Assets/Materials/Door Knob.mat";

    // Kunde steht etwas hinter der Mitte des Triggers, damit der Spieler davor stehen bleibt
    private static readonly Vector3 CustomerPosition = new Vector3(0.5f, 0f, 0f);

    [MenuItem("Tools/Jump and Run/Build Customer into Goal Prefab")]
    public static void BuildCustomer()
    {
        // Weiß, damit die Farbe aus dem MaterialPropertyBlock (Customer-Skript) unverfälscht ankommt
        Material customerMaterial = GetOrCreateMaterial(CustomerMaterialPath, Color.white, 0.3f);
        Material wallMaterial = GetOrCreateMaterial(WallMaterialPath, new Color(0.93f, 0.86f, 0.74f), 0.1f);
        Material doorMaterial = GetOrCreateMaterial(DoorMaterialPath, new Color(0.45f, 0.26f, 0.13f), 0.3f);
        Material knobMaterial = GetOrCreateMaterial(KnobMaterialPath, new Color(1f, 0.8f, 0.2f), 0.8f);

        GameObject root = PrefabUtility.LoadPrefabContents(GoalPrefabPath);
        try
        {
            foreach (string oldName in new[] { "Visual", "Front Door", "Customer" })
            {
                Transform old = root.transform.Find(oldName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            BuildFrontDoor(root.transform, wallMaterial, doorMaterial, knobMaterial);
            BuildCustomerFigure(root.transform, customerMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, GoalPrefabPath);
            Debug.Log("CustomerSetup: Kunde und Haustür ins Goal-Prefab gebaut.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildFrontDoor(Transform parent, Material wall, Material door, Material knob)
    {
        Transform frontDoor = new GameObject("Front Door").transform;
        frontDoor.SetParent(parent, false);

        // Hinter der Laufbahn (Spieler ist bei Z = 0 eingefroren), Tür etwas vor der Wand
        CreatePart("Wall", PrimitiveType.Cube, frontDoor, new Vector3(0.5f, 1.8f, 1.3f), new Vector3(3.2f, 3.6f, 0.2f), wall);
        CreatePart("Door", PrimitiveType.Cube, frontDoor, new Vector3(0.5f, 1f, 1.16f), new Vector3(1f, 2f, 0.08f), door);
        CreatePart("Knob", PrimitiveType.Sphere, frontDoor, new Vector3(0.85f, 1f, 1.1f), Vector3.one * 0.1f, knob);
        CreatePart("Doorstep", PrimitiveType.Cube, frontDoor, new Vector3(0.5f, 0.06f, 0.95f), new Vector3(1.4f, 0.12f, 0.5f), wall);
    }

    private static void BuildCustomerFigure(Transform parent, Material material)
    {
        var customerObject = new GameObject("Customer");
        customerObject.transform.SetParent(parent, false);
        customerObject.transform.localPosition = CustomerPosition;

        // Rig = beweglicher Teil. Customer bewegt nur das Rig, die Wurzel bleibt stehen
        Transform rig = new GameObject("Rig").transform;
        rig.SetParent(customerObject.transform, false);

        // Unity-Kapsel ist 2 hoch und 1 breit: Skalierung 0.6/0.55 ergibt 1.1 m Körper mit 0.3 m Radius
        Renderer body = CreatePart("Body", PrimitiveType.Capsule, rig, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), material);
        Renderer head = CreatePart("Head", PrimitiveType.Sphere, rig, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.45f, material);

        // Drehpunkt an der Schulter, der Arm hängt darunter: Dreht man den Pivot, schwingt der Arm um die Schulter
        Transform leftArm = CreatePivot("Arm Pivot L", rig, new Vector3(-0.33f, 0.95f, 0f));
        Renderer leftArmMesh = CreatePart("Arm", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), material);
        Transform rightArm = CreatePivot("Arm Pivot R", rig, new Vector3(0.33f, 0.95f, 0f));
        Renderer rightArmMesh = CreatePart("Arm", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), material);

        // Vor dem Bauch, zur Kamera hin (Kamera schaut entlang +Z, also liegt "vorne" bei -Z)
        Transform hands = CreatePivot("Hands Point", rig, new Vector3(0f, 0.7f, -0.4f));

        var customer = customerObject.AddComponent<Customer>();
        var serialized = new SerializedObject(customer);
        serialized.FindProperty("rig").objectReferenceValue = rig;
        serialized.FindProperty("leftArm").objectReferenceValue = leftArm;
        serialized.FindProperty("rightArm").objectReferenceValue = rightArm;
        serialized.FindProperty("handsPoint").objectReferenceValue = hands;
        SetRenderers(serialized.FindProperty("shirtRenderers"), body);
        SetRenderers(serialized.FindProperty("skinRenderers"), head, leftArmMesh, rightArmMesh);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRenderers(SerializedProperty array, params Renderer[] renderers)
    {
        array.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
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
        // CreatePrimitive bringt immer einen Collider mit, den brauchen wir nicht
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static Material GetOrCreateMaterial(string path, Color color, float smoothness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
