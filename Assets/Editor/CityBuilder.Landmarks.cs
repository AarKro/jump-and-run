using UnityEditor;
using UnityEngine;

// Sehenswürdigkeiten der Stadt: Piazza Pisa mit dem Leaning Tower of Pizza, Park mit Brunnen, Founder's Circle mit Statue
public static partial class CityBuilder
{
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    private static Material Marble => Mat("Marble", new Color(0.93f, 0.91f, 0.86f), 0.3f);
    private static Material Paving => Mat("Piazza Paving", new Color(0.8f, 0.72f, 0.58f));
    private static Material Grass => Mat("Grass", new Color(0.42f, 0.66f, 0.3f), 0.05f);

    // ---------- Piazza Pisa ----------

    // Häuser nur an drei Seiten, die Ostseite bleibt offen zur Basil Street und die Südreihe ab PiazzaSouthOpeningFrom
    // zur Margherita Avenue, damit man den Turm von beiden Straßen aus sieht
    private static void BuildPiazza(Transform parent, CityLayout.Block block, System.Random random)
    {
        Rect area = block.Lots;
        foreach (Rect lot in Lots(area, random, skipEast: true))
        {
            bool inSouthOpening = lot.yMin < area.yMin + 1f && lot.center.x > CityLayout.PiazzaSouthOpeningFrom;
            if (!inSouthOpening)
            {
                House(parent, lot, Pick(random, 10f, 11f, 13f, 14f));
            }
        }
        Rect walk = block.Sidewalk;
        Slab("Piazza Paving", parent, area.xMin + 8f, area.yMin + 8f, walk.xMax, area.yMax - 8f, Surface - 0.1f, Surface + 0.02f, Paving);
        Slab("Piazza Paving South", parent, CityLayout.PiazzaSouthOpeningFrom, area.yMin, walk.xMax, area.yMin + 8f, Surface - 0.1f, Surface + 0.02f, Paving);
        BuildLeaningTower(parent, CityLayout.LeaningTower, random);
    }

