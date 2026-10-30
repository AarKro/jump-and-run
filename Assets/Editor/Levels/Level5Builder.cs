using System.Collections.Generic;
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
// rechts auf den Ostgehweg des Mozzarella Blvd (x -33, gleiche Ecke wie Level 3) und nach Norden über die Crust Street.
// Am Founder's Circle ist die Straße versperrt (Krater, Schutt, ein umgestürzter Turm quer über dem Mozzarella Blvd):
// Umweg durch den Pesto Park, links auf den Südost-Diagonalweg (dort der Feueratem), quer über den Rasen nach Norden aus
// dem Park hinaus und über die Four Cheese Avenue, links auf deren Nordgehweg nach Westen an den Ruinen entlang (die Echse
// rechts im Hintergrund, nie zwischen ihren Beinen), über die Oregano Street bis zur Basil Street, rechts auf deren
// Ostgehweg nach Norden über die Salami Street zum Kunden (Nr. 13, z 292, zwischen Salami Street und Calzone Road).
// Die Bahn ist in +u-Richtung definiert (Häuser links davon): Sie beginnt beim Kunden und endet hinter der Pizzeria.
// Auf der Margherita Avenue ist u = Welt-X, auf dem unteren Mozzarella Blvd u = -31 - z.
// 2026-10-29 umgebaut: Vorher lief die Route auf dem Nordost-Diagonalweg zurück auf den Mozzarella Blvd und nach Norden
// zwischen den Beinen der Echse hindurch zum Kunden (Nr. 1, z 305).
public static partial class Level5Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level05.unity";
    private const string TemplateScenePath = "Assets/Scenes/levels/level02.unity";
    private const string PreviousScenePath = "Assets/Scenes/levels/level04.unity";
    // Route seit 2026-10-29 etwa 45 m länger (Four Cheese und Basil statt oberer Mozzarella Blvd)
    private const float KeepWarmTime = 106f;

    // Mozzarella Blvd: Laufbahn auf dem Ostgehweg, Bordstein bei -34, Fassaden bei -30, Fahrbahn bis -46 (wie Level 3)
    private const float MozzarellaX = -33f;
    private const float AvenueCurbLocalZ = -1f;
    private const float FacadeLocalZ = 3f;

    // ---------- Neues Ende im Nordwesten ----------
    // Basil Street (Mitte x -180): Laufbahn auf dem Ostgehweg (Bordstein -174, Fassaden -170), Kamera über der Fahrbahn.
    // Four Cheese Avenue (Mitte z 215): Laufbahn auf dem Nordgehweg (Bordstein 221, Fassaden 225)
    private const float BasilX = -173f;
    private const float FourCheeseLaneZ = 222f;
    // Kundenhaus Nr. 13 mitten im Block zwischen Salami Street (z 265) und Calzone Road (CityLayout.Level5CustomerLot)
    private static float GoalZ => CityLayout.Level5CustomerLot.center.y;
    private static float LaneTopZ => GoalZ + 7f;
    // Ecke Basil/Four Cheese: um die Südwest-Ecke des Häuserblocks (Fassaden-Ecke bei x -170, z 225, 3 m von der Bahn)
    private const float BasilCornerRadius = 3f;
    private const float SalamiZ = 265f, OreganoX = -110f;
    private const float AvenueHalfRoad = 6f;
    private const float MainEndX = 16f;
    // Ecke Margherita → Mozzarella: Radius so, dass ganze Meter z auf ganze Meter u fallen (wie Level 3)
    private static readonly float CornerRadius = 2f / (2f - Mathf.PI / 2f);

    // Crust Street östlich (z 112, 12 m Fahrbahn) quert die Bahn auf dem unteren Mozzarella Blvd; Four Cheese liegt im Umweg
    private const float CrustEastZ = 112f;

    // ---------- Umweg durch den Park ----------
    // Die Diagonalwege laufen von den Park-Ecken genau auf den Brunnen zu. Verlängert treffen sie die Laufbahn auf dem
    // Mozzarella Blvd (x -33) bei z ~130 (Founder's Circle) und z ~215 (Four Cheese Avenue). Dazwischen geht es quer über
    // den Rasen nach Norden, ParkLegS vom Brunnen entfernt. Alle Ecken mit DetourRadius
    private const float ParkLegS = 35f;
    private const float DetourRadius = 6f;
    // Blockade: Krater und Schutt auf dem Gehweg gleich nördlich der Abzweigung, dahinter ein umgestürzter Turm quer über
    // der Straße (vom Downtown-Block bis zum Westbordstein)
    private const float BlockadeTowerZ = 154f;

    // Zerstörtes Hochhausviertel: Grundstücke beidseits des Mozzarella Blvd (Welt-Rechtecke von/bis), ab der Four Cheese Avenue
    // Ab den Fassaden (z 225): Davor liegt der Nordgehweg der Four Cheese Avenue, auf dem jetzt die Bahn läuft
    private static readonly Rect RuinsWest = Rect.MinMaxRect(-100f, 225f, -50f, 315f);
    private static readonly Rect RuinsEast = Rect.MinMaxRect(-30f, 225f, 45f, 315f);
    // Hochhaus, das die Echse im Intro zerschlägt: östlich der Bahn, gleich südlich ihres Fußes, in Reichweite ihres Arms
    // (Schulter bei etwa x -29, z 246, 60 m hoch; der Schlag schräg nach unten trifft die Spitze)
    private static readonly Rect IntroTowerLot = Rect.MinMaxRect(-30f, 226f, -16f, 240f);
    // Hier bleibt Platz: Intro-Hochhaus und die Füße der Monster (x -40 ± 14, Echse z 254 + 3, Roboter z 290)
    private static readonly Rect[] KeepClear =
    {
        Rect.MinMaxRect(-34f, 245f, -16f, 266f), Rect.MinMaxRect(-64f, 245f, -46f, 266f),
        Rect.MinMaxRect(-34f, 280f, -16f, 300f), Rect.MinMaxRect(-64f, 280f, -46f, 300f),
        IntroTowerLot,
    };
    private const float IntroTowerHeight = 52f;
    // Hier liegt die Echse nach dem Sturz (westlich ihrer Füße, Kopf auf der Oregano Street an der Salami Street): Dort stehen
    // keine Stümpfe oder umgestürzten Türme (sie lag mitten hindurch), sondern ein flachgedrücktes Schuttbett. 2026-10-29
    private static readonly Rect LizardBed = Rect.MinMaxRect(-100f, 238f, -46f, 280f);
    // Hier liegt ihr Schwanz im Kampf und nach dem Sturz: flach auf der Fahrbahn des Mozzarella Blvd nach Süden, über die
    // Kreuzung mit der Four Cheese Avenue bis an den Park (vorher schwebte er quer über die Four Cheese Avenue in den Park)
    private static readonly Rect TailRoad = Rect.MinMaxRect(-49f, 176f, -31f, 252f);
    // Um so viel kippt sie im Finale zur Seite: so weit, dass der Kopf auf der Straße liegt statt darüber zu schweben
    private const float LizardFallAngle = 86f;
    // Kopf nach dem Sturz: schaut nach Westen die Salami Street hinunter, der Unterkiefer liegt flach auf der Straße, das Maul
    // steht so weit offen (KaijuHiccups.jawOpen; beim Hicks hebt sich der Oberkopf noch weiter)
    private const float HiccupJawRest = 22f;
    // Oberkörper nach dem Sturz um die Wirbelsäule auf den Bauch gerollt: Rückenplatten zeigen schräg nach oben
    private const float FallenBellyRoll = -60f;
    // Kamera im Vorspann: südwestlich, Echse und Turm nebeneinander im Bild
    private static readonly Vector3 ExposeCameraFrom = new Vector3(-100f, 34f, 176f);
    private static readonly Vector3 ExposeCameraTo = new Vector3(-86f, 42f, 192f);
    private static readonly Vector3 ExposeLook = new Vector3(-32f, 38f, 240f);
    private const float ExposeDuration = 6f;
    private const float TailYaw = 35f;
    // ---------- Feueratem (einmal, beim Betreten des Parks) ----------
    //   Auslöser: Der Spieler hat auf dem Südost-Diagonalweg die liegende Statue hinter sich (sonst bliebe für das Chili darauf
    //   keine Zeit, 2026-10-29; vorher schon beim Abbiegen vom Mozzarella Blvd). Die Echse lädt auf, der Eisverkäufer
    //   hinter seinem umgekippten Wagen (TruckS vom Brunnen, hinter der Bahn = zur Echse hin) ruft, dann 3 s Feuer auf dem
    //   ganzen Park-Stück der Bahn, außer direkt vor dem Wagen. Bäume nördlich des Wegs fangen Feuer
    private const float TruckS = IceCreamTruckKit.S;
    private const float TruckLength = IceCreamTruckKit.Length, TruckHeight = IceCreamTruckKit.Width, TruckDepth = IceCreamTruckKit.Height, TruckBackOffset = 2.7f;
    private const float ShelterHalf = 2.4f;
    private static readonly Rect BurnArea = Rect.MinMaxRect(-88f, 142f, -47f, 207f);
    // Kamera-Zone während des Ereignisses: weiter weg und schräg von hinten (+u, über der Kreuzung Crust Street/Mozzarella
    // Blvd, dort stehen keine Häuser), Blick leicht nach oben: Spieler und Wagen unten, die Echse (~110 m nördlich) oben
    private static readonly Vector3 BreathCameraOffset = new Vector3(18f, 6f, -16f);
    private const float BreathCameraLookHeight = 8.3f;
    // Vom Auslöser hinter der Statue bis in den Schutz des Wagens sind es ~10 m (1.5 s), 3.5 s Aufladen lassen gut 2 s Luft
    // (vorher 5 s ab der Abbiegung, ~23 m)
    private const float BreathChargeTime = 3.5f;
    private const float BreathTime = 4.5f;

    // ---------- Roboter ----------
    //   Spieler verlässt den Park nach Norden (ArrivalZ, Rand des Rasens): Zwischensequenz, der Roboter stürzt herab, die
    //           Echse dreht sich um, ab dann ringen beide (kein Feuer mehr)
    //   Spieler überquert die Oregano Street: Zwischensequenz, Aufwärtshaken, die Echse kippt nach Westen um
    private const float ArrivalZ = 205f;
    // Bis hierher reicht der Feuerbereich auf dem Rasen-Stück (dort, wo es früher auf den Nordost-Weg abbog)
    private static float BreathLegTopZ => CityLayout.ParkCenter.y + DirNE.y * ParkLegS;

    // Mittelpunkt der Monster wie früher in der Stadt (CityLayout.KaijuFight): Echse 16 m südlich davon, Roboter 20 m nördlich
    private static Vector2 KaijuCenter => CityLayout.KaijuFight;

    private static Transform groundParent, gameplayParent, propParent;
    private static readonly List<float> pieceStartU = new List<float>();

    // Basil Street (Kunde), Four Cheese Avenue (an den Ruinen), Rasen-Stück durch den Park nach Norden, unterer Mozzarella Blvd
    // (Crust Street bis zur Abzweigung in den Park)
    public static float BasilU(float z) => pieceStartU[0] + (LaneTopZ - z);
    public static float FourCheeseU(float x) => pieceStartU[2] + (x - (BasilX + BasilCornerRadius));
    public static float LegU(float z) => pieceStartU[4] + (FourCheeseLaneZ - DetourRadius - z);
    public static float LowerMozzarellaU(float z) => pieceStartU[8] + (lowerTopZ - z);
    // Südost-Diagonalweg (Feueratem): s = Abstand vom Brunnen, die Bahn läuft dort in +u nach außen
    public static float ParkSEU(float s) => pieceStartU[6] + (s - (ParkLegS + detourTangent));
    private static Vector2 DirSE => (new Vector2(CityLayout.ParkArea.xMax + 1f, CityLayout.ParkArea.yMin - 1f) - CityLayout.ParkCenter).normalized;
    private static Vector2 DirNE => (new Vector2(CityLayout.ParkArea.xMax + 1f, CityLayout.ParkArea.yMax + 1f) - CityLayout.ParkCenter).normalized;
    private static float detourTangent, lowerTopZ, diagonalS, legX;

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
        LevelBuildKit.EnsureScene(ScenePath, TemplateScenePath, PreviousScenePath, "Level5Builder");
        var scene = LevelBuildKit.OpenAndClear(ScenePath);
        BuildLane();
        groundParent = LevelBuildKit.Group("Level Ground");
        gameplayParent = LevelBuildKit.Group("Level Gameplay");
        propParent = LevelBuildKit.Group("Level Props");

        Transform link = LevelBuildKit.CityLink("Pizzeria");
        BuildGround(link);
        BuildFallenStatue(link);
        BuildBlockade();
        BuildParkBlockade(link);
        BuildStartBlockade(link);
        BuildRuins(link);
        BuildKaiju();
        BuildGameplay();
        BuildFirefighters();
        BuildFireStation();
        BuildRubble();
        BuildPanic();

        LevelBuildKit.SetupEssentials(Mathf.Round(BasilU(GoalZ)), "13", KeepWarmTime,
            new Vector3(-100f, -20f, 170f), new Vector3(200f, 1f, 340f),
            "No. 13, Basil Street", "don't mind the lizard", new Vector3(-40f, 80f, -100f));
        SetupIntroExpose();
        LevelBuildKit.Save(scene);
        Debug.Log($"Level5Builder: Level 5 (Kaiju) gebaut. Bahn u {LevelBuildKit.Lane.StartU:F2} bis {LevelBuildKit.Lane.EndU:F2}, Basil u(z) = {BasilU(0f):F2} - z");
    }

    // ---------- Bahn ----------

    private static void BuildLane()
    {
        float mainStartX = MozzarellaX + CornerRadius;
        Vector2 center = CityLayout.ParkCenter;
        // Wo die Diagonalen x -33 treffen (s = Abstand vom Brunnen), und um wie viel Grad die Bahn dort abknickt
        diagonalS = (MozzarellaX - center.x) / DirSE.x;
        float southCornerZ = center.y + DirSE.y * diagonalS;
        float turn = Mathf.Acos(-DirSE.y) * Mathf.Rad2Deg;   // von Nord-Süd auf die Diagonale, ~61°
        detourTangent = DetourRadius * Mathf.Tan(turn / 2f * Mathf.Deg2Rad);
        float diagonalLength = diagonalS - ParkLegS - 2f * detourTangent;
        lowerTopZ = southCornerZ - detourTangent;
        // Das Rasen-Stück liegt auf der Linie, wo der Südost-Weg ParkLegS vom Brunnen entfernt ist, und läuft geradeaus nach
        // Norden aus dem Park, über die Four Cheese Avenue bis auf deren Nordgehweg
        Vector2 legBottom = center + DirSE * ParkLegS;
        legX = legBottom.x;

        var pieces = new List<LanePath.Piece>
        {
            LevelBuildKit.Straight(LaneTopZ - FourCheeseLaneZ - BasilCornerRadius),                    // 0 Basil Street (Kunde)
            LevelBuildKit.Arc(BasilCornerRadius, 90f),                                                  // 1 um die Blockecke
            LevelBuildKit.Straight(legX - DetourRadius - (BasilX + BasilCornerRadius)),                  // 2 Four Cheese Avenue (Ruinen)
            LevelBuildKit.Arc(DetourRadius, -90f),                                                      // 3 über die Fahrbahn
            LevelBuildKit.Straight(FourCheeseLaneZ - DetourRadius - (legBottom.y + detourTangent)),      // 4 quer über den Rasen nach Süden
            LevelBuildKit.Arc(DetourRadius, turn),                                                      // 5
            LevelBuildKit.Straight(diagonalLength),                             // 6 Südost-Diagonalweg (Feueratem)
            LevelBuildKit.Arc(DetourRadius, -turn),                             // 7 zurück auf den Mozzarella Blvd (Founder's Circle)
            LevelBuildKit.Straight(lowerTopZ - CornerRadius),                   // 8 unterer Mozzarella Blvd (Crust Street)
            LevelBuildKit.Arc(CornerRadius, 90f),                               // 9
            LevelBuildKit.Straight(MainEndX - mainStartX),                      // 10 Margherita Avenue (Pizzeria)
        };
        float beforeMain = 0f;
        for (int i = 0; i < pieces.Count - 1; i++)
        {
            beforeMain += pieces[i].length;
        }
        float startU = mainStartX - beforeMain;
        pieceStartU.Clear();
        float u = startU;
        foreach (LanePath.Piece piece in pieces)
        {
            pieceStartU.Add(u);
            u += piece.length;
        }
        LevelBuildKit.BuildLane(new Vector3(BasilX, 0f, LaneTopZ), -90f, startU, pieces);
    }

    // ---------- Boden ----------

    private static void BuildGround(Transform link)
    {
        // Querstraßen haben in der Stadt keinen Collider: unsichtbarer Laufboden auf Gehweghöhe (Autos fahren hier keine,
        // die Stadt ist auf der Flucht)
        LevelBuildKit.WalkGround("Walk Crossing Crust", LowerMozzarellaU(CrustEastZ + 6.5f), LowerMozzarellaU(CrustEastZ - 6.5f), CityLayout.Surface, groundParent);
        LevelBuildKit.WalkGround("Walk Crossing Oregano", FourCheeseU(OreganoX - AvenueHalfRoad - 1f), FourCheeseU(OreganoX + AvenueHalfRoad + 1f), CityLayout.Surface, groundParent);
        LevelBuildKit.WalkGround("Walk Crossing Salami", BasilU(SalamiZ + AvenueHalfRoad + 1f), BasilU(SalamiZ - AvenueHalfRoad - 1f), CityLayout.Surface, groundParent);
        // Nordgehweg der Four Cheese Avenue an den Ruinen: Der Gehweg-Streifen des Hochhaus-Blocks hat dort eine Kante (die
        // Ruinen beginnen an den Fassaden), ein durchgehender Laufboden ist sicherer
        LevelBuildKit.WalkGround("Walk Four Cheese Ruins", FourCheeseU(RuinsWest.xMin - 3f), pieceStartU[3], CityLayout.Surface, groundParent);
        // Umweg: Four Cheese Avenue (Fahrbahn), Park-Wege und Rasen (ohne Collider), Founder's Circle (ebenso).
        // Auf Höhe der Park-Wege; auf den Gehweg-Stücken an den Enden sind das 4 cm mehr, das merkt man nicht
        // Stück für Stück: Über den ganzen Umweg zeigt die Bahn am Anfang, in der Mitte und am Ende nach Süden, WalkGround
        // hielte ihn sonst für eine Gerade
        var bounds = new List<float> { pieceStartU[3] };
        for (int i = 4; i <= 8; i++)
        {
            bounds.Add(pieceStartU[i]);
        }
        bounds.Add(LowerMozzarellaU(123f));
        for (int i = 0; i < bounds.Count - 1; i++)
        {
            LevelBuildKit.WalkGround($"Walk Detour {i + 1}", bounds[i], bounds[i + 1], CityLayout.ParkPathTop, groundParent);
        }

        // Bäume, die im Weg stünden (nur Bäume und Büsche, Boden und Wege bleiben): auf dem Rasen-Stück (es führt quer durch
        // den Baumbestand bis an den Nordrand des Parks) und westlich davon (Kamera), auf der Kameraseite des Südost-Wegs
        // (Südwesten, die Kamera des Feueratems steht dort weiter weg als sonst)
        Vector2 center = CityLayout.ParkCenter;
        Vector2 legBottom = center + DirSE * ParkLegS;
        LevelBuildKit.Cutout(link, "Trees Park Leg", legX - 13f, legX + 3.5f, legBottom.y - 4f, CityLayout.ParkArea.yMax + 6f, treesOnly: true);
        var normalSE = new Vector2(DirSE.y, -DirSE.x);
        for (float s = ParkLegS; s <= diagonalS; s += 8f)
        {
            Vector2 se = center + DirSE * s + normalSE * 11f;
            LevelBuildKit.Cutout(link, $"Trees SE s{s:F0}", se.x - 9f, se.x + 9f, se.y - 9f, se.y + 9f, treesOnly: true);
        }

        // Das Haus fester Größe der Stadt auf dem Kunden-Grundstück weg, dort steht das Kundenhaus des Goal-Prefabs. Den Boden
        // stehen lassen (er liegt unter dem Haus)
        Rect customer = CityLayout.Level5CustomerLot;
        LevelBuildKit.Cutout(link, "Customer House", customer.xMin - 0.2f, customer.xMax + 0.2f, customer.yMin - 0.2f, customer.yMax + 0.2f, keepGround: true);
    }

    // Der Gründer liegt umgestürzt auf seinem Platz (der Founder's Circle liegt mitten auf dem Mozzarella Blvd, die Kamera 8 m
    // neben der Bahn stünde sonst in der Statue). Ein kleiner Ausschnitt nimmt Sockel, Statue, Rasen und Pflaster der Stadt
    // weg (alle haben ihre Mitte dort), das Level legt Pflaster und Rasen neu hin, dazu den zerbrochenen Sockel und die Statue.
    // 2026-10-29: Die Statue lag vorher hinter der Bahn im Krater, jetzt liegt sie auf der Bahn zum Klettern (Chili 1)
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
        PrimitiveKit.Cylinder("Paving", circle, new Vector3(0f, (roadTop - 0.1f + CityLayout.Surface + 0.01f) / 2f, 0f), 15f, CityLayout.Surface + 0.01f - (roadTop - 0.1f), paving);
        // Rasen nur knapp über dem Laufboden: Die Bahn biegt 2 m nördlich der Mitte über den Platz in den Park
        PrimitiveKit.Cylinder("Lawn", circle, new Vector3(0f, 0.4f, 0f), 7.5f, 0.3f, grass);
        // Sockel abgebrochen, schief, vom Sturz nach Südwesten verschoben
        Renderer pedestal = PrimitiveKit.Box("Broken Pedestal", circle, new Vector3(-1.5f, 0.6f, -2f), new Vector3(3.2f, 0.4f, 3.2f), marble);
        pedestal.transform.localRotation = Quaternion.Euler(4f, 20f, -6f);
        BuildStatueClimb();
        // Schutt vom Sockel
        var random = new System.Random(130);
        for (int i = 0; i < 6; i++)
        {
            Vector3 position = new Vector3((float)random.NextDouble() * 5f - 3.5f, 0.85f, (float)random.NextDouble() * 4f - 5f);
            Renderer chunk = PrimitiveKit.Box("Marble Chunk", circle, position, Vector3.one * (0.4f + (float)random.NextDouble() * 0.6f), marble);
            chunk.transform.localRotation = Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360));
        }
    }

    // Die Statue liegt rücklings neben dem Sockel quer über dem Platz, genau auf der Bahn (Südost-Diagonalweg, gleich hinter
    // der Biegung vom Mozzarella Blvd): Füße zum Mozzarella Blvd (+u), Kopf zum Park. Der rechte Arm hält die Pizza noch
    // immer in den Himmel. Begehbar (Layer Ground): auf den Körper springen (1.5 m), zur Schulter laufen, auf die Pizza
    // (3 m über dem Boden), darüber schwebt Chili 1. Wer sie auslässt, läuft einfach weiter
    private const float StatueBodyHeight = 1.5f, StatueBodyLength = 4.4f;
    private const float StatuePizzaTop = 3.5f, StatuePizzaRadius = 1.1f;
    private const float StatueChiliY = 5f;
    // Hier (hinter der Pizza, in Laufrichtung) beginnt der Auslöser des Feueratems
    private static float statueEndU;

    private static void BuildStatueClimb()
    {
        Material bronze = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Bronze.mat");
        const float ground = CityLayout.ParkPathTop;
        // Die Pizza (und das Chili, Prefab-Wurzeln stehen auf ganzen Metern) auf ganzem u, der Körper schließt in +u an
        Vector2 offset = CityLayout.FoundersCircle - CityLayout.ParkCenter;
        float centerU = ParkSEU(Vector2.Dot(offset, DirSE));
        // Der Körper muss vor der Biegung enden (die Collider liegen gerade im Lane-Rahmen): notfalls weiter in den Park
        float pizzaU = Mathf.Min(Mathf.Round(centerU - 1f), Mathf.Floor(pieceStartU[7] - 0.5f - 0.4f - StatueBodyLength));
        float bodyU = pizzaU + 0.4f + StatueBodyLength / 2f;
        statueEndU = pizzaU - StatuePizzaRadius - 0.5f;
        Transform statue = new GameObject("Founder Statue (fallen)").transform;
        statue.SetParent(gameplayParent, false);
        statue.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(bodyU) + Vector3.up * ground, LevelBuildKit.Frame(bodyU));
        // Ein sauberer Quader zum Laufen, die Optik aus Kapseln liegt drumherum (oben nur knapp über der Kante)
        var body = new GameObject("Body Walk") { layer = 3 };
        body.transform.SetParent(statue, false);
        PrimitiveKit.AddBoxCollider(body, new Vector3(0f, StatueBodyHeight / 2f, 0f), new Vector3(StatueBodyLength, StatueBodyHeight, 2.2f));
        float half = StatueBodyLength / 2f;
        // Lokal +X = Füße. Kapseln liegen um Z gedreht entlang X
        Quaternion lying = Quaternion.Euler(0f, 0f, 90f);
        foreach (float side in new[] { -0.4f, 0.4f })
        {
            PrimitiveKit.Part("Leg", PrimitiveType.Capsule, statue, new Vector3(half - 1.1f, 0.65f, side), new Vector3(0.75f, 1.2f, 0.75f), bronze).transform.localRotation = lying;
        }
        PrimitiveKit.Part("Body", PrimitiveType.Capsule, statue, new Vector3(-0.6f, 0.8f, 0f), new Vector3(1.55f, 1.3f, 1.4f), bronze).transform.localRotation = lying;
        PrimitiveKit.Part("Head", PrimitiveType.Sphere, statue, new Vector3(-half + 0.35f, 0.75f, -0.1f), Vector3.one * 1.05f, bronze);
        PrimitiveKit.Cylinder("Chef Hat", statue, new Vector3(-half - 0.25f, 0.75f, -0.1f), 0.45f, 0.7f, bronze).transform.localRotation = lying;
        PrimitiveKit.Part("Arm Down", PrimitiveType.Capsule, statue, new Vector3(-0.4f, 0.55f, -0.85f), new Vector3(0.38f, 0.75f, 0.38f), bronze).transform.localRotation = lying;

        // Rechter Arm senkrecht aus der Schulter (hinter der Bahn), oben die Pizza: Sie reicht über die Bahn
        float pizzaX = pizzaU - bodyU;
        const float pizzaZ = 0.5f;
        float armTop = StatuePizzaTop - 0.25f;
        PrimitiveKit.Part("Arm Up", PrimitiveType.Capsule, statue, new Vector3(pizzaX + 0.2f, armTop / 2f + 0.3f, pizzaZ + 0.3f), new Vector3(0.42f, armTop / 2f - 0.1f, 0.42f), bronze);
        PrimitiveKit.Part("Hand", PrimitiveType.Sphere, statue, new Vector3(pizzaX + 0.2f, armTop - 0.05f, pizzaZ + 0.3f), Vector3.one * 0.55f, bronze);
        var pizza = new GameObject("Pizza Walk") { layer = 3 };
        pizza.transform.SetParent(statue, false);
        pizza.transform.localPosition = new Vector3(pizzaX, StatuePizzaTop - 0.125f, pizzaZ);
        PrimitiveKit.AddBoxCollider(pizza, Vector3.zero, new Vector3(StatuePizzaRadius * 2f, 0.25f, StatuePizzaRadius * 2f));
        PrimitiveKit.Cylinder("Pizza", pizza.transform, Vector3.zero, StatuePizzaRadius, 0.25f, bronze);
        // Pepperoni-Scheiben aus Bronze, damit man die Pizza von unten schräg erkennt
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f * Mathf.Deg2Rad;
            PrimitiveKit.Cylinder("Pepperoni", pizza.transform, new Vector3(Mathf.Cos(angle) * 0.6f, 0.13f, Mathf.Sin(angle) * 0.6f), 0.17f, 0.04f, bronze);
        }
        LevelBuildKit.HeatPickup(pizzaU, StatueChiliY, gameplayParent);
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Östlich der Pizzeria geht es nicht weiter (wie in Level 2–4). Die Autos stehen gleich vor dem umgestürzten Hochhaus
        // (BuildStartBlockade), das hintere halb darunter
        LevelBuildKit.Place("Car Crash", 3, 0, gameplayParent);
        // Hinter dem Kunden: unsichtbare Wand (die Bahn ginge sonst geradeaus weiter)
        var wall = new GameObject("North End Wall");
        wall.transform.SetParent(gameplayParent, false);
        float wallU = pieceStartU[0] - 1f;
        wall.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(wallU) + Vector3.up * 5f, LevelBuildKit.Frame(wallU));
        PrimitiveKit.AddBoxCollider(wall, Vector3.zero, new Vector3(0.5f, 10f, 6f));
    }

    // Leute in Panik: fliehen auf einem Streifen 2.4 m hinter der Bahn (zwischen Bahn und Häusern) weg von der Echse: in der
    // Basil Street nach Norden (der Spieler überholt sie), am unteren Mozzarella Blvd nach Süden, ihm entgegen. Auf der Four
    // Cheese Avenue liefen sie früher an den Ruinen entlang, dort liegt jetzt der Schutt zum Klettern
    private const float PanicBehind = 2.4f;
    private static (Vector3 from, Vector3 to, int count)[] PanicStrips => new[]
    {
        (new Vector3(BasilX + PanicBehind, CityLayout.Surface, 234f), new Vector3(BasilX + PanicBehind, CityLayout.Surface, 262f), 4),
        (new Vector3(MozzarellaX + PanicBehind, CityLayout.Surface, 126f), new Vector3(MozzarellaX + PanicBehind, CityLayout.Surface, 100f), 4),
    };

    private static void BuildPanic()
    {
        Transform crowd = new GameObject("Panicking People").transform;
        crowd.SetParent(propParent, false);
        var random = new System.Random(77);
        foreach ((Vector3 stripFrom, Vector3 stripTo, int count) in PanicStrips)
        {
            Vector3 run = stripTo - stripFrom;
            float length = run.magnitude;
            Vector3 direction = run / length;
            var sideways = new Vector3(direction.z, 0f, -direction.x);
            float yaw = Quaternion.LookRotation(direction).eulerAngles.y;
            for (int i = 0; i < count; i++)
            {
                float offset = (i + (float)random.NextDouble() * 0.6f) * length / count;
                Vector3 jitter = sideways * (((float)random.NextDouble() - 0.5f) * 0.6f);
                Vector3 from = stripFrom + jitter;
                CrowdFigure figure = MarketSetup.Person(crowd, "Panicking Person", from + direction * offset, yaw, 0f);
                var figureData = new SerializedObject(figure);
                figureData.FindProperty("panic").boolValue = true;
                figureData.FindProperty("strideLength").floatValue = 1.6f;
                figureData.FindProperty("bobHeight").floatValue = 0.1f;
                figureData.ApplyModifiedPropertiesWithoutUndo();
                var runner = new SerializedObject(figure.gameObject.AddComponent<PanicRunner>());
                runner.FindProperty("from").vector3Value = from;
                runner.FindProperty("to").vector3Value = stripTo + jitter;
                runner.FindProperty("speed").floatValue = 4.2f + (float)random.NextDouble() * 1.6f;
                runner.FindProperty("startOffset").floatValue = offset;
                runner.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
