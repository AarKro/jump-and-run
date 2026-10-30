using UnityEngine;

// Strandpromenade an der Margherita Avenue zwischen Basil Street und Mozzarella Blvd (Level 2 läuft hier entlang).
// Von den Häusern zum Meer: Plattenweg (Laufbahn bei z 0) → Steinkante → Sand mit Liegen, Schirmen, Bällen →
// flacher Abhang mit Brandung → Meer. Die Kamera steht dort über dem Wasser und schaut über den Strand auf die Häuser.
// An beiden Enden decken Steinmolen den Übergang zum alten breiten Strand ab. Die Oregano Street endet mit Pollern.
// Plattenweg, Kante und Sand sind CityGround (Level schneiden dort Bachmündungen hinein), der Abhang nicht:
// Er ist schräg, und CityCutout kann nur achsenparallele Platten zerschneiden.
public static partial class CityBuilder
{
    private static void BuildBeach()
    {
        Transform beach = Group("Beach Promenade");
        const float x0 = CityLayout.BeachFromX, x1 = CityLayout.BeachToX;
        Material sand = Mat("Sand", new Color(0.9f, 0.82f, 0.6f), 0.05f);

        // Plattenweg auf Gehweghöhe, 1 m dick wie die Gehwege. Er geht auch über das Ende der Oregano Street
        GameObject walk = Slab("Promenade", beach, x0, CityLayout.PromenadeFrontZ, x1, CityLayout.PromenadeBackZ, Surface - 1f, Surface, PavingMaterial()).gameObject;
        walk.layer = GroundLayer;
        walk.AddComponent<BoxCollider>();
        walk.AddComponent<CityGround>();
        // Niedrige Steinkante zum Sand, verdeckt auch die Seitenfläche des Plattenwegs
        Slab("Promenade Edge", beach, x0, CityLayout.PromenadeFrontZ - 0.3f, x1, CityLayout.PromenadeFrontZ, 0f, Surface + 0.08f, Mat("Curb"))
            .gameObject.AddComponent<CityGround>();
        Slab("Sand", beach, x0, CityLayout.SandFrontZ, x1, CityLayout.PromenadeFrontZ, -4f, CityLayout.SandTop, sand)
            .gameObject.AddComponent<CityGround>();

        BuildSlopeAndSurf(beach, x0, x1, sand);
        BuildGroyne(beach, x0, new System.Random(11));
        BuildGroyne(beach, x1, new System.Random(12));
        BuildBollards(beach);
        BuildBeachDecor(beach, x0, x1);
    }