    // Turm aus gestapelten Pizzen: pro Etage ein Kern aus Teig, ein Säulenkranz und obendrauf eine Pizza (Rand, Soße, Käse,
    // Salami), am Rand tropft Käse herunter. Wie in Pisa schief: Der ganze Stapel hängt an einem Pivot, der um 5° gekippt ist
    private static void BuildLeaningTower(Transform parent, Vector2 at, System.Random random)
    {
        Material dough = Mat("Pizza Crust", new Color(0.85f, 0.62f, 0.33f));
        Material sauce = Mat("Pizza Sauce", new Color(0.75f, 0.16f, 0.1f), 0.35f);
        Material cheese = Mat("Pizza Cheese", new Color(0.98f, 0.82f, 0.36f), 0.3f);
        Material pepperoni = Mat("Pepperoni", new Color(0.6f, 0.1f, 0.08f), 0.3f);

        Transform tower = new GameObject("Leaning Tower of Pizza").transform;
        tower.SetParent(parent, false);
        tower.position = new Vector3(at.x, Surface, at.y);
        Cylinder("Plinth", tower, new Vector3(0f, 0.3f, 0f), 10f, 0.6f, Marble);

        // Kippt Richtung Basil Street (+X): Drehung um die Z-Achse, negativ = Spitze nach +X
        Transform lean = PrimitiveKit.Pivot("Lean", tower, new Vector3(0f, 0.6f, 0f));
        lean.localRotation = Quaternion.Euler(0f, 0f, -5f);

        const int tiers = 7;
        const float tierHeight = 6f;
        const float discHeight = 0.8f;
        for (int i = 0; i < tiers; i++)
        {
            bool top = i == tiers - 1;
            float y = i * tierHeight;
            float core = top ? 3.2f : 4.6f;
            float columnRing = top ? 4f : 5.6f;
            float disc = top ? 4.8f : 6.8f;
            float wall = tierHeight - discHeight;
            Transform tier = PrimitiveKit.Pivot($"Tier {i + 1}", lean, new Vector3(0f, y, 0f));

            Cylinder("Core", tier, new Vector3(0f, wall / 2f, 0f), core, wall, dough);
            int columns = top ? 8 : 12;
            for (int c = 0; c < columns; c++)
            {
                float angle = c * Mathf.PI * 2f / columns;
                Cylinder($"Column {c + 1}", tier, new Vector3(Mathf.Sin(angle) * columnRing, wall / 2f, Mathf.Cos(angle) * columnRing), 0.3f, wall, Marble);
            }

            // Pizza obendrauf: Rand, darauf etwas kleinere Soße und Käse, jeweils ein Stück höher
            Cylinder("Crust", tier, new Vector3(0f, wall + discHeight / 2f, 0f), disc, discHeight, dough);
            Cylinder("Sauce", tier, new Vector3(0f, wall + discHeight / 2f + 0.03f, 0f), disc - 0.5f, discHeight, sauce);
            Cylinder("Cheese", tier, new Vector3(0f, wall + discHeight / 2f + 0.06f, 0f), disc - 0.9f, discHeight, cheese);
            for (int p = 0; p < (top ? 3 : 6); p++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 1.2f + (float)random.NextDouble() * (disc - 2.6f);
                Cylinder($"Pepperoni {p + 1}", tier, new Vector3(Mathf.Sin(angle) * radius, wall + discHeight + 0.08f, Mathf.Cos(angle) * radius), 0.8f, 0.1f, pepperoni);
            }
            for (int d = 0; d < 4; d++)
            {
                float angle = (d + (float)random.NextDouble() * 0.6f) * Mathf.PI / 2f;
                float length = 1f + (float)random.NextDouble() * 2.2f;
                Renderer drip = PrimitiveKit.Box($"Cheese Drip {d + 1}", tier,
                    new Vector3(Mathf.Sin(angle) * (disc - 0.1f), wall + discHeight - length / 2f, Mathf.Cos(angle) * (disc - 0.1f)),
                    new Vector3(0.7f, length, 0.25f), cheese);
                drip.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
        }

        // Fahne auf der Spitze
        float tip = tiers * tierHeight;
        Cylinder("Flag Pole", lean, new Vector3(0f, tip + 2.5f, 0f), 0.12f, 5f, Marble);
        PrimitiveKit.Box("Flag", lean, new Vector3(1f, tip + 4.4f, 0f), new Vector3(2f, 1.2f, 0.05f), sauce);
    }

    // ---------- Park ----------

    // Wiese mit Wegen von den Ecken zum Brunnen, Rundweg um den Brunnen, Bäume dazwischen
    private static void BuildPark(Transform parent, CityLayout.Block block, System.Random random)
    {
        Material path = Mat("Park Path", new Color(0.86f, 0.78f, 0.6f));
        Rect area = block.Lots;
        Slab("Lawn", parent, area.xMin - 1f, area.yMin - 1f, area.xMax + 1f, area.yMax + 1f, Surface - 0.1f, Surface + 0.05f, Grass);

        Vector2 center = area.center;
        float ring = Mathf.Min(14f, area.height / 2f - 4f);
        Vector2[] corners =
        {
            new Vector2(area.xMin - 1f, area.yMin - 1f), new Vector2(area.xMax + 1f, area.yMin - 1f),
            new Vector2(area.xMin - 1f, area.yMax + 1f), new Vector2(area.xMax + 1f, area.yMax + 1f)
        };
        foreach (Vector2 corner in corners)
        {
            Walkway("Path", parent, corner, center + (corner - center).normalized * ring, 3f, path);
        }
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Walkway("Ring Path", parent, center + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * ring,
                center + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * ring, 3f, path);
        }

        BuildFountain(parent, new Vector3(center.x, Surface + 0.05f, center.y), Mathf.Min(7f, ring - 3f));

        // Bäume zufällig verteilt, aber nicht auf den Wegen und nicht am Brunnen
        Vector2 half = area.size / 2f;
        int count = Mathf.FloorToInt(area.width * area.height / 95f);
        for (int i = 0; i < count; i++)
        {
            var spot = new Vector2(area.xMin + 2f + (float)random.NextDouble() * (area.width - 4f), area.yMin + 2f + (float)random.NextDouble() * (area.height - 4f));
            Vector2 offset = spot - center;
            if (offset.magnitude < ring + 4f)
            {
                continue;
            }
            // Abstand zur Diagonale (alle vier Wege liegen auf den Diagonalen des Blocks)
            float toDiagonal = Mathf.Abs(Mathf.Abs(offset.x) * half.y - Mathf.Abs(offset.y) * half.x) / half.magnitude;
            if (toDiagonal < 3.5f)
            {
                continue;
            }
            Tree(parent, new Vector3(spot.x, Surface + 0.05f, spot.y), 0.8f + (float)random.NextDouble() * 0.6f, random);
        }
    }

