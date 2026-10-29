using UnityEditor;
using UnityEngine;

// Der Eiswagen im Pesto Park: in Level 3 steht er aufrecht am Südost-Diagonalweg und verkauft Eis, in Level 5 liegt derselbe
// Wagen an derselben Stelle umgekippt (Deckung vor dem Feueratem). Beide Level bauen ihn mit diesem Kit, damit es wirklich
// derselbe Wagen mit demselben Verkäufer ist.
// Modell-Koordinaten (aufrecht): Ursprung = Bodenmitte, lokal X entlang des Wegs (zur Park-Ecke hin, = +u beider Level),
// lokal Z vom Weg weg (Hinterseite), lokal -Z = Verkaufsseite zum Weg und zur Kamera
public static class IceCreamTruckKit
{
    // Abstand vom Brunnen entlang des Südost-Wegs
    public const float S = 64f;
    public const float Length = 6f;     // entlang des Wegs
    public const float Width = 2.4f;    // quer (liegend = Höhe)
    public const float Height = 3f;     // mit Rädern (liegend = Tiefe)

    // Lage des Wagens (Bodenmitte) in der Welt: auf dem Südost-Weg bei S, so weit vom Weg weg. Drehung wie der Lane-Rahmen dort
    public static void Spot(float backOffset, out Vector3 position, out Quaternion rotation)
    {
        Vector2 center = CityLayout.ParkCenter;
        Vector2 dir = (new Vector2(CityLayout.ParkArea.xMax + 1f, CityLayout.ParkArea.yMin - 1f) - center).normalized;
        var back = new Vector2(-dir.y, dir.x);
        Vector2 at = center + dir * S + back * backOffset;
        position = new Vector3(at.x, CityLayout.ParkLawnTop, at.y);
        rotation = Quaternion.LookRotation(new Vector3(back.x, 0f, back.y), Vector3.up);
    }

    private static Material Lit(string name, Color color) =>
        PrimitiveKit.Material($"Assets/Materials/City/{name}.mat", PrimitiveKit.LitShader, color);

    public static Material Pink => Lit("Ice Cream Pink", new Color(0.98f, 0.55f, 0.7f));
    public static Material Mint => Lit("Ice Cream Mint", new Color(0.6f, 0.9f, 0.75f));
    public static Material Waffle => Lit("Waffle", new Color(0.85f, 0.62f, 0.32f));

