using TMPro;
using UnityEditor;
using UnityEngine;

// Absperrungen am Ende einer Laufbahn, damit man nicht in die falsche Richtung in die Stadt läuft.
// Menü: Tools > Jump and Run > City > Build Roadblock Prefabs. Mehrfach ausführbar, die Prefabs werden ersetzt.
// Gebaut im Lane-Rahmen wie alle Stadt-Prefabs (lokal +X = Laufrichtung, +Z = Hausseite, Gehweg von Z -2 bis 3,
// Oberkante 0.5). Jede Absperrung hat eine unsichtbare, hohe Wand quer über den Gehweg: Man kommt weder durch noch drüber.
//  - "Car Crash" (Level 2, östlich der Pizzeria): Spieler kommt von -X, zwei ineinander gekrachte Autos, Rauch, Hütchen, Absperrband
//  - "Delivery Truck" (Level 1, westlich der Pizzeria): Spieler kommt von +X, Lieferwagen lädt Kisten auf den Gehweg ab
public static class RoadblockSetup
{
    private const float Surface = CityPropsSetup.Surface;
    private const float RoadTop = 0.3f;
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    private static Material wheel, glass, headlight, cone, white, red, tapeRed, crate, iron;

    [MenuItem("Tools/Jump and Run/City/Build Roadblock Prefabs")]
    public static void BuildAll()
    {
        CityPropsSetup.CreateMaterials();
        wheel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wheel.mat");
        headlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Headlight.mat");
        glass = Mat("Window Glass", Color.gray);
        cone = Mat("Barrier Orange", Color.gray);
        white = Mat("Awning White", Color.gray);
        red = Mat("Pizzeria Red", Color.gray);
        iron = Mat("Iron", Color.gray);
        tapeRed = Mat("Police Tape Red", new Color(0.85f, 0.1f, 0.1f));
        crate = Mat("Crate Wood", new Color(0.72f, 0.55f, 0.32f));

        BuildCarCrash();
        BuildDeliveryTruck();
        AssetDatabase.SaveAssets();
        Debug.Log("RoadblockSetup: Absperrungen gebaut.");
    }

    // ---------- Unfall ----------

    private static void BuildCarCrash()
    {
        var root = new GameObject("Car Crash");
        Transform t = root.transform;

        // Auto A ist auf den Gehweg geschleudert (leicht schief über dem Bordstein), Auto B steht quer auf der Straße
        Transform carA = Car(t, "Car A", new Vector3(2.2f, Surface, 0.4f), Quaternion.Euler(0f, 70f, 5f), Mat("Crash Red", new Color(0.75f, 0.15f, 0.12f)));
        Car(t, "Car B", new Vector3(4.2f, RoadTop, -4.4f), Quaternion.Euler(0f, -35f, 0f), Mat("Crash Blue", new Color(0.18f, 0.35f, 0.7f)));
        Smoke(carA, new Vector3(0f, 1.1f, 1.6f));

        // Scherben und Teile auf der Straße
        Material debris = Mat("Rubble", Color.gray);
        PrimitiveKit.Box("Bumper", t, new Vector3(3.1f, RoadTop + 0.1f, -2.6f), new Vector3(1.6f, 0.2f, 0.3f), debris).transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        PrimitiveKit.Box("Shard", t, new Vector3(2.4f, RoadTop + 0.02f, -3.4f), new Vector3(0.3f, 0.03f, 0.2f), glass);
        PrimitiveKit.Box("Shard", t, new Vector3(3.6f, RoadTop + 0.02f, -1.9f), new Vector3(0.25f, 0.03f, 0.3f), glass);

        // Vor dem Unfall (Spielerseite, -X): Hütchen und Absperrband quer über den Gehweg.
        // Die Wand steht davor, damit der Spieler vor den Hütchen stehen bleibt statt in ihnen
        foreach (float z in new[] { 2.2f, 0.6f, -1.0f })
        {
            TrafficCone(t, new Vector3(-1.8f, Surface, z));
        }
        PoliceTape(t, -1.4f);
        Wall(root, -2.4f);
        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Car Crash.prefab");
    }

    // ---------- Lieferwagen ----------

