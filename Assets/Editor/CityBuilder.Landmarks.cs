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

    // Wiese mit Wegen von den Ecken zum Brunnen, Rundweg um den Brunnen (ohne Südhälfte: dort wächst ein Heckengarten,
    // Level 3 läuft deshalb nördlich herum), Ententeich mit Holzsteg am Südwest-Weg, Bäume dazwischen. Rasen und Wege
    // liegen knapp über Gehweg-Niveau (CityLayout.ParkLawnTop/ParkPathTop)
    private static void BuildPark(Transform parent, CityLayout.Block block, System.Random random)
    {
        Material path = Mat("Park Path", new Color(0.86f, 0.78f, 0.6f));
        Rect area = block.Lots;
        Slab("Lawn", parent, area.xMin - 1f, area.yMin - 1f, area.xMax + 1f, area.yMax + 1f, Surface - 0.1f, CityLayout.ParkLawnTop, Grass);

        Vector2 center = area.center;
        float ring = Mathf.Min(14f, area.height / 2f - 4f);
        Vector2[] corners =
        {
            new Vector2(area.xMin - 1f, area.yMin - 1f), new Vector2(area.xMax + 1f, area.yMin - 1f),
            new Vector2(area.xMin - 1f, area.yMax + 1f), new Vector2(area.xMax + 1f, area.yMax + 1f)
        };
        foreach (Vector2 corner in corners)
        {
            Vector2 dir = (corner - center).normalized;
            if (corner == corners[0])
            {
                // Südwest-Weg: über dem Teich ein Holzsteg statt des Wegs (die Stücke stecken etwas unter den Stegenden)
                Walkway("Path", parent, center + dir * ring, center + dir * (CityLayout.BoardwalkFrom + 0.3f), 3f, path);
                Boardwalk(parent, dir);
                Walkway("Path", parent, center + dir * (CityLayout.BoardwalkTo - 0.3f), corner, 3f, path);
                continue;
            }
            Walkway("Path", parent, corner, center + dir * ring, 3f, path);
        }

        // Rundweg nur über die Nordhälfte, von der Südwest- bis zur Südost-Einmündung (Winkel im Uhrzeigersinn ab Norden),
        // an beiden Enden etwas über die Einmündung hinaus, damit die 3 m breiten Diagonalwege sauber anschließen
        float AngleOf(Vector2 corner) => Mathf.Atan2(corner.x - center.x, corner.y - center.y) * Mathf.Rad2Deg;
        const float junctionMargin = 7f;
        float ringFrom = Mathf.Repeat(AngleOf(corners[0]), 360f) - junctionMargin;   // Südwest, ~231°
        float ringTo = Mathf.Repeat(AngleOf(corners[1]), 360f) + 360f + junctionMargin;   // Südost, ~489° (über Norden)
        int segments = Mathf.CeilToInt((ringTo - ringFrom) / 11.25f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Lerp(ringFrom, ringTo, i / (float)segments) * Mathf.Deg2Rad;
            float a1 = Mathf.Lerp(ringFrom, ringTo, (i + 1) / (float)segments) * Mathf.Deg2Rad;
            Walkway("Ring Path", parent, center + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * ring,
                center + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * ring, 3f, path);
        }
        HedgeGarden(parent, center, ring, ringTo - 360f, ringFrom, random);

        BuildFountain(parent, new Vector3(center.x, Surface + 0.05f, center.y), Mathf.Min(7f, ring - 3f));
        BuildPond(parent, random);
        BuildParkSign(parent, corners[1], center);

        BuildParkDecor(parent, area, center, ring, corners, random);
    }

    // Heckengarten auf der Südhälfte des Brunnenrings (zwischen den Winkeln from und to, im Uhrzeigersinn ab Norden):
    // dichte Hecke genau auf der Linie des Rundwegs, dahinter eine lockere zweite Reihe und Blumenbeete
    private static void HedgeGarden(Transform parent, Vector2 center, float ring, float from, float to, System.Random random)
    {
        Transform garden = new GameObject("Hedge Garden").transform;
        garden.SetParent(parent, false);
        Vector2 At(float angle, float radius) =>
            center + new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;

        float arc = (to - from) * Mathf.Deg2Rad * ring;
        int bushes = Mathf.CeilToInt(arc / 1.2f);
        for (int i = 0; i <= bushes; i++)
        {
            float angle = Mathf.Lerp(from + 1f, to - 1f, i / (float)bushes);
            Bush(garden, At(angle, ring - 0.6f), 1.25f + (float)random.NextDouble() * 0.3f, random);
            Bush(garden, At(angle + 2.5f, ring + 0.7f), 1.15f + (float)random.NextDouble() * 0.3f, random);
        }
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.Lerp(from, to, (i + 0.5f) / 4f);
            FlowerBed(garden, At(angle, ring + 3f), 1.1f, random);
        }
    }

    // Holzsteg des Südwest-Wegs über den Rand des Ententeichs (von BoardwalkFrom bis BoardwalkTo). Deck aus Stücken: An
    // jeder Lücke für Level 3 ein eigenes Stück mit der Mitte genau auf der Lücke (das schneidet Level 3 aus), dazwischen die
    // übrigen Stücke. Pfähle stehen an den Stückenden, außerhalb der Lücken-Ausschnitte
    private static void Boardwalk(Transform parent, Vector2 dir)
    {
        Material wood = Mat("Bridge Wood", new Color(0.55f, 0.38f, 0.22f));
        Material woodDark = Mat("Boardwalk Dark", new Color(0.4f, 0.27f, 0.16f));
        Transform walk = new GameObject("Boardwalk").transform;
        walk.SetParent(parent, false);
        Vector2 Point(float s) => CityLayout.ParkPathSW(s);
        const float width = 3f, thickness = 0.1f;
        float top = CityLayout.BoardwalkTop;

        void Deck(string name, float s0, float s1)
        {
            Vector2 middle = Point((s0 + s1) / 2f);
            Transform piece = new GameObject(name).transform;
            piece.SetParent(walk, false);
            piece.SetPositionAndRotation(new Vector3(middle.x, 0f, middle.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up));
            PrimitiveKit.Box("Deck", piece, new Vector3(0f, top - thickness / 2f, 0f), new Vector3(width, thickness, s1 - s0), wood);
            // Randbalken (dunkler), damit man die Stückkanten über dem Wasser sieht
            foreach (float x in new[] { -width / 2f + 0.06f, width / 2f - 0.06f })
            {
                PrimitiveKit.Box("Edge Beam", piece, new Vector3(x, top - thickness - 0.06f, 0f), new Vector3(0.12f, 0.12f, s1 - s0), woodDark);
            }
        }

        void Posts(float s)
        {
            Vector2 at = Point(s);
            Vector2 side = new Vector2(-dir.y, dir.x);
            foreach (float x in new[] { -width / 2f + 0.1f, width / 2f - 0.1f })
            {
                Vector2 post = at + side * x;
                PrimitiveKit.Part("Post", PrimitiveType.Cylinder, walk, new Vector3(post.x, top - 0.15f, post.y), new Vector3(0.18f, 0.25f, 0.18f), woodDark);
            }
        }

        float half = CityLayout.BoardwalkGapWidth / 2f;
        float from = CityLayout.BoardwalkFrom;
        foreach (float gap in CityLayout.BoardwalkGaps)
        {
            Deck("Deck", from, gap - half);
            Deck("Gap Deck", gap - half, gap + half);
            Posts(gap - half - 0.25f);
            Posts(gap + half + 0.25f);
            from = gap + half;
        }
        Deck("Deck", from, CityLayout.BoardwalkTo);
        Posts(CityLayout.BoardwalkFrom + 0.4f);
        Posts(CityLayout.BoardwalkTo - 0.4f);
    }

    // ---------- Park-Deko ----------

    // Bäume (dicht), Büsche, Findlinge, Blumenbeete, Laternen und Bänke neben den Wegen. Frei bleiben die Wege, die Teiche
    // und Bäche, und ein Streifen auf der Kameraseite der Wege, über die Level 3 läuft (sonst stünde etwas zwischen Kamera
    // und Spieler). Laternen und Bänke stehen dort nur auf der Seite hinter der Bahn
    private static void BuildParkDecor(Transform parent, Rect area, Vector2 center, float ring, Vector2[] corners, System.Random random)
    {
        Transform decor = new GameObject("Park Decor").transform;
        decor.SetParent(parent, false);
        Vector2 half = area.size / 2f;

        // Abstand zur nächsten Diagonale (alle vier Wege liegen auf den Diagonalen des Blocks)
        float ToDiagonal(Vector2 offset) => Mathf.Abs(Mathf.Abs(offset.x) * half.y - Mathf.Abs(offset.y) * half.x) / half.magnitude;
        bool OnPaths(Vector2 offset, float clear) =>
            ToDiagonal(offset) < 1.5f + clear || Mathf.Abs(offset.magnitude - ring) < 1.5f + clear;
        Vector2 RandomSpot() => new Vector2(area.xMin + 1.5f + (float)random.NextDouble() * (area.width - 3f),
            area.yMin + 1.5f + (float)random.NextDouble() * (area.height - 3f));

        // Erst die festen Stücke (Beete, Laternen, Bänke), dann das Verstreute drumherum: Bäume, Büsche und Steine halten
        // Abstand zu allem, was hier eingetragen ist
        var occupied = new System.Collections.Generic.List<(Vector2 at, float radius)>();
        void Occupy(Vector2 at, float radius) => occupied.Add((at, radius));
        bool Free(Vector2 at, float radius) => occupied.TrueForAll(o => Vector2.Distance(o.at, at) > o.radius + radius);

        // Blumenbeete: im Brunnenring und neben den Wegen
        for (int i = 0; i < 4; i++)
        {
            float angle = (45f + i * 90f) * Mathf.Deg2Rad;
            Occupy(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring - 3.3f), 1.4f);
            FlowerBed(decor, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring - 3.3f), 1.4f, random);
        }

        // Entlang der vier Wege: Laternen, Bänke und Blumenbeete im festen Abstand. Auf den südlichen Wegen (Level 3) nur auf
        // der Seite hinter der Bahn und nicht dort, wo Level 3 etwas hinstellt
        foreach (Vector2 corner in corners)
        {
            Vector2 dir = (corner - center).normalized;
            float length = (corner - center).magnitude;
            Vector2 left = new Vector2(-dir.y, dir.x), right = -left;
            bool south = corner.y < center.y;
            Vector2 far = left.y > 0f ? left : right;
            float[] keepClear = !south ? new float[0] : corner.x > center.x ? CityLayout.ParkDecorKeepClearSE : CityLayout.ParkDecorKeepClearSW;
            int index = 0;
            for (float s = ring + 5f; s < length - 4f; s += 6f, index++)
            {
                if (System.Array.Exists(keepClear, k => Mathf.Abs(k - s) < 4f) || NearPond(center + dir * s + far * 2.5f, 0.5f))
                {
                    continue;
                }
                Vector2 side = south ? far : (index % 2 == 0 ? left : right);
                Vector2 at = center + dir * s;
                switch (index % 4)
                {
                    case 0: ParkLamp(decor, at + side * 2.3f); Occupy(at + side * 2.3f, 0.5f); break;
                    case 1: ParkBench(decor, at + side * 2.4f, side); Occupy(at + side * 2.4f, 1f); break;
                    case 2: FlowerBed(decor, at + side * 4f, 1.1f, random); Occupy(at + side * 4f, 1.1f); break;
                }
            }
        }

        // Bäume: ~2.5-mal so dicht wie früher
        int trees = Mathf.FloorToInt(area.width * area.height / 38f);
        for (int i = 0; i < trees; i++)
        {
            Vector2 spot = RandomSpot();
            Vector2 offset = spot - center;
            if (offset.magnitude < ring + 4f || OnPaths(offset, 2f) || InCameraStrip(offset, center, ring, corners) || NearPond(spot, 3f) || !Free(spot, 2f))
            {
                continue;
            }
            Tree(decor, new Vector3(spot.x, Surface + 0.05f, spot.y), 0.75f + (float)random.NextDouble() * 0.7f, random);
        }

        // Büsche: verstreut und als Hecke am Parkrand (nicht an den Ecken, dort beginnen die Wege)
        int bushes = Mathf.FloorToInt(area.width * area.height / 90f);
        for (int i = 0; i < bushes; i++)
        {
            Vector2 spot = RandomSpot();
            Vector2 offset = spot - center;
            if (offset.magnitude < ring + 2f || OnPaths(offset, 1.2f) || NearPond(spot, 0.8f) || !Free(spot, 1f))
            {
                continue;
            }
            Bush(decor, spot, 0.7f + (float)random.NextDouble() * 0.6f, random);
        }
        Vector2 min = area.min + Vector2.one * 0.8f, max = area.max - Vector2.one * 0.8f;
        for (float x = min.x; x <= max.x; x += 3f)
        {
            foreach (float z in new[] { min.y, max.y })
            {
                HedgeBush(decor, new Vector2(x, z), corners, random);
            }
        }
        for (float z = min.y; z <= max.y; z += 3f)
        {
            foreach (float x in new[] { min.x, max.x })
            {
                HedgeBush(decor, new Vector2(x, z), corners, random);
            }
        }

        // Findlinge: Gruppen auf dem Rasen und am Ufer des großen Teichs (nicht auf der Wegseite)
        for (int i = 0; i < 28; i++)
        {
            Vector2 spot = RandomSpot();
            Vector2 offset = spot - center;
            if (offset.magnitude < ring + 2f || OnPaths(offset, 1.5f) || NearPond(spot, 1f) || !Free(spot, 1.2f))
            {
                continue;
            }
            Boulders(decor, spot, random);
        }
        for (int i = 0; i < 7; i++)
        {
            float angle = 0.3f + i * Mathf.PI * 2f / 7f;
            Vector2 local = new Vector2(Mathf.Cos(angle) * (CityLayout.PondHalfLength + 1.3f), Mathf.Sin(angle) * (CityLayout.PondHalfWidth + 1.3f));
            if (local.y < 0f)
            {
                continue;
            }
            Vector2 spot = CityLayout.PondCenter - CityLayout.ParkDirSW * local.x + CityLayout.ParkNormalNW * local.y;
            Boulders(decor, spot, random);
        }

    }

    // Wo die Kamera von Level 3 steht: auf der Südseite der beiden südlichen Diagonalen (8 m vor der Bahn, 10 m bleiben frei)
    // und außen um den Rundweg (Nordhälfte, ohne den Heckengarten), dort steht sie 9 m hinter der Bahn und schaut nach innen
    private static bool InCameraStrip(Vector2 offset, Vector2 center, float ring, Vector2[] corners)
    {
        const float strip = 10f;
        foreach (Vector2 corner in corners)
        {
            if (corner.y > center.y)
            {
                continue;
            }
            Vector2 dir = (corner - center).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            if (normal.y > 0f)
            {
                normal = -normal;
            }
            float along = Vector2.Dot(offset, dir);
            float side = Vector2.Dot(offset, normal);
            if (along > ring - 2f && along < (corner - center).magnitude + 2f && side > 0f && side < strip)
            {
                return true;
            }
        }
        float distance = offset.magnitude;
        if (distance < ring || distance > ring + 12f)
        {
            return false;
        }
        // Winkel im Uhrzeigersinn ab Norden; der Heckengarten liegt zwischen der Südost- und der Südwest-Einmündung
        float Angle(Vector2 v) => Mathf.Repeat(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg, 360f);
        float angle = Angle(offset);
        return angle < Angle(corners[1] - center) || angle > Angle(corners[0] - center);
    }

    // Busch aus 2-3 Kugeln in verschiedenen Grüntönen
    private static void Bush(Transform parent, Vector2 at, float scale, System.Random random)
    {
        Material[] greens = { Mat("Leaves"), Mat("Leaves Light"), Mat("Bush Dark", new Color(0.16f, 0.4f, 0.2f)) };
        Transform bush = new GameObject("Bush").transform;
        bush.SetParent(parent, false);
        bush.position = new Vector3(at.x, CityLayout.ParkLawnTop, at.y);
        bush.localScale = Vector3.one * scale;
        int parts = 2 + random.Next(2);
        for (int i = 0; i < parts; i++)
        {
            var offset = new Vector3(((float)random.NextDouble() - 0.5f) * 0.9f, 0.4f, ((float)random.NextDouble() - 0.5f) * 0.9f);
            float size = 0.9f + (float)random.NextDouble() * 0.5f;
            PrimitiveKit.Part("Leaves", PrimitiveType.Sphere, bush, offset, new Vector3(size, size * 0.8f, size), greens[random.Next(greens.Length)]);
        }
    }

    private static void HedgeBush(Transform parent, Vector2 at, Vector2[] corners, System.Random random)
    {
        if (System.Array.Exists(corners, corner => Vector2.Distance(corner, at) < 6f))
        {
            return;
        }
        Bush(parent, at, 0.9f + (float)random.NextDouble() * 0.3f, random);
    }

    // 2-4 graue, abgeflachte Steine nebeneinander
    private static void Boulders(Transform parent, Vector2 at, System.Random random)
    {
        Material stone = Mat("Boulder", new Color(0.55f, 0.54f, 0.52f));
        Material stoneDark = Mat("Boulder Dark", new Color(0.42f, 0.41f, 0.4f));
        int count = 2 + random.Next(3);
        for (int i = 0; i < count; i++)
        {
            float size = 0.4f + (float)random.NextDouble() * 0.8f;
            var position = new Vector3(at.x + ((float)random.NextDouble() - 0.5f) * 1.6f, CityLayout.ParkLawnTop + size * 0.2f, at.y + ((float)random.NextDouble() - 0.5f) * 1.6f);
            Renderer rock = PrimitiveKit.Part("Boulder", PrimitiveType.Sphere, parent, position, new Vector3(size * 1.2f, size * 0.7f, size), random.Next(2) == 0 ? stone : stoneDark);
            rock.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, ((float)random.NextDouble() - 0.5f) * 20f);
        }
    }

    // Rundes Beet: dunkle Erde mit bunten Blüten-Kugeln
    private static void FlowerBed(Transform parent, Vector2 at, float radius, System.Random random)
    {
        Material soil = Mat("Soil", new Color(0.3f, 0.2f, 0.13f));
        Material[] blossoms =
        {
            Mat("Flower Red", new Color(0.9f, 0.18f, 0.2f)), Mat("Flower Yellow", new Color(1f, 0.85f, 0.2f)),
            Mat("Flower Purple", new Color(0.6f, 0.3f, 0.8f)), Mat("Flower White", new Color(0.97f, 0.96f, 0.92f)),
        };
        Transform bed = new GameObject("Flower Bed").transform;
        bed.SetParent(parent, false);
        bed.position = new Vector3(at.x, CityLayout.ParkLawnTop, at.y);
        Cylinder("Soil", bed, new Vector3(0f, 0.04f, 0f), radius, 0.08f, soil);
        int count = Mathf.RoundToInt(radius * radius * 28f);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * (radius - 0.15f);
            var position = new Vector3(Mathf.Cos(angle) * distance, 0.18f + (float)random.NextDouble() * 0.08f, Mathf.Sin(angle) * distance);
            PrimitiveKit.Part("Blossom", PrimitiveType.Sphere, bed, position, Vector3.one * 0.22f, blossoms[random.Next(blossoms.Length)]);
        }
    }

    // Altmodische Parklaterne: dunkelgrüner Mast, Laterne mit leuchtendem Glas
    private static void ParkLamp(Transform parent, Vector2 at)
    {
        Material green = Mat("Park Lamp", new Color(0.12f, 0.22f, 0.16f), 0.5f);
        Transform lamp = new GameObject("Park Lamp").transform;
        lamp.SetParent(parent, false);
        lamp.position = new Vector3(at.x, CityLayout.ParkLawnTop, at.y);
        Cylinder("Base", lamp, new Vector3(0f, 0.15f, 0f), 0.18f, 0.3f, green);
        Cylinder("Post", lamp, new Vector3(0f, 1.6f, 0f), 0.06f, 3.2f, green);
        PrimitiveKit.Box("Glass", lamp, new Vector3(0f, 3.45f, 0f), new Vector3(0.32f, 0.45f, 0.32f), Mat("Lamp Glow", new Color(1f, 0.92f, 0.65f)));
        PrimitiveKit.Box("Cap", lamp, new Vector3(0f, 3.75f, 0f), new Vector3(0.45f, 0.12f, 0.45f), green);
    }

    // Parkbank neben dem Weg (nur Deko, ohne Collider), Sitzfläche zum Weg hin. away = Richtung vom Weg weg
    private static void ParkBench(Transform parent, Vector2 at, Vector2 away)
    {
        Material wood = Mat("Bridge Wood", new Color(0.55f, 0.38f, 0.22f));
        Material green = Mat("Park Lamp", new Color(0.12f, 0.22f, 0.16f), 0.5f);
        Transform bench = new GameObject("Park Bench").transform;
        bench.SetParent(parent, false);
        bench.position = new Vector3(at.x, CityLayout.ParkLawnTop, at.y);
        bench.rotation = Quaternion.LookRotation(new Vector3(away.x, 0f, away.y), Vector3.up);
        PrimitiveKit.Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), wood);
        PrimitiveKit.Box("Backrest", bench, new Vector3(0f, 0.8f, 0.25f), new Vector3(1.8f, 0.4f, 0.06f), wood);
        foreach (float x in new[] { -0.75f, 0.75f })
        {
            PrimitiveKit.Box("Leg", bench, new Vector3(x, 0.22f, 0.05f), new Vector3(0.08f, 0.44f, 0.55f), green);
        }
    }

    // Liegt der Punkt im Ententeich (um margin vergrößert)?
    private static bool NearPond(Vector2 point, float margin)
    {
        return InOval(point, CityLayout.PondCenter, CityLayout.PondHalfLength + margin, CityLayout.PondHalfWidth + margin);
    }

    private static bool InOval(Vector2 point, Vector2 center, float a, float b)
    {
        Vector2 local = point - center;
        float along = Vector2.Dot(local, CityLayout.ParkDirSW) / a;
        float across = Vector2.Dot(local, CityLayout.ParkNormalNW) / b;
        return along * along + across * across < 1f;
    }

    // Ententeich (Oval) mit Steinrand, Schilf, Seerosen und einem kleinen Steg am Nordufer. Seine Südostkante liegt unter
    // dem Holzsteg des Südwest-Wegs. Wasser etwas über dem Rasen, damit nichts flackert
    private static void BuildPond(Transform parent, System.Random random)
    {
        Material wood = Mat("Bridge Wood", new Color(0.55f, 0.38f, 0.22f));
        float waterTop = CityLayout.ParkLawnTop + 0.04f;
        float a = CityLayout.PondHalfLength, b = CityLayout.PondHalfWidth;

        // Weg-Seite ist lokal -Z
        Transform pond = PondBasin(parent, "Duck Pond", CityLayout.PondCenter, a, b, 9, 10, -1f, random);
        // Holzsteg vom Nordufer ins Wasser
        PrimitiveKit.Box("Jetty", pond, new Vector3(a * 0.3f, waterTop + 0.15f, b - 1.5f), new Vector3(1.4f, 0.1f, 4f), wood);
        foreach (float x in new[] { -0.6f, 0.6f })
        {
            PrimitiveKit.Box("Jetty Post", pond, new Vector3(a * 0.3f + x, waterTop - 0.1f, b - 3.3f), new Vector3(0.12f, 0.6f, 0.12f), wood);
        }
    }

    // Ovaler Teich: Steinrand, Wasser, Schilf-Büschel am Rand (nicht auf der Hälfte zum Weg, pathSide = Vorzeichen von
    // lokal Z dorthin), Seerosenblätter. Lokal X = entlang des Wegs, lokal Z = nach Nordwesten
    private static Transform PondBasin(Transform parent, string name, Vector2 center, float a, float b, int reedClusters, int lilies,
        float pathSide, System.Random random)
    {
        Material water = Mat("Fountain Water", new Color(0.3f, 0.6f, 0.85f), 0.9f);
        Material stone = Mat("Pond Stone", new Color(0.62f, 0.6f, 0.56f));
        Material reed = Mat("Reed", new Color(0.33f, 0.5f, 0.2f));
        Material cattail = Mat("Cattail", new Color(0.4f, 0.26f, 0.14f));
        Material lily = Mat("Lily Pad", new Color(0.25f, 0.55f, 0.22f));
        Vector2 normal = CityLayout.ParkNormalNW;
        Transform pond = new GameObject(name).transform;
        pond.SetParent(parent, false);
        pond.position = new Vector3(center.x, 0f, center.y);
        // LookRotation(normal) dreht lokal X nach Kreuz(up, normal), also entlang des Wegs (das Oval ist symmetrisch)
        pond.rotation = Quaternion.LookRotation(new Vector3(normal.x, 0f, normal.y), Vector3.up);

        PrimitiveKit.Part("Rim", PrimitiveType.Cylinder, pond, new Vector3(0f, (CityLayout.ParkLawnTop + 0.03f) / 2f, 0f),
            new Vector3(a * 2f + 1f, (CityLayout.ParkLawnTop + 0.03f) / 2f, b * 2f + 1f), stone);
        PrimitiveKit.Part("Water", PrimitiveType.Cylinder, pond, new Vector3(0f, (CityLayout.ParkLawnTop + 0.04f) / 2f, 0f),
            new Vector3(a * 2f, (CityLayout.ParkLawnTop + 0.04f) / 2f, b * 2f), water);
        float waterTop = CityLayout.ParkLawnTop + 0.04f;

        for (int i = 0; i < reedClusters; i++)
        {
            float angle = (i + 0.3f) * Mathf.PI * 2f / reedClusters;
            Vector3 edge = new Vector3(Mathf.Cos(angle) * (a - 0.4f), waterTop, Mathf.Sin(angle) * (b - 0.4f));
            if (edge.z * pathSide > 0f)
            {
                continue;
            }
            for (int r = 0; r < 6; r++)
            {
                float height = 0.6f + (float)random.NextDouble() * 0.6f;
                Vector3 offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.8f;
                PrimitiveKit.Part("Reed", PrimitiveType.Cylinder, pond, edge + offset + Vector3.up * height / 2f, new Vector3(0.05f, height / 2f, 0.05f), reed);
                if (r % 2 == 0)
                {
                    PrimitiveKit.Part("Cattail", PrimitiveType.Capsule, pond, edge + offset + Vector3.up * (height + 0.05f), new Vector3(0.09f, 0.12f, 0.09f), cattail);
                }
            }
        }
        for (int i = 0; i < lilies; i++)
        {
            // Nur auf der Seite weg vom Weg (dort liegt beim Ententeich der Steg darüber)
            float across = (-0.1f + (float)random.NextDouble() * 0.7f) * b * -pathSide;
            var spot = new Vector3(((float)random.NextDouble() * 2f - 1f) * a * 0.7f, waterTop + 0.01f, across);
            PrimitiveKit.Part("Lily Pad", PrimitiveType.Cylinder, pond, spot, new Vector3(0.7f, 0.01f, 0.7f), lily);
        }
        return pond;
    }

    // Parkschild "PESTO PARK" am Eingang beim Founder's Circle, neben dem Weg, Schrift zur Kamera von Level 3
    private static void BuildParkSign(Transform parent, Vector2 corner, Vector2 center)
    {
        Material green = Mat("Park Sign Green", new Color(0.12f, 0.36f, 0.2f));
        Material wood = Mat("Bridge Wood", new Color(0.55f, 0.38f, 0.22f));
        Vector2 walk = (center - corner).normalized;
        Vector2 right = new Vector2(walk.y, -walk.x);
        Vector2 at = corner + walk * 3f + right * 2.6f;
        Transform sign = new GameObject("Park Sign").transform;
        sign.SetParent(parent, false);
        sign.position = new Vector3(at.x, 0f, at.y);
        // Lesbare Seite (-Z des Texts) zeigt nach links vom Spieler, wo die Kamera steht
        sign.rotation = Quaternion.LookRotation(new Vector3(right.x, 0f, right.y), Vector3.up);
        foreach (float x in new[] { -1.3f, 1.3f })
        {
            PrimitiveKit.Box("Post", sign, new Vector3(x, Surface + 1.1f, 0f), new Vector3(0.15f, 2.2f, 0.15f), wood);
        }
        PrimitiveKit.Box("Board", sign, new Vector3(0f, Surface + 1.8f, 0f), new Vector3(3f, 0.8f, 0.1f), green);
        PrimitiveKit.Text("Text", sign, new Vector3(0f, Surface + 1.8f, -0.06f), new Vector2(2.5f, 0.6f), "PESTO PARK", new Color(0.97f, 0.93f, 0.75f)).fontSizeMax = 5f;
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
        // Sockel 1.5 m hoch auf dem Rasen (Oberkante 0.75)
        const float pedestalHeight = 1.5f;
        PrimitiveKit.Box("Pedestal", circle, new Vector3(0f, 0.75f + pedestalHeight / 2f, 0f), new Vector3(3.2f, pedestalHeight, 3.2f), Marble);

        Transform statue = PrimitiveKit.Pivot("Founder Statue", circle, new Vector3(0f, 0.75f + pedestalHeight, 0f));
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
        Renderer piece = PrimitiveKit.Box(name, parent, new Vector3(middle.x, CityLayout.ParkPathTop - 0.03f, middle.y), new Vector3(width, 0.06f, delta.magnitude + 0.3f), material);
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
