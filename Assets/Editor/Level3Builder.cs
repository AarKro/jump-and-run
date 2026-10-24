using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Baut Level 3 (Pesto Park & Founder's Circle) komplett neu auf. Läuft in der gemeinsamen Stadt wie Level 2.
// Menü: Tools > Jump and Run > City > Rebuild Level 3 (Pesto Park). Erneut ausführen überschreibt Handänderungen.
// Gibt es die Szene noch nicht, wird sie als Kopie von Level 2 angelegt (Kamera, Player, GameManager, HUD, Goal ...)
// und in die Build Settings hinter Level 2 eingetragen.
//
// Route (der Spieler läuft nach LINKS, also Richtung -u): aus der Pizzeria nach Westen, rechts in den Mozzarella Blvd
// (Ostseite, x -33, Baustelle mit Gruben), über die Crust Street (Autos), in einer S-Kurve südlich und westlich um die
// Gründer-Statue herum (sie bleibt rechts, die Kamera also außen über der Straße), dann den diagonalen Parkweg hinauf
// zum Brunnen, im Uhrzeigersinn halb um den Brunnen und den Südwest-Weg hinunter zum Ententeich (Kunde: Picknick).
// Die Bahn ist in +u-Richtung definiert (Häuser bzw. Brunnen links davon): Sie beginnt beim Kunden und endet hinter
// der Pizzeria. Auf der Margherita Avenue ist u = Welt-X, auf dem Mozzarella Blvd u = -31 - z.
//
// Fertig: Baustelle (Gruben, Scherenbühnen, Gerüste, 2 Chilis), Überweg, Laufboden im Park und Ziel. Bauwagen-Checkpoint,
// Frisbee-Spieler, Hund, Enten, Teich und das dritte Chili kommen als eigene Arbeitsschritte dazu.
public static class Level3Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level03.unity";
    private const string TemplateScenePath = "Assets/Scenes/levels/level02.unity";
    private const float KeepWarmTime = 120f;

    // Mozzarella Blvd: Laufbahn auf dem östlichen Gehweg, Bordstein bei -34, Fassaden bei -30, Fahrbahn bis -46
    private const float MozzarellaX = -33f;
    private const float AvenueCurbLocalZ = -1f;      // Lane-Rahmen: Bordstein 1 m vor der Laufbahn
    private const float AvenueRoadEndLocalZ = -13f;  // gegenüberliegender Bordstein (12 m Fahrbahn)
    private const float FacadeLocalZ = 3f;
    // Crust Street östlich des Mozzarella Blvd (Mitte z 112, Avenue = 12 m Fahrbahn)
    private const float CrustZ = 112f;
    private const float CrustNorthCurb = CrustZ + 6f;
    private const float MainEndX = 16f;

    // Ecke Margherita → Mozzarella. Der Radius ist so gewählt, dass auf dem Mozzarella Blvd ganze Meter z auf ganze
    // Meter u fallen (u = -31 - z): Der PrefabGridEnforcer rastet Prefabs auf ganze u ein, sonst säße der Überweg
    // ein Stück neben der echten Crust Street. u(z) = (MozzarellaX + R) - R·π/2 - (z - R) = MozzarellaX + R·(2 - π/2) - z
    private static readonly float CornerRadius = 2f / (2f - Mathf.PI / 2f);

    // S-Kurve um die Statue: links nach Westen (südlich der Statue vorbei), rechts nach Norden (westlich vorbei),
    // dann links auf den diagonalen Parkweg. Alle Stücke bleiben mindestens 8.5 m von der Statue weg (Rasenring 7.5 m)
    private const float StatueSouthRadius = 3.5f;   // Gerade danach bei z 121.5, 8.5 m südlich der Statue
    private const float StatueWestRadius = 4f;      // Gerade danach bei x = Parkecke (-49), 9 m westlich der Statue
    private const float ParkTurnRadius = 7f;        // auf den Diagonalweg
    // Kleine Bögen vom Diagonalweg auf den Brunnenring und vom Ring auf den Südwest-Weg
    private const float RingFilletRadius = 4f;
    // Bahn-Anfang auf dem Südwest-Weg (Abstand von der Brunnenmitte), Kunde 8 m davor
    private const float LaneStartFromCenter = 63f;
    private const float GoalFromCenter = 55f;

    // Laufboden: Pflaster des Founder's Circle bzw. Parkwege (beides ohne Collider in der Stadt)
    private const float PavingTop = CityLayout.Surface + 0.01f;
    private const float ParkPathTop = CityLayout.Surface + 0.1f;

    // ---------- Baustelle (Mozzarella Blvd, Welt-z in Laufrichtung, Gehweg-Oberkante 0.5, Sprung ~3.2 m hoch / ~5.5 m weit) ----------
    //   z   9        Straßensperre
    //   z  19        Grube 3.5 m zum Aufwärmen
    //   z  36.5–51.5 Brücken-Bühne 1 mitten in 15 m Grube (je 6 m Lücke, zu weit für einen flachen Sprung): unten (-1.5)
    //                aufspringen, hochfahren, oben (+2.5) abspringen. Chili 1 schwebt über ihrer oberen Stellung
    //   z  58        Grube 3.5 m
    //   z  66 / 70   Aufzugs-Bühne auf dem Gehweg (1.5 → 6.5) neben einem Gerüst auf 6.5 mit Chili 2 (nur per Bühne erreichbar)
    //   z  79        Gerüst auf 3.5 an der Kante der zweiten Brücken-Grube: Von dort erreicht man die Bühne auch oben
    //   z  80.5–95.5 Brücken-Bühne 2, startet oben (gegenläufig zu Bühne 1)
    //   z 103        Straßensperre
    private static readonly (float z, float gap, bool barriers)[] Pits =
    {
        (19f, 3.5f, true), (44f, 15f, true), (58f, 3.5f, true), (88f, 15f, false),
    };
    // Höhe der Grubensohle (Oberkante "Dirt" im Construction Pit)
    private const float PitFloorY = -3.9f;
    private const float BridgeLowY = -2f, BridgeHighY = 2f;      // Wurzel; Oberkante je 0.5 höher
    private const float ElevatorLowY = 1f, ElevatorHighY = 6f;
    private static readonly float[] RoadClosureZ = { 9f, 103f };

    private static Transform groundParent, gameplayParent, propParent;

    // Aus dem Stadtplan, gleiche Rechnung wie CityBuilder.BuildPark
    private static Rect ParkArea => System.Array.Find(CityLayout.Blocks, block => block.Kind == CityLayout.BlockKind.Park).Lots;
    private static Vector2 ParkCenter => ParkArea.center;
    private static float RingRadius => Mathf.Min(14f, ParkArea.height / 2f - 4f);
    private static Vector2 ParkCornerSE => new Vector2(ParkArea.xMax + 1f, ParkArea.yMin - 1f);
    // Richtung Brunnen → Südost-Ecke; der Südwest-Weg ist dazu gespiegelt
    private static Vector2 DiagonalSE => (ParkCornerSE - ParkCenter).normalized;
    private static Vector2 DiagonalSW => new Vector2(-DiagonalSE.x, DiagonalSE.y);

    // Bahn-Stücke und wo sie anfangen (u), nach BuildLane gefüllt
    private static readonly List<float> pieceStartU = new List<float>();
    private static float startU;

    public static float MozzarellaU(float z) => MozzarellaX + CornerRadius * (2f - Mathf.PI / 2f) - z;

    [MenuItem("Tools/Jump and Run/City/Rebuild Level 3 (Pesto Park)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Level 3 neu bauen",
                "Löscht alles in Level 3 außer Kamera, Player, GameManager, HUD, KillZone, Goal und StartPoint und baut es neu auf. Fortfahren?",
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
        BuildGameplay();
        BuildProps();

        float goalU = Mathf.Round(startU + (LaneStartFromCenter - GoalFromCenter));
        LevelBuildKit.SetupEssentials(goalU, "", KeepWarmTime,
            new Vector3(MozzarellaX - 5f, -3f, 55f), new Vector3(30f, 1f, 110f),
            "Picnic at the Duck Pond, Pesto Park", new Vector3(20f, 90f, -110f));
        LevelBuildKit.Save(scene);
        Debug.Log($"Level3Builder: Level 3 (Pesto Park) gebaut. Bahn u {startU:F2} bis {LevelBuildKit.Lane.EndU:F2}.");
    }

    // Neue Szene als Kopie von Level 2 (bringt die festen Objekte mit), hinter Level 2 in die Build Settings
    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
            {
                throw new System.Exception($"Level3Builder: Konnte {TemplateScenePath} nicht nach {ScenePath} kopieren");
            }
        }
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(entry => entry.path == ScenePath))
        {
            return;
        }
        int after = scenes.FindIndex(entry => entry.path == TemplateScenePath);
        scenes.Insert(after + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- Bahn ----------

    private static void BuildLane()
    {
        Vector2 center = ParkCenter;
        float ring = RingRadius;

        // Bogen vom Diagonalweg (läuft genau auf die Brunnenmitte zu) auf den Ring: ein Kreis mit RingFilletRadius,
        // der den Weg und den Ring von außen berührt. reach = Abstand seines Startpunkts von der Mitte
        float filletReach = Mathf.Sqrt((ring + RingFilletRadius) * (ring + RingFilletRadius) - RingFilletRadius * RingFilletRadius);
        float filletOffset = Mathf.Atan2(RingFilletRadius, filletReach) * Mathf.Rad2Deg;
        float filletTurn = 90f - filletOffset;
        // Ring vom Südost- bis zum Südwest-Weg (über die Südseite), an beiden Enden um den Bogen verkürzt
        float seAngle = Mathf.Atan2(DiagonalSE.y, DiagonalSE.x) * Mathf.Rad2Deg;
        float ringTurn = 180f + 2f * seAngle - 2f * filletOffset;

        // Von Norden auf den Diagonalweg Richtung Brunnen: so weit nach links drehen
        float parkTurn = Mathf.Asin(DiagonalSE.x) * Mathf.Rad2Deg;
        float parkTangent = ParkTurnRadius * Mathf.Tan(parkTurn / 2f * Mathf.Deg2Rad);
        float diagonalLength = Vector2.Distance(ParkCornerSE, center) - parkTangent - filletReach;

        float westLineZ = CrustNorthCurb + StatueSouthRadius;
        float westLength = (MozzarellaX - StatueSouthRadius) - (ParkCornerSE.x + StatueWestRadius);
        float northLength = (ParkCornerSE.y - parkTangent) - (westLineZ + StatueWestRadius);
        float mozzarellaLength = CrustNorthCurb - CornerRadius;
        float mainStartX = MozzarellaX + CornerRadius;

        var pieces = new List<LanePath.Piece>
        {
            LevelBuildKit.Straight(LaneStartFromCenter - filletReach),   // Südwest-Weg (Kunde, Enten)
            LevelBuildKit.Arc(RingFilletRadius, -filletTurn),
            LevelBuildKit.Arc(ring, ringTurn),                           // halb um den Brunnen (Hund)
            LevelBuildKit.Arc(RingFilletRadius, -filletTurn),
            LevelBuildKit.Straight(diagonalLength),                      // Diagonalweg (Frisbee)
            LevelBuildKit.Arc(ParkTurnRadius, -parkTurn),
            LevelBuildKit.Straight(northLength),                         // westlich an der Statue vorbei
            LevelBuildKit.Arc(StatueWestRadius, 90f),
            LevelBuildKit.Straight(westLength),                          // südlich an der Statue vorbei
            LevelBuildKit.Arc(StatueSouthRadius, -90f),
            LevelBuildKit.Straight(mozzarellaLength),                    // Mozzarella Blvd (Baustelle, Crust Street)
            LevelBuildKit.Arc(CornerRadius, 90f),
            LevelBuildKit.Straight(MainEndX - mainStartX),               // Margherita Avenue (Pizzeria)
        };

        // So gewählt, dass auf der Margherita Avenue u = Welt-X gilt
        float beforeMain = 0f;
        for (int i = 0; i < pieces.Count - 1; i++)
        {
            beforeMain += pieces[i].length;
        }
        startU = mainStartX - beforeMain;
        pieceStartU.Clear();
        float u = startU;
        foreach (LanePath.Piece piece in pieces)
        {
            pieceStartU.Add(u);
            u += piece.length;
        }

        Vector2 start = center + DiagonalSW * LaneStartFromCenter;
        float heading = Mathf.Atan2(-DiagonalSW.y, -DiagonalSW.x) * Mathf.Rad2Deg;
        LevelBuildKit.BuildLane(new Vector3(start.x, 0f, start.y), heading, startU, pieces);
    }

    // ---------- Boden ----------

    private static void BuildGround(Transform link)
    {
        // Baustelle auf dem Mozzarella Blvd: Gruben über Gehweg und Fahrbahn (die Straße ist gesperrt)
        foreach (var pit in Pits)
        {
            Pit(link, pit.z, pit.gap, pit.barriers);
        }

        // Überweg über die Crust Street: Die Autos kommen von Osten (lokal +Z = Hausseite) die Crust Street entlang
        GameObject crossing = LevelBuildKit.PlaceAsset("Assets/Prefabs/Avenue Crossing.prefab", Mathf.Round(MozzarellaU(CrustZ)), 0, groundParent);
        foreach (TrafficLane trafficLane in crossing.GetComponentsInChildren<TrafficLane>(true))
        {
            var serialized = new SerializedObject(trafficLane);
            serialized.FindProperty("spawnZ").floatValue = 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Founder's Circle liegt über der Kreuzung (nur Optik, die Fahrbahn darunter hat keinen Collider), die Parkwege
        // sind auch nur Optik: unsichtbarer Laufboden ab dem Überweg bis zum Bahn-Anfang. Auf dem Pflaster 0.51, auf den
        // Wegen 0.6; dazwischen steigt er während des Bogens auf den Diagonalweg langsam an
        float rampFrom = pieceStartU[5], rampTo = pieceStartU[6];
        WalkGround(startU - 0.5f, MozzarellaU(CrustNorthCurb) + 0.5f,
            u => Mathf.Lerp(ParkPathTop, PavingTop, Mathf.InverseLerp(rampFrom, rampTo, u)));
    }

    // Baugrube auf dem Mozzarella Blvd, Lücke um z (Welt), quer über Gehweg und Fahrbahn bis zum gegenüberliegenden Bordstein.
    // barriers: Absperrungen an beiden Kanten (nicht, wo ein Gerüst an der Kante steht)
    private static void Pit(Transform link, float z, float gap, bool barriers)
    {
        float u = Mathf.Round(MozzarellaU(z));
        float from = u - gap / 2f, to = u + gap / 2f;
        // Das Prefab ist 1 m länger und steckt unter den Gehwegkanten
        LevelBuildKit.Stretched("Construction Pit", u, 0, gap + 1f, groundParent);
        if (barriers)
        {
            LevelBuildKit.Place("Barrier", Mathf.Round(to + 1f), 0, groundParent);
            LevelBuildKit.Place("Barrier", Mathf.Round(from - 1f), 0, groundParent);
        }
        LevelBuildKit.Cutout(link, $"Pit z{z:F0}", MozzarellaX + AvenueRoadEndLocalZ, MozzarellaX + FacadeLocalZ, z - gap / 2f, z + gap / 2f);

        // Seitenwände 0.1 m in der Lücke (genau auf der Schnittkante flackerten sie mit dem Stadt-Boden): unter dem Gehweg
        // bis zu dessen Oberkante, unter der Fahrbahn bis zu deren Oberkante
        Material foundation = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Foundation.mat");
        foreach (float edge in new[] { from + 0.1f, to - 0.1f })
        {
            LaneBox("Pit Wall Walk", edge, AvenueCurbLocalZ, FacadeLocalZ, CityLayout.Surface - 0.01f, foundation);
            LaneBox("Pit Wall Road", edge, AvenueRoadEndLocalZ, AvenueCurbLocalZ, CityLayout.RoadTop - 0.01f, foundation);
        }
    }

    // 0.2 m dicke Wand quer zur Bahn an der Stelle u, von lokal z0 bis z1, von der Grubensohle (-4) bis top
    private static void LaneBox(string name, float u, float z0, float z1, float top, Material material)
    {
        Renderer box = PrimitiveKit.Box(name, groundParent, Vector3.zero, new Vector3(0.2f, top + 4f, z1 - z0), material);
        Quaternion frame = LevelBuildKit.Frame(u);
        box.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(u) + frame * new Vector3(0f, (top - 4f) / 2f, (z0 + z1) / 2f), frame);
    }

    // Unsichtbare Boden-Collider (Layer Ground) entlang der Bahn von u0 bis u1: auf Geraden ein Stück, in Bögen etwa 1 m
    // lange Sehnen. Jedes Stück ist 4 m breit und 1 m dick, Oberkante top(u)
    private static void WalkGround(float u0, float u1, System.Func<float, float> top)
    {
        Transform parent = new GameObject("Walk Ground").transform;
        parent.SetParent(groundParent, false);
        var cuts = new List<float> { u0 };
        LanePath lane = LevelBuildKit.Lane;
        for (int i = 0; i < pieceStartU.Count; i++)
        {
            float pieceFrom = pieceStartU[i];
            float pieceTo = i + 1 < pieceStartU.Count ? pieceStartU[i + 1] : lane.EndU;
            if (pieceTo <= u0 || pieceFrom >= u1)
            {
                continue;
            }
            float a = Mathf.Max(pieceFrom, u0), b = Mathf.Min(pieceTo, u1);
            // Gerade: die Richtung ändert sich nicht. Im Bogen (oder in der Rampe) unterteilen
            bool straight = Vector3.Angle(lane.TangentAt(a + 0.01f), lane.TangentAt(b - 0.01f)) < 0.1f && Mathf.Approximately(top(a), top(b));
            int steps = straight ? 1 : Mathf.Max(1, Mathf.CeilToInt(b - a));
            for (int s = 1; s <= steps; s++)
            {
                cuts.Add(Mathf.Lerp(a, b, s / (float)steps));
            }
        }
        for (int i = 0; i < cuts.Count - 1; i++)
        {
            if (cuts[i + 1] - cuts[i] < 0.01f)
            {
                continue;
            }
            Vector3 p0 = lane.PointAt(cuts[i]), p1 = lane.PointAt(cuts[i + 1]);
            float mid = (cuts[i] + cuts[i + 1]) / 2f;
            float height = top(mid);
            var piece = new GameObject("Walk " + (i + 1));
            piece.layer = 3;
            piece.transform.SetParent(parent, false);
            piece.transform.SetPositionAndRotation((p0 + p1) / 2f + Vector3.up * (height - 0.5f),
                Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized, Vector3.up));
            // Etwas länger als die Sehne, damit zwischen den Stücken im Bogen keine Kerbe entsteht
            PrimitiveKit.AddBoxCollider(piece, Vector3.zero, new Vector3(Vector3.Distance(p0, p1) + 0.1f, 1f, 4f));
        }
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Östlich der Pizzeria geht es nicht weiter: Unfall mit unsichtbarer Wand (wie in Level 2)
        LevelBuildKit.Place("Car Crash", 4, 0, gameplayParent);

        // Baustelle (siehe Tabelle oben)
        Lift(44f, BridgeLowY, BridgeHighY, PitFloorY, startHigh: false);
        LevelBuildKit.HeatPickup(Mathf.Round(MozzarellaU(44f)), BridgeHighY + 2, gameplayParent);

        Lift(66f, ElevatorLowY, ElevatorHighY, CityLayout.Surface, startHigh: false);
        LevelBuildKit.Place("Scaffold", Mathf.Round(MozzarellaU(70f)), ElevatorHighY, gameplayParent);
        LevelBuildKit.HeatPickup(Mathf.Round(MozzarellaU(70f)), ElevatorHighY + 2, gameplayParent);

        LevelBuildKit.Place("Scaffold", Mathf.Round(MozzarellaU(79f)), 3, gameplayParent);
        Lift(88f, BridgeLowY, BridgeHighY, PitFloorY, startHigh: true);

        foreach (float z in RoadClosureZ)
        {
            RoadClosure(z);
        }
    }

    // Scherenbühne an der Stelle z auf dem Mozzarella Blvd. Fährt zwischen lowY und highY (Wurzel-Höhen), Sockel auf groundY.
    // startHigh: steht am Anfang oben (fährt zuerst runter), sonst unten
    private static void Lift(float z, float lowY, float highY, float groundY, bool startHigh)
    {
        float u = Mathf.Round(MozzarellaU(z));
        GameObject lift = LevelBuildKit.Place("Scissor Lift", u, startHigh ? highY : lowY, gameplayParent);
        Vector3 bottom = LevelBuildKit.Lane.PointAt(u) + Vector3.up * lowY;
        lift.transform.Find("moving platform start").position = bottom;
        lift.transform.Find("moving platform end").position = bottom + Vector3.up * (highY - lowY);

        var serialized = new SerializedObject(lift.GetComponent<ScissorLift>());
        serialized.FindProperty("groundY").floatValue = groundY;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        lift.GetComponent<ScissorLift>().Arrange();
    }

    // Straßensperre quer über den Mozzarella Blvd (nur Optik, vor der Laufbahn): rot-weiße Baken und ein Schild
    private static void RoadClosure(float z)
    {
        float u = Mathf.Round(MozzarellaU(z));
        Quaternion frame = LevelBuildKit.Frame(u);
        Vector3 at = LevelBuildKit.Lane.PointAt(u);
        Transform closure = new GameObject($"Road Closure z{z:F0}").transform;
        closure.SetParent(propParent, false);
        closure.SetPositionAndRotation(at, frame);

        Material orange = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Barrier Orange.mat");
        Material white = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Awning White.mat");
        Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Iron.mat");
        const float road = CityLayout.RoadTop;
        // Absperrgitter in einer Reihe quer über die Fahrbahn, schräg gestellt: Gerade quer zur Straße sähe die Kamera
        // von der Seite nur ihre Kanten. Dazwischen Pylonen
        for (float localZ = AvenueCurbLocalZ - 1.5f; localZ > AvenueRoadEndLocalZ; localZ -= 2.5f)
        {
            Transform panel = PrimitiveKit.Pivot("Barrier Panel", closure, new Vector3(0f, road, localZ));
            panel.localRotation = Quaternion.Euler(0f, 40f, 0f);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                PrimitiveKit.Box("Post", panel, new Vector3(x, 0.5f, 0f), new Vector3(0.08f, 1f, 0.08f), iron);
            }
            for (int i = 0; i < 4; i++)
            {
                PrimitiveKit.Box("Stripe", panel, new Vector3(-0.45f + i * 0.3f, 0.85f, 0f), new Vector3(0.3f, 0.25f, 0.04f), i % 2 == 0 ? orange : white);
            }
            PrimitiveKit.Part("Cone", PrimitiveType.Cylinder, closure, new Vector3(0f, road + 0.25f, localZ - 1.25f), new Vector3(0.3f, 0.25f, 0.3f), orange);
        }
        // Schild an der Bordsteinkante, Schrift zur Kamera
        PrimitiveKit.Box("Sign Post", closure, new Vector3(0f, road + 0.9f, AvenueCurbLocalZ - 0.4f), new Vector3(0.08f, 1.8f, 0.08f), iron);
        PrimitiveKit.Box("Sign", closure, new Vector3(0f, road + 1.9f, AvenueCurbLocalZ - 0.45f), new Vector3(1.6f, 0.6f, 0.04f), white);
        var text = PrimitiveKit.Text("Sign Text", closure, new Vector3(0f, road + 1.9f, AvenueCurbLocalZ - 0.48f), new Vector2(1.5f, 0.5f), "ROAD CLOSED", new Color(0.8f, 0.13f, 0.1f));
        text.fontSizeMax = 4f;
    }

    // ---------- Deko ----------

    private static void BuildProps()
    {
        // Straßenlaternen und Bäume am Mozzarella Blvd, nicht an Gruben und nicht auf dem Überweg
        var blocked = new List<(float from, float to)> { (MozzarellaU(120f), MozzarellaU(104f)) };
        foreach (var pit in Pits)
        {
            blocked.Add((MozzarellaU(pit.z + pit.gap / 2f + 2f), MozzarellaU(pit.z - pit.gap / 2f - 2f)));
        }
        // Aufzugs-Bühne und Gerüste (die Stangen stehen hinter der Laufbahn, wo sonst Laternen stehen)
        blocked.Add((MozzarellaU(73f), MozzarellaU(63f)));
        blocked.Add((MozzarellaU(82f), MozzarellaU(76f)));
        bool Free(float u) => blocked.TrueForAll(zone => u < zone.from || u > zone.to);
        for (float z = 12f; z < 104f; z += 12f)
        {
            float u = Mathf.Round(MozzarellaU(z));
            if (Free(u)) LevelBuildKit.Place("Street Lamp", u, 0, propParent);
            if (Free(u - 6f)) LevelBuildKit.Place("Tree", u - 6f, 0, propParent);
        }
        LevelBuildKit.Place("Street Lamp", -20, 0, propParent);
        LevelBuildKit.Place("Trash Can", -17, 0, propParent);

        (float u, int y)[] clouds = { (-10f, 30), (MozzarellaU(40f), 26), (MozzarellaU(90f), 33), (pieceStartU[4] + 20f, 28), (startU + 20f, 31) };
        foreach (var cloud in clouds)
        {
            LevelBuildKit.Place("Cloud", Mathf.Round(cloud.u), cloud.y, propParent);
        }
    }
}
