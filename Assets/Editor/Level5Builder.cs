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
public static class Level5Builder
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
    // Kopf nach dem Sturz: schaut nach Westen die Salami Street hinunter, leicht nach unten (Kinn am Boden)
    private const float FallenHeadDip = 16f;
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
        EnsureScene();
        var scene = LevelBuildKit.OpenAndClear(ScenePath);
        BuildLane();
        groundParent = LevelBuildKit.Group("Level Ground");
        gameplayParent = LevelBuildKit.Group("Level Gameplay");
        propParent = LevelBuildKit.Group("Level Props");

        Transform link = LevelBuildKit.CityLink("Pizzeria");
        BuildGround(link);
        BuildFallenStatue(link);
        BuildBlockade();
        BuildStartBlockade(link);
        BuildRuins(link);
        BuildKaiju();
        BuildGameplay();
        BuildFirefighters();
        BuildRubble();
        BuildPanic();

        LevelBuildKit.SetupEssentials(Mathf.Round(BasilU(GoalZ)), "13", KeepWarmTime,
            new Vector3(-100f, -20f, 170f), new Vector3(200f, 1f, 340f),
            "No. 13, Basil Street", "don't mind the lizard", new Vector3(-40f, 80f, -100f));
        SetupIntroExpose();
        LevelBuildKit.Save(scene);
        Debug.Log($"Level5Builder: Level 5 (Kaiju) gebaut. Bahn u {LevelBuildKit.Lane.StartU:F2} bis {LevelBuildKit.Lane.EndU:F2}, Basil u(z) = {BasilU(0f):F2} - z");
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
        Cylinder("Paving", circle, new Vector3(0f, (roadTop - 0.1f + CityLayout.Surface + 0.01f) / 2f, 0f), 15f, CityLayout.Surface + 0.01f - (roadTop - 0.1f), paving);
        // Rasen nur knapp über dem Laufboden: Die Bahn biegt 2 m nördlich der Mitte über den Platz in den Park
        Cylinder("Lawn", circle, new Vector3(0f, 0.4f, 0f), 7.5f, 0.3f, grass);
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
        Cylinder("Chef Hat", statue, new Vector3(-half - 0.25f, 0.75f, -0.1f), 0.45f, 0.7f, bronze).transform.localRotation = lying;
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
        Cylinder("Pizza", pizza.transform, Vector3.zero, StatuePizzaRadius, 0.25f, bronze);
        // Pepperoni-Scheiben aus Bronze, damit man die Pizza von unten schräg erkennt
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f * Mathf.Deg2Rad;
            Cylinder("Pepperoni", pizza.transform, new Vector3(Mathf.Cos(angle) * 0.6f, 0.13f, Mathf.Sin(angle) * 0.6f), 0.17f, 0.04f, bronze);
        }
        LevelBuildKit.HeatPickup(pizzaU, StatueChiliY, gameplayParent);
    }

    // ---------- Blockade am Founder's Circle ----------

    // Warum der Umweg: gleich nördlich der Abzweigung ein Krater mit Schutt auf Gehweg und Fahrbahn, dahinter liegt ein
    // umgestürzter Turm quer über der Straße (vom Downtown-Block bis zum Westbordstein). Kulisse, keine Collider (die Bahn
    // biegt vorher ab)
    private static void BuildBlockade()
    {
        Transform blockade = new GameObject("Blockade").transform;
        blockade.SetParent(propParent, false);
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        Material scorched = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Scorched.mat");
        var random = new System.Random(150);
        float craterZ = lowerTopZ + 10f;
        Cylinder("Crater", blockade, new Vector3(-36f, CityLayout.Surface + 0.02f, craterZ), 5f, 0.1f, scorched);
        Cylinder("Crater Rim", blockade, new Vector3(-36f, CityLayout.Surface + 0.1f, craterZ), 6.2f, 0.25f, rubble);
        RubblePile(blockade, new Vector3(-33f, CityLayout.Surface, craterZ - 3f), 3f, rubble, facade, random);
        RubblePile(blockade, new Vector3(-38f, CityLayout.RoadTop, craterZ + 4f), 4f, rubble, facade, random);
        RubblePile(blockade, new Vector3(-31f, CityLayout.Surface, craterZ + 6f), 3f, rubble, facade, random);

        // Turmstück liegt quer über der Straße, von den Fassaden der Ostseite (x -30) bis an den Park, Schutt an beiden Enden
        Renderer tower = PrimitiveKit.Box("Fallen Tower", blockade, new Vector3(-38f, CityLayout.Surface + 3.5f, BlockadeTowerZ), new Vector3(19f, 9f, 9f), facade);
        tower.transform.localRotation = Quaternion.Euler(-3f, 6f, 4f);
        RubblePile(blockade, new Vector3(-30f, CityLayout.Surface, BlockadeTowerZ - 4f), 4f, rubble, facade, random);
        RubblePile(blockade, new Vector3(-47f, CityLayout.RoadTop, BlockadeTowerZ - 2f), 3f, rubble, facade, random);
        Smoke(blockade, new Vector3(-34f, CityLayout.Surface, BlockadeTowerZ - 5f));
    }

    // ---------- Blockade östlich der Pizzeria ----------

    // Warum es nach links (Westen) geht: Rechts der Pizzeria ist ein Wohnhochhaus quer über Gehweg und Straße gestürzt (oben
    // ~9 m, viel höher als der kleine Schutthügel links). Vom Haus steht nur noch ein Stumpf, der Turm liegt mit dem
    // abgebrochenen Ende darauf und mit dem Dach auf der Fahrbahn. Kulisse ohne Collider: Die unsichtbare Wand des Car Crash
    // davor hält den Spieler auf
    private static readonly Rect CrushedHouseLot = Rect.MinMaxRect(5.2f, 3f, 14.4f, 11f);
    private const float StartTowerX = 10.5f, StartTowerWidth = 7f, StartTowerThickness = 8f;
    // Unterkante: abgebrochenes Ende auf dem Stumpf (z, y), Dach auf der Fahrbahn jenseits der Mittellinie
    private static readonly Vector2 StartTowerBreak = new Vector2(9f, 5.5f), StartTowerRoof = new Vector2(-15f, CityLayout.RoadTop);

    private static void BuildStartBlockade(Transform link)
    {
        Rect lot = CrushedHouseLot;
        // Das Stadthaus auf dem Grundstück weg (seine Mitte liegt darin), Boden bleibt
        LevelBuildKit.Cutout(link, "Crushed House", lot.xMin, lot.xMax, lot.yMin, lot.yMax, keepGround: true);
        Transform blockade = new GameObject("Start Blockade").transform;
        blockade.SetParent(propParent, false);
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        var random = new System.Random(240);
        Stump(blockade, lot, StartTowerBreak.y - CityLayout.Surface, facade, rubble, random);

        // Liegender Turm: ein Fassaden-Quader, dessen Hochachse (lokal Y, dort liegen die Stockwerke) jetzt schräg nach Norden
        // zeigt. Gekippt um X: Y wird zu (0, sin a, cos a), die Fenster-Seiten liegen oben und an den Seiten
        Vector2 axis = StartTowerBreak - StartTowerRoof;   // (dz, dy)
        float tilt = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        float length = axis.magnitude + 2f;   // das Dach-Ende steckt etwas im Schutt
        Quaternion rotation = Quaternion.Euler(0f, 4f, 0f) * Quaternion.Euler(90f - tilt, 0f, 0f);
        Vector2 bottomMid = (StartTowerBreak + StartTowerRoof) / 2f;
        Vector3 up = rotation * Vector3.back;   // lokal -Z zeigt nach dem Kippen schräg nach oben
        Vector3 center = new Vector3(StartTowerX, bottomMid.y, bottomMid.x) + up * (StartTowerThickness / 2f);
        Renderer tower = PrimitiveKit.Box("Fallen High-Rise", blockade, Vector3.zero, new Vector3(StartTowerWidth, length, StartTowerThickness), facade);
        tower.transform.SetPositionAndRotation(center, rotation);
        Transform t = tower.transform;
        // Bruchkante oben (Norden): Eisen und Brocken; Dachkante unten (Süden) mit abgeknickter Antenne
        Material concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Broken Concrete.mat");
        Material rebar = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rebar.mat");
        for (int i = 0; i < 12; i++)
        {
            // Lokale Maße sind wegen der Skalierung Anteile: X und Z -0.5…0.5, Y = 0.5 ist die Bruchkante
            Vector3 local = new Vector3(((float)random.NextDouble() - 0.5f) * 0.9f, 0.5f + (float)random.NextDouble() * 0.03f, ((float)random.NextDouble() - 0.5f) * 0.9f);
            Vector3 world = t.TransformPoint(local);
            Renderer piece = i % 3 == 0
                ? PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, blockade, Vector3.zero, new Vector3(0.08f, 0.8f, 0.08f), rebar)
                : PrimitiveKit.Box("Shard", blockade, Vector3.zero, Vector3.one * (0.8f + (float)random.NextDouble()), concrete);
            piece.transform.SetPositionAndRotation(world, rotation * Quaternion.Euler(random.Next(-40, 40), random.Next(360), random.Next(-40, 40)));
        }
        Vector3 roof = t.TransformPoint(new Vector3(0f, -0.5f, 0f));
        Renderer roofEdge = PrimitiveKit.Box("Roof Edge", blockade, Vector3.zero, new Vector3(StartTowerWidth + 0.3f, 0.4f, StartTowerThickness + 0.3f), concrete);
        roofEdge.transform.SetPositionAndRotation(roof, rotation);
        Renderer antenna = PrimitiveKit.Part("Antenna", PrimitiveType.Cylinder, blockade, Vector3.zero, new Vector3(0.15f, 3f, 0.15f), rebar);
        antenna.transform.SetPositionAndRotation(roof + Vector3.up * 3f + Vector3.forward * -2.5f, Quaternion.Euler(-50f, 20f, 0f));

        // Schutt am Fuß: auf dem Gehweg vor dem Stumpf, auf der Fahrbahn beidseits des Turms
        RubblePile(blockade, new Vector3(StartTowerX - 4f, CityLayout.Surface, 1f), 2.5f, rubble, facade, random);
        RubblePile(blockade, new Vector3(StartTowerX - 4.5f, CityLayout.RoadTop, -7f), 3f, rubble, facade, random);
        RubblePile(blockade, new Vector3(StartTowerX + 4.5f, CityLayout.RoadTop, -10f), 3f, rubble, facade, random);
        RubblePile(blockade, new Vector3(StartTowerX - 3f, CityLayout.RoadTop, -14f), 2.5f, rubble, facade, random);
        // Es brennt in ein paar Fenstern auf der Seite zur Pizzeria, Rauch steigt vom Bruch auf
        foreach (float k in new[] { 0.15f, 0.4f })
        {
            Vector3 window = t.TransformPoint(new Vector3(-0.5f, k, -0.1f));
            HouseFire(blockade, window + Vector3.left * 0.3f);
        }
        Smoke(blockade, t.TransformPoint(new Vector3(0f, 0.45f, -0.4f)));
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
            Cylinder("Crater", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.02f, z), 4.5f, 0.44f, scorched);
            Cylinder("Crater Rim", ruins, new Vector3(-41f, CityLayout.RoadTop + 0.1f, z), 5.7f, 0.3f, rubble);
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

    // ---------- Kaiju ----------

    // Beide Monster wie früher in der Stadt, aber: Die Echse schaut anfangs nach Süden (zum Spieler und zum Park, dorthin
    // speit sie Feuer), der Roboter ist noch nicht da (kommt später aus dem Himmel, eigener Arbeitsschritt). Der Ringkampf
    // (KaijuFight) ist darum erst mal aus
    private static KaijuBattle battleComponent;

    // Intro-Vorspann: Die Kamera gleitet von Südwesten auf die Echse zu, während sie das Hochhaus zerschlägt. Danach fliegt
    // die Kamera von dort zum Kunden
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
        // Der Schwanz zeigt jetzt nach Norden und lag quer über dem Kundenhaus (und im Flug des Intros zum Kunden): weiter nach
        // Osten über die Ruinen schwenken. KaijuFight merkt sich das als Ruhehaltung
        kaiju.lizardTail[0].localRotation *= Quaternion.Euler(0f, TailYaw, 0f);
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

        TowerCollapse tower = BuildIntroTower(particles);
        battleComponent = kaiju.root.gameObject.AddComponent<KaijuBattle>();
        var battle = new SerializedObject(battleComponent);
        battle.FindProperty("lizardRoot").objectReferenceValue = kaiju.lizard;
        battle.FindProperty("lizardUpper").objectReferenceValue = kaiju.lizardUpper;
        battle.FindProperty("lizardHead").objectReferenceValue = kaiju.lizardHead;
        battle.FindProperty("breath").objectReferenceValue = breath;
        // Die Echse ist um 180° gedreht (schaut nach Süden): ihr linker Arm (lokal -X) ist der östliche, zum Turm hin
        battle.FindProperty("lizardSmashArm").objectReferenceValue = kaiju.lizardArmLeft;
        battle.FindProperty("introTower").objectReferenceValue = tower;
        battle.FindProperty("robotRoot").objectReferenceValue = kaiju.robot;
        battle.FindProperty("robotUpper").objectReferenceValue = kaiju.robotUpper;
        battle.FindProperty("robotArmLeft").objectReferenceValue = kaiju.robotArmLeft;
        battle.FindProperty("robotArmRight").objectReferenceValue = kaiju.robotArmRight;
        battle.FindProperty("fight").objectReferenceValue = kaiju.fight;
        PrimitiveKit.SetReferences(battle, "thrusters", thrusters.ToArray());
        battle.FindProperty("landingDust").objectReferenceValue = landingDust;
        battle.FindProperty("fallDust").objectReferenceValue = fallDust;
        // Feueratem, sobald der Spieler an der Statue vorbei ist (bis zum alten Ende des Rasen-Stücks), Roboter, sobald er den
        // Park nach Norden verlässt, Finale an der Oregano Street
        battle.FindProperty("breathFromU").floatValue = LegU(BreathLegTopZ);
        battle.FindProperty("breathToU").floatValue = statueEndU;
        battle.FindProperty("vendor").objectReferenceValue = breathVendor;
        battle.FindProperty("breathCamera").objectReferenceValue = breathCameraZone;
        battle.FindProperty("chargeTime").floatValue = BreathChargeTime;
        // Der Strom braucht gut eine Sekunde bis zum Einschlag, dann rollt die Walze 1.7 s: etwas länger als früher (3 s)
        battle.FindProperty("breathTime").floatValue = BreathTime;
        battle.FindProperty("arrivalU").floatValue = LegU(ArrivalZ);
        battle.FindProperty("finaleU").floatValue = FourCheeseU(OreganoX);
        PrimitiveKit.SetReferences(battle, "lizardTail", kaiju.lizardTail);
        battle.FindProperty("fallAngle").floatValue = LizardFallAngle;
        SetLizardPoses(battle, kaiju);
        battle.ApplyModifiedPropertiesWithoutUndo();
        // Der Schwanz liegt im Kampf auf der Straße: nur leicht schwingen (8° pro Glied fegte die Spitze durch Park und Häuser)
        var fightData = new SerializedObject(kaiju.fight);
        fightData.FindProperty("tailSwing").floatValue = 1.5f;
        fightData.ApplyModifiedPropertiesWithoutUndo();

        // Bildschirm-Wackeln an der Kamera
        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null && mainCamera.GetComponent<CameraShake>() == null)
        {
            mainCamera.AddComponent<CameraShake>();
        }
    }

    // Rechnet die Haltungen für KaijuBattle aus, indem die Echse kurz in die jeweilige Lage gedreht wird: Schwanz im Kampf
    // (sie schaut nach Norden, Schwanz flach nach Süden auf dem Mozzarella Blvd), nach dem Sturz (gekippt, Kopf zur Salami
    // Street, Schwanz von der Hüfte schräg zurück auf den Mozzarella Blvd). Danach steht alles wieder wie gebaut
    private static void SetLizardPoses(SerializedObject battle, CityBuilder.KaijuParts kaiju)
    {
        Transform[] tail = kaiju.lizardTail;
        Quaternion lizardRest = kaiju.lizard.localRotation, headRest = kaiju.lizardHead.localRotation, upperRest = kaiju.lizardUpper.localRotation;
        var tailRest = new Quaternion[tail.Length];
        for (int i = 0; i < tail.Length; i++)
        {
            tailRest[i] = tail[i].localRotation;
        }

        Quaternion facingNorth = lizardRest * Quaternion.Euler(0f, 180f, 0f);
        kaiju.lizard.localRotation = facingNorth;
        Vector3 root = tail[0].position;
        SetQuaternions(battle, "tailFightPose", LayTail(tail, CityLayout.RoadTop,
            new Vector2(root.x, root.z), new Vector2(root.x, root.z - 200f)));

        kaiju.lizard.localRotation = facingNorth * Quaternion.Euler(0f, 0f, LizardFallAngle);
        kaiju.lizardUpper.localRotation = upperRest * Quaternion.Euler(0f, FallenBellyRoll, 0f);
        battle.FindProperty("upperFallen").quaternionValue = kaiju.lizardUpper.localRotation;
        kaiju.lizardHead.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(FallenHeadDip, Vector3.forward) * Vector3.left, Vector3.up);
        battle.FindProperty("headFallen").quaternionValue = kaiju.lizardHead.localRotation;
        root = tail[0].position;
        SetQuaternions(battle, "tailFallenPose", LayTail(tail, CityLayout.Surface,
            new Vector2(root.x, root.z), new Vector2(-48f, 232f), new Vector2(-41f, 214f), new Vector2(-41f, 0f)));

        kaiju.lizard.localRotation = lizardRest;
        kaiju.lizardUpper.localRotation = upperRest;
        kaiju.lizardHead.localRotation = headRest;
        for (int i = 0; i < tail.Length; i++)
        {
            tail[i].localRotation = tailRest[i];
        }
    }

    // Legt die Schwanzkette entlang eines Pfades (Welt X/Z, beginnt an der Wurzel) auf den Boden: Jedes Glied zeigt (mit
    // lokal -Z) auf den nächsten Punkt, der höchstens 70° tiefer liegt und sonst auf Bodenhöhe plus Glied-Radius. Glieder
    // werden der Reihe nach gedreht, weil jedes Kind dort sitzt, wo sein Elternglied hinzeigt. Gibt die lokalen Drehungen zurück
    private static Quaternion[] LayTail(Transform[] tail, float ground, params Vector2[] path)
    {
        var pose = new Quaternion[tail.Length];
        float s = 0f, scale = 1f;
        for (int i = 0; i < tail.Length; i++)
        {
            Vector3 current = tail[i].position;
            float length = i + 1 < tail.Length ? tail[i + 1].localPosition.magnitude : 17f * scale;
            // Segment-Kugel: 12 * scale hoch, etwas eingesunken
            float targetY = ground + 6f * scale * 0.82f - 1f;
            float drop = Mathf.Clamp(current.y - targetY, -0.3f * length, Mathf.Sin(70f * Mathf.Deg2Rad) * length);
            s += Mathf.Sqrt(length * length - drop * drop);
            Vector2 flat = PathPoint(path, s);
            var next = new Vector3(flat.x, current.y - drop, flat.y);
            tail[i].rotation = Quaternion.LookRotation(current - next, Vector3.up);
            pose[i] = tail[i].localRotation;
            scale *= 0.82f;
        }
        return pose;
    }

    // Punkt auf einem Streckenzug nach der Länge s (über das Ende hinaus geradeaus weiter)
    private static Vector2 PathPoint(Vector2[] path, float s)
    {
        for (int i = 0; i + 1 < path.Length; i++)
        {
            float length = Vector2.Distance(path[i], path[i + 1]);
            if (s <= length || i + 2 == path.Length)
            {
                return path[i] + (path[i + 1] - path[i]).normalized * s;
            }
            s -= length;
        }
        return path[0];
    }

    private static void SetQuaternions(SerializedObject data, string name, Quaternion[] values)
    {
        SerializedProperty array = data.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).quaternionValue = values[i];
        }
    }

    private static TowerCollapse BuildIntroTower(Material particles)
    {
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        Rect lot = IntroTowerLot;
        var root = new GameObject("Intro Tower");
        root.transform.SetParent(propParent, false);
        root.transform.position = new Vector3(lot.center.x, CityLayout.Surface, lot.center.y);
        Transform body = PrimitiveKit.Pivot("Body", root.transform, Vector3.zero);
        PrimitiveKit.Box("Tower", body, new Vector3(0f, IntroTowerHeight / 2f, 0f), new Vector3(lot.width - 1f, IntroTowerHeight, lot.height - 1f), facade);
        PrimitiveKit.Box("Crown", body, new Vector3(0f, IntroTowerHeight + 3f, 0f), new Vector3(lot.width - 5f, 6f, lot.height - 5f), facade);

        var random = new System.Random(808);
        Transform aftermath = PrimitiveKit.Pivot("Aftermath", root.transform, Vector3.zero);
        Stump(aftermath, Rect.MinMaxRect(lot.xMin + 1.5f, lot.yMin + 1.5f, lot.xMax - 1.5f, lot.yMax - 1.5f), 9f, facade, rubble, random);
        RubblePile(aftermath, root.transform.position, 7f, rubble, facade, random);

        Transform debrisParent = PrimitiveKit.Pivot("Debris", root.transform, Vector3.zero);
        var debris = new List<Transform>();
        for (int i = 0; i < 16; i++)
        {
            float size = 1.5f + (float)random.NextDouble() * 2.5f;
            Renderer piece = PrimitiveKit.Box("Chunk", debrisParent, Vector3.up * IntroTowerHeight, new Vector3(size, size * 0.7f, size * 1.2f), i % 2 == 0 ? rubble : facade);
            debris.Add(piece.transform);
        }

        ParticleSystem impactDust = Dust(root.transform, "Impact Dust", root.transform.position + Vector3.up * IntroTowerHeight, 8f, particles);
        ParticleSystem baseDust = Dust(root.transform, "Collapse Dust", root.transform.position, 11f, particles);

        var tower = root.AddComponent<TowerCollapse>();
        var serialized = new SerializedObject(tower);
        serialized.FindProperty("body").objectReferenceValue = body;
        serialized.FindProperty("height").floatValue = IntroTowerHeight + 6f;
        serialized.FindProperty("aftermath").objectReferenceValue = aftermath.gameObject;
        PrimitiveKit.SetReferences(serialized, "debris", debris.ToArray());
        serialized.FindProperty("impactDust").objectReferenceValue = impactDust;
        serialized.FindProperty("baseDust").objectReferenceValue = baseDust;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return tower;
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

    // Feueratem: Aufladen am Maul (Kiefer-Scharnier, Feuerball, Schein, eingesaugte Funken, Licht, Qualm aus den Nüstern),
    // Flammenstrom aus Partikeln, Feuerball beim Einschlag hinter dem umgekippten Eiswagen, Feuerwalze über den Boden (im
    // Schatten des Wagens frei), Flammen auf dem Park-Stück der Bahn (links und rechts vom Wagen, nicht davor) und im Park,
    // ein oranges Licht, Vorlage für brennende Bäume, Kamera-Zone fürs Ereignis
    private static FireBreath BuildFireBreath(CityBuilder.KaijuParts kaiju)
    {
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Material core = PrimitiveKit.Material("Assets/Materials/City/Fire Core.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.88f, 0.4f));
        Material glowing = FireAdditiveMaterial(particles);
        Transform root = new GameObject("Fire Breath").transform;
        root.SetParent(gameplayParent, false);

        // ---------- Aufladen am Maul ----------
        // Kopf-Pivot: Schnauze bis z 16.5 (Oberkante y 5.5), Unterkiefer am Scharnier darunter. Der Strom beginnt vorne an der
        // Schnauze, der Feuerball sitzt vor dem offenen Maul (drinnen sähe man ihn von der Spielkamera weit unten nicht)
        Transform mouth = PrimitiveKit.Pivot("Mouth", kaiju.lizardHead, new Vector3(0f, 1f, 16.5f));
        var ballAt = new Vector3(0f, -0.6f, 1.2f);
        Transform glow = PrimitiveKit.Part("Mouth Glow", PrimitiveType.Sphere, mouth, ballAt, Vector3.one, core).transform;
        // Weicher Schein um den Feuerball: wenige große Teilchen, die mit dem Kopf mitgehen (lokale Simulation). Normal
        // gemischt, nicht additiv: Vor dem hellen Himmel ginge ein additiver Schein unter
        ParticleSystem halo = Particles("Glow Halo", mouth, particles, new Color(1f, 0.55f, 0.12f, 0.9f), new Color(1f, 0.72f, 0.25f, 0.85f),
            lifetime: 0.35f, speed: 0f, size: new Vector2(0.8f, 1f), rate: 0f, radius: 0.2f, growth: 0.3f);
        halo.transform.localPosition = ballAt;
        ParticleSystem.MainModule haloMain = halo.main;
        haloMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        // Eine feste Größe 1: FireBreath setzt startSizeMultiplier, und der wirkt bei "zufällig zwischen zwei Werten" nicht
        // auf beide Grenzen (der Schein blieb 1 m groß, versteckt im Feuerball)
        haloMain.startSize = 1f;
        // Funken aus einer Kugel ums Maul, mit negativer Geschwindigkeit fliegen sie zur Mitte (Lebensdauer = Radius / Tempo)
        ParticleSystem embers = Particles("Charge Embers", mouth, particles, new Color(1f, 0.5f, 0.08f, 1f), new Color(1f, 0.82f, 0.3f, 1f),
            lifetime: 1f, speed: 24f, size: new Vector2(2.5f, 5f), rate: 0f, radius: 24f, growth: -0.6f);
        embers.transform.localPosition = ballAt;
        ParticleSystem.MainModule embersMain = embers.main;
        embersMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        embersMain.startSpeed = new ParticleSystem.MinMaxCurve(-26f, -22f);
        embersMain.startLifetime = new ParticleSystem.MinMaxCurve(0.95f, 1.05f);
        embersMain.maxParticles = 600;
        ParticleSystem.ShapeModule embersShape = embers.shape;
        embersShape.shapeType = ParticleSystemShapeType.Sphere;
        embersShape.radius = 24f;
        embersShape.radiusThickness = 0f;
        // Erst unsichtbar, dann voll da: Sie sollen am Rand erscheinen und zur Mitte hin aufleuchten
        var emberFade = new Gradient();
        emberFade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 1f) });
        ParticleSystem.ColorOverLifetimeModule emberColor = embers.colorOverLifetime;
        emberColor.color = emberFade;
        // Qualm aus den Nüstern (oben vorne auf der Schnauze), steigt auf
        var nostrils = new List<ParticleSystem>();
        foreach (float x in new[] { -2f, 2f })
        {
            ParticleSystem smoke = Particles("Nostril Smoke", kaiju.lizardHead, particles, new Color(0.2f, 0.19f, 0.18f, 0.7f), new Color(0.35f, 0.33f, 0.32f, 0.55f),
                lifetime: 2.5f, speed: 5f, size: new Vector2(1.5f, 2.5f), rate: 0f, radius: 0.4f, growth: 3f);
            smoke.transform.localPosition = new Vector3(x, 5.6f, 15.5f);
            nostrils.Add(smoke);
        }
        var mouthLightObject = new GameObject("Mouth Light");
        mouthLightObject.transform.SetParent(mouth, false);
        mouthLightObject.transform.localPosition = new Vector3(0f, 0f, 3f);
        Light mouthLight = mouthLightObject.AddComponent<Light>();
        mouthLight.type = LightType.Point;
        mouthLight.color = new Color(1f, 0.55f, 0.2f);
        mouthLight.range = 45f;
        mouthLight.enabled = false;

        // ---------- Flammenstrom ----------
        // Drei Partikel-Ströme (lokal Z = Flugrichtung, FireBreath richtet sie aufs Ziel und setzt die Lebensdauer): ein heller,
        // schmaler Kern, darum größere Flammen mit Turbulenz, die nach vorn breiter werden, außen dunkler Rauch
        var stream = new List<ParticleSystem>
        {
            Stream("Stream Core", root, glowing, new Color(1f, 0.9f, 0.6f, 0.9f), new Color(1f, 0.75f, 0.35f, 0.9f), 120f, 1.2f, 0.8f, new Vector2(2.5f, 4f), 1.2f, 350f, 1f),
            Stream("Stream Flames", root, glowing, new Color(1f, 0.38f, 0.05f, 0.8f), new Color(1f, 0.6f, 0.15f, 0.8f), 120f, 2.5f, 1.4f, new Vector2(3.5f, 6f), 2.2f, 420f, 3f),
            Stream("Stream Smoke", root, particles, new Color(0.15f, 0.13f, 0.11f, 0.4f), new Color(0.28f, 0.25f, 0.22f, 0.35f), 120f, 4f, 1.8f, new Vector2(4f, 7f), 2.5f, 50f, 5f),
        };

        // Wagen hinter der Bahn (zur Echse hin), der Strom schlägt dahinter auf
        float truckU = ParkSEU(TruckS);
        Vector3 back = LevelBuildKit.Frame(truckU) * Vector3.forward;
        Vector3 truckBase = LevelBuildKit.Lane.PointAt(truckU) + Vector3.up * CityLayout.ParkLawnTop + back * TruckBackOffset;
        Vector3 target = truckBase + back * 7f;
        HidingVendor vendor = IceCreamTruck(truckU);

        // ---------- Einschlag und Walze ----------
        ParticleSystem fireball = Burst("Impact Fireball", root, glowing, new Color(1f, 0.5f, 0.08f, 1f), new Color(1f, 0.85f, 0.35f, 1f),
            lifetime: 1.3f, speed: 22f, size: new Vector2(8f, 18f), count: 90, growth: 1.2f, gravity: -0.3f);
        ParticleSystem mushroom = Burst("Impact Smoke", root, particles, new Color(0.17f, 0.15f, 0.13f, 0.75f), new Color(0.32f, 0.29f, 0.26f, 0.6f),
            lifetime: 4f, speed: 10f, size: new Vector2(10f, 20f), count: 50, growth: 1.6f, gravity: -0.15f);
        ParticleSystem wave = Burst("Fire Wave", root, glowing, new Color(1f, 0.42f, 0.06f, 0.95f), new Color(1f, 0.78f, 0.25f, 0.95f),
            lifetime: 0.9f, speed: 0f, size: new Vector2(3.5f, 7f), count: 8000, growth: 0.6f, gravity: 0f);

        // Flammen auf der Bahn: auf dem Rasen-Stück (bis zur alten Abzweigung) und vom Südost-Weg vor dem Wagen bis zur
        // Abzweigung vom Mozzarella Blvd. Direkt vor dem Wagen (die Deckung) brennt es nicht
        float laneFrom = LegU(BreathLegTopZ);
        float laneTo = pieceStartU[8];
        var fires = new List<ParticleSystem>
        {
            LaneFire(root, "Lane Fire Leg", laneFrom, pieceStartU[5], particles),
            LaneFire(root, "Lane Fire Before", pieceStartU[6] - 3f, truckU - ShelterHalf - 0.6f, particles),
            LaneFire(root, "Lane Fire After", truckU + ShelterHalf + 0.6f, laneTo, particles),
            GroundFire(root, "Impact Fire", target, new Vector3(14f, 14f), particles, 120f),
            GroundFire(root, "Park Fire", new Vector3(BurnArea.center.x, CityLayout.ParkLawnTop, BurnArea.center.y), new Vector3(BurnArea.width, BurnArea.height), particles, 160f),
        };

        var lightObject = new GameObject("Impact Light");
        lightObject.transform.SetParent(root, false);
        lightObject.transform.position = target + Vector3.up * 6f;
        Light impact = lightObject.AddComponent<Light>();
        impact.type = LightType.Point;
        impact.color = new Color(1f, 0.55f, 0.2f);
        impact.range = 60f;
        impact.intensity = 60f;
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

        // Kamera-Zone für das Ereignis (KaijuBattle blendet sie ein): über das Südost-Stück und die Abzweigung
        var zone = new SerializedObject(new GameObject("Breath Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        zone.FindProperty("fromU").floatValue = pieceStartU[5];
        zone.FindProperty("toU").floatValue = pieceStartU[8] + 4f;
        zone.FindProperty("blend").floatValue = 6f;
        zone.FindProperty("offset").vector3Value = BreathCameraOffset;
        zone.FindProperty("lookHeight").floatValue = BreathCameraLookHeight;
        zone.FindProperty("strength").floatValue = 0f;
        zone.ApplyModifiedPropertiesWithoutUndo();
        breathCameraZone = (CameraZone)zone.targetObject;
        breathCameraZone.transform.SetParent(gameplayParent, false);
        breathVendor = vendor;

        var breath = new SerializedObject(root.gameObject.AddComponent<FireBreath>());
        breath.FindProperty("mouth").objectReferenceValue = mouth;
        breath.FindProperty("target").vector3Value = target;
        PrimitiveKit.SetReferences(breath, "stream", stream.ToArray());
        PrimitiveKit.SetReferences(breath, "impactBursts", fireball, mushroom);
        breath.FindProperty("impactLight").objectReferenceValue = impact;
        breath.FindProperty("waveFlames").objectReferenceValue = wave;
        // Schatten: Mitte des liegenden Wagens. Halbe Breite so, dass er auf der Bahn genau so breit ist wie die Lücke in den
        // Flammen davor (ShelterHalf + 0.6)
        Vector3 truckCenter = truckBase;
        float casterDistance = Vector3.Distance(new Vector3(truckCenter.x, 0f, truckCenter.z), new Vector3(target.x, 0f, target.z));
        float laneDistance = casterDistance + TruckBackOffset;
        breath.FindProperty("shadowCaster").vector3Value = truckCenter;
        breath.FindProperty("shadowHalfWidth").floatValue = (ShelterHalf + 0.6f) * casterDistance / laneDistance;
        PrimitiveKit.SetReferences(breath, "groundFires", fires.ToArray());
        breath.FindProperty("jaw").objectReferenceValue = kaiju.lizardJaw;
        breath.FindProperty("mouthGlow").objectReferenceValue = glow;
        breath.FindProperty("glowHalo").objectReferenceValue = halo;
        breath.FindProperty("chargeEmbers").objectReferenceValue = embers;
        PrimitiveKit.SetReferences(breath, "nostrilSmoke", nostrils.ToArray());
        breath.FindProperty("mouthLight").objectReferenceValue = mouthLight;
        breath.FindProperty("hitFromU").floatValue = laneFrom;
        breath.FindProperty("hitToU").floatValue = laneTo;
        breath.FindProperty("burnArea").rectValue = BurnArea;
        breath.FindProperty("treeFireTemplate").objectReferenceValue = treeFire;
        SerializedProperty coverList = breath.FindProperty("covers");
        coverList.arraySize = 1;
        SerializedProperty entry = coverList.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("shelterFrom").floatValue = truckU - ShelterHalf;
        entry.FindPropertyRelative("shelterTo").floatValue = truckU + ShelterHalf;
        // Wer hochspringt, schaut über den Wagen und wird getroffen
        entry.FindPropertyRelative("top").floatValue = CityLayout.ParkLawnTop + TruckHeight;
        breath.ApplyModifiedPropertiesWithoutUndo();
        return (FireBreath)breath.targetObject;
    }

    // Additiv: Die Farben überlagerter Teilchen addieren sich, viele Flammen übereinander werden heller statt trüber (so
    // leuchtet Feuer). Kopie des URP-Partikel-Materials, auf Transparent + Additiv gestellt (Quelle × Alpha + Ziel)
    private static Material FireAdditiveMaterial(Material source)
    {
        const string path = "Assets/Materials/City/Fire Additive.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        material.CopyPropertiesFromMaterial(source);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Ein Strang des Flammenstroms: schmaler Kegel entlang lokal Z (startet aus, FireBreath spielt ihn ab). Die Größe wächst
    // über die Lebensdauer (vorne breiter), Turbulenz (Noise) lässt die Flammen züngeln, die Farbe wird zum Ende dunkler
    private static ParticleSystem Stream(string name, Transform parent, Material material, Color colorA, Color colorB,
        float speed, float angle, float radius, Vector2 size, float growth, float rate, float turbulence)
    {
        ParticleSystem system = Particles(name, parent, material, colorA, colorB, lifetime: 1f, speed: speed, size: size, rate: rate, radius: radius, growth: growth);
        system.transform.localRotation = Quaternion.identity;
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = speed;
        main.maxParticles = 1500;
        main.playOnAwake = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = radius;
        // Klein aus dem Maul, nach vorn immer größer
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1f + growth)));
        var colors = new Gradient();
        colors.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.55f), 0.7f), new GradientColorKey(new Color(0.6f, 0.35f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.9f, 0.85f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.color = colors;
        if (turbulence > 0f)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = turbulence;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 1.5f;
        }
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    // Partikelsystem, das nur auf Emit reagiert (Einschlag, Walze): Halbkugel nach oben, Welt-Simulation
    private static ParticleSystem Burst(string name, Transform parent, Material material, Color colorA, Color colorB,
        float lifetime, float speed, Vector2 size, int count, float growth, float gravity)
    {
        ParticleSystem system = Particles(name, parent, material, colorA, colorB, lifetime, speed, size, rate: 0f, radius: 3f, growth: growth);
        ParticleSystem.MainModule main = system.main;
        main.maxParticles = count;
        main.gravityModifier = gravity;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 3f;
        return Stopped(system);
    }

    private static CameraZone breathCameraZone;
    private static HidingVendor breathVendor;

    // Flammen-Streifen entlang der Bahn von u0 bis u1 (gerades Stück), 7 m breit
    private static ParticleSystem LaneFire(Transform parent, string name, float u0, float u1, Material material)
    {
        Vector3 a = LevelBuildKit.Lane.PointAt(u0), b = LevelBuildKit.Lane.PointAt(u1);
        ParticleSystem fire = GroundFire(parent, name, (a + b) / 2f + Vector3.up * CityLayout.ParkPathTop, new Vector3(Vector3.Distance(a, b), 7f), material, 9f * Vector3.Distance(a, b));
        // Das Rechteck liegt in Welt-X/Z: um die Hochachse in Richtung der Bahn drehen
        fire.transform.rotation = Quaternion.FromToRotation(Vector3.right, (b - a).normalized) * fire.transform.rotation;
        return fire;
    }

    // Umgekippter Eiswagen: derselbe Wagen wie in Level 3 (IceCreamTruckKit), auf die Seite gekippt: Dach mit der großen
    // Waffel zur Kamera, Unterboden mit Rädern zur Echse, die Verkaufsseite liegt unten. Daneben verschüttetes Eis, dahinter
    // kauert der Verkäufer (HidingVendor). Collider auf Layer Ground (man kann draufspringen). Die Stadt-Deko an der Stelle
    // (Laternen, Bäume) blendet ein Ausschnitt aus, der den Boden stehen lässt
    private static HidingVendor IceCreamTruck(float u)
    {
        Quaternion frame = LevelBuildKit.Frame(u);
        var truck = new GameObject("Ice Cream Truck") { layer = 3 };
        truck.transform.SetParent(gameplayParent, false);
        // Wurzel in der Mitte des liegenden Wagens: lokal X entlang der Bahn, Y hoch (liegend die Wagenbreite), Z zur Echse
        truck.transform.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(u) + Vector3.up * (CityLayout.ParkLawnTop + TruckHeight / 2f)
            + frame * Vector3.forward * TruckBackOffset, frame);
        Transform t = truck.transform;
        // Aufrecht: Y = hoch, Z = Rückseite. Um -90° um X gekippt wird aus Y (Dach) -Z (zur Kamera), aus Z (Rückseite) Y (oben)
        Transform model = IceCreamTruckKit.BuildModel(t, roofCone: false);
        model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        model.localPosition = new Vector3(0f, 0f, TruckDepth / 2f);
        PrimitiveKit.AddBoxCollider(truck, Vector3.zero, new Vector3(TruckLength, TruckHeight, TruckDepth));
        Vector3 c = truck.transform.position;
        LevelBuildKit.Cutout(GameObject.Find("City Link").transform, "Ice Cream Truck", c.x - 6.5f, c.x + 6.5f, c.z - 6.5f, c.z + 6.5f, keepGround: true);
        Material pink = IceCreamTruckKit.Pink, mint = IceCreamTruckKit.Mint;
        BrokenCone(t, pink, mint);

        // Verschüttetes Eis vor dem Wagen (flache Kugeln)
        var random = new System.Random(42);
        for (int i = 0; i < 6; i++)
        {
            Vector3 spot = new Vector3(((float)random.NextDouble() - 0.5f) * TruckLength, -TruckHeight / 2f + 0.05f, -TruckDepth / 2f - 0.4f - (float)random.NextDouble() * 0.6f);
            PrimitiveKit.Part("Spilled Scoop", PrimitiveType.Sphere, t, spot, new Vector3(0.45f, 0.15f, 0.45f), i % 2 == 0 ? pink : mint);
        }

        // Verkäufer (derselbe wie in Level 3) an dem Ende, zu dem der Spieler hinläuft (-u), im Spalt zwischen Bahn und Wagen,
        // schaut zur Kamera
        CrowdFigure figure = IceCreamTruckKit.Vendor(t, new Vector3(-TruckLength / 2f + 0.9f, -TruckHeight / 2f, -TruckDepth / 2f - 0.55f), 0f);
        var figureData = new SerializedObject(figure);
        figureData.FindProperty("gesture").floatValue = 0f;
        figureData.ApplyModifiedPropertiesWithoutUndo();
        Transform rightArm = (Transform)figureData.FindProperty("rightArm").objectReferenceValue;
        Transform leftArm = (Transform)figureData.FindProperty("leftArm").objectReferenceValue;
        // Sehr groß über der Mitte des Wagens (vor dem weißen Wagen ging die weiße Blase unter), HidingVendor lässt sie
        // aufploppen und pulsieren
        GameObject bubble = ParkSetup.SpeechBubble(figure.transform, new Vector3(TruckLength / 2f - 0.9f, 5.2f, 0f), "COME HERE!");
        bubble.transform.localScale = Vector3.one * 4.8f;
        var vendor = figure.gameObject.AddComponent<HidingVendor>();
        var vendorData = new SerializedObject(vendor);
        vendorData.FindProperty("figure").objectReferenceValue = figure;
        vendorData.FindProperty("rightArm").objectReferenceValue = rightArm;
        vendorData.FindProperty("leftArm").objectReferenceValue = leftArm;
        vendorData.FindProperty("bubble").objectReferenceValue = bubble;
        vendorData.ApplyModifiedPropertiesWithoutUndo();
        return vendor;
    }

    // Die große Dach-Waffel ist beim Umkippen abgebrochen (2026-10-29: am liegenden Wagen ragte sie 0.9 m über die Bahn,
    // der Spieler lief hindurch). Sie liegt am +u-Ende (dort kommt der Spieler an) im Gras, so weit hinter der Bahn wie die
    // Wagenmitte, die rosa Kugel klebt noch zerdrückt in der Öffnung, die Minzkugel ist weggerollt, dazwischen eine Pfütze.
    // Nur Optik, keine Collider. Koordinaten im Wagen-Rahmen (lokal X entlang der Bahn, Y hoch, -Z zur Bahn)
    private static void BrokenCone(Transform truck, Material pink, Material mint)
    {
        float ground = -TruckHeight / 2f;
        Transform cone = PrimitiveKit.Pivot("Broken Roof Cone", truck, new Vector3(TruckLength / 2f + 1.4f, ground, 0.2f));
        cone.localRotation = Quaternion.Euler(0f, -25f, 0f);
        // Waffel liegt (Achse entlang X, Öffnung nach +X), ein Stück eingesunken
        Renderer waffle = PrimitiveKit.Part("Waffle", PrimitiveType.Cylinder, cone, new Vector3(0f, 0.27f, 0f), new Vector3(0.6f, 0.5f, 0.6f), IceCreamTruckKit.Waffle);
        waffle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        PrimitiveKit.Part("Scoop Pink", PrimitiveType.Sphere, cone, new Vector3(0.75f, 0.33f, 0f), new Vector3(0.75f, 0.62f, 0.85f), pink);
        PrimitiveKit.Part("Puddle Pink", PrimitiveType.Sphere, cone, new Vector3(1.3f, 0.02f, 0.1f), new Vector3(1.5f, 0.05f, 1.1f), pink);
        PrimitiveKit.Part("Scoop Mint", PrimitiveType.Sphere, cone, new Vector3(2.1f, 0.25f, 0.6f), new Vector3(0.7f, 0.5f, 0.7f), mint);
        PrimitiveKit.Part("Puddle Mint", PrimitiveType.Sphere, cone, new Vector3(1.75f, 0.015f, 0.4f), new Vector3(0.9f, 0.04f, 0.7f), mint);
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

    // ---------- Feuerwehr (unterer Mozzarella Blvd) ----------
    //   Brennende Stellen auf dem Gehweg (7 m, zu lang zum Drüberspringen), je ein Feuerwehrmann löscht sie im Takt (FirePatch):
    //   erst eine allein (Takt lernen), dann ein Schuttberg (BuildRubble), von dem zwei Stahlträger weiterführen, dazwischen
    //   eine hohe Feuersäule aus einer geborstenen Gasleitung (auf dem Träger warten, bis sie gelöscht ist), dann ein breites
    //   Dauerfeuer, über das der Korb einer Drehleiter fährt. Die Fahrzeuge stehen auf der Fahrbahn vor der Bahn, darum steht
    //   die Kamera hier höher und weiter weg (sie stünde sonst neben bzw. in den Fahrzeugen).
    //   2026-10-29 nach dem Playtest: vorher drei Stellen direkt hintereinander, das war zu viel Warten und zu flach. Danach
    //   die zweite Stelle als Säule in die Trägerlücke (vorher lag sie unter den Trägern, wer oben blieb, musste nicht warten)
    //   und Chili 1 vom Korb an den Founder's Circle (vom fahrenden Korb hochzuspringen fühlte sich falsch an)
    // Prefab, Mitte der Stelle (z), Takt-Versatz (s)
    private static readonly (string prefab, float z, float offset)[] FirePatches = { ("Fire Patch", 22f, 0f), ("Fire Column", 50f, 1.5f) };
    private const float FireBedZ = 72f;
    private const float BasketFromZ = 64f, BasketToZ = 80f;
    private const float BasketY = 1f;     // Oberkante 1.5, ein Meter über dem Gehweg
    // Die Fahrzeuge stehen in den Lücken neben den Stellen, nicht davor (sonst verdecken sie die Feuerwehrleute): Löschfahrzeuge
    // vor Stelle 1 und vor dem Schuttberg, die Drehleiter hinter dem breiten Feuer (die Leiter reicht schräg zurück zum Korb)
    private static readonly float[] FireTruckZ = { 12f, 31f };
    // Zu welchem Löschfahrzeug der Schlauch jeder Stelle läuft
    private static readonly int[] HoseTruck = { 0, 1 };
    private const float LadderTruckZ = 83f;   // Drehkranz 3 m weiter südlich, gleich hinter dem Ende des Feuers
    private const float FireZoneFromZ = 14f, FireZoneToZ = 90f;
    private static readonly Vector3 FireCameraOffset = new Vector3(0f, 8f, -12f);
    private const float FireCameraLookHeight = 3f;

    private static void BuildFirefighters()
    {
        Transform section = new GameObject("Firefighters").transform;
        section.SetParent(gameplayParent, false);
        string folder = FireSetup.PrefabFolder;
        foreach (float z in FireTruckZ)
        {
            LevelBuildKit.PlaceAsset($"{folder}/Fire Truck.prefab", LowerMozzarellaU(z), 0, section);
        }
        Material hose = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Fire Hose.mat");
        for (int i = 0; i < FirePatches.Length; i++)
        {
            var patch = FirePatches[i];
            float u = LowerMozzarellaU(patch.z);
            GameObject fire = LevelBuildKit.PlaceAsset($"{folder}/{patch.prefab}.prefab", u, 0, section);
            var data = new SerializedObject(fire.GetComponent<FirePatch>());
            data.FindProperty("startOffset").floatValue = patch.offset;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Dort, wo er hinspritzt, brennt das Haus
            Vector3 house = fire.transform.TransformPoint(new Vector3(1.5f, 5f, 2.7f));
            HouseFire(section, house);

            // Schlauch: von seinen Füßen auf die Fahrbahn zwischen Bordstein und Fahrzeug, dort entlang bis zum Fahrzeug, dann
            // zur Pumpe an dessen Seite (zur Bahn hin)
            float truckZ = FireTruckZ[HoseTruck[i]];
            float toTruck = Mathf.Sign(truckZ - patch.z);
            float endZ = truckZ - toTruck * (FireSetup.TruckLength / 2f - 1f);
            float runLocalZ = FireSetup.FirefighterZ + 0.5f;
            float pumpLocalZ = FireSetup.TruckZ + FireSetup.TruckWidth / 2f;
            Vector3 feet = HosePoint(patch.z, FireSetup.FirefighterZ - 0.2f);
            Vector3 bend = HosePoint(patch.z + toTruck * 0.8f, runLocalZ);
            Vector3 run = HosePoint(endZ, runLocalZ);
            Vector3 pump = HosePoint(endZ + toTruck * 0.6f, pumpLocalZ);
            HoseSegment(section, feet, bend, hose);
            HoseSegment(section, bend, run, hose);
            HoseSegment(section, run, pump, hose);
        }

        LevelBuildKit.PlaceAsset($"{folder}/Fire Bed.prefab", LowerMozzarellaU(FireBedZ), 0, section);
        HouseFire(section, LevelBuildKit.Lane.PointAt(LowerMozzarellaU(FireBedZ - 3f)) + LevelBuildKit.Frame(LowerMozzarellaU(FireBedZ)) * new Vector3(0f, 4f, 2.7f));
        HouseFire(section, LevelBuildKit.Lane.PointAt(LowerMozzarellaU(FireBedZ + 4f)) + LevelBuildKit.Frame(LowerMozzarellaU(FireBedZ)) * new Vector3(0f, 7f, 2.7f));

        float fromU = LowerMozzarellaU(BasketFromZ);
        GameObject basket = LevelBuildKit.PlaceAsset($"{folder}/Rescue Basket.prefab", fromU, BasketY, section);
        basket.transform.Find("moving platform start").position = LevelBuildKit.Lane.PointAt(fromU) + Vector3.up * BasketY;
        basket.transform.Find("moving platform end").position = LevelBuildKit.Lane.PointAt(LowerMozzarellaU(BasketToZ)) + Vector3.up * BasketY;
        GameObject ladderTruck = LevelBuildKit.PlaceAsset($"{folder}/Ladder Truck.prefab", LowerMozzarellaU(LadderTruckZ), 0, section);
        var boom = new SerializedObject(ladderTruck.GetComponent<LadderBoom>());
        boom.FindProperty("basket").objectReferenceValue = basket.transform;
        boom.FindProperty("basketMount").vector3Value = FireSetup.BasketMount;
        boom.ApplyModifiedPropertiesWithoutUndo();

        var zone = new SerializedObject(new GameObject("Fire Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        // u läuft hier nach Süden: das nördliche Ende ist das kleinere u
        zone.FindProperty("fromU").floatValue = LowerMozzarellaU(FireZoneToZ);
        zone.FindProperty("toU").floatValue = LowerMozzarellaU(FireZoneFromZ);
        zone.FindProperty("blend").floatValue = 8f;
        zone.FindProperty("offset").vector3Value = FireCameraOffset;
        zone.FindProperty("lookHeight").floatValue = FireCameraLookHeight;
        zone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)zone.targetObject).transform.SetParent(section, false);
    }

    // Punkt auf der Fahrbahn bei z (Mozzarella Blvd), lokal z im Lane-Rahmen (negativ = zur Kamera)
    private static Vector3 HosePoint(float z, float localZ)
    {
        float u = LowerMozzarellaU(z);
        return LevelBuildKit.Lane.PointAt(u) + LevelBuildKit.Frame(u) * new Vector3(0f, CityLayout.RoadTop + 0.06f, localZ);
    }

    private static void HoseSegment(Transform parent, Vector3 a, Vector3 b, Material material)
    {
        Renderer piece = PrimitiveKit.Box("Hose", parent, Vector3.zero, new Vector3(0.12f, 0.12f, Vector3.Distance(a, b) + 0.12f), material);
        piece.transform.SetPositionAndRotation((a + b) / 2f, Quaternion.LookRotation(b - a, Vector3.up));
    }

    // Flammen aus einem Fenster an der Fassade, darüber Rauch (Kulisse)
    private static void HouseFire(Transform parent, Vector3 at)
    {
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Material additive = FireAdditiveMaterial(particles);
        Transform spot = PrimitiveKit.Pivot("House Fire", parent, Vector3.zero);
        spot.position = at;
        Particles("Flames", spot, additive, new Color(1f, 0.42f, 0.08f, 0.9f), new Color(1f, 0.75f, 0.2f, 0.9f),
            lifetime: 1f, speed: 2.5f, size: new Vector2(1f, 2f), rate: 30f, radius: 1f, growth: -0.4f);
        ParticleSystem smoke = Particles("Smoke", spot, particles, new Color(0.16f, 0.15f, 0.15f, 0.7f), new Color(0.3f, 0.28f, 0.27f, 0.5f),
            lifetime: 7f, speed: 3f, size: new Vector2(2.5f, 5f), rate: 4f, radius: 1f, growth: 2f);
        smoke.transform.localPosition = Vector3.up * 1.5f;
    }

    // ---------- Schutt zum Klettern ----------
    //   Margherita Avenue: kleiner Aufwärm-Hügel gleich hinter der Pizzeria (zwei Stufen hoch, Platte runter)
    //   Unterer Mozzarella Blvd: Schuttberg bis 5 m zwischen den beiden Feuerwehr-Stellen. Vom Gipfel über zwei Stahlträger,
    //     in der Lücke dazwischen die Feuersäule (BuildFirefighters, z 50): warten, bis gelöscht ist. Wer herunterfällt, steht
    //     unten vor derselben Säule. Landehaufen dahinter
    //   Rasen-Stück im Park: umgestürzter Baum (auf den Stamm, über die brennende Krone), zwei Hochhaus-Brocken, ein brennender Krater
    //     (hineinfallen = Respawn), Platte hinunter. Danach landet der Roboter
    //   Four Cheese Avenue: das Hochhaus-Stück, das vor dem Spieler umkippt, schräg hinauf bis 6.2 m, Stufen hinunter auf die
    //     Oregano Street (Finale), dabei fallen Brocken aus dem Kampf. Danach Balkone, ein Stahlträger, zwei Bruchplatten über
    //     einem brennenden Krater, Schuttberg und Platte hinunter
    // Fällt man irgendwo daneben, landet man auf dem Gehweg (kostet nur Zeit); tödlich sind nur Feuer und Krater
    private static void BuildRubble()
    {
        Transform rubble = new GameObject("Rubble Course").transform;
        rubble.SetParent(gameplayParent, false);
        const float walk = CityLayout.Surface;
        const float park = CityLayout.ParkPathTop;
        float M(float z) => LowerMozzarellaU(z);
        float L(float z) => LegU(z);
        float F(float x) => FourCheeseU(x);
        int seed = 500;

        // Margherita (u = x)
        RubbleKit.Step(rubble, -13f, -15f, walk, 1.7f, seed++);
        RubbleKit.Step(rubble, -15f, -18f, walk, 2.9f, seed++);
        RubbleKit.Slab(rubble, -18f, 2.9f, -23f, walk, seed++);

        // Unterer Mozzarella Blvd: hinauf
        RubbleKit.Step(rubble, M(29f), M(31f), walk, 1.9f, seed++);
        RubbleKit.Step(rubble, M(31f), M(33.5f), walk, 3.2f, seed++);
        RubbleKit.Slab(rubble, M(33.5f), 3.2f, M(38f), 5f, seed++);
        RubbleKit.Step(rubble, M(38f), M(43f), walk, 5f, seed++);
        // ... dann die Träger, die Lücke (2.5 m) liegt mittig über der Feuersäule bei z 50
        RubbleKit.Beam(rubble, M(45.5f), M(48.75f), 5f, M(47f), walk, seed++);
        RubbleKit.Beam(rubble, M(51.25f), M(54.5f), 5.2f, M(53f), walk, seed++);
        RubbleKit.Step(rubble, M(57f), M(60f), walk, 2f, seed++);

        // Rasen-Stück im Park
        BurningTree(rubble, L(162f), L(169.5f), 2.5f, park);
        RubbleKit.Step(rubble, L(172f), L(177f), park, 2.2f, seed++, towerChunk: true);
        RubbleKit.Step(rubble, L(179f), L(184f), park, 3.8f, seed++, towerChunk: true);
        RubbleKit.Crater(rubble, L(184f), L(189f), park, seed++);
        RubbleKit.Step(rubble, L(189f), L(193f), park, 3.2f, seed++, towerChunk: true);
        RubbleKit.Slab(rubble, L(193f), 3.2f, L(199f), park, seed++);

        // Four Cheese Avenue: Hochhaus-Stück vor der Oregano Street. Es steht noch, bis der Spieler auf dem Rasen-Stück
        // ToppleTriggerZ erreicht, dann kippt es von ihm weg auf den Schuttberg (TopplingRuin) und ist die Rampe.
        // Das untere Ende beginnt bündig mit dem Gehweg: Schon eine 30 cm hohe Kante hält den Spieler auf (er steigt nicht
        // von selbst über Stufen, nur Schrägen hinauf)
        Transform pivot = RubbleKit.ToppledTower(rubble, F(-84f), walk, F(-99f), 6.2f, seed++, out float standingTilt, out Collider towerSolid);
        var ruin = new SerializedObject(pivot.gameObject.AddComponent<TopplingRuin>());
        ruin.FindProperty("triggerU").floatValue = L(ToppleTriggerZ);
        ruin.FindProperty("pivot").objectReferenceValue = pivot;
        ruin.FindProperty("standingTilt").floatValue = standingTilt;
        ruin.FindProperty("creakTime").floatValue = ToppleCreakTime;
        ruin.FindProperty("toppleTime").floatValue = ToppleFallTime;
        PrimitiveKit.SetReferences(ruin, "solids", towerSolid);
        // Staub nicht am Drehpunkt aufhängen, er drehte sich sonst mit
        ParticleSystem baseDust = RubbleKit.Dust(rubble, "Base Dust", Vector3.zero, 5f);
        baseDust.transform.position = pivot.position + pivot.rotation * new Vector3(-2f, 0.3f, 2f);
        ruin.FindProperty("baseDust").objectReferenceValue = baseDust;
        ruin.FindProperty("impactDust").objectReferenceValue = RubbleKit.Dust(rubble, "Topple Dust", Vector3.zero, 12f);
        ruin.ApplyModifiedPropertiesWithoutUndo();
        var impactAt = (ParticleSystem)ruin.FindProperty("impactDust").objectReferenceValue;
        impactAt.transform.position = LevelBuildKit.Lane.PointAt(F(-96f)) + Vector3.up * 3f;
        // Damit man den Sturz sieht: Auf dem Rasen-Stück stünde die Kamera westlich und schaute nach Osten, der Turm stünde
        // hinter ihr. Am Ende des Rasen-Stücks rückt sie hinter den Spieler und schaut wie er nach Norden (der Turm vorne
        // links); auf der Four Cheese Avenue schaut sie ohnehin nach Norden, so entfällt auch der 90°-Schwenk in der Kurve
        var corner = new SerializedObject(new GameObject("Corner Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        corner.FindProperty("fromU").floatValue = pieceStartU[4];
        corner.FindProperty("toU").floatValue = L(CornerCameraFromZ);
        corner.FindProperty("blend").floatValue = 5f;
        corner.FindProperty("offset").vector3Value = CornerCameraOffset;
        corner.FindProperty("lookHeight").floatValue = CornerCameraLookHeight;
        corner.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)corner.targetObject).transform.SetParent(rubble, false);
        RubbleKit.Step(rubble, F(-100f), F(-102.5f), walk, 4.3f, seed++);
        RubbleKit.Step(rubble, F(-102.5f), F(-105f), walk, 2.6f, seed++);
        // Brocken aus dem Kampf: einer landet auf der oberen Stufe, einer auf der Kreuzung der Oregano Street (vor dem Finale)
        foreach (var (spotX, ground, triggerX) in DebrisDrops)
        {
            RubbleKit.FallingChunk(rubble, F(spotX), ground, F(triggerX), seed++);
        }
        // ... hinter der Oregano Street: Balkone der stehen gebliebenen Häuser (Oberkante = Wurzel + 0.5), Träger, Schuttberg
        LevelBuildKit.Place("Balcony", Mathf.Round(F(-121f)), 2, rubble);
        LevelBuildKit.Place("Balcony", Mathf.Round(F(-125.5f)), 4, rubble);
        RubbleKit.Beam(rubble, F(-130f), F(-135f), 5f, F(-132f), walk, seed++);
        // ... über den brennenden Krater auf zwei Bruchplatten (halten nach dem Landen nur kurz, wer zögert, fällt ins Feuer).
        // 4 m lang, 1.5 m Lücken: Ein voller Sprung von einer Kante landet auf der nächsten Platte (im Test flog er mit 3-m-
        // Platten und 1-m-Lücken über die zweite hinweg in den Krater)
        RubbleKit.Crater(rubble, F(-135.5f), F(-147f), walk, seed++);
        RubbleKit.CrumblingSlab(rubble, F(-136.5f), F(-140.5f), 4.6f, seed++);
        RubbleKit.CrumblingSlab(rubble, F(-142f), F(-146f), 4.2f, seed++);
        RubbleKit.Step(rubble, F(-147.5f), F(-151f), walk, 3.5f, seed++);
        RubbleKit.Slab(rubble, F(-151f), 3.5f, F(-157f), walk, seed++);
        // Kamera dort weiter weg und tiefer blickend: Sonst steigt sie mit dem Spieler auf die Platten (4–5 m) und das Feuer im
        // Krater darunter liegt unter dem Bildrand
        var crater = new SerializedObject(new GameObject("Crater Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        crater.FindProperty("fromU").floatValue = F(-152f);
        crater.FindProperty("toU").floatValue = F(-128f);
        crater.FindProperty("blend").floatValue = 6f;
        crater.FindProperty("offset").vector3Value = new Vector3(0f, 6.5f, -11f);
        crater.FindProperty("lookHeight").floatValue = 1.2f;
        crater.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)crater.targetObject).transform.SetParent(rubble, false);
    }

    // Lebendiger Schutt auf der Four Cheese Avenue (2026-10-29, nach dem Wunsch, dass Schutt "live" passiert):
    //   Hochhaus-Stück kippt um, sobald der Spieler auf dem Rasen-Stück hier ist (nach der Landung des Roboters bei z 205).
    //   Zügig (1.5 s): Er liegt, bevor der Spieler (1.8 s später) an seinem Fuß ist. Normalerweise schaut die Kamera auf dem
    //   Rasen-Stück nach Osten, der Turm stünde hinter ihr, darum die Kurven-Kamera (Corner Camera Zone, BuildRubble)
    private const float ToppleTriggerZ = 211f;
    private const float ToppleCreakTime = 0.35f, ToppleFallTime = 1.15f;
    //   Kamera dabei: ab hier auf dem Rasen-Stück direkt hinter dem Spieler (lokal +X = Süden), Blick nach Norden, etwas
    //   höher, damit der ganze Turm ins Bild kommt (getestet: er fällt links ins Bild)
    private const float CornerCameraFromZ = 207f;
    private static readonly Vector3 CornerCameraOffset = new Vector3(8f, 6f, -0.5f);
    private const float CornerCameraLookHeight = 4f;
    //   Brocken aus dem Kampf: Landestelle x, Boden dort, ausgelöst sobald der Spieler an x vorbei ist. Auf der oberen Stufe
    //   (Oberkante 4.3) und auf der Oregano-Kreuzung (ab x -110 läuft das Finale). Vom Auslöser bis zum Aufprall vergehen
    //   1.9 s (1 s Warnung + 0.9 s Fall), so weit (~13 m bei 7 m/s) liegt der Auslöser vor der Stelle: Wer ohne zu schauen
    //   durchläuft, kommt genau beim Aufprall an; wer die Warnung sieht, bleibt davor stehen (2026-10-29, vorher 1.3 s und
    //   6–8 m ohne Warnung)
    private static readonly (float spotX, float ground, float triggerX)[] DebrisDrops =
    {
        (-101.25f, 4.3f, -88f),
        (-107.5f, CityLayout.Surface, -94.5f),
    };

    // Umgestürzter Baum entlang der Bahn: Wurzelteller am Ende, wo der Spieler ankommt (uRoot), der Stamm ist begehbar (nicht
    // heiß), nur die Krone am anderen Ende (uCrown, crownLength lang) brennt (Berührung = Respawn). Auf den Stamm springen,
    // drauf entlang laufen, über die Krone springen.
    // 2026-10-29: Vorher brannte der ganze Stamm (4.5 m, 1.2 m hoch), das war nicht zu schaffen (über 1.2 m bleibt man im
    // Sprung nur etwa 4 m weit, die Kapsel bräuchte 5.5)
    // Gefahr 1.2 m über dem Boden = 0.3 über dem Stamm: Vom Stamm aus ist man sofort darüber
    private const float TrunkTop = 0.9f, CrownHazard = 1.2f, CrownFireInset = 0.7f;

    private static void BurningTree(Transform parent, float uRoot, float uCrown, float crownLength, float ground)
    {
        Material bark = PrimitiveKit.Material("Assets/Materials/City/Charred Bark.mat", PrimitiveKit.LitShader, new Color(0.16f, 0.11f, 0.08f), 0.05f);
        Material soil = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        float toCrown = Mathf.Sign(uCrown - uRoot);
        float trunkEnd = uCrown - toCrown * crownLength;
        float u = (uRoot + trunkEnd) / 2f, length = Mathf.Abs(trunkEnd - uRoot);
        Transform tree = PrimitiveKit.Pivot("Burning Tree", parent, Vector3.zero);
        tree.SetPositionAndRotation(LevelBuildKit.Lane.PointAt(u) + Vector3.up * ground, LevelBuildKit.Frame(u));
        // Stamm: begehbarer Quader, Optik ein liegender Zylinder (bis in die Krone hinein)
        var walk = new GameObject("Trunk Walk") { layer = 3 };
        walk.transform.SetParent(tree, false);
        PrimitiveKit.AddBoxCollider(walk, new Vector3(0f, TrunkTop / 2f, 0f), new Vector3(length, TrunkTop, 1.2f));
        float visualLength = length + crownLength * 0.6f;
        Renderer trunk = PrimitiveKit.Part("Trunk", PrimitiveType.Cylinder, tree, new Vector3(toCrown * crownLength * 0.3f, TrunkTop / 2f, 0.05f),
            new Vector3(TrunkTop, visualLength / 2f, TrunkTop), bark);
        trunk.transform.localRotation = Quaternion.Euler(0f, 3f, 90f);
        // Wurzelteller: aufgerichtete Scheibe mit Erde, ein paar Wurzeln. Nach hinten versetzt, damit der Spieler, der hier
        // ankommt und aufspringt, nicht hindurchläuft
        float rootX = -toCrown * (length / 2f + 0.15f);
        Renderer plate = PrimitiveKit.Part("Root Plate", PrimitiveType.Cylinder, tree, new Vector3(rootX, 0.85f, 1.1f), new Vector3(1.7f, 0.2f, 1.7f), soil);
        plate.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var random = new System.Random(164);
        for (int i = 0; i < 5; i++)
        {
            Renderer root = PrimitiveKit.Part("Root", PrimitiveType.Cylinder, tree, new Vector3(rootX - toCrown * 0.3f, 0.3f + (float)random.NextDouble() * 1.1f, 0.6f + (float)random.NextDouble() * 1.2f),
                new Vector3(0.12f, 0.5f, 0.12f), bark);
            root.transform.localRotation = Quaternion.Euler(random.Next(-60, 60), 0f, 90f + random.Next(-40, 40));
        }
        // Krone: Aststummel, die brennen
        float crownX = toCrown * (length / 2f + crownLength / 2f);
        foreach (var (x, angle) in new[] { (-0.7f, 40f), (0f, -35f), (0.7f, 55f) })
        {
            Renderer branch = PrimitiveKit.Part("Branch", PrimitiveType.Cylinder, tree, new Vector3(crownX + x, 0.9f, 0.4f), new Vector3(0.22f, 0.7f, 0.22f), bark);
            branch.transform.localRotation = Quaternion.Euler(angle, 0f, angle * 0.5f);
        }
        // Das Feuer beginnt erst CrownFireInset in der Krone: Wer an der Stammkante abspringt, ragt mit seiner Kapsel (Radius
        // 0.5) sonst schon in den Trigger, solange er noch steigt (im Test genau so verbrannt)
        GameObject fire = FireSetup.BurningGround("Crown Fire", crownLength - CrownFireInset, CrownHazard);
        fire.transform.SetParent(tree, false);
        fire.transform.localPosition = new Vector3(crownX + toCrown * CrownFireInset / 2f, -0.5f, 0f);
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

    private static Renderer Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
    {
        return PrimitiveKit.Part(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), material);
    }
}
