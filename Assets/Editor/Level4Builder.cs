using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Baut Level 4 (Hafen) komplett neu auf. Läuft in der gemeinsamen Stadt wie Level 2 und 3.
// Menü: Tools > Jump and Run > City > Rebuild Level 4 (Port). Erneut ausführen überschreibt Handänderungen.
// Gibt es die Szene noch nicht, wird sie als Kopie von Level 2 angelegt (Kamera, Player, GameManager, HUD, Goal ...)
// und in die Build Settings hinter Level 3 eingetragen.
//
// Route (der Spieler läuft nach LINKS, also Richtung -u), ~650 m: aus der Pizzeria nach Westen, über den Mozzarella Blvd
// auf die Strandpromenade, rechts in die Oregano Street (Ostseite, x -103), am Ende der Board-Shop (Checkpoint 1, ab dort
// auf dem Skateboard), über die Crust Street (Autos) und links auf ihren Nordgehweg am Park entlang (z 137), über die
// Basil Street (Autos), rechts um die Blockecke auf die Marinara Promenade (x -255, Markt mit Menschenmenge), beim Kiosk an
// der Salami Street (Checkpoint 2, Mikrowelle, Board wird abgestellt) links in den Containerhafen zu den Bauarbeitern.
// Die Bahn ist in +u-Richtung definiert (Häuser links davon): Sie beginnt am Kunden im Hafen und endet hinter der Pizzeria.
// Auf der Margherita Avenue ist u = Welt-X.
//
// Die Ecken haben Radien R = k / (2 - π/2) mit ganzem k: Dann ist der Weg um die Ecke genau k Meter kürzer als über den
// Eckpunkt, und auf jeder Geraden gilt u = Welt-Koordinate + ganze Zahl. Der PrefabGridEnforcer rastet Prefabs auf ganze u
// ein; so landen sie trotzdem auf ganzen Welt-Metern (z.B. ein Überweg genau auf der Straßenmitte).
//
// Stand: Gerüst (Arbeitsschritt 1). Bahn, Überwege mit Autos, Laufboden über Straßen und im Hafen, Board-Shop, Ziel als
// Platzhalter. Markt, Kiosk mit Mikrowelle, Hafen mit Kränen, Bauarbeiter und der Weg zu Fuß kommen als eigene Schritte.
public static class Level4Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level04.unity";
    private const string TemplateScenePath = "Assets/Scenes/levels/level02.unity";
    private const string PreviousScenePath = "Assets/Scenes/levels/level03.unity";
    private const float KeepWarmTime = 90f;

    // Laufbahn-Linien (Welt): Oregano Street Ostseite (Fassaden x -100, Bordstein -104), Crust Street Nordseite am Park
    // (Bordstein z 136), Marinara Promenade 8 m vor den Fassaden (x -247) auf dem Plattenweg, wo früher die Fahrbahn war:
    // dazwischen stehen die Marktstände. Hafen vorläufig (wird mit dem Hafen geplant)
    private const float OreganoX = -103f;
    private const float CrustLaneZ = 137f;
    private const float MarinaraX = -255f;
    // Am Nordende des Markts biegt die Bahn in einer sanften S-Kurve an die Fassaden heran (x -250, Gehweg-Linie wie am Ring),
    // dort steht der Kiosk (Checkpoint 2). Danach bleibt sie dort, über die Salami Street bis zur Abzweigung in den Hafen
    private const float MarinaraNorthX = -250f;
    private const float KioskZ = 249f;
    private const float SBendNorthZ = 245f;
    private const float PortZ = CityLayout.PortRouteZ;
    private const float PortEndX = -380f;
    private const float MainEndX = 16f;

    // Ecken (k = so viele Meter kürzer als über den Eckpunkt, siehe oben)
    private static float CornerRadius(int k) => k / (2f - Mathf.PI / 2f);
    private static readonly float OreganoCorner = CornerRadius(2);    // Margherita → Oregano, außen um die Blockecke
    private static readonly float CrustCorner = CornerRadius(1);      // Oregano → Crust, über die Kreuzung (klein, bleibt auf dem Überweg)
    private static readonly float MarinaraCorner = CornerRadius(2);   // Crust → Marinara, außen um die Blockecke
    private static readonly float PortCorner = CornerRadius(3);       // Marinara → Hafen, über die Ringstraße

    // S-Kurve: zwei gleiche Bögen (Winkel θ, Radius R) versetzen die Bahn um 5 m. θ ist so gewählt, dass der Weg genau 1 m
    // länger ist als geradeaus (wie bei den Ecken: u bleibt danach Welt-Koordinate + ganze Zahl).
    // Versatz = 2R(1 - cos θ), Umweg = 2R(θ - sin θ) → (θ - sin θ) / (1 - cos θ) = Umweg / Versatz, per Intervallhalbierung
    private static float SBendAngle
    {
        get
        {
            float target = 1f / (MarinaraNorthX - MarinaraX);
            float low = 0.01f, high = 1.5f;
            for (int i = 0; i < 60; i++)
            {
                float mid = (low + high) / 2f;
                if ((mid - Mathf.Sin(mid)) / (1f - Mathf.Cos(mid)) < target) low = mid; else high = mid;
            }
            return (low + high) / 2f;
        }
    }
    private static float SBendRadius => (MarinaraNorthX - MarinaraX) / (2f * (1f - Mathf.Cos(SBendAngle)));
    private static float SBendSouthZ => SBendNorthZ - 2f * SBendRadius * Mathf.Sin(SBendAngle);

    // Straßen, die die Bahn quert (Welt): Crust Street (Mitte z 130), Basil Street (x -180), Four Cheese Avenue (z 215),
    // Salami Street (z 265). Avenue = 12 m Fahrbahn
    private const float CrustZ = 130f;
    private const float BasilX = -180f;
    private const float FourCheeseZ = 215f;

    // Kamera auf der Strandpromenade wie in Level 2: weiter hinten und höher über dem Wasser
    private static readonly Vector3 BeachCameraOffset = new Vector3(0f, 9f, -18f);
    private const float BeachCameraLookHeight = 1f;
    // Auf dem Markt: etwas weiter weg als normal (bei 14 m/s sieht man so ~14 m voraus), aber nicht so weit wie am Strand,
    // dort wäre die untere Bildhälfte leerer Plattenweg
    private static readonly Vector3 MarketCameraOffset = new Vector3(0f, 7f, -14f);
    private const float MarketCameraLookHeight = 2.5f;

    private static Transform groundParent, gameplayParent, propParent;

    // Bahn-Stücke und wo sie anfangen (u), nach BuildLane gefüllt
    private static readonly List<float> pieceStartU = new List<float>();

    // Stelle u auf den einzelnen Geraden (Welt-Koordinate entlang der Straße)
    public static float PortU(float x) => pieceStartU[0] + (x - PortEndX);
    // Marinara nördlich der S-Kurve (Kiosk, Salami Street, x -250) bzw. auf dem Markt (x -255)
    public static float MarinaraNorthU(float z) => pieceStartU[2] + (PortZ - PortCorner - z);
    public static float MarinaraU(float z) => pieceStartU[5] + (SBendSouthZ - z);
    public static float CrustU(float x) => pieceStartU[7] + (x - (MarinaraX + MarinaraCorner));
    public static float OreganoU(float z) => pieceStartU[9] + (CrustLaneZ - CrustCorner - z);

    [MenuItem("Tools/Jump and Run/City/Rebuild Level 4 (Port)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Level 4 neu bauen",
                "Löscht alles in Level 4 außer Kamera, Player, GameManager, HUD, KillZone, Goal und StartPoint und baut es neu auf. Fortfahren?",
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
        UseCrewGoal();
        BuildLane();
        groundParent = LevelBuildKit.Group("Level Ground");
        gameplayParent = LevelBuildKit.Group("Level Gameplay");
        propParent = LevelBuildKit.Group("Level Props");

        LevelBuildKit.CityLink("Pizzeria", "Board Shop", "Kiosk");
        BuildGround();
        BuildGameplay();

        // KillZone = Wasser im Hafenbecken (knapp unter der Oberfläche, -0.5)
        Rect basin = CityLayout.PortBasin;
        LevelBuildKit.SetupEssentials(Mathf.Round(PortU(GoalX)), "", KeepWarmTime,
            new Vector3(basin.center.x, -0.9f, basin.center.y), new Vector3(basin.width, 1f, basin.height),
            "Lunch for the crew, Pier 3, Crust Harbor", new Vector3(60f, 80f, -110f));
        LevelBuildKit.Save(scene);
        Debug.Log($"Level4Builder: Level 4 (Port) gebaut. Bahn u {LevelBuildKit.Lane.StartU:F2} bis {LevelBuildKit.Lane.EndU:F2}. " +
                  $"Oregano u(z) = {OreganoU(0f):F2} - z, Crust u(x) = x + {CrustU(0f):F2}, Markt u(z) = {MarinaraU(0f):F2} - z, " +
                  $"Marinara Nord u(z) = {MarinaraNorthU(0f):F2} - z, Hafen u(x) = x + {PortU(0f):F2}, S-Kurve z {SBendSouthZ:F2}–{SBendNorthZ:F2}");
    }

    // Ziel ohne Haus: die Bauarbeiter auf dem Stahlträger statt des Goal-Prefabs mit Kundenhaus (aus der kopierten Szene).
    // Heißt weiter "Goal", damit OpenAndClear und SetupEssentials es finden
    private static void UseCrewGoal()
    {
        const string crewPath = PortSetup.PrefabFolder + "/Crew Goal.prefab";
        GameObject old = GameObject.Find("Goal");
        if (old != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old) == crewPath)
        {
            return;
        }
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        var goal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(crewPath));
        goal.name = "Goal";
    }

    // Neue Szene als Kopie von Level 2 (bringt die festen Objekte mit, Ziel mit Kundenhaus), hinter Level 3 in die Build Settings
    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
            {
                throw new System.Exception($"Level4Builder: Konnte {TemplateScenePath} nicht nach {ScenePath} kopieren");
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
        var pieces = new List<LanePath.Piece>
        {
            LevelBuildKit.Straight(MarinaraNorthX - PortCorner - PortEndX),                       // Hafen (Kunde am Kai)
            LevelBuildKit.Arc(PortCorner, -90f),
            LevelBuildKit.Straight(PortZ - PortCorner - SBendNorthZ),                             // Marinara Nord (Kiosk, Salami Street)
            LevelBuildKit.Arc(SBendRadius, -SBendAngle * Mathf.Rad2Deg),                          // S-Kurve hinaus auf die Promenade
            LevelBuildKit.Arc(SBendRadius, SBendAngle * Mathf.Rad2Deg),
            LevelBuildKit.Straight(SBendSouthZ - (CrustLaneZ + MarinaraCorner)),                  // Markt
            LevelBuildKit.Arc(MarinaraCorner, 90f),
            LevelBuildKit.Straight(OreganoX - CrustCorner - (MarinaraX + MarinaraCorner)),        // Crust Street am Park (Basil-Überweg)
            LevelBuildKit.Arc(CrustCorner, -90f),
            LevelBuildKit.Straight(CrustLaneZ - CrustCorner - OreganoCorner),                     // Oregano Street (Board-Shop am Ende)
            LevelBuildKit.Arc(OreganoCorner, 90f),
            LevelBuildKit.Straight(MainEndX - (OreganoX + OreganoCorner)),                        // Margherita Avenue (Pizzeria)
        };

        // So gewählt, dass auf der Margherita Avenue u = Welt-X gilt
        float beforeMain = 0f;
        for (int i = 0; i < pieces.Count - 1; i++)
        {
            beforeMain += pieces[i].length;
        }
        float startU = OreganoX + OreganoCorner - beforeMain;
        pieceStartU.Clear();
        float u = startU;
        foreach (LanePath.Piece piece in pieces)
        {
            pieceStartU.Add(u);
            u += piece.length;
        }
        LevelBuildKit.BuildLane(new Vector3(PortEndX, 0f, PortZ), 0f, startU, pieces);
    }

    // ---------- Boden ----------

    private static void BuildGround()
    {
        // Überwege mit Autos. Mozzarella Blvd wie in Level 2 (Autos von hinter der Crust-Kreuzung)
        Crossing(-40f, 118f, -15f);
        // Crust Street am Ende der Oregano Street: Die Autos kommen von Osten (vom Founder's Circle, lokal +Z) und
        // verschwinden westlich der Kreuzung hinter der Kamera
        Crossing(Mathf.Round(OreganoU(CrustZ)), 57f, -15f);
        // Basil Street: Die Autos kommen von Norden (ab der Four Cheese Avenue) und verschwinden in der Crust-Kreuzung hinter der Kamera
        Crossing(Mathf.Round(CrustU(BasilX)), FourCheeseZ - CrustLaneZ, -15f);

        // Die Stadt hat nur auf den Gehweg-Platten Collider. Über die Fahrbahnen ohne Überweg (vorläufig, der Markt bekommt
        // an der Four Cheese Avenue Poller, siehe Arbeitsschritt 2) und im Hafen liegt unsichtbarer Laufboden.
        // Der Bogen von der Oregano Street auf die Crust Street geht ein Stück über den Rand des Überwegs hinaus
        LevelBuildKit.WalkGround("Walk Crust Corner", pieceStartU[8] - 0.5f, pieceStartU[9] + 0.5f, CityLayout.Surface, groundParent);
        // Nördlich der Marktpromenade liegt die Bahn auf der Ringstraße (bis zur Abzweigung in den Hafen)
        LevelBuildKit.WalkGround("Walk Salami", pieceStartU[2] - 0.5f, MarinaraNorthU(CityLayout.MarketToZ - 0.5f), CityLayout.Surface, groundParent);
        // Bogen über die Ringstraße auf Gehweghöhe, dann das Stück Ringstraße bis zum Hafen-Beton (der hat eigene Collider)
        LevelBuildKit.WalkGround("Walk Port Corner", pieceStartU[1], pieceStartU[2] + 0.5f, CityLayout.Surface, groundParent);
        LevelBuildKit.WalkGround("Walk Port Road", PortU(CityLayout.West - 13f), pieceStartU[1], CityLayout.PortTop, groundParent);
    }

    // Erhöhter Überweg über eine 12 m breite Avenue mit zwei Spuren. spawnZ/despawnZ: wo die Autos auftauchen und
    // verschwinden, lokal Z im Lane-Rahmen (+ = hinter der Bahn, - = Kameraseite)
    private static void Crossing(float u, float spawnZ, float despawnZ)
    {
        GameObject crossing = LevelBuildKit.PlaceAsset("Assets/Prefabs/Avenue Crossing.prefab", u, 0, groundParent);
        foreach (TrafficLane trafficLane in crossing.GetComponentsInChildren<TrafficLane>(true))
        {
            var serialized = new SerializedObject(trafficLane);
            serialized.FindProperty("spawnZ").floatValue = spawnZ;
            serialized.FindProperty("despawnZ").floatValue = despawnZ;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------- Markt (Marinara Promenade, auf dem Board) ----------

    // Hindernisse auf der Bahn, Welt-z in Fahrtrichtung (nach Norden). Abstand ~9–10 m: Ein Ollie bei 14 m/s geht ~10 m weit,
    // dazwischen braucht man einen Moment zum Reagieren. Passanten laufen in ihrem Bereich (± range) hin und her
    private enum MarketThing { Pedestrian, CratesLow, CratesHigh, Group, Cart, BouncyStall, Jugglers }
    private static readonly (float z, MarketThing thing, float range)[] MarketLayout =
    {
        (146f, MarketThing.Pedestrian, 3f),
        (156f, MarketThing.CratesLow, 0f),
        (165f, MarketThing.Jugglers, 0f),      // werfen Keulen quer über die Bahn
        (174f, MarketThing.Group, 0f),
        (184f, MarketThing.Pedestrian, 4f),
        (194f, MarketThing.CratesHigh, 0f),
        (203f, MarketThing.Pedestrian, 3f),
        (213f, MarketThing.BouncyStall, 0f),   // vor den Pollern der Four Cheese Avenue, Chili 2 darüber
        (222f, MarketThing.Cart, 4f),
        (232f, MarketThing.Group, 0f),
        (241f, MarketThing.Pedestrian, 3f),
        // Danach: S-Kurve an die Fassaden, Kiosk (Checkpoint 2, Board abstellen, Mikrowelle)
    };
    // Chili 2 schwebt über dem federnden Stand: nur mit dessen Schwung (~5 m über der Markise) erreichbar
    private const float MarketChiliHeight = 7f;
    // Marktstände hinter der Bahn: alle 5 m, Sorte reihum (mit kleinen Unregelmäßigkeiten)
    private const float StallSpacing = 5f;
    private static readonly string[] StallTypes = { "Food", "Fish", "Flowers", "Food", "Flea", "Fish", "Flowers", "Flea" };

    // Kulisse vor der Bahn (zum Wasser hin, lokal z -4 bis -7: weiter vorne sieht die Kamera nicht hin, näher an der Bahn
    // würden Leute den Spieler verdecken). Welt-z, Prefab
    private static readonly (float z, string prefab)[] MarketForeground =
    {
        (150f, "Promenade Bench"), (156f, "Lemonade Stand"), (180f, "Street Musician"), (192f, "Promenade Bench"),
        (202f, "Ice Cream Cart"), (222f, "Promenade Bench"), (230f, "Balloon Seller"), (240f, "Promenade Bench"),
    };
    private const float LampSpacing = 12f;
    // Spaziergänger (Kulisse, ohne Collider): Welt-z, seitlicher Abstand zur Bahn (lokal z: - = am Wasser, + = zwischen den
    // Ständen), Bereich in Metern
    private static readonly (float z, float side, float range)[] MarketStrollers =
    {
        (160f, -5f, 6f), (196f, -6f, 5f), (214f, -4.6f, 6f), (244f, -5.8f, 5f),
        (147f, 1.6f, 5f), (170f, 1.5f, 4f), (188f, 1.7f, 5f), (207f, 1.5f, 4f), (222f, 1.6f, 4f),
    };

    private static void BuildMarket()
    {
        Transform market = new GameObject("Market").transform;
        market.SetParent(gameplayParent, false);
        string folder = MarketSetup.PrefabFolder;
        float direction = 1f;
        float bouncyZ = 0f;
        var keepClear = new List<float>();
        foreach (var entry in MarketLayout)
        {
            float u = Mathf.Round(MarinaraU(entry.z));
            switch (entry.thing)
            {
                case MarketThing.Pedestrian:
                case MarketThing.Cart:
                    string prefab = entry.thing == MarketThing.Cart ? "Hand Cart" : "Pedestrian";
                    GameObject walker = LevelBuildKit.PlaceAsset($"{folder}/{prefab}.prefab", u, 1, market);
                    var patrol = new SerializedObject(walker.GetComponent<GroundPatrol>());
                    patrol.FindProperty("startDirection").floatValue = direction;
                    patrol.FindProperty("patrolLimits").vector2Value = new Vector2(-entry.range, entry.range);
                    patrol.ApplyModifiedPropertiesWithoutUndo();
                    // Abwechselnd: einer kommt entgegen, der nächste läuft mit
                    direction = -direction;
                    break;
                case MarketThing.CratesLow:
                    LevelBuildKit.PlaceAsset($"{folder}/Crate Stack Low.prefab", u, 0, market);
                    break;
                case MarketThing.CratesHigh:
                    LevelBuildKit.PlaceAsset($"{folder}/Crate Stack High.prefab", u, 0, market);
                    break;
                case MarketThing.Group:
                    LevelBuildKit.PlaceAsset($"{folder}/Market Group.prefab", u, 0, market);
                    break;
                case MarketThing.BouncyStall:
                    LevelBuildKit.PlaceAsset($"{folder}/Bouncy Market Stall.prefab", u, 0, market);
                    LevelBuildKit.HeatPickup(u, MarketChiliHeight, market);
                    bouncyZ = entry.z;
                    break;
                case MarketThing.Jugglers:
                    // Der hintere Jongleur steht da, wo sonst ein Stand wäre
                    LevelBuildKit.PlaceAsset($"{folder}/Jugglers Across.prefab", u, 0, market);
                    keepClear.Add(entry.z);
                    break;
            }
        }

        // Stände hinter der Bahn, außer vor der Mündung der Four Cheese Avenue (Blick auf die Poller) und neben dem federnden Stand
        int index = 0;
        // In der S-Kurve rückt die Bahn an die Fassaden, dort ist kein Platz mehr für Stände
        for (float z = CrustLaneZ + 7f; z <= SBendSouthZ - 2f; z += StallSpacing)
        {
            bool fourCheese = z > FourCheeseZ - 6f && z < FourCheeseZ + 6f;
            if (fourCheese || Mathf.Abs(z - bouncyZ) < 4f || keepClear.Exists(clear => Mathf.Abs(z - clear) < 4f))
            {
                continue;
            }
            string type = StallTypes[index++ % StallTypes.Length];
            LevelBuildKit.PlaceAsset($"{folder}/Market Stall {type}.prefab", Mathf.Round(MarinaraU(z)), 0, market);
        }

        // Vor der Bahn: Bänke, Musiker, Eiswagen, Ballons, Limonade, dazwischen Laternen (nicht direkt an einer Attraktion)
        Transform front = new GameObject("Market Foreground").transform;
        front.SetParent(market, false);
        foreach (var entry in MarketForeground)
        {
            LevelBuildKit.PlaceAsset($"{folder}/{entry.prefab}.prefab", Mathf.Round(MarinaraU(entry.z)), 0, front);
        }
        for (float z = CrustLaneZ + 7f; z <= SBendNorthZ; z += LampSpacing)
        {
            float lampZ = z;
            if (System.Array.Exists(MarketForeground, entry => Mathf.Abs(entry.z - lampZ) < 3f) || keepClear.Exists(clear => Mathf.Abs(lampZ - clear) < 3f))
            {
                continue;
            }
            LevelBuildKit.PlaceAsset($"{folder}/Harbor Lamp.prefab", Mathf.Round(MarinaraU(z)), 0, front);
        }
        float strollDirection = 1f;
        foreach (var entry in MarketStrollers)
        {
            GameObject stroller = LevelBuildKit.PlaceAsset($"{folder}/Stroller.prefab", Mathf.Round(MarinaraU(entry.z)), 0, front);
            Transform figure = stroller.transform.Find("Figure");
            figure.localPosition = new Vector3(0f, figure.localPosition.y, entry.side);
            var settings = new SerializedObject(stroller.GetComponent<Stroller>());
            settings.FindProperty("range").floatValue = entry.range;
            settings.FindProperty("startDirection").floatValue = strollDirection;
            settings.ApplyModifiedPropertiesWithoutUndo();
            strollDirection = -strollDirection;
        }
    }

    // ---------- Hafen (zu Fuß) ----------
    //   Hof: Container-Treppe (1, 2 hoch), 4 m Lücke, 2 hoch, 3 hoch als Absprung (Oberkante 8.3)
    //   Becken (x -300 … -340): Fähre 1 (waagerecht), Aufzug (senkrecht, Chili 3 über der oberen Stellung), Fähre 2, dann der Kai
    //   Kai: Bauarbeiter auf dem Stahlträger (Ziel)
    // Ein Container ist 6 m lang, Stapel stehen Kante an Kante, wo es eine Treppe sein soll. Welt-x = Mitte
    private static readonly (float x, int levels)[] YardStacks = { (-273f, 1), (-279f, 2), (-289f, 2), (-295f, 3) };
    // Kran-Container: Mitte von/bis (Welt-x) und Wurzel-Höhe von/bis (Oberkante = Wurzel + 0.5)
    private static readonly (float fromX, float toX, float fromY, float toY)[] CraneRides =
    {
        (-304f, -312f, 5f, 5f),     // Fähre 1
        (-320f, -320f, 5f, 10f),    // Aufzug
        (-328f, -336f, 5f, 5f),     // Fähre 2
    };
    // Hoch genug, dass die Kamera über dem Becken (steigt mit dem Spieler auf den Containern) unter dem Träger bleibt
    private const float CraneBeamY = 18f;
    private const float GoalX = -372f;
    // Über dem Becken: Kamera weiter weg und höher, damit man die Container, ihre Wege und das Wasser sieht
    private static readonly Vector3 BasinCameraOffset = new Vector3(0f, 8f, -16f);
    private const float BasinCameraLookHeight = 2f;

    private static void BuildPort()
    {
        Transform port = new GameObject("Port").transform;
        port.SetParent(gameplayParent, false);
        foreach (var stack in YardStacks)
        {
            LevelBuildKit.PlaceAsset($"{PortSetup.PrefabFolder}/Container Stack {stack.levels}.prefab", Mathf.Round(PortU(stack.x)), 0, port);
        }

        float liftTop = 0f;
        foreach (var ride in CraneRides)
        {
            float u = Mathf.Round(PortU(ride.fromX));
            GameObject container = LevelBuildKit.PlaceAsset($"{PortSetup.PrefabFolder}/Crane Container.prefab", u, ride.fromY, port);
            container.transform.Find("moving platform start").position = LevelBuildKit.Lane.PointAt(u) + Vector3.up * ride.fromY;
            container.transform.Find("moving platform end").position = LevelBuildKit.Lane.PointAt(Mathf.Round(PortU(ride.toX))) + Vector3.up * ride.toY;
            var mover = new SerializedObject(container.GetComponent<MovingPlatform>());
            bool lift = Mathf.Approximately(ride.fromX, ride.toX);
            mover.FindProperty("speed").floatValue = lift ? 1.6f : 2.2f;
            mover.FindProperty("waitTime").floatValue = 1f;
            mover.ApplyModifiedPropertiesWithoutUndo();
            var cable = new SerializedObject(container.GetComponent<CraneCable>());
            cable.FindProperty("topY").floatValue = CraneBeamY - 0.4f;
            cable.ApplyModifiedPropertiesWithoutUndo();
            if (lift)
            {
                liftTop = ride.toY + 0.5f;
                // Chili 3 über der oberen Stellung des Aufzugs: nur mit ihm erreichbar
                LevelBuildKit.HeatPickup(u, Mathf.Round(liftTop + 1.5f), port);
            }
        }
        BasinGantry(port);

        var zone = new SerializedObject(new GameObject("Basin Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        zone.FindProperty("fromU").floatValue = PortU(CityLayout.PortBasin.xMin - 4f);
        zone.FindProperty("toU").floatValue = PortU(CityLayout.PortBasin.xMax + 6f);
        zone.FindProperty("blend").floatValue = 6f;
        zone.FindProperty("offset").vector3Value = BasinCameraOffset;
        zone.FindProperty("lookHeight").floatValue = BasinCameraLookHeight;
        zone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)zone.targetObject).transform.SetParent(port, false);
    }

    // Halbportalkran über dem Becken: Träger entlang der Bahn, getragen von zwei Stützen hinter der Bahn (Norden) mit
    // Auslegern nach vorn. Vorne (Kameraseite) steht nichts, sonst verdeckte eine Stütze den Spieler beim Springen
    private static void BasinGantry(Transform parent)
    {
        Material paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Crane Yellow.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Crane Steel.mat");
        Transform gantry = new GameObject("Basin Gantry").transform;
        gantry.SetParent(parent, false);
        Rect basin = CityLayout.PortBasin;
        float x0 = basin.xMin - 4f, x1 = basin.xMax + 4f;
        const float legZ = PortZ + 9f;
        // Träger knapp über den Laufkatzen
        PrimitiveKit.Box("Beam", gantry, new Vector3((x0 + x1) / 2f, CraneBeamY + 0.5f, PortZ), new Vector3(x1 - x0, 1f, 1.2f), paint);
        PrimitiveKit.Box("Rail", gantry, new Vector3((x0 + x1) / 2f, CraneBeamY - 0.05f, PortZ), new Vector3(x1 - x0, 0.1f, 0.5f), dark);
        foreach (float x in new[] { x0 + 1f, x1 - 1f })
        {
            PrimitiveKit.Box("Leg", gantry, new Vector3(x, (CityLayout.PortTop + CraneBeamY + 2f) / 2f, legZ), new Vector3(1.2f, CraneBeamY + 2f - CityLayout.PortTop, 1.2f), paint);
            PrimitiveKit.Box("Arm", gantry, new Vector3(x, CraneBeamY + 1.5f, (legZ + PortZ) / 2f), new Vector3(1f, 1f, legZ - PortZ + 1.2f), paint);
            // Schräge Strebe vom Bein zum Ausleger
            Vector3 from = new Vector3(x, CraneBeamY - 4f, legZ);
            Vector3 to = new Vector3(x, CraneBeamY + 1f, PortZ + 2f);
            Renderer brace = PrimitiveKit.Box("Brace", gantry, (from + to) / 2f, new Vector3(0.5f, 0.5f, Vector3.Distance(from, to)), paint);
            brace.transform.rotation = Quaternion.LookRotation(to - from);
            PrimitiveKit.Box("Foot", gantry, new Vector3(x, CityLayout.PortTop + 0.3f, legZ), new Vector3(2f, 0.6f, 2.5f), dark);
        }
        PrimitiveKit.Box("Cabin", gantry, new Vector3(x1 - 3f, CraneBeamY - 0.8f, PortZ + 1.6f), new Vector3(2f, 1.6f, 1.8f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Window Glass.mat"));
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Östlich der Pizzeria geht es nicht weiter: Unfall mit unsichtbarer Wand (wie in Level 2 und 3)
        LevelBuildKit.Place("Car Crash", 4, 0, gameplayParent);

        // Strandpromenade: Kamera über dem Wasser wie in Level 2, vor der Oregano-Ecke zurück auf die normale Kamera
        var beachZone = new SerializedObject(new GameObject("Beach Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        beachZone.FindProperty("fromU").floatValue = -92f;
        beachZone.FindProperty("toU").floatValue = -54f;
        beachZone.FindProperty("blend").floatValue = 8f;
        beachZone.FindProperty("offset").vector3Value = BeachCameraOffset;
        beachZone.FindProperty("lookHeight").floatValue = BeachCameraLookHeight;
        beachZone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)beachZone.targetObject).transform.SetParent(gameplayParent, false);

        // Marktpromenade: Kamera weiter weg (über der Kaimauer), ab kurz nach der Ecke bis hinter den Kiosk
        var marketZone = new SerializedObject(new GameObject("Market Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        marketZone.FindProperty("fromU").floatValue = MarinaraNorthU(KioskZ + 3f);
        marketZone.FindProperty("toU").floatValue = MarinaraU(CrustLaneZ + 8f);
        marketZone.FindProperty("blend").floatValue = 8f;
        marketZone.FindProperty("offset").vector3Value = MarketCameraOffset;
        marketZone.FindProperty("lookHeight").floatValue = MarketCameraLookHeight;
        marketZone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)marketZone.targetObject).transform.SetParent(gameplayParent, false);

        BuildMarket();
        BuildPort();

        // Crust Street (auf dem Board): zwei Bänke zum Drüberspringen, damit man die Ollie-Weite ausprobieren kann.
        // Rampen über Baugruben, Poller und der Hund kommen mit dem Feinschliff dieses Stücks
        LevelBuildKit.Place("Bench", Mathf.Round(CrustU(-130f)), 0, gameplayParent);
        LevelBuildKit.Place("Bench", Mathf.Round(CrustU(-150f)), 0, gameplayParent);

        // Hinter dem Kunden ist der Kai zu Ende: unsichtbare Wand, die Bahn ginge sonst geradeaus weiter ins Wasser
        var wall = new GameObject("Port End Wall");
        wall.transform.SetParent(gameplayParent, false);
        float wallU = PortU(PortEndX - 1f);
        wall.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(wallU) + Vector3.up * 5f, LevelBuildKit.Frame(wallU));
        PrimitiveKit.AddBoxCollider(wall, Vector3.zero, new Vector3(0.5f, 10f, 6f));
    }
}