    // Brunnen: Becken mit Wasser, Säule mit Schale, oben eine Wasserfontäne aus Partikeln
    private static void BuildFountain(Transform parent, Vector3 at, float radius)
    {
        Material water = Mat("Fountain Water", new Color(0.3f, 0.6f, 0.85f), 0.9f);
        Transform fountain = new GameObject("Fountain").transform;
        fountain.SetParent(parent, false);
        fountain.position = at;

        Cylinder("Basin", fountain, new Vector3(0f, 0.4f, 0f), radius, 0.8f, Marble);
        Cylinder("Water", fountain, new Vector3(0f, 0.42f, 0f), radius - 0.5f, 0.8f, water);
        Cylinder("Column", fountain, new Vector3(0f, 2f, 0f), 0.6f, 2.4f, Marble);
        Cylinder("Bowl", fountain, new Vector3(0f, 3.3f, 0f), 2.2f, 0.4f, Marble);
        Cylinder("Bowl Water", fountain, new Vector3(0f, 3.32f, 0f), 1.9f, 0.4f, water);

        var sprayObject = new GameObject("Spray");
        sprayObject.transform.SetParent(fountain, false);
        sprayObject.transform.localPosition = new Vector3(0f, 3.6f, 0f);
        sprayObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var spray = sprayObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = spray.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.93f, 1f, 0.8f), new Color(1f, 1f, 1f, 0.9f));
        main.gravityModifier = 1f;
        main.maxParticles = 300;
        ParticleSystem.EmissionModule emission = spray.emission;
        emission.rateOverTime = 120f;
        ParticleSystem.ShapeModule shape = spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.15f;
        sprayObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
    }

    // ---------- Founder's Circle ----------

    // Runder gepflasterter Platz an der Parkecke, in der Mitte eine Bronzestatue des Pizzeria-Gründers, der eine Pizza hochhält.
    // Der Platz liegt auf Gehweghöhe über den Kreuzungen (wie ein großer Bordstein), er schaut zur Pizzeria
    private static void BuildFoundersCircle(Transform parent)
    {
        Material bronze = Mat("Bronze", new Color(0.55f, 0.4f, 0.22f), 0.6f);
        Vector2 at = CityLayout.FoundersCircle;
        Transform circle = new GameObject("Founder's Circle").transform;
        circle.SetParent(parent, false);
        circle.position = new Vector3(at.x, 0f, at.y);

        Cylinder("Paving", circle, new Vector3(0f, (RoadTop - 0.1f + Surface + 0.01f) / 2f, 0f), 15f, Surface + 0.01f - (RoadTop - 0.1f), Paving);
        Cylinder("Lawn", circle, new Vector3(0f, 0.6f, 0f), 7.5f, 0.3f, Grass);
        PrimitiveKit.Box("Pedestal", circle, new Vector3(0f, 0.75f + 1.5f, 0f), new Vector3(3.2f, 3f, 3.2f), Marble);

        Transform statue = PrimitiveKit.Pivot("Founder Statue", circle, new Vector3(0f, 3.75f, 0f));
        Vector2 toPizzeria = new Vector2(-8f, 0f) - at;
        statue.localRotation = Quaternion.Euler(0f, Mathf.Atan2(toPizzeria.x, toPizzeria.y) * Mathf.Rad2Deg, 0f);
        PrimitiveKit.Part("Leg L", PrimitiveType.Capsule, statue, new Vector3(-0.4f, 1.3f, 0f), new Vector3(0.7f, 1.3f, 0.7f), bronze);
        PrimitiveKit.Part("Leg R", PrimitiveType.Capsule, statue, new Vector3(0.4f, 1.3f, 0f), new Vector3(0.7f, 1.3f, 0.7f), bronze);
        PrimitiveKit.Part("Body", PrimitiveType.Capsule, statue, new Vector3(0f, 3.4f, 0f), new Vector3(1.7f, 1.4f, 1.2f), bronze);
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, statue, new Vector3(0f, 5.1f, 0f), Vector3.one * 1.1f, bronze);
        Cylinder("Chef Hat", statue, new Vector3(0f, 5.8f, 0f), 0.45f, 0.7f, bronze);
        PrimitiveKit.Part("Chef Hat Top", PrimitiveType.Sphere, statue, new Vector3(0f, 6.25f, 0f), new Vector3(1.2f, 0.6f, 1.2f), bronze);
        Renderer armUp = PrimitiveKit.Part("Arm Raised", PrimitiveType.Capsule, statue, new Vector3(1.05f, 5f, 0f), new Vector3(0.45f, 0.9f, 0.45f), bronze);
        armUp.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
        PrimitiveKit.Part("Arm Down", PrimitiveType.Capsule, statue, new Vector3(-1.1f, 3.3f, 0f), new Vector3(0.45f, 0.9f, 0.45f), bronze);
        Renderer pizza = Cylinder("Pizza", statue, new Vector3(1.45f, 6.15f, 0f), 1.3f, 0.15f, bronze);
        pizza.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
    }

    // ---------- Hilfen ----------

    private static Renderer Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
    {
        // Der Unity-Zylinder ist 2 m hoch und 1 m breit
        return PrimitiveKit.Part(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), material);
    }

    // Flaches Wegstück zwischen zwei Punkten (X/Z), knapp über dem Rasen
    private static void Walkway(string name, Transform parent, Vector2 from, Vector2 to, float width, Material material)
    {
        Vector2 delta = to - from;
        Vector2 middle = (from + to) / 2f;
        Renderer piece = PrimitiveKit.Box(name, parent, new Vector3(middle.x, Surface + 0.07f, middle.y), new Vector3(width, 0.06f, delta.magnitude + 0.3f), material);
        piece.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 0f);
    }

    private static void Tree(Transform parent, Vector3 at, float scale, System.Random random)
    {
        Material trunk = Mat("Tree Trunk");
        Material leaves = random.Next(2) == 0 ? Mat("Leaves") : Mat("Leaves Light");
        Transform tree = new GameObject("Tree").transform;
        tree.SetParent(parent, false);
        tree.position = at;
        tree.localScale = Vector3.one * scale;
        Cylinder("Trunk", tree, new Vector3(0f, 1.3f, 0f), 0.25f, 2.6f, trunk);
        PrimitiveKit.Part("Crown", PrimitiveType.Sphere, tree, new Vector3(0f, 3.6f, 0f), Vector3.one * 3.4f, leaves);
        PrimitiveKit.Part("Crown Top", PrimitiveType.Sphere, tree, new Vector3(0.5f, 4.8f, 0.3f), Vector3.one * 2.4f, leaves);
    }
}