    // Abhang ins Wasser mit nassem Streifen und Schaumlinien (BeachSurf bewegt sie)
    private static void BuildSlopeAndSurf(Transform parent, float x0, float x1, Material sand)
    {
        CityLayout.BeachSlope(out Vector3 surfaceCenter, out Quaternion rotation, out float length, out float waterlineZ);
        float width = x1 - x0;
        surfaceCenter.x = (x0 + x1) / 2f;
        Vector3 uphill = rotation * Vector3.forward;
        Vector3 normal = rotation * Vector3.up;

        // 0.6 m dick, unten 2 m verlängert (verschwindet im Wasser). Oben nicht: Da würde er aus dem Sand ragen
        Renderer slope = PrimitiveKit.Box("Beach Slope", parent, surfaceCenter - normal * 0.3f - uphill * 1f, new Vector3(width, 0.6f, length + 2f), sand);
        slope.transform.rotation = rotation;

        // Alles Weitere liegt in der Ebene des Hangs: lokal Z = hangaufwärts, lokal Y = knapp über der Oberfläche
        Transform surf = new GameObject("Surf").transform;
        surf.SetParent(parent, false);
        surf.SetPositionAndRotation(surfaceCenter, rotation);

        const float reach = 1.4f;
        // Nasser Sand: so weit die Wellen kommen, etwas dunkler
        PrimitiveKit.Box("Wet Sand", surf, new Vector3(0f, 0.005f, waterlineZ + (reach + 0.5f) / 2f), new Vector3(width, 0.01f, reach + 0.5f), Mat("Wet Sand", new Color(0.72f, 0.64f, 0.46f), 0.3f));

        Material foam = Mat("Foam", new Color(0.97f, 0.98f, 1f), 0.6f);
        var lines = new Transform[3];
        for (int i = 0; i < lines.Length; i++)
        {
            // Startet knapp unter der Wasserlinie, jede Linie etwas weiter unten (sie liegt dann zum Teil unter Wasser)
            lines[i] = PrimitiveKit.Box("Foam " + (i + 1), surf, new Vector3(0f, 0.015f + i * 0.004f, waterlineZ - 0.25f - i * 0.1f), new Vector3(width, 0.02f, 0.45f), foam).transform;
        }
        var surfScript = surf.gameObject.AddComponent<BeachSurf>();
        var serialized = new UnityEditor.SerializedObject(surfScript);
        PrimitiveKit.SetReferences(serialized, "foamLines", lines);
        serialized.FindProperty("reach").floatValue = reach;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Steinmole quer zum Strand, vom Plattenweg bis weit ins Wasser: verdeckt die Kanten zum alten Strand und zur Fahrbahn
    private static void BuildGroyne(Transform parent, float x, System.Random random)
    {
        Material rock = Mat("Rock", new Color(0.5f, 0.5f, 0.48f));
        Transform groyne = new GameObject("Groyne").transform;
        groyne.SetParent(parent, false);
        for (float z = CityLayout.PromenadeFrontZ - 0.6f; z > -32f; z -= 0.9f)
        {
            for (int i = 0; i < 2; i++)
            {
                // Flach genug, dass sie vor der Kamera (die hier über dem Wasser steht) nicht wie Felsbrocken aufragen
                float size = 0.7f + (float)random.NextDouble() * 0.5f;
                var center = new Vector3(x + (i - 0.5f) * 1.0f + ((float)random.NextDouble() - 0.5f) * 0.4f, (float)random.NextDouble() * 0.25f - 0.2f, z);
                Renderer stone = PrimitiveKit.Box("Rock", groyne, center, Vector3.one * size, rock);
                stone.transform.rotation = Quaternion.Euler(random.Next(0, 40), random.Next(0, 90), random.Next(0, 40));
            }
        }
    }

    // Die Oregano Street endet an der Promenade: Poller quer über die Fahrbahn
    private static void BuildBollards(Transform parent)
    {
        Material white = Mat("Awning White");
        Material red = Mat("Pizzeria Red");
        Transform bollards = new GameObject("Oregano Bollards").transform;
        bollards.SetParent(parent, false);
        for (float x = -115f; x <= -105f; x += 1.25f)
        {
            PrimitiveKit.Cylinder("Bollard", bollards, new Vector3(x, RoadTop + 0.45f, CityLayout.PromenadeBackZ + 0.6f), 0.11f, 0.9f, white);
            PrimitiveKit.Cylinder("Band", bollards, new Vector3(x, RoadTop + 0.7f, CityLayout.PromenadeBackZ + 0.6f), 0.115f, 0.12f, red);
        }
    }

    // ---------- Strand-Deko (ohne Collider) ----------

    private static void BuildBeachDecor(Transform parent, float x0, float x1)
    {
        Transform decor = new GameObject("Beach Decor").transform;
        decor.SetParent(parent, false);
        var random = new System.Random(4242);
        // Freie Stellen für Spiel-Elemente von Level 2 und an den Enden (Molen, Gelateria-Ecke)
        for (float x = x1 - 8f; x > x0 + 8f; x -= 6.5f + (float)random.NextDouble() * 3.5f)
        {
            if (System.Array.Exists(CityLayout.BeachKeepClearX, clear => Mathf.Abs(clear - x) < 4.5f))
            {
                continue;
            }
            Transform spot = new GameObject("Beach Spot").transform;
            spot.SetParent(decor, false);
            spot.position = new Vector3(x, CityLayout.SandTop, 0f);
            switch (random.Next(4))
            {
                case 0:
                    Umbrella(spot, new Vector3(0f, 0f, -4.7f), random);
                    Lounger(spot, new Vector3(-0.9f, 0f, -3.6f), random);
                    Lounger(spot, new Vector3(0.9f, 0f, -3.6f), random);
                    break;
                case 1:
                    Towel(spot, new Vector3(-0.5f, 0f, -3.5f), random);
                    Ball(spot, new Vector3(0.9f, 0f, -4.6f));
                    break;
                case 2:
                    Umbrella(spot, new Vector3(0.6f, 0f, -4.8f), random);
                    Towel(spot, new Vector3(-0.6f, 0f, -3.7f), random);
                    Cooler(spot, new Vector3(1.3f, 0f, -3.2f));
                    break;
                default:
                    Sandcastle(spot, new Vector3(0f, 0f, -4.2f));
                    Ball(spot, new Vector3(-1.4f, 0f, -3.3f));
                    break;
            }
        }
    }

    private static readonly Color[] BeachColors =
    {
        new Color(0.9f, 0.2f, 0.18f), new Color(0.2f, 0.5f, 0.85f), new Color(0.98f, 0.75f, 0.15f),
        new Color(0.2f, 0.7f, 0.55f), new Color(0.95f, 0.45f, 0.65f)
    };

    private static Material BeachColor(System.Random random)
    {
        int index = random.Next(BeachColors.Length);
        return Mat("Beach Color " + (index + 1), BeachColors[index], 0.3f);
    }

    // Sonnenschirm: Stange und flaches Dach mit weißen Streifen. Höchstens 2.4 m hoch, damit er den Spieler nicht verdeckt
    private static void Umbrella(Transform spot, Vector3 at, System.Random random)
    {
        Material cloth = BeachColor(random);
        Material white = Mat("Awning White");
        PrimitiveKit.Cylinder("Umbrella Pole", spot, at + new Vector3(0f, 1.1f, 0f), 0.04f, 2.2f, white);
        PrimitiveKit.Cylinder("Umbrella Canopy", spot, at + new Vector3(0f, 2.15f, 0f), 1.1f, 0.1f, cloth);
        PrimitiveKit.Cylinder("Umbrella Top", spot, at + new Vector3(0f, 2.24f, 0f), 0.55f, 0.1f, cloth);
        for (int i = 0; i < 2; i++)
        {
            Renderer stripe = PrimitiveKit.Box("Umbrella Stripe", spot, at + new Vector3(0f, 2.21f, 0f), new Vector3(2.15f, 0.02f, 0.16f), white);
            stripe.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
        }
    }

    // Liege: Längs zum Meer, die Rückenlehne am landseitigen Ende hochgeklappt (Blick aufs Wasser)
    private static void Lounger(Transform spot, Vector3 at, System.Random random)
    {
        Material frame = Mat("Awning White");
        Material cushion = BeachColor(random);
        PrimitiveKit.Box("Lounger Frame", spot, at + new Vector3(0f, 0.3f, -0.15f), new Vector3(0.66f, 0.06f, 1.3f), frame);
        PrimitiveKit.Box("Lounger Cushion", spot, at + new Vector3(0f, 0.35f, -0.15f), new Vector3(0.6f, 0.05f, 1.25f), cushion);
        Renderer back = PrimitiveKit.Box("Lounger Back", spot, at + new Vector3(0f, 0.55f, 0.72f), new Vector3(0.6f, 0.06f, 0.65f), cushion);
        // Negative Drehung um X hebt das landseitige (+Z) Ende an
        back.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        foreach (float x in new[] { -0.28f, 0.28f })
        {
            foreach (float z in new[] { -0.7f, 0.4f })
            {
                PrimitiveKit.Box("Lounger Leg", spot, at + new Vector3(x, 0.14f, z), new Vector3(0.04f, 0.28f, 0.04f), frame);
            }
        }
    }

    private static void Towel(Transform spot, Vector3 at, System.Random random)
    {
        PrimitiveKit.Box("Towel", spot, at + new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.02f, 1.7f), BeachColor(random));
    }

