using UnityEditor;
using UnityEngine;

// Baut das Kundenhaus (eigenes Gebäude mit Tür, aus der der Kunde herauskommt) und den Kunden ins Goal-Prefab.
// Alle Teile ohne Collider: Der Trigger am Goal bleibt das Einzige, was mit dem Spieler interagiert.
// Goal-Wurzel = Mitte der 1 m dicken Bodenplatte (wie alle Stadt-Prefabs), Gehweg-Oberkante also bei +0.5.
// Menü: Tools > Jump and Run > Build Customer into Goal Prefab. Mehrfach ausführbar, vorhandenes wird ersetzt.
public static class CustomerSetup
{
    private const string GoalPrefabPath = "Assets/Prefabs/Goal.prefab";
    private const string CustomerMaterialPath = "Assets/Materials/Customer.mat";
    private const float Surface = CityPropsSetup.Surface;
    // Der Kunde steht (Füße) erst im dunklen Raum hinter der Tür, dann draußen etwas rechts der Tür.
    // Der Spieler kommt von links und bleibt am Trigger davor stehen
    private static readonly Vector3 CustomerInside = new Vector3(0f, Surface, CityPropsSetup.FacadeZ + 1.3f);
    private static readonly Vector3 CustomerOutside = new Vector3(0.9f, Surface, 0.4f);

    [MenuItem("Tools/Jump and Run/Build Customer into Goal Prefab")]
    public static void BuildCustomer()
    {
        // Weiß, damit die Farbe aus dem MaterialPropertyBlock (Customer-Skript) unverfälscht ankommt
        Material customerMaterial = PrimitiveKit.Material(CustomerMaterialPath, PrimitiveKit.LitShader, Color.white, 0.3f);

        GameObject root = PrefabUtility.LoadPrefabContents(GoalPrefabPath);
        try
        {
            foreach (string oldName in new[] { "Visual", "Front Door", "Customer", "Customer House" })
            {
                Transform old = root.transform.Find(oldName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            SpawnDoor door = CityPropsSetup.BuildCustomerHouse(root.transform);
            // Die Tür-Punkte gelten hier für den Kunden (Füße auf dem Boden), nicht für den Spieler (Kugelmitte)
            door.transform.Find("Inside Point").localPosition = CustomerInside;
            door.transform.Find("Outside Point").localPosition = CustomerOutside;
            BuildCustomerFigure(root.transform, customerMaterial, door);

            // Ziel-Trigger vor der Haustür, auf Gehweg-Höhe
            var trigger = root.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0f, Surface + 1.5f, 0f);
            trigger.size = new Vector3(2f, 3f, 3f);

            PrefabUtility.SaveAsPrefabAsset(root, GoalPrefabPath);
            Debug.Log("CustomerSetup: Kunde und Haustür ins Goal-Prefab gebaut.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildCustomerFigure(Transform parent, Material material, SpawnDoor homeDoor)
    {
        var customerObject = new GameObject("Customer");
        customerObject.transform.SetParent(parent, false);
        customerObject.transform.localPosition = CustomerInside;

        // Rig = beweglicher Teil. Customer bewegt nur das Rig, die Wurzel bleibt stehen
        Transform rig = new GameObject("Rig").transform;
        rig.SetParent(customerObject.transform, false);

        // Unity-Kapsel ist 2 hoch und 1 breit: Skalierung 0.6/0.55 ergibt 1.1 m Körper mit 0.3 m Radius
        Renderer body = PrimitiveKit.Part("Body", PrimitiveType.Capsule, rig, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), material);
        Renderer head = PrimitiveKit.Part("Head", PrimitiveType.Sphere, rig, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.45f, material);

        // Drehpunkt an der Schulter, der Arm hängt darunter: Dreht man den Pivot, schwingt der Arm um die Schulter
        Transform leftArm = PrimitiveKit.Pivot("Arm Pivot L", rig, new Vector3(-0.33f, 0.95f, 0f));
        Renderer leftArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), material);
        Transform rightArm = PrimitiveKit.Pivot("Arm Pivot R", rig, new Vector3(0.33f, 0.95f, 0f));
        Renderer rightArmMesh = PrimitiveKit.Part("Arm", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), material);


        var customer = customerObject.AddComponent<Customer>();
        var serialized = new SerializedObject(customer);
        serialized.FindProperty("rig").objectReferenceValue = rig;
        serialized.FindProperty("leftArm").objectReferenceValue = leftArm;
        serialized.FindProperty("rightArm").objectReferenceValue = rightArm;
        serialized.FindProperty("homeDoor").objectReferenceValue = homeDoor;
        PrimitiveKit.SetReferences(serialized, "shirtRenderers", body);
        PrimitiveKit.SetReferences(serialized, "skinRenderers", head, leftArmMesh, rightArmMesh);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
