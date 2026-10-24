using UnityEditor;
using UnityEngine;

// Spiel-Elemente für den Strand an der Promenade (Level 2), als Stadt-Prefabs in Assets/Prefabs/City:
// - Lifeguard Tower: Rettungsturm, dessen Plattform über der Laufbahn liegt (Oberkante 3 m über dem Weg, wie ein Balkon)
// - Bouncy Umbrella: schräg im Sand steckender Sonnenschirm, das Dach über der Laufbahn federt wie die Markise (BounceAwning)
// Menü: Tools > Jump and Run > City > Build Beach Prefabs. Mehrfach ausführbar, Prefabs werden ersetzt.
//
// Konvention wie bei allen Stadt-Prefabs: Wurzel auf der Laufbahn (lokal +X = Laufrichtung, +Z = Hausseite, -Z = Meer).
// Die Wurzel sitzt auf der Mitte der begehbaren Fläche, deren Oberkante bei lokal +0.5 liegt. Der Turm wird bei y = 3 gesetzt
// (Plattform-Oberkante 3.5), der Schirm bei y = 2 (Dach-Oberkante 2.5). Weg-Oberkante liegt in der Welt bei 0.5, Sand bei 0.2.
public static class BeachPropsSetup
{
    private const string MaterialFolder = "Assets/Materials/City";
    private const int GroundLayer = 3;

    [MenuItem("Tools/Jump and Run/City/Build Beach Prefabs")]
    public static void BuildAll()
    {
        BuildLifeguardTower();
        BuildBouncyUmbrella();
        AssetDatabase.SaveAssets();
        Debug.Log("BeachPropsSetup: Strand-Prefabs gebaut.");
    }

    private static Material Mat(string name, Color color, float smoothness = 0.2f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static Renderer Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
    {
        // Der Unity-Zylinder ist 2 m hoch und 1 m breit
        return PrimitiveKit.Part(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), material);
    }

