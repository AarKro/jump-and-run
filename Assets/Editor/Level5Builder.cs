using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Baut Level 5 (Kaiju-Finale) komplett neu auf. Läuft in der gemeinsamen Stadt wie Level 2–4.
// Menü: Tools > Jump and Run > City > Rebuild Level 5 (Kaiju). Erneut ausführen überschreibt Handänderungen.
// Gibt es die Szene noch nicht, wird sie als Kopie von Level 2 angelegt (Kamera, Player, GameManager, HUD, Goal mit Kundenhaus)
// und in die Build Settings hinter Level 4 eingetragen.
//
// Die Stadt wird angegriffen: Die Riesenechse steht mitten im Hochhausviertel am Mozzarella Blvd (z 254, die Füße links und
// rechts der Straße) und verwüstet es. Das Level schneidet die Hochhäuser dort aus und legt sie zerstört hin (Stümpfe,
// umgestürzte Türme, Schutt, Rauch). Route (der Spieler läuft nach LINKS, Richtung -u): aus der Pizzeria nach Westen,
// rechts auf den Ostgehweg des Mozzarella Blvd (x -33, gleiche Ecke wie Level 3) und geradeaus nach Norden über die Crust
// Street, am umgestürzten Gründer-Denkmal vorbei, über Olive Lane und Four Cheese Avenue, zwischen den Beinen der Echse
// hindurch zum letzten heilen Haus kurz vor der Calzone Road (z 305).
// Die Bahn ist in +u-Richtung definiert (Häuser links davon): Sie beginnt beim Kunden und endet hinter der Pizzeria.
// Auf der Margherita Avenue ist u = Welt-X, auf dem Mozzarella Blvd u = -31 - z.
//
// Stand: Gerüst (Arbeitsschritt 2 von Level 5). Feueratem, Roboter, Intro und Leute in Panik kommen als eigene Schritte.
public static class Level5Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level05.unity";
    private const string TemplateScenePath = "Assets/Scenes/levels/level02.unity";
    private const string PreviousScenePath = "Assets/Scenes/levels/level04.unity";
    private const float KeepWarmTime = 100f;

    // Mozzarella Blvd: Laufbahn auf dem Ostgehweg, Bordstein bei -34, Fassaden bei -30, Fahrbahn bis -46 (wie Level 3)
    private const float MozzarellaX = -33f;
    private const float AvenueCurbLocalZ = -1f;
    private const float FacadeLocalZ = 3f;
    private const float LaneTopZ = 312f;
    // Kundenhaus nördlich des Roboter-Fußes (der steht später bei z 280 … 296 auf der Ostseite)
    private const float GoalZ = 305f;
    private const float MainEndX = 16f;
    // Ecke Margherita → Mozzarella: Radius so, dass ganze Meter z auf ganze Meter u fallen (wie Level 3)
    private static readonly float CornerRadius = 2f / (2f - Mathf.PI / 2f);

    // Straßen, die die Bahn quert (Mitte, 12 m Fahrbahn): Crust Street östlich (z 112), Olive Lane (z 165), Four Cheese (z 215)
    private static readonly float[] CrossingZ = { 112f, 165f, 215f };

    // Zerstörtes Hochhausviertel: Grundstücke beidseits des Mozzarella Blvd (Welt-Rechtecke von/bis), ab der Four Cheese Avenue
    private static readonly Rect RuinsWest = Rect.MinMaxRect(-100f, 222f, -50f, 315f);
    private static readonly Rect RuinsEast = Rect.MinMaxRect(-30f, 222f, 45f, 315f);
    // Hier bleibt Platz: Kundenhaus (Ost) und die Füße der Monster (x -40 ± 14, Echse z 254 + 3, Roboter z 290)
    private static readonly Rect[] KeepClear =
    {
        Rect.MinMaxRect(-31f, GoalZ - 7f, -14f, GoalZ + 7f),
        Rect.MinMaxRect(-34f, 245f, -16f, 266f), Rect.MinMaxRect(-64f, 245f, -46f, 266f),
        Rect.MinMaxRect(-34f, 280f, -16f, 300f), Rect.MinMaxRect(-64f, 280f, -46f, 300f),
    };
    // ---------- Feueratem ----------
    //   z 150 … 205   Bereich, in dem die Echse Feuer speit (sobald der Spieler dort ist, im Takt)
    //   z 168 … 202   Feuer auf der Bahn: tödlich, außer direkt hinter einer Deckung (dort wird die Pizza warm)
    //   z 180, 194    Deckungen: umgestürzte Busse auf dem Gehweg (2.4 m hoch, man kann drüberspringen)
    //   Ziel des Strahls zwischen Bahn und Straße bei z 186, die Flammen reichen in den Park (Bäume fangen Feuer)
    private const float BreathZoneFromZ = 150f, BreathZoneToZ = 205f;
    private const float FireFromZ = 168f, FireToZ = 202f;
    private static readonly float[] CoverZ = { 180f, 194f };
    private const float CoverLength = 5f, CoverHeight = 2.4f, ShelterLength = 3f;
    private static readonly Vector3 BreathTarget = new Vector3(-40f, CityLayout.Surface, 186f);
    private static readonly Rect BurnArea = Rect.MinMaxRect(-80f, 166f, -46f, 208f);

    // ---------- Roboter ----------
    //   z 210   Spieler überquert die Four Cheese Avenue: Zwischensequenz, der Roboter stürzt herab, die Echse dreht sich um,
    //           ab dann ringen beide (kein Feuer mehr)
    //   z 285   Spieler zwischen den Beinen des Roboters: Zwischensequenz, Aufwärtshaken, die Echse kippt nach Westen um
    private const float RobotArrivalZ = 210f, FinaleZ = 285f;

    // Mittelpunkt der Monster wie früher in der Stadt (CityLayout.KaijuFight): Echse 16 m südlich davon, Roboter 20 m nördlich
    private static Vector2 KaijuCenter => CityLayout.KaijuFight;

    private static Transform groundParent, gameplayParent, propParent;
    private static readonly List<float> pieceStartU = new List<float>();

    public static float MozzarellaU(float z) => pieceStartU[0] + (LaneTopZ - z);

    [MenuItem("Tools/Jump and Run/City/Rebuild Level 5 (Kaiju)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Level 5 neu bauen",
                "Löscht alles in Level 5 außer Kamera, Player, GameManager, HUD, KillZone, Goal und StartPoint und baut es neu auf. Fortfahren?",
                "Neu bauen", "Abbrechen"))
        {
            return;
        }
        Build();
    }

    // Ohne Dialog, für den Aufruf über Skripte (MCP)
    public static void Build()
    {
        EnsureScene();
        var scene = LevelBuildKit.OpenAndClear(ScenePath);
        BuildLane();
        groundParent = LevelBuildKit.Group("Level Ground");
        gameplayParent = LevelBuildKit.Group("Level Gameplay");
        propParent = LevelBuildKit.Group("Level Props");

        Transform link = LevelBuildKit.CityLink("Pizzeria");
        BuildGround(link);
        BuildFallenStatue(link);
        BuildRuins(link);
        BuildKaiju();
        BuildGameplay();

        LevelBuildKit.SetupEssentials(Mathf.Round(MozzarellaU(GoalZ)), "1", KeepWarmTime,
            new Vector3(MozzarellaX, -20f, 150f), new Vector3(40f, 1f, 340f),
            "No. 1, Mozzarella Blvd (the last house standing)", new Vector3(-60f, 70f, -90f));
        SetupIntroExpose();
        LevelBuildKit.Save(scene);
        Debug.Log($"Level5Builder: Level 5 (Kaiju) gebaut. Bahn u {LevelBuildKit.Lane.StartU:F2} bis {LevelBuildKit.Lane.EndU:F2}, Mozzarella u(z) = {MozzarellaU(0f):F2} - z");
    }

    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
            {
                throw new System.Exception($"Level5Builder: Konnte {TemplateScenePath} nicht nach {ScenePath} kopieren");
            }
        }
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(entry => entry.path == ScenePath))
        {
            return;
        }
        int after = scenes.FindIndex(entry => entry.path == PreviousScenePath);
        scenes.Insert(after + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- Bahn ----------

    private static void BuildLane()
    {
        float mainStartX = MozzarellaX + CornerRadius;
        var pieces = new List<LanePath.Piece>
        {
            LevelBuildKit.Straight(LaneTopZ - CornerRadius),     // Mozzarella Blvd (Kunde, Kaiju, Ruinen, Denkmal)
            LevelBuildKit.Arc(CornerRadius, 90f),
            LevelBuildKit.Straight(MainEndX - mainStartX),       // Margherita Avenue (Pizzeria)
        };
        float startU = mainStartX - (pieces[0].length + pieces[1].length);
        pieceStartU.Clear();
        float u = startU;
        foreach (LanePath.Piece piece in pieces)
        {
            pieceStartU.Add(u);
            u += piece.length;
        }
        LevelBuildKit.BuildLane(new Vector3(MozzarellaX, 0f, LaneTopZ), -90f, startU, pieces);
    }

    // ---------- Boden ----------

    private static void BuildGround(Transform link)
    {
        // Querstraßen haben in der Stadt keinen Collider: unsichtbarer Laufboden auf Gehweghöhe (Autos fahren hier keine,
        // die Stadt ist auf der Flucht)
        foreach (float z in CrossingZ)
        {
            LevelBuildKit.WalkGround($"Walk Crossing z{z:F0}", MozzarellaU(z + 6.5f), MozzarellaU(z - 6.5f), CityLayout.Surface, groundParent);
        }
    }

    // Der Gründer liegt umgestürzt auf seinem Platz (der Founder's Circle liegt mitten auf dem Mozzarella Blvd, die Kamera 8 m
    // neben der Bahn stünde sonst in der Statue). Ein kleiner Ausschnitt nimmt Sockel, Statue, Rasen und Pflaster der Stadt
    // weg (alle haben ihre Mitte dort), das Level legt Pflaster und Rasen neu hin, dazu den zerbrochenen Sockel und die Statue
    private static void BuildFallenStatue(Transform link)
    {
        Vector2 at = CityLayout.FoundersCircle;
        LevelBuildKit.Cutout(link, "Founder Statue", at.x - 2.5f, at.x + 2.5f, at.y - 2.5f, at.y + 2.5f);
        Material paving = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Paving.mat");
        Material grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Grass.mat");
        Material marble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Marble.mat");
        Material bronze = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Bronze.mat");
        Transform circle = new GameObject("Fallen Founder").transform;
        circle.SetParent(propParent, false);
        circle.position = new Vector3(at.x, 0f, at.y);
        const float roadTop = CityLayout.RoadTop;
        Cylinder("Paving", circle, new Vector3(0f, (roadTop - 0.1f + CityLayout.Surface + 0.01f) / 2f, 0f), 15f, CityLayout.Surface + 0.01f - (roadTop - 0.1f), paving);
        Cylinder("Lawn", circle, new Vector3(0f, 0.6f, 0f), 7.5f, 0.3f, grass);
        // Sockel abgebrochen, schief
        Renderer pedestal = PrimitiveKit.Box("Broken Pedestal", circle, new Vector3(0f, 0.95f, 0f), new Vector3(3.2f, 0.4f, 3.2f), marble);
        pedestal.transform.localRotation = Quaternion.Euler(4f, 20f, -6f);
        // Statue liegt nach Nordwesten (weg von der Bahn) auf dem Rasen, die Pizza ist ihr aus der Hand gefallen
        Transform statue = PrimitiveKit.Pivot("Founder Statue (fallen)", circle, new Vector3(-1.5f, 1.1f, 1.5f));
        statue.localRotation = Quaternion.Euler(0f, -45f, 0f) * Quaternion.Euler(0f, 0f, 90f);
        PrimitiveKit.Part("Leg L", PrimitiveType.Capsule, statue, new Vector3(-0.4f, -1.3f, 0f), new Vector3(0.7f, 1.3f, 0.7f), bronze);
        PrimitiveKit.Part("Leg R", PrimitiveType.Capsule, statue, new Vector3(0.4f, -1.3f, 0f), new Vector3(0.7f, 1.3f, 0.7f), bronze);
        PrimitiveKit.Part("Body", PrimitiveType.Capsule, statue, new Vector3(0f, 0.8f, 0f), new Vector3(1.7f, 1.4f, 1.2f), bronze);
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, statue, new Vector3(0f, 2.5f, 0f), Vector3.one * 1.1f, bronze);
        Cylinder("Chef Hat", statue, new Vector3(0f, 3.2f, 0f), 0.45f, 0.7f, bronze);
        Cylinder("Pizza", circle, new Vector3(3.5f, 0.82f, -2f), 1.3f, 0.15f, bronze).transform.localRotation = Quaternion.Euler(8f, 0f, 14f);
        // Schutt vom Sockel
        var random = new System.Random(130);
        for (int i = 0; i < 6; i++)
        {
            Vector3 position = new Vector3((float)random.NextDouble() * 5f - 3.5f, 0.85f, (float)random.NextDouble() * 5f - 1f);
            Renderer chunk = PrimitiveKit.Box("Marble Chunk", circle, position, Vector3.one * (0.4f + (float)random.NextDouble() * 0.6f), marble);
            chunk.transform.localRotation = Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360));
        }
    }

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
                    if (System.Array.Exists(KeepClear, clear => clear.Overlaps(lot)))
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
                if (System.Array.Exists(KeepClear, clear => clear.Contains(spot)))
                {
                    continue;
                }
                RubblePile(ruins, new Vector3(spot.x, CityLayout.Surface, spot.y), 2f + (float)random.NextDouble() * 3f, rubble, facade, random);
            }
        }

        // Spuren auf der Straße: Krater, Schutt, ein paar brennende Stellen (nicht auf dem Ostgehweg, dort läuft die Bahn)
        foreach (float z in new[] { 232f, 270f })
        {
            Cylinder("Crater", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.02f, z), 4.5f, 0.44f, scorched);
            Cylinder("Crater Rim", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.1f, z), 5.7f, 0.3f, rubble);
        }
        for (int i = 0; i < 8; i++)
        {
            float z = 225f + (float)random.NextDouble() * 85f;
            RubblePile(ruins, new Vector3(-43f + (float)random.NextDouble() * 5f, CityLayout.RoadTop, z), 2f, rubble, facade, random);
        }
        // Querstraßen im Viertel sind verschüttet (Four Cheese nach Osten und Westen)
        RubblePile(ruins, new Vector3(-58f, CityLayout.RoadTop, 215f), 5f, rubble, facade, random);
        RubblePile(ruins, new Vector3(-20f, CityLayout.RoadTop, 215f), 5f, rubble, facade, random);

        // Rauchsäulen mit Feuer am Fuß
        foreach (Vector3 spot in new[] { new Vector3(-75f, 0.5f, 240f), new Vector3(-12f, 0.5f, 236f), new Vector3(10f, 0.5f, 280f), new Vector3(-85f, 0.5f, 300f), new Vector3(-60f, 0.5f, 312f) })
        {
            Smoke(ruins, spot);
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

    // ---------- Kaiju ----------

    // Beide Monster wie früher in der Stadt, aber: Die Echse schaut anfangs nach Süden (zum Spieler und zum Park, dorthin
    // speit sie Feuer), der Roboter ist noch nicht da (kommt später aus dem Himmel, eigener Arbeitsschritt). Der Ringkampf
    // (KaijuFight) ist darum erst mal aus
    private static KaijuBattle battleComponent;

    // Intro-Vorspann: Die Kamera gleitet von Westen über den Ruinen auf die Echse zu, während sie den Park in Brand setzt
    // (Strahl und Echse von der Seite, beide im Bild). Danach fliegt die Kamera von dort zum Kunden
    private static readonly Vector3 ExposeCameraFrom = new Vector3(-150f, 30f, 168f);
    private static readonly Vector3 ExposeCameraTo = new Vector3(-125f, 42f, 190f);
    private static readonly Vector3 ExposeLook = new Vector3(-40f, 32f, 222f);
    private const float ExposeDuration = 5f;

    private static void SetupIntroExpose()
    {
        GameObject mainCamera = GameObject.Find("Main Camera");
        IntroFlyover intro = mainCamera != null ? mainCamera.GetComponent<IntroFlyover>() : null;
        if (intro == null || battleComponent == null)
        {
            return;
        }
        var so = new SerializedObject(intro);
        so.FindProperty("exposeActor").objectReferenceValue = battleComponent;
        so.FindProperty("exposeCameraFrom").vector3Value = ExposeCameraFrom;
        so.FindProperty("exposeCameraTo").vector3Value = ExposeCameraTo;
        so.FindProperty("exposeLook").vector3Value = ExposeLook;
        so.FindProperty("exposeDuration").floatValue = ExposeDuration;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildKaiju()
    {
        CityBuilder.KaijuParts kaiju = CityBuilder.BuildKaijuForLevel(gameplayParent, KaijuCenter);
        kaiju.lizard.localRotation = Quaternion.Euler(0f, 180f, 0f);
        kaiju.robot.gameObject.SetActive(false);
        kaiju.fight.enabled = false;

        FireBreath breath = BuildFireBreath(kaiju);
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        // Düsen unter den Füßen des Roboters (Flammen nach unten) und ein Rauchschweif am Rücken, nur während er herabstürzt
        var thrusters = new List<ParticleSystem>();
        foreach (float x in new[] { -14f, 14f })
        {
            Transform foot = PrimitiveKit.Pivot("Thruster", kaiju.robot, new Vector3(x, 0f, 2f));
            ParticleSystem flame = Particles("Flame", foot, particles, new Color(1f, 0.5f, 0.1f, 0.95f), new Color(0.6f, 0.85f, 1f, 0.95f),
                lifetime: 0.5f, speed: 35f, size: new Vector2(3f, 6f), rate: 90f, radius: 2f, growth: 1f);
            flame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            thrusters.Add(Stopped(flame));
        }
        ParticleSystem trail = Particles("Smoke Trail", PrimitiveKit.Pivot("Trail", kaiju.robot, new Vector3(0f, 45f, -6f)), particles,
            new Color(0.4f, 0.4f, 0.42f, 0.8f), new Color(0.75f, 0.75f, 0.78f, 0.6f), lifetime: 4f, speed: 2f, size: new Vector2(8f, 16f), rate: 60f, radius: 4f, growth: 1.5f);
        thrusters.Add(Stopped(trail));

        // Staubwolken: beim Aufschlag des Roboters und wo die Echse hinfällt (nur Emit)
        Vector3 robotLanding = kaiju.robot.position;
        ParticleSystem landingDust = Dust(gameplayParent, "Landing Dust", robotLanding, 16f, particles);
        ParticleSystem fallDust = Dust(gameplayParent, "Fall Dust", new Vector3(KaijuCenter.x - 40f, CityLayout.Surface, KaijuCenter.y - 16f), 30f, particles);

        battleComponent = kaiju.root.gameObject.AddComponent<KaijuBattle>();
        var battle = new SerializedObject(battleComponent);
        battle.FindProperty("lizardRoot").objectReferenceValue = kaiju.lizard;
        battle.FindProperty("lizardUpper").objectReferenceValue = kaiju.lizardUpper;
        battle.FindProperty("lizardHead").objectReferenceValue = kaiju.lizardHead;
        battle.FindProperty("breath").objectReferenceValue = breath;
        battle.FindProperty("robotRoot").objectReferenceValue = kaiju.robot;
        battle.FindProperty("robotUpper").objectReferenceValue = kaiju.robotUpper;
        battle.FindProperty("robotArmLeft").objectReferenceValue = kaiju.robotArmLeft;
        battle.FindProperty("robotArmRight").objectReferenceValue = kaiju.robotArmRight;
        battle.FindProperty("fight").objectReferenceValue = kaiju.fight;
        PrimitiveKit.SetReferences(battle, "thrusters", thrusters.ToArray());
        battle.FindProperty("landingDust").objectReferenceValue = landingDust;
        battle.FindProperty("fallDust").objectReferenceValue = fallDust;
        battle.FindProperty("breathFromU").floatValue = MozzarellaU(BreathZoneToZ);
        battle.FindProperty("breathToU").floatValue = MozzarellaU(BreathZoneFromZ);
        battle.FindProperty("arrivalU").floatValue = MozzarellaU(RobotArrivalZ);
        battle.FindProperty("finaleU").floatValue = MozzarellaU(FinaleZ);
        battle.ApplyModifiedPropertiesWithoutUndo();

        // Bildschirm-Wackeln an der Kamera
        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null && mainCamera.GetComponent<CameraShake>() == null)
        {
            mainCamera.AddComponent<CameraShake>();
        }
    }

    // Partikelsystem, das erst auf Befehl startet
    private static ParticleSystem Stopped(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    // Staubwolke: graue Brocken quellen in einer Halbkugel auf und sinken langsam, nur per Emit
    private static ParticleSystem Dust(Transform parent, string name, Vector3 at, float radius, Material material)
    {
        ParticleSystem dust = Particles(name, parent, material, new Color(0.55f, 0.52f, 0.48f, 0.85f), new Color(0.75f, 0.72f, 0.68f, 0.7f),
            lifetime: 3.5f, speed: 14f, size: new Vector2(6f, 14f), rate: 0f, radius: radius, growth: 1.5f);
        dust.transform.position = at;
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius;
        ParticleSystem.MainModule main = dust.main;
        main.gravityModifier = 0.15f;
        main.maxParticles = 400;
        return Stopped(dust);
    }

    // Maul mit Glut am Kopf, Strahl (drei immer dickere Zylinder = Kegel), Flammen am Boden auf der Bahn, an der Aufschlagstelle
    // und im Park, ein oranges Licht, Deckungen auf der Bahn, Vorlage für brennende Bäume
    private static FireBreath BuildFireBreath(CityBuilder.KaijuParts kaiju)
    {
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Material outer = PrimitiveKit.Material("Assets/Materials/City/Fire Outer.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.42f, 0.08f));
        Material core = PrimitiveKit.Material("Assets/Materials/City/Fire Core.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.88f, 0.4f));
        Transform root = new GameObject("Fire Breath").transform;
        root.SetParent(gameplayParent, false);

        // Maul: vorne an der Schnauze (Kopf-Pivot: Schnauze bei z 12, Kiefer bei y 0), dort die Glut
        Transform mouth = PrimitiveKit.Pivot("Mouth", kaiju.lizardHead, new Vector3(0f, 1.5f, 16.5f));
        Transform glow = PrimitiveKit.Part("Mouth Glow", PrimitiveType.Sphere, mouth, Vector3.zero, Vector3.one, core).transform;

        // Strahl: Pivot im Maul, schaut zum Ziel, lokal Z wird auf die Länge gestreckt (FireBreath). Die Zylinder (Achse Y) sind
        // um 90° um X gedreht, damit ihre Achse entlang Z liegt; jeder deckt ein Drittel der Länge ab, nach vorn immer dicker
        Transform beam = PrimitiveKit.Pivot("Beam", root, Vector3.zero);
        float[] outerRadius = { 3f, 6f, 10f };
        for (int i = 0; i < 3; i++)
        {
            float center = (i + 0.5f) / 3f;
            Renderer shell = PrimitiveKit.Part($"Beam Outer {i + 1}", PrimitiveType.Cylinder, beam, new Vector3(0f, 0f, center), new Vector3(outerRadius[i] * 2f, 1f / 6f + 0.01f, outerRadius[i] * 2f), outer);
            shell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Renderer inner = PrimitiveKit.Part($"Beam Core {i + 1}", PrimitiveType.Cylinder, beam, new Vector3(0f, 0f, center), new Vector3(outerRadius[i] * 1.1f, 1f / 6f + 0.01f, outerRadius[i] * 1.1f), core);
            inner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Feuerstrom: Partikel schießen aus dem Maul zum Ziel (FireBreath dreht das System jeden Frame dorthin, lokal Z = Richtung)
        ParticleSystem stream = Particles("Beam Stream", root, particles, new Color(1f, 0.4f, 0.05f, 0.9f), new Color(1f, 0.85f, 0.3f, 0.9f),
            lifetime: 1.3f, speed: 70f, size: new Vector2(3f, 7f), rate: 140f, radius: 1f, growth: 2.5f);
        stream.transform.localRotation = Quaternion.identity;
        ParticleSystem.ShapeModule streamShape = stream.shape;
        streamShape.shapeType = ParticleSystemShapeType.Cone;
        streamShape.angle = 6f;
        streamShape.radius = 1.5f;
        ParticleSystem.MainModule streamMain = stream.main;
        streamMain.playOnAwake = false;
        stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Flammen am Boden: auf der Bahn (der ganze Feuerbereich), an der Aufschlagstelle und im Park
        float laneMidZ = (FireFromZ + FireToZ) / 2f;
        var fires = new List<ParticleSystem>
        {
            GroundFire(root, "Lane Fire", new Vector3(MozzarellaX - 2f, CityLayout.Surface, laneMidZ), new Vector3(8f, FireToZ - FireFromZ), particles, 140f),
            GroundFire(root, "Impact Fire", BreathTarget, new Vector3(16f, 16f), particles, 120f),
            GroundFire(root, "Park Fire", new Vector3(BurnArea.center.x, CityLayout.ParkLawnTop, BurnArea.center.y), new Vector3(BurnArea.width, BurnArea.height), particles, 160f),
        };

        var lightObject = new GameObject("Impact Light");
        lightObject.transform.SetParent(root, false);
        lightObject.transform.position = BreathTarget + Vector3.up * 6f;
        Light impact = lightObject.AddComponent<Light>();
        impact.type = LightType.Point;
        impact.color = new Color(1f, 0.55f, 0.2f);
        impact.range = 50f;
        impact.intensity = 40f;
        impact.enabled = false;

        // Vorlage für eine brennende Baumkrone (FireBreath kopiert sie pro Baum)
        Transform treeFireHolder = PrimitiveKit.Pivot("Tree Fire Template", root, Vector3.zero);
        ParticleSystem treeFire = Particles("Fire", treeFireHolder, particles, new Color(1f, 0.45f, 0.08f, 0.9f), new Color(1f, 0.8f, 0.2f, 0.9f),
            lifetime: 1f, speed: 3f, size: new Vector2(2f, 4.5f), rate: 30f, radius: 1.6f, growth: 0.4f);
        Particles("Smoke", treeFire.transform, particles, new Color(0.2f, 0.19f, 0.18f, 0.6f), new Color(0.35f, 0.33f, 0.32f, 0.4f),
            lifetime: 5f, speed: 2.5f, size: new Vector2(1.5f, 3f), rate: 3f, radius: 1f, growth: 2f).transform.localRotation = Quaternion.identity;
        treeFire.transform.SetParent(root, true);
        Object.DestroyImmediate(treeFireHolder.gameObject);
        treeFire.gameObject.SetActive(false);

        // Deckungen: umgestürzte Busse auf dem Gehweg, sicherer Bereich direkt dahinter (vom Strahl abgewandt = Süden = +u)
        var covers = new List<FireBreath.Cover>();
        foreach (float z in CoverZ)
        {
            float u = Mathf.Round(MozzarellaU(z));
            WreckedBus(u);
            covers.Add(new FireBreath.Cover
            {
                shelterFrom = u + CoverLength / 2f,
                shelterTo = u + CoverLength / 2f + ShelterLength,
                top = CityLayout.Surface + CoverHeight,
            });
        }

        var breath = new SerializedObject(root.gameObject.AddComponent<FireBreath>());
        breath.FindProperty("mouth").objectReferenceValue = mouth;
        breath.FindProperty("target").vector3Value = BreathTarget;
        breath.FindProperty("beam").objectReferenceValue = beam;
        breath.FindProperty("beamParticles").objectReferenceValue = stream;
        PrimitiveKit.SetReferences(breath, "groundFires", fires.ToArray());
        breath.FindProperty("impactLight").objectReferenceValue = impact;
        breath.FindProperty("mouthGlow").objectReferenceValue = glow;
        breath.FindProperty("hitFromU").floatValue = MozzarellaU(FireToZ);
        breath.FindProperty("hitToU").floatValue = MozzarellaU(FireFromZ);
        breath.FindProperty("burnArea").rectValue = BurnArea;
        breath.FindProperty("treeFireTemplate").objectReferenceValue = treeFire;
        SerializedProperty coverList = breath.FindProperty("covers");
        coverList.arraySize = covers.Count;
        for (int i = 0; i < covers.Count; i++)
        {
            SerializedProperty entry = coverList.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("shelterFrom").floatValue = covers[i].shelterFrom;
            entry.FindPropertyRelative("shelterTo").floatValue = covers[i].shelterTo;
            entry.FindPropertyRelative("top").floatValue = covers[i].top;
        }
        breath.ApplyModifiedPropertiesWithoutUndo();
        return (FireBreath)breath.targetObject;
    }

    // Flammenteppich (Rechteck X/Z in Welt-Größe), brennt nur, solange FireBreath ihn abspielt
    private static ParticleSystem GroundFire(Transform parent, string name, Vector3 at, Vector3 size, Material material, float rate)
    {
        ParticleSystem fire = Particles(name, parent, material, new Color(1f, 0.4f, 0.06f, 0.95f), new Color(1f, 0.82f, 0.25f, 0.95f),
            lifetime: 1f, speed: 4.5f, size: new Vector2(2.5f, 5.5f), rate: rate, radius: 1f, growth: 0.5f);
        fire.transform.position = at;
        ParticleSystem.ShapeModule shape = fire.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Das System ist um -90° um X gedreht (Partikel fliegen nach oben): lokal X/Y = Welt X/Z
        shape.scale = new Vector3(size.x, size.y, 0.1f);
        ParticleSystem.MainModule main = fire.main;
        main.playOnAwake = false;
        fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return fire;
    }

    // Umgestürzter Stadtbus auf dem Gehweg (Deckung vor dem Feueratem): gelb, auf der Seite liegend, Dach zur Kamera,
    // Fensterband, Räder zur Hauswand. Begehbar und zum Drüberspringen (Layer Ground)
    private static void WreckedBus(float u)
    {
        Material yellow = PrimitiveKit.Material("Assets/Materials/City/Bus Yellow.mat", PrimitiveKit.LitShader, new Color(0.95f, 0.72f, 0.12f));
        Material windows = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Window Glass.mat");
        Material wheel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wheel.mat");
        Material soot = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Scorched.mat");
        Quaternion frame = LevelBuildKit.Frame(u);
        var bus = new GameObject($"Wrecked Bus u{u:F0}") { layer = 3 };
        bus.transform.SetParent(gameplayParent, false);
        bus.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(u) + Vector3.up * (CityLayout.Surface + CoverHeight / 2f), frame);
        Transform t = bus.transform;
        PrimitiveKit.Box("Body", t, Vector3.zero, new Vector3(CoverLength, CoverHeight, 2.5f), yellow);
        PrimitiveKit.Box("Window Band", t, new Vector3(0f, 0.1f, -1.26f), new Vector3(CoverLength - 0.6f, 0.8f, 0.02f), windows);
        // Ruß an der Seite, die dem Feuer zugewandt ist (lokal -X = Norden)
        PrimitiveKit.Box("Soot", t, new Vector3(-CoverLength / 2f - 0.01f, 0f, 0f), new Vector3(0.02f, CoverHeight - 0.1f, 2.4f), soot);
        foreach (float x in new[] { -1.6f, 1.6f })
        {
            foreach (float y in new[] { -0.6f, 0.6f })
            {
                Renderer w = PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, t, new Vector3(x, y, 1.3f), new Vector3(0.9f, 0.15f, 0.9f), wheel);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        PrimitiveKit.AddBoxCollider(bus, Vector3.zero, new Vector3(CoverLength, CoverHeight, 2.5f));
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Östlich der Pizzeria geht es nicht weiter (wie in Level 2–4)
        LevelBuildKit.Place("Car Crash", 4, 0, gameplayParent);
        // Hinter dem Kunden: unsichtbare Wand (die Bahn ginge sonst geradeaus weiter)
        var wall = new GameObject("North End Wall");
        wall.transform.SetParent(gameplayParent, false);
        float wallU = pieceStartU[0] - 1f;
        wall.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(wallU) + Vector3.up * 5f, LevelBuildKit.Frame(wallU));
        PrimitiveKit.AddBoxCollider(wall, Vector3.zero, new Vector3(0.5f, 10f, 6f));

        // Schutt auf dem Gehweg zum Drüberspringen (Layer Ground) im zerstörten Viertel
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        foreach (float z in new[] { 228f, 241f, 276f })
        {
            float u = MozzarellaU(z);
            Quaternion frame = LevelBuildKit.Frame(u);
            var chunk = new GameObject($"Rubble Obstacle z{z:F0}") { layer = 3 };
            chunk.transform.SetParent(gameplayParent, false);
            chunk.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(u) + Vector3.up * (CityLayout.Surface + 0.6f), frame);
            PrimitiveKit.Box("Block", chunk.transform, Vector3.zero, new Vector3(1.6f, 1.2f, 2.2f), rubble);
            PrimitiveKit.Box("Top", chunk.transform, new Vector3(0.3f, 0.7f, 0.2f), new Vector3(1f, 0.5f, 1.4f), rubble).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            PrimitiveKit.AddBoxCollider(chunk, Vector3.zero, new Vector3(1.6f, 1.2f, 2.2f));
        }
    }

    private static Renderer Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
    {
        return PrimitiveKit.Part(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), material);
    }
}