    private static void BuildDeliveryTruck()
    {
        var root = new GameObject("Delivery Truck");
        Transform t = root.transform;

        // Lieferwagen auf der Straße, Heck zum Spieler (+X), Hecktüren offen, Rampe hinunter, Kisten auf dem Gehweg.
        // Weit genug hinter den Kisten: Die Kamera (8 m vor der Bahn) schaut sonst durch das Heck auf den Spieler
        Transform truck = PrimitiveKit.Pivot("Truck", t, new Vector3(-6.5f, RoadTop, -4.6f));
        Material cargo = Mat("Truck White", new Color(0.95f, 0.95f, 0.93f));
        PrimitiveKit.Box("Cargo", truck, new Vector3(0.4f, 1.9f, 0f), new Vector3(6f, 2.8f, 2.4f), cargo);
        PrimitiveKit.Box("Cab", truck, new Vector3(-3.6f, 1.45f, 0f), new Vector3(2f, 1.9f, 2.3f), red);
        PrimitiveKit.Box("Windshield", truck, new Vector3(-4.62f, 1.9f, 0f), new Vector3(0.05f, 0.8f, 2f), glass);
        PrimitiveKit.Box("Bed", truck, new Vector3(0.4f, 0.4f, 0f), new Vector3(7.8f, 0.3f, 2.3f), iron);
        foreach (float x in new[] { -3.6f, 2.2f })
        {
            foreach (float z in new[] { -1.1f, 1.1f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, truck, new Vector3(x, 0.4f, z), new Vector3(0.8f, 0.15f, 0.8f), wheel)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        // Firmenname auf der Seite, die zur Kamera zeigt (-Z)
        PrimitiveKit.Text("Sign", truck, new Vector3(0.4f, 2.1f, -1.22f), new Vector2(5.4f, 1.2f), "PIZZA SUPPLY CO.\n<size=60%>fresh tomatoes · mozzarella</size>", new Color(0.8f, 0.13f, 0.1f));
        // Hecktüren: an den Kanten angeschlagen, weit aufgeklappt
        foreach (float side in new[] { -1f, 1f })
        {
            Transform hinge = PrimitiveKit.Pivot("Door Hinge", truck, new Vector3(3.4f, 1.9f, side * 1.2f));
            hinge.localRotation = Quaternion.Euler(0f, side * -100f, 0f);
            PrimitiveKit.Box("Door", hinge, new Vector3(0.6f, 0f, -side * 0.0f), new Vector3(1.2f, 2.7f, 0.06f), cargo);
        }
        // Rampe vom Ladeboden auf die Straße
        Renderer ramp = PrimitiveKit.Box("Ramp", truck, new Vector3(4.6f, 0.35f, 0f), new Vector3(2.6f, 0.08f, 1.6f), iron);
        ramp.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

        // Kistenstapel quer über den Gehweg
        Crate(t, new Vector3(0.6f, Surface, -1.3f), 0, "TOMATOES");
        Crate(t, new Vector3(0.6f, Surface, -0.4f), 0, "MOZZARELLA");
        Crate(t, new Vector3(0.6f, Surface, 0.5f), 0, "BASIL");
        Crate(t, new Vector3(0.6f, Surface, 1.4f), 0, "TOMATOES");
        Crate(t, new Vector3(0.6f, Surface, -0.85f), 1, "FLOUR");
        Crate(t, new Vector3(0.6f, Surface, 0.95f), 1, "TOMATOES");
        Crate(t, new Vector3(0.6f, Surface, 0.05f), 2, "OLIVE OIL");
        // Sackkarre an den Kisten
        Transform cart = PrimitiveKit.Pivot("Hand Truck", t, new Vector3(1.5f, Surface, 2.3f));
        cart.localRotation = Quaternion.Euler(0f, 0f, 15f);
        PrimitiveKit.Box("Frame", cart, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 1.2f, 0.5f), iron);
        PrimitiveKit.Box("Plate", cart, new Vector3(0.12f, 0.03f, 0f), new Vector3(0.3f, 0.04f, 0.5f), iron);

        Wall(root, 1.6f);
        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Delivery Truck.prefab");
    }

    // ---------- Bausteine ----------

    // Einfaches Auto, vorne = lokal +Z, Reifenunterkante auf der Wurzel
    private static Transform Car(Transform parent, string name, Vector3 position, Quaternion rotation, Material paint)
    {
        Transform car = PrimitiveKit.Pivot(name, parent, position);
        car.localRotation = rotation;
        PrimitiveKit.Box("Body", car, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.7f, 4f), paint);
        PrimitiveKit.Box("Cabin", car, new Vector3(0f, 1.2f, -0.3f), new Vector3(1.6f, 0.55f, 2f), paint);
        PrimitiveKit.Box("Windows", car, new Vector3(0f, 1.2f, -0.3f), new Vector3(1.62f, 0.4f, 1.6f), glass);
        // Eingedrückte Motorhaube: schräg nach oben geknickt
        PrimitiveKit.Box("Crumpled Hood", car, new Vector3(0f, 1.05f, 1.55f), new Vector3(1.7f, 0.12f, 1f), paint).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        foreach (float x in new[] { -0.9f, 0.9f })
        {
            foreach (float z in new[] { -1.3f, 1.3f })
            {
                PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, car, new Vector3(x, 0.3f, z), new Vector3(0.6f, 0.12f, 0.6f), wheel)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }
        PrimitiveKit.Box("Headlight L", car, new Vector3(-0.6f, 0.65f, 2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);
        PrimitiveKit.Box("Headlight R", car, new Vector3(0.6f, 0.65f, 2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);
        return car;
    }

    // Grauer Rauch aus der Motorhaube, steigt langsam auf und wird dabei größer und blasser
    private static void Smoke(Transform parent, Vector3 position)
    {
        var smokeObject = new GameObject("Smoke");
        smokeObject.transform.SetParent(parent, false);
        smokeObject.transform.localPosition = position;
        smokeObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var smoke = smokeObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = smoke.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.35f, 0.37f, 0.6f), new Color(0.55f, 0.55f, 0.57f, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.rateOverTime = 10f;
        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.25f;
        ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
        ParticleSystem.ColorOverLifetimeModule color = smoke.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        smokeObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
    }

    private static void TrafficCone(Transform parent, Vector3 position)
    {
        Transform c = PrimitiveKit.Pivot("Traffic Cone", parent, position);
        PrimitiveKit.Box("Base", c, new Vector3(0f, 0.02f, 0f), new Vector3(0.45f, 0.04f, 0.45f), cone);
        // Kegel aus gestapelten, nach oben schmaleren Zylindern, mit weißem Ring
        float[] radii = { 0.17f, 0.14f, 0.11f, 0.08f, 0.05f };
        for (int i = 0; i < radii.Length; i++)
        {
            Material material = i == 2 ? white : cone;
            PrimitiveKit.Part("Ring " + (i + 1), PrimitiveType.Cylinder, c, new Vector3(0f, 0.1f + i * 0.13f, 0f), new Vector3(radii[i] * 2f, 0.065f, radii[i] * 2f), material);
        }
    }

    // Rot-weißes Band zwischen zwei Pfosten quer über den Gehweg bei lokal x
    private static void PoliceTape(Transform parent, float x)
    {
        const float from = -1.8f, to = 2.8f;
        PrimitiveKit.Box("Tape Post", parent, new Vector3(x, Surface + 0.55f, from), new Vector3(0.06f, 1.1f, 0.06f), iron);
        PrimitiveKit.Box("Tape Post", parent, new Vector3(x, Surface + 0.55f, to), new Vector3(0.06f, 1.1f, 0.06f), iron);
        const int segments = 10;
        float length = (to - from) / segments;
        for (int i = 0; i < segments; i++)
        {
            PrimitiveKit.Box("Tape " + (i + 1), parent, new Vector3(x, Surface + 1f, from + (i + 0.5f) * length), new Vector3(0.02f, 0.12f, length), i % 2 == 0 ? tapeRed : white);
        }
    }

    // Holzkiste (0.8 m), ebene 0 steht auf dem Gehweg, mit Aufschrift zur Kamera
    private static void Crate(Transform parent, Vector3 position, int level, string label)
    {
        const float size = 0.85f;
        Transform box = PrimitiveKit.Pivot("Crate", parent, position + Vector3.up * (level * size + size / 2f));
        box.localRotation = Quaternion.Euler(0f, (label.Length * 7f) % 12f - 6f, 0f);
        PrimitiveKit.Box("Box", box, Vector3.zero, Vector3.one * size, crate);
        TextMeshPro text = PrimitiveKit.Text("Label", box, new Vector3(0f, 0f, -size / 2f - 0.01f), new Vector2(size * 0.9f, size * 0.35f), label, new Color(0.55f, 0.1f, 0.08f));
        text.fontSizeMax = 6f;
    }

    // Unsichtbare Wand quer über den Gehweg (Z -2 bis 3), 10 m hoch: nicht durchlaufen, nicht drüberspringen.
    // Layer Default statt Ground: Darauf landen zählt nicht als Boden
    private static void Wall(GameObject root, float x)
    {
        PrimitiveKit.AddBoxCollider(root, new Vector3(x, 5f, 0.5f), new Vector3(0.6f, 10f, 5f));
    }

    private static Material Mat(string name, Color color)
    {
        return PrimitiveKit.Material($"Assets/Materials/City/{name}.mat", PrimitiveKit.LitShader, color);
    }
}
