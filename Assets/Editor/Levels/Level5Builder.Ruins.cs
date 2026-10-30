using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Zerstörtes Hochhausviertel (Stümpfe, umgestürzte Türme, Schutt, Rauch) und der Partikel-Helfer Particles
public static partial class Level5Builder
{
    // ---------- Zerstörtes Hochhausviertel ----------

    // Beidseits der Straße werden die Hochhäuser ausgeschnitten (samt Gehweg-Platte darunter, darum ein eigener Boden),
    // dafür stehen dort Stümpfe mit gezackter Bruchkante, umgestürzte Türme (gleiche Fassade, liegend, vom Weg weg gekippt)
    // und Schuttberge. Dazu Rauchsäulen mit Feuer am Fuß. Alles Kulisse ohne Collider, außer dem Schutt auf der Bahn
    private static void BuildRuins(Transform link)
    {
        Transform ruins = new GameObject("Destroyed District").transform;
        ruins.SetParent(propParent, false);
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = PrimitiveKit.Material("Assets/Materials/City/Rubble.mat", PrimitiveKit.LitShader, new Color(0.55f, 0.52f, 0.48f));
        Material ground = PrimitiveKit.Material("Assets/Materials/City/Ruin Ground.mat", PrimitiveKit.LitShader, new Color(0.45f, 0.43f, 0.4f));
        Material scorched = PrimitiveKit.Material("Assets/Materials/City/Scorched.mat", PrimitiveKit.LitShader, new Color(0.2f, 0.18f, 0.16f));

        var random = new System.Random(555);
        foreach ((Rect area, float away) in new[] { (RuinsWest, -1f), (RuinsEast, 1f) })
        {
            LevelBuildKit.Cutout(link, away < 0f ? "Ruins West" : "Ruins East", area.xMin, area.xMax, area.yMin, area.yMax);
            PrimitiveKit.Box("Ruin Ground", ruins, new Vector3(area.center.x, CityLayout.Surface - 0.5f, area.center.y), new Vector3(area.width, 1f, area.height), ground);

            // Grundstücke in Reihen (16 m tief wie die Hochhäuser, 2 m Gasse dazwischen) über die ganze Fläche
            float frontX = away > 0f ? area.xMin : area.xMax;
            int rows = Mathf.FloorToInt(area.width / 18f);
            for (int row = 0; row < rows; row++)
            {
                float z = area.yMin;
                while (z < area.yMax - 6f)
                {
                    float width = 12f + (float)random.NextDouble() * 8f;
                    float rowX = frontX + away * (8f + row * 18f);
                    var lot = new Rect(rowX - 8f, z, 16f, Mathf.Min(width, area.yMax - z));
                    z += width;
                    if (System.Array.Exists(KeepClear, clear => clear.Overlaps(lot)) || LizardBed.Overlaps(lot))
                    {
                        continue;
                    }
                    double roll = random.NextDouble();
                    if (roll < 0.4)
                    {
                        Stump(ruins, lot, 5f + (float)random.NextDouble() * 16f, facade, rubble, random);
                    }
                    else if (roll < 0.7)
                    {
                        FallenTower(ruins, lot, away, facade, rubble, area, random);
                    }
                    else
                    {
                        RubblePile(ruins, new Vector3(lot.center.x, CityLayout.Surface, lot.center.y), 5f, rubble, facade, random);
                    }
                }
            }
        }

        // Dazwischen überall Schutt, damit keine leeren Flächen bleiben
        foreach (Rect area in new[] { RuinsWest, RuinsEast })
        {
            for (int i = 0; i < 18; i++)
            {
                var spot = new Vector2(area.xMin + 3f + (float)random.NextDouble() * (area.width - 6f), area.yMin + 3f + (float)random.NextDouble() * (area.height - 6f));
                if (System.Array.Exists(KeepClear, clear => clear.Contains(spot)) || LizardBed.Contains(spot))
                {
                    continue;
                }
                RubblePile(ruins, new Vector3(spot.x, CityLayout.Surface, spot.y), 2f + (float)random.NextDouble() * 3f, rubble, facade, random);
            }
        }

        // Spuren auf der Straße: Krater, Schutt, ein paar brennende Stellen (nicht auf dem Ostgehweg, dort läuft die Bahn)
        foreach (float z in new[] { 270f, 302f })
        {
            PrimitiveKit.Cylinder("Crater", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.02f, z), 4.5f, 0.44f, scorched);
            PrimitiveKit.Cylinder("Crater Rim", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.1f, z), 5.7f, 0.3f, rubble);
        }
        for (int i = 0; i < 8; i++)
        {
            float z = 225f + (float)random.NextDouble() * 85f;
            float x = -43f + (float)random.NextDouble() * 5f;
            if (TailRoad.Contains(new Vector2(x, z)))
            {
                continue;
            }
            RubblePile(ruins, new Vector3(x, CityLayout.RoadTop, z), 2f, rubble, facade, random);
        }
        // Querstraßen im Viertel sind verschüttet (Four Cheese nach Osten und Westen; im Westen nur die Südhälfte, auf dem
        // Nordgehweg läuft die Bahn)
        RubblePile(ruins, new Vector3(-58f, CityLayout.RoadTop, 212f), 4f, rubble, facade, random);
        RubblePile(ruins, new Vector3(-20f, CityLayout.RoadTop, 215f), 5f, rubble, facade, random);

        // Rauchsäulen mit Feuer am Fuß
        foreach (Vector3 spot in new[] { new Vector3(-88f, 0.5f, 231f), new Vector3(-12f, 0.5f, 236f), new Vector3(10f, 0.5f, 280f), new Vector3(-85f, 0.5f, 300f), new Vector3(-60f, 0.5f, 312f) })
        {
            Smoke(ruins, spot);
        }
        CrushedBed(ruins, rubble, facade, scorched, random);

        // Stadt-Deko (Laternen, Schilder, Bäume) dort, wo Schwanz und Kopf liegen, ausblenden. Westlich der Oregano Street nur
        // in der Salami Street (sonst träfe es die Eckhäuser, deren Mitte im Ausschnitt läge)
        LevelBuildKit.Cutout(link, "Lizard Tail", TailRoad.xMin, TailRoad.xMax, TailRoad.yMin, TailRoad.yMax, keepGround: true);
        LevelBuildKit.Cutout(link, "Lizard Oregano", -120.5f, RuinsWest.xMin, LizardBed.yMin, LizardBed.yMax, keepGround: true);
        LevelBuildKit.Cutout(link, "Lizard Head", -132f, -120.5f, 255.5f, 274.5f, keepGround: true);
    }

    // Wo die Echse hinfällt: dunkler, versengter Boden, flachgedrückte Wandstücke und niedriger Schutt, an den Rändern zu
    // Haufen zusammengeschoben. Nichts davon ist höher als ~1.5 m, damit die liegende Echse nirgends hindurchragt
    private static void CrushedBed(Transform parent, Material rubble, Material facade, Material scorched, System.Random random)
    {
        Transform bed = new GameObject("Crushed Bed").transform;
        bed.SetParent(parent, false);
        Rect area = LizardBed;
        PrimitiveKit.Box("Scorched Ground", bed, new Vector3(area.center.x, CityLayout.Surface + 0.01f, area.center.y), new Vector3(area.width - 1f, 0.02f, area.height - 1f), scorched);
        for (int i = 0; i < 22; i++)
        {
            var spot = new Vector3(area.xMin + 2f + (float)random.NextDouble() * (area.width - 4f), CityLayout.Surface, area.yMin + 2f + (float)random.NextDouble() * (area.height - 4f));
            float size = 3f + (float)random.NextDouble() * 5f;
            Renderer slab = PrimitiveKit.Box("Flattened Wall", bed, spot + Vector3.up * 0.2f, new Vector3(size, 0.4f + (float)random.NextDouble() * 0.4f, size * (0.5f + (float)random.NextDouble() * 0.5f)),
                random.NextDouble() < 0.5 ? facade : rubble);
            slab.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 8f - 4f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 8f - 4f);
        }
        // Zusammengeschobener Schutt am Rand (vorne zur Four Cheese Avenue und hinten zum Roboter)
        for (int i = 0; i < 14; i++)
        {
            float x = area.xMin + 3f + (float)random.NextDouble() * (area.width - 6f);
            float z = i % 2 == 0 ? area.yMin + 1.5f : area.yMax - 1.5f;
            RubblePile(bed, new Vector3(x, CityLayout.Surface, z), 2.5f, rubble, facade, random);
        }
    }

    // Stumpf eines Hochhauses: unterer Teil mit Fassade, oben gezackte Bruchstücke, etwas Schutt drumherum
    private static void Stump(Transform parent, Rect lot, float height, Material facade, Material rubble, System.Random random)
    {
        Transform stump = new GameObject("Tower Stump").transform;
        stump.SetParent(parent, false);
        stump.position = new Vector3(lot.center.x, CityLayout.Surface, lot.center.y);
        PrimitiveKit.Box("Base", stump, new Vector3(0f, height / 2f, 0f), new Vector3(lot.width - 0.5f, height, lot.height - 0.5f), facade);
        for (int i = 0; i < 4; i++)
        {
            float w = lot.width * (0.25f + (float)random.NextDouble() * 0.3f);
            float h = 2f + (float)random.NextDouble() * 6f;
            Vector3 position = new Vector3(((float)random.NextDouble() - 0.5f) * (lot.width - w), height + h / 2f - 1f, ((float)random.NextDouble() - 0.5f) * (lot.height - w));
            Renderer shard = PrimitiveKit.Box("Shard", stump, position, new Vector3(w, h, w * 0.8f), facade);
            shard.transform.localRotation = Quaternion.Euler(random.Next(-15, 15), random.Next(360), random.Next(-15, 15));
        }
        RubblePile(parent, stump.position + new Vector3(lot.width * 0.4f, 0f, 0f), 3f, rubble, facade, random);
    }

    // Umgestürzter Turm: liegt mit der Längsachse vom Weg weg (away = Richtung in X), leicht schräg, ein Ende auf einem
    // Schuttberg. Bleibt im Ruinen-Rechteck (sonst ragte er in die Straße oder die Nachbarblöcke)
    private static void FallenTower(Transform parent, Rect lot, float away, Material facade, Material rubble, Rect area, System.Random random)
    {
        float width = Mathf.Min(lot.height - 1f, 12f + (float)random.NextDouble() * 4f);
        float maxLength = away > 0f ? area.xMax - lot.xMin - 2f : lot.xMax - area.xMin - 2f;
        float length = Mathf.Min(maxLength, 34f + (float)random.NextDouble() * 20f);
        float startX = away > 0f ? lot.xMin + 1f : lot.xMax - 1f;
        var center = new Vector3(startX + away * length / 2f, CityLayout.Surface + width / 2f - 1.5f, lot.center.y);
        Renderer tower = PrimitiveKit.Box("Fallen Tower", parent, center, new Vector3(length, width, width), facade);
        tower.transform.localRotation = Quaternion.Euler(random.Next(-8, 8), random.Next(-12, 12), away * random.Next(2, 7));
        // Abbruchkante am Fuß: Schutt
        RubblePile(parent, new Vector3(startX, CityLayout.Surface, lot.center.y), 4f, rubble, facade, random);
    }

    // Schuttberg aus schräg liegenden Brocken, ein paar mit Fassade (abgebrochene Wandstücke)
    private static void RubblePile(Transform parent, Vector3 at, float radius, Material rubble, Material facade, System.Random random)
    {
        Transform pile = new GameObject("Rubble").transform;
        pile.SetParent(parent, false);
        pile.position = at;
        int pieces = 4 + random.Next(4);
        for (int i = 0; i < pieces; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = (float)random.NextDouble() * radius;
            float size = 0.8f + (float)random.NextDouble() * radius * 0.5f;
            var scale = new Vector3(size * (1f + (float)random.NextDouble()), size * 0.6f, size);
            Renderer chunk = PrimitiveKit.Box("Chunk", pile, new Vector3(Mathf.Sin(angle) * distance, scale.y * 0.3f, Mathf.Cos(angle) * distance), scale,
                random.NextDouble() < 0.35 ? facade : rubble);
            chunk.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 40f - 20f);
        }
    }

    // Dunkle Rauchsäule, unten Flammen. Partikel im Weltraum, laufen dauerhaft
    private static void Smoke(Transform parent, Vector3 at)
    {
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Transform spot = PrimitiveKit.Pivot("Smoke Column", parent, Vector3.zero);
        spot.position = at;
        ParticleSystem smoke = Particles("Smoke", spot, particles, new Color(0.18f, 0.17f, 0.17f, 0.7f), new Color(0.35f, 0.33f, 0.32f, 0.5f),
            lifetime: 9f, speed: 4f, size: new Vector2(4f, 9f), rate: 6f, radius: 2.5f, growth: 2.5f);
        ParticleSystem fire = Particles("Fire", spot, particles, new Color(1f, 0.45f, 0.08f, 0.9f), new Color(1f, 0.8f, 0.2f, 0.9f),
            lifetime: 0.9f, speed: 3f, size: new Vector2(1.2f, 2.6f), rate: 25f, radius: 2f, growth: 0.3f);
        fire.transform.localPosition = Vector3.up * 0.3f;
    }

    // Partikelsystem für Rauch und Feuer: steigt nach oben, wird größer und blendet aus
    public static ParticleSystem Particles(string name, Transform parent, Material material, Color colorA, Color colorB,
        float lifetime, float speed, Vector2 size, float rate, float radius, float growth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f + growth)));
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        system.Play();
        return system;
    }
}