    // Stange von a nach b (lokal): Zylinder in die Mitte, Länge = Abstand, gedreht von "oben" auf die Richtung a→b
    private static Renderer Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        Renderer rod = Cylinder(name, parent, (a + b) / 2f, radius, Vector3.Distance(a, b), material);
        rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        return rod;
    }

    // ---------- Rettungsturm ----------

    private static void BuildLifeguardTower()
    {
        Material wood = Mat("Deck Wood", new Color(0.78f, 0.62f, 0.42f));
        Material red = Mat("Lifeguard Red", new Color(0.86f, 0.16f, 0.12f));
        Material white = Mat("Awning White", new Color(0.97f, 0.97f, 0.95f));
        Material glass = Mat("Window Glass", new Color(0.28f, 0.38f, 0.52f), 0.8f);
        Material yellow = Mat("Beach Color 3", new Color(0.98f, 0.75f, 0.15f), 0.3f);

        var root = new GameObject("Lifeguard Tower");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float path = -2.5f, sand = -2.8f;
        const float front = -2.6f, back = 1.8f;

        // Plattform: reicht vom Sand (vorne) bis hinter die Laufbahn. Nur sie hat einen Collider
        var deckCenter = new Vector3(0f, 0.35f, (front + back) / 2f);
        var deckSize = new Vector3(3.2f, 0.3f, back - front);
        PrimitiveKit.Box("Deck", t, deckCenter, deckSize, wood);
        PrimitiveKit.AddBoxCollider(root, deckCenter, deckSize);

        // Pfosten: hinten auf dem Weg, vorne im Sand. Weit genug neben der Laufbahn (z 0), dass der Spieler nicht hindurchläuft
        foreach (float x in new[] { -1.4f, 1.4f })
        {
            Rod("Post Back", t, new Vector3(x, path, 1.6f), new Vector3(x, 0.2f, 1.6f), 0.09f, white);
            Rod("Post Front", t, new Vector3(x, sand, -2.4f), new Vector3(x, 0.2f, -2.4f), 0.09f, white);
            Rod("Brace", t, new Vector3(x, path + 0.2f, 1.6f), new Vector3(x, -0.1f, -2.4f), 0.04f, white);
            Rod("Brace", t, new Vector3(x, sand + 0.2f, -2.4f), new Vector3(x, -0.1f, 1.6f), 0.04f, white);
        }
        Rod("Brace Front", t, new Vector3(-1.4f, sand + 0.2f, -2.4f), new Vector3(1.4f, -0.1f, -2.4f), 0.04f, white);
        Rod("Brace Front", t, new Vector3(1.4f, sand + 0.2f, -2.4f), new Vector3(-1.4f, -0.1f, -2.4f), 0.04f, white);

        // Rote Blende vorne mit Schriftzug, zur Kamera hin
        PrimitiveKit.Box("Skirt", t, new Vector3(0f, 0.12f, front - 0.03f), new Vector3(3.2f, 0.42f, 0.05f), red);
        var label = PrimitiveKit.Text("Label", t, new Vector3(0f, 0.12f, front - 0.07f), new Vector2(2.8f, 0.36f), "LIFEGUARD", Color.white);
        label.fontSizeMax = 3f;

        // Hütte hinten auf der Plattform (hinter der Laufbahn, verdeckt den Spieler also nie)
        const float hutFront = 0.75f;
        PrimitiveKit.Box("Hut", t, new Vector3(0f, 0.5f + 0.95f, (hutFront + back) / 2f), new Vector3(2.4f, 1.9f, back - hutFront), red);
        PrimitiveKit.Box("Hut Roof", t, new Vector3(0f, 2.5f, (hutFront + back) / 2f), new Vector3(2.9f, 0.15f, back - hutFront + 0.5f), white);
        PrimitiveKit.Quad("Hut Window", t, new Vector3(-0.35f, 1.75f, hutFront - 0.01f), new Vector2(1.2f, 0.55f), glass);
        // Rettungsring: weiße Scheibe, darauf eine kleinere in Hüttenfarbe als "Loch"
        Renderer ring = Cylinder("Lifebuoy", t, new Vector3(0.8f, 1.2f, hutFront - 0.04f), 0.28f, 0.06f, white);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer hole = Cylinder("Lifebuoy Hole", t, new Vector3(0.8f, 1.2f, hutFront - 0.08f), 0.13f, 0.02f, red);
        hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Rod("Flag Pole", t, new Vector3(1.1f, 2.55f, 1.3f), new Vector3(1.1f, 3.8f, 1.3f), 0.03f, white);
        PrimitiveKit.Box("Flag", t, new Vector3(1.42f, 3.6f, 1.3f), new Vector3(0.6f, 0.36f, 0.02f), yellow);

        // Leiter am +X-Ende, knapp hinter der Laufbahn
        foreach (float z in new[] { 0.7f, 1.1f })
        {
            Rod("Ladder Rail", t, new Vector3(1.75f, path, z), new Vector3(1.75f, 0.5f, z), 0.03f, white);
        }
        for (float y = path + 0.3f; y < 0.4f; y += 0.35f)
        {
            Rod("Ladder Rung", t, new Vector3(1.75f, y, 0.7f), new Vector3(1.75f, y, 1.1f), 0.025f, white);
        }

        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Lifeguard Tower.prefab");
    }

    // ---------- Federnder Sonnenschirm ----------

    private static void BuildBouncyUmbrella()
    {
        Material red = Mat("Lifeguard Red", new Color(0.86f, 0.16f, 0.12f));
        Material white = Mat("Awning White", new Color(0.97f, 0.97f, 0.95f));
        Material sandMaterial = Mat("Sand", new Color(0.9f, 0.82f, 0.6f), 0.05f);

        var root = new GameObject("Bouncy Umbrella");
        root.layer = GroundLayer;
        Transform t = root.transform;
        const float sand = -1.8f;
        // Dach über der Laufbahn, etwas zum Meer hin. Der Collider deckt die Laufbahn (z 0) mit ab
        var canopyCenter = new Vector3(0f, 0.35f, -0.6f);

        // Das Dach hat einen eigenen Pivot, damit BounceAwning es beim Abfedern stauchen kann
        Transform canvas = PrimitiveKit.Pivot("Canvas", t, canopyCenter);
        Cylinder("Canopy", canvas, Vector3.zero, 1.4f, 0.2f, red);
        Cylinder("Canopy Top", canvas, new Vector3(0f, 0.12f, 0f), 0.75f, 0.16f, red);
        for (int i = 0; i < 4; i++)
        {
            Renderer stripe = PrimitiveKit.Box("Stripe", canvas, new Vector3(0f, 0.11f, 0f), new Vector3(2.76f, 0.03f, 0.22f), white);
            stripe.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
        }
        PrimitiveKit.Part("Knob", PrimitiveType.Sphere, canvas, new Vector3(0f, 0.24f, 0f), Vector3.one * 0.18f, white);
        // Fransen am Rand, abwechselnd rot und weiß
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            Vector3 rim = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, -0.16f, 1.36f);
            Renderer fringe = PrimitiveKit.Box("Fringe", canvas, rim, new Vector3(0.7f, 0.14f, 0.04f), i % 2 == 0 ? red : white);
            fringe.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
        PrimitiveKit.AddBoxCollider(root, canopyCenter, new Vector3(2.6f, 0.3f, 2.6f));

        // Schräg im Sand steckende Stange, unten ein kleiner Sandhügel
        var foot = new Vector3(0f, sand, -3f);
        Rod("Pole", t, foot, canopyCenter + Vector3.down * 0.1f, 0.05f, white);
        PrimitiveKit.Part("Sand Mound", PrimitiveType.Sphere, t, foot, new Vector3(0.6f, 0.2f, 0.6f), sandMaterial);

        var bounce = root.AddComponent<BounceAwning>();
        var serialized = new SerializedObject(bounce);
        serialized.FindProperty("canvas").objectReferenceValue = canvas;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{CityPropsSetup.PrefabFolder}/Bouncy Umbrella.prefab");
    }
}