    // Wasserball: rote Kugel mit weißem Gürtel (eine flachgedrückte, etwas breitere Kugel)
    private static void Ball(Transform spot, Vector3 at)
    {
        PrimitiveKit.Part("Ball", PrimitiveType.Sphere, spot, at + new Vector3(0f, 0.21f, 0f), Vector3.one * 0.42f, Mat("Beach Color 1", BeachColors[0], 0.3f));
        PrimitiveKit.Part("Ball Band", PrimitiveType.Sphere, spot, at + new Vector3(0f, 0.21f, 0f), new Vector3(0.43f, 0.14f, 0.43f), Mat("Awning White"));
    }

    private static void Cooler(Transform spot, Vector3 at)
    {
        PrimitiveKit.Box("Cooler", spot, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.4f), Mat("Beach Color 2", BeachColors[1], 0.3f));
        PrimitiveKit.Box("Cooler Lid", spot, at + new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.08f, 0.42f), Mat("Awning White"));
    }

    private static void Sandcastle(Transform spot, Vector3 at)
    {
        Material sand = Mat("Castle Sand", new Color(0.82f, 0.72f, 0.5f), 0.05f);
        PrimitiveKit.Box("Castle Base", spot, at + new Vector3(0f, 0.15f, 0f), new Vector3(1f, 0.3f, 0.8f), sand);
        foreach (float x in new[] { -0.45f, 0.45f })
        {
            foreach (float z in new[] { -0.35f, 0.35f })
            {
                PrimitiveKit.Cylinder("Castle Tower", spot, at + new Vector3(x, 0.3f, z), 0.13f, 0.6f, sand);
            }
        }
        PrimitiveKit.Cylinder("Castle Keep", spot, at + new Vector3(0f, 0.45f, 0f), 0.22f, 0.9f, sand);
        PrimitiveKit.Box("Castle Flag", spot, at + new Vector3(0.12f, 1.0f, 0f), new Vector3(0.22f, 0.14f, 0.02f), Mat("Beach Color 1", BeachColors[0], 0.3f));
    }

    private static Material PavingMaterial()
    {
        string path = $"{MaterialFolder}/Paving.mat";
        var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("JumpAndRun/Paving"));
            UnityEditor.AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
}