    // Aufrechter Wagen: unten Karosserie, auf der Wegseite eine offene Verkaufsklappe (man sieht hinein, dort steht der
    // Verkäufer), darüber ein Vordach, auf dem Dach eine große Waffel mit zwei Kugeln, Fahrerkabine am -X-Ende, Räder.
    // Ohne roofCone fehlt die Waffel (Level 5: beim Umkippen abgebrochen, sie läge sonst quer über der Bahn)
    public static Transform BuildModel(Transform parent, bool roofCone = true)
    {
        Material white = Lit("Truck White", new Color(0.95f, 0.94f, 0.9f));
        Material inside = Lit("Truck Inside", new Color(0.35f, 0.3f, 0.32f));
        Material wheel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wheel.mat");
        Material windows = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Window Glass.mat");
        Material pink = Pink;

        Transform model = PrimitiveKit.Pivot("Truck Model", parent, Vector3.zero);
        const float x0 = -Length / 2f + 1.3f, x1 = Length / 2f;   // Aufbau (hinter der Kabine)
        const float z0 = -Width / 2f, z1 = Width / 2f;
        const float bottom = 0.5f, sill = 1.5f, lintel = 2.5f, top = Height;
        float bodyLength = x1 - x0, bodyCenter = (x0 + x1) / 2f;
        // Boden, untere Wand rundum, Rückwand, Stirnwände, Dach
        PrimitiveKit.Box("Floor", model, new Vector3(bodyCenter, bottom + 0.05f, 0f), new Vector3(bodyLength, 0.1f, Width), inside);
        PrimitiveKit.Box("Lower Body", model, new Vector3(bodyCenter, (bottom + sill) / 2f, 0f), new Vector3(bodyLength, sill - bottom, Width), white);
        PrimitiveKit.Box("Stripe", model, new Vector3(bodyCenter, sill - 0.25f, 0f), new Vector3(bodyLength + 0.02f, 0.25f, Width + 0.02f), pink);
        PrimitiveKit.Box("Back Wall", model, new Vector3(bodyCenter, (sill + lintel) / 2f, z1 - 0.05f), new Vector3(bodyLength, lintel - sill, 0.1f), white);
        PrimitiveKit.Box("End Wall", model, new Vector3(x1 - 0.05f, (sill + lintel) / 2f, 0f), new Vector3(0.1f, lintel - sill, Width), white);
        PrimitiveKit.Box("Front Wall", model, new Vector3(x0 + 0.05f, (sill + lintel) / 2f, 0f), new Vector3(0.1f, lintel - sill, Width), white);
        // Wegseite: links und rechts der Klappe Wand, dazwischen offen
        const float hatch0 = x0 + 0.5f, hatch1 = x1 - 0.5f;
        PrimitiveKit.Box("Side Pillar", model, new Vector3((x0 + hatch0) / 2f, (sill + lintel) / 2f, z0 + 0.05f), new Vector3(hatch0 - x0, lintel - sill, 0.1f), white);
        PrimitiveKit.Box("Side Pillar", model, new Vector3((hatch1 + x1) / 2f, (sill + lintel) / 2f, z0 + 0.05f), new Vector3(x1 - hatch1, lintel - sill, 0.1f), white);
        PrimitiveKit.Box("Upper Body", model, new Vector3(bodyCenter, (lintel + top) / 2f, 0f), new Vector3(bodyLength, top - lintel, Width), white);
        PrimitiveKit.Box("Roof Stripe", model, new Vector3(bodyCenter, top - 0.2f, 0f), new Vector3(bodyLength + 0.02f, 0.15f, Width + 0.02f), pink);
        // Theke vor der Klappe, Vordach schräg darüber
        PrimitiveKit.Box("Counter", model, new Vector3((hatch0 + hatch1) / 2f, sill + 0.03f, z0 - 0.15f), new Vector3(hatch1 - hatch0, 0.06f, 0.4f), pink);
        Renderer awning = PrimitiveKit.Box("Awning", model, new Vector3((hatch0 + hatch1) / 2f, lintel + 0.12f, z0 - 0.45f), new Vector3(hatch1 - hatch0 + 0.2f, 0.06f, 1f), pink);
        awning.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        TMPro.TextMeshPro sign = PrimitiveKit.Text("Sign", model, new Vector3(bodyCenter, 1.0f, z0 - 0.02f), new Vector2(bodyLength - 0.6f, 0.6f), "GELATO", new Color(0.9f, 0.3f, 0.5f));
        sign.fontSizeMax = 8f;

        // Fahrerkabine: niedriger, mit Windschutzscheibe nach -X und Seitenfenster
        float cabX = -Length / 2f + 0.65f;
        PrimitiveKit.Box("Cab", model, new Vector3(cabX, (bottom + 2.2f) / 2f, 0f), new Vector3(1.3f, 2.2f - bottom, Width - 0.1f), white);
        PrimitiveKit.Box("Windshield", model, new Vector3(-Length / 2f - 0.01f, 1.75f, 0f), new Vector3(0.02f, 0.7f, Width - 0.4f), windows);
        PrimitiveKit.Box("Cab Window", model, new Vector3(cabX + 0.1f, 1.75f, z0 + 0.04f), new Vector3(0.8f, 0.6f, 0.02f), windows);

        // Große Waffel auf dem Dach
        if (roofCone)
        {
            Transform cone = PrimitiveKit.Pivot("Roof Cone", model, new Vector3(bodyCenter + 0.6f, top, 0f));
            PrimitiveKit.Part("Waffle", PrimitiveType.Cylinder, cone, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f), Waffle);
            PrimitiveKit.Part("Scoop Pink", PrimitiveType.Sphere, cone, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.9f, pink);
            PrimitiveKit.Part("Scoop Mint", PrimitiveType.Sphere, cone, new Vector3(0f, 1.75f, 0f), Vector3.one * 0.75f, Mint);
        }

        foreach (float x in new[] { -1.9f, 1.9f })
        {
            foreach (float z in new[] { z0 + 0.15f, z1 - 0.15f })
            {
                Renderer w = PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, model, new Vector3(x, 0.4f, z), new Vector3(0.8f, 0.12f, 0.8f), wheel);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        return model;
    }

    // Der Verkäufer: Figur wie die Leute auf dem Markt, aber immer gleich angezogen (rosa Hemd, weiße Papiermütze), damit
    // man ihn in Level 5 wiedererkennt. Füße bei localPosition, schaut bei yaw 0 nach -Z
    public static CrowdFigure Vendor(Transform parent, Vector3 localPosition, float yaw)
    {
        CrowdFigure figure = MarketSetup.Person(parent, "Ice Cream Vendor", localPosition, yaw, 1f);
        var data = new SerializedObject(figure);
        data.FindProperty("fixedOutfit").boolValue = true;
        data.FindProperty("outfitShirt").colorValue = new Color(0.98f, 0.55f, 0.7f);
        data.FindProperty("outfitSkin").colorValue = new Color(0.9f, 0.72f, 0.58f);
        data.FindProperty("outfitHair").colorValue = new Color(0.35f, 0.2f, 0.1f);
        data.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.Part("Paper Hat", PrimitiveType.Cylinder, figure.transform.Find("Rig"), new Vector3(0f, 1.62f, 0f), new Vector3(0.42f, 0.12f, 0.42f),
            Lit("Truck White", new Color(0.95f, 0.94f, 0.9f)));
        return figure;
    }

    // Eiswaffel (z.B. in der Hand: an den Arm-Drehpunkt hängen, die Hand ist ~0.55 m darunter)
    public static Transform Cone(Transform parent, Vector3 localPosition, bool mint)
    {
        Transform cone = PrimitiveKit.Pivot("Ice Cream Cone", parent, localPosition);
        PrimitiveKit.Part("Waffle", PrimitiveType.Cylinder, cone, new Vector3(0f, 0.08f, 0f), new Vector3(0.09f, 0.08f, 0.09f), Waffle);
        PrimitiveKit.Part("Scoop", PrimitiveType.Sphere, cone, new Vector3(0f, 0.19f, 0f), Vector3.one * 0.13f, mint ? Mint : Pink);
        return cone;
    }
}
