using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Baut Level 2 (Leaning Tower of Pizza) komplett neu auf. Läuft in der gemeinsamen Stadt: Boden und Häuser kommen aus
// der Stadt-Szene, das Level bringt Laufbahn, Baugruben (mit CityCutouts), Überwege mit Autos, Spiel-Elemente, Möwen und Deko.
// Menü: Tools > Jump and Run > City > Rebuild Level 2 (Leaning Tower). Erneut ausführen überschreibt Handänderungen.
//
// Route (der Spieler läuft nach LINKS, also Richtung -u): aus der Pizzeria nach Westen die Margherita Avenue entlang,
// über den Mozzarella Blvd, dann die Strandpromenade (die Stadt hat dort statt Fahrbahn Sand und Meer, Kamera über dem
// Wasser), Gelateria-Checkpoint nach dem dritten Chili, rechts in die Basil Street, dann links über die Straße auf die Piazza Pisa
// zum Kunden am Turm.
// Die Bahn ist in +u-Richtung definiert (Häuser links davon): Sie beginnt auf der Piazza und endet hinter der Pizzeria.
// Auf der Margherita Avenue ist u = Welt-X (wie in Level 1).
//
// Ablauf in Laufrichtung (Welt-X auf der Margherita Avenue, Gehweg-Oberkante 0.5, Sprung ~3.2 m hoch / ~5.5 m weit):
//   -7.5        Start aus der Pizzeria
//   -27..-23    Baugrube 1
//   -46..-34    Überweg Mozzarella Blvd mit Autos
//   ab -46      Strandpromenade
//   -60..-56    Bachmündung 1, danach Hund zwischen Container und Bank, Balkon-Kletterei mit Chili
//  -110         Rettungsturm (Plattform 3 m hoch), wo früher die Oregano Street kreuzte
//  -126..-130   federnder Sonnenschirm → Balkon mit Chili (das dritte), Gelateria-Checkpoint (Tür bei -137.5)
//  -146..-142   Bachmündung 2, danach Hund zwischen Bank und Container bis zur Ecke
//   Ecke        rechts in die Basil Street (Ostseite, x -173), Bank, links über die Straße auf die Piazza
//   Piazza      Hund zwischen zwei Bänken, Kunde westlich des Turms (x ≈ -228)
// Möwen über dem Strand-Stück: zwei vor dem Checkpoint (u -138 bis -45), eine danach (u -170 bis -138).
public static class Level2Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level02.unity";
    private const float KeepWarmTime = 100f;

    // Bahn: Piazza (Start bei x -236, z 37, Richtung Osten) → Rechtskurve über die Basil Street → nach Süden → Linkskurve
    // an der Basil-Ecke → Margherita Avenue nach Osten bis hinter die Pizzeria
    private const float PiazzaStartX = -236f;
    private const float PiazzaZ = 37f;
    private const float PiazzaStraight = 51f;          // bis x -185
    private const float CrossRadius = 12f;             // über die Basil Street, Kamera schwenkt innen über die Straße
    private const float BasilStraight = 21f;           // z 25 → 4 auf der Ostseite der Basil Street (x -173)
    private const float CornerRadius = 4f;             // um die Basil-Ecke, Kamera schwenkt außen
    private const float MainStartX = -169f;
    private const float MainEndX = 16f;

    private static float CrossLength => Mathf.PI / 2f * CrossRadius;
    private static float CornerLength => Mathf.PI / 2f * CornerRadius;
    // So gewählt, dass auf der Margherita Avenue u = Welt-X gilt
    private static float StartU => MainStartX - (PiazzaStraight + CrossLength + BasilStraight + CornerLength);
    private static float PiazzaU(float x) => StartU + (x - PiazzaStartX);
    private static float BasilU(float z) => StartU + PiazzaStraight + CrossLength + (25f - z);

    // Kamera auf der Strandpromenade (CameraZone): weiter hinten und höher als normal (0, 5, -8)
    private static readonly Vector3 BeachCameraOffset = new Vector3(0f, 9f, -18f);
    private const float BeachCameraLookHeight = 1f;

    private static Transform groundParent, gameplayParent, propParent;

    [MenuItem("Tools/Jump and Run/City/Rebuild Level 2 (Leaning Tower)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Level 2 neu bauen",
                "Löscht alles in Level 2 außer Kamera, Player, GameManager, HUD, KillZone, Goal und StartPoint und baut es neu auf. Fortfahren?",
                "Neu bauen", "Abbrechen"))
        {
            return;
        }
        Build();
    }

    // Ohne Dialog, für den Aufruf über Skripte (MCP)
    public static void Build()
    {
        var scene = LevelBuildKit.OpenAndClear(ScenePath);
        LevelBuildKit.BuildLane(new Vector3(PiazzaStartX, 0f, PiazzaZ), 0f, StartU, new List<LanePath.Piece>
        {
            LevelBuildKit.Straight(PiazzaStraight),
            LevelBuildKit.Arc(CrossRadius, -90f),
            LevelBuildKit.Straight(BasilStraight),
            LevelBuildKit.Arc(CornerRadius, 90f),
            LevelBuildKit.Straight(MainEndX - MainStartX),
        });
        groundParent = LevelBuildKit.Group("Level Ground");
        gameplayParent = LevelBuildKit.Group("Level Gameplay");
        propParent = LevelBuildKit.Group("Level Props");

        Transform link = LevelBuildKit.CityLink("Pizzeria", "Gelato Shop");
        BuildGround(link);
        BuildGameplay();
        BuildProps();
        LevelBuildKit.SetupEssentials(PiazzaU(-228f), "7", KeepWarmTime, new Vector3(-110f, -3f, 10f), new Vector3(280f, 1f, 90f),
            "No. 7, Piazza Pisa", new Vector3(-30f, 90f, -110f));
        LevelBuildKit.Save(scene);
        Debug.Log("Level2Builder: Level 2 (Leaning Tower) gebaut.");
    }

    // ---------- Boden ----------

    private static void BuildGround(Transform link)
    {
        // Baugrube vor dem Strand: Lücke von/bis in Welt-X. Das Prefab ist 1 m länger und steckt unter den Gehwegkanten;
        // der Cutout schneidet Gehweg, Straße und alles darunter genau in der Lücke aus
        Pit(link, -26.75f, -23.25f);
        // Auf der Promenade: Bachmündungen statt Baugruben (gleiche Lücken)
        Creek(link, -59.75f, -56.25f);
        Creek(link, -145.75f, -142.25f);

        // Überweg mit Verkehr: Die Autos kommen den Mozzarella Blvd herunter, von hinter der Kreuzung mit der Crust Street.
        // Die Oregano Street endet an der Promenade, dort fahren keine Autos mehr
        Crossing(-40, 118f);

        // Über die Basil Street auf die Piazza: begehbare Platte auf Gehweghöhe über der Fahrbahn (dort fahren keine Autos)
        Material asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Asphalt.mat");
        Material marking = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road Marking.mat");
        Renderer table = PrimitiveKit.Box("Basil Crossing", groundParent, new Vector3(-180f, 0.41f, 31f), new Vector3(12f, 0.22f, 18f), asphalt);
        table.gameObject.layer = 3;
        table.gameObject.AddComponent<BoxCollider>();
        // Zebrastreifen: Die Balken laufen parallel zur Fahrbahn (Basil Street = Nord-Süd, also entlang Z)
        // und liegen nebeneinander quer über die Straße
        for (int i = 0; i < 5; i++)
        {
            PrimitiveKit.Box("Stripe " + (i + 1), table.transform.parent, new Vector3(-185f + i * 2.5f, 0.525f, 31f), new Vector3(1.1f, 0.005f, 16f), marking);
        }
    }

    // Baugrube und Bachmündung: gemeinsam mit Level 4 (dessen Anfang ist genau wie hier), siehe LevelBuildKit
    private static void Pit(Transform link, float fromX, float toX) => LevelBuildKit.RingPit(link, fromX, toX, groundParent);
    private static void Creek(Transform link, float fromX, float toX) => LevelBuildKit.BeachCreek(link, fromX, toX, groundParent);

    private static void Crossing(float u, float spawnZ)
    {
        GameObject crossing = LevelBuildKit.PlaceAsset("Assets/Prefabs/Avenue Crossing.prefab", u, 0, groundParent);
        foreach (TrafficLane trafficLane in crossing.GetComponentsInChildren<TrafficLane>(true))
        {
            var serialized = new SerializedObject(trafficLane);
            serialized.FindProperty("spawnZ").floatValue = spawnZ;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------- Spiel-Elemente ----------

    private static void BuildGameplay()
    {
        // Zwischen Mozzarella und Oregano: Hund zwischen Container und Bank (schaut dem Spieler entgegen, also +u),
        // dann zwei Balkone als Kletterpartie mit Chili oben
        LevelBuildKit.Place("Dumpster", -66, 0, gameplayParent);
        BeachDog(-76);
        LevelBuildKit.Place("Bench", -86, 0, gameplayParent);
        LevelBuildKit.Place("Balcony", -92, 3, gameplayParent);
        LevelBuildKit.Place("Balcony", -96, 6, gameplayParent);
        LevelBuildKit.HeatPickup(-96, 8, gameplayParent);

        // Wo früher die Oregano Street kreuzte: Rettungsturm (Plattform-Oberkante 3.5), ohne Chili (sonst zu viele im Level)
        LevelBuildKit.Place("Lifeguard Tower", -110, 3, gameplayParent);

        // Federnder Sonnenschirm (Oberkante 2.5) → Balkon (Oberkante 6.5) mit Chili
        LevelBuildKit.Place("Bouncy Umbrella", -126, 2, gameplayParent);
        LevelBuildKit.Place("Balcony", -130, 6, gameplayParent);
        LevelBuildKit.HeatPickup(-130, 8, gameplayParent);

        // Nach dem Gelateria-Checkpoint und Bachmündung 2: noch ein Hund zwischen Bank und Container bis zur Ecke
        LevelBuildKit.Place("Bench", -152, 0, gameplayParent);
        BeachDog(-158);
        LevelBuildKit.Place("Dumpster", -165, 0, gameplayParent);

        // Basil Street: eine Bank als kleines Hindernis
        LevelBuildKit.Place("Bench", Mathf.Round(BasilU(14f)), 0, gameplayParent);

        // Piazza: Hund zwischen zwei Bänken vor dem Turm
        LevelBuildKit.Place("Bench", Mathf.Round(PiazzaU(-195f)), 0, gameplayParent);
        LevelBuildKit.Dog(Mathf.Round(PiazzaU(-205f)), 1f, gameplayParent);
        LevelBuildKit.Place("Bench", Mathf.Round(PiazzaU(-215f)), 0, gameplayParent);

        // Östlich der Pizzeria geht es nicht weiter: Unfall mit unsichtbarer Wand (der Spieler käme von -u)
        LevelBuildKit.Place("Car Crash", 4, 0, gameplayParent);

        // Vor dem Turm: Kamera weiter weg (35 m), höher und mit Blick nach oben, damit der ganze Turm ins Bild passt.
        // Dort ist die Piazza zur Margherita Avenue hin offen, die Kamera steht also nicht in einem Haus.
        // Voll von x -190 (Piazza-Rand) bis hinter die zweite Bank (x -216), davor über die Basil Street und danach
        // zum Kunden hin (x -228) wieder weich auf die normale Kamera
        var zone = new SerializedObject(new GameObject("Tower Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        zone.FindProperty("fromU").floatValue = PiazzaU(-216f);
        zone.FindProperty("toU").floatValue = PiazzaU(-190f);
        zone.FindProperty("blend").floatValue = 8f;
        zone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)zone.targetObject).transform.SetParent(gameplayParent, false);

        // Strandpromenade: Kamera weiter weg und höher über dem Wasser, damit Sand und Brandung vor dem Spieler ins Bild passen.
        // Voll vom Ende des Überwegs (x -54) bis kurz vor die Basil-Ecke, dort zurück auf die normale Kamera
        var beachZone = new SerializedObject(new GameObject("Beach Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        beachZone.FindProperty("fromU").floatValue = -158f;
        beachZone.FindProperty("toU").floatValue = -54f;
        beachZone.FindProperty("blend").floatValue = 8f;
        beachZone.FindProperty("offset").vector3Value = BeachCameraOffset;
        beachZone.FindProperty("lookHeight").floatValue = BeachCameraLookHeight;
        beachZone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)beachZone.targetObject).transform.SetParent(gameplayParent, false);

        // Möwen über dem Strand-Stück, zeitversetzt: zwei bis zum Gelateria-Checkpoint, eine danach bis zur Ecke.
        // So greift nach einem Respawn an der Gelateria nur die dritte an
        Seagull(-100, 2f, -138f, -45f);
        Seagull(-120, 4.5f, -138f, -45f);
        Seagull(-155, 2f, -170f, -138f);
    }

    private static void BeachDog(float u) => LevelBuildKit.BeachDog(u, gameplayParent);

    private static void Seagull(float u, float firstDelay, float territoryFrom, float territoryTo) =>
        LevelBuildKit.Seagull(u, firstDelay, territoryFrom, territoryTo, gameplayParent);

    // ---------- Deko ----------

    private static void BuildProps()
    {
        // Nicht in Baugruben, nicht auf Überwegen, nicht vor Türen und nicht unter Balkonen/Markise
        var blocked = new List<(float from, float to)>
        {
            (-30f, -20f), (-48f, -32f), (-63f, -53f), (-114f, -106f), (-149f, -139f),
            (-11f, -4f), (-143f, -133f), (-100f, -88f), (-134f, -122f)
        };
        bool Free(float u) => blocked.TrueForAll(zone => u < zone.from || u > zone.to);

        for (int u = -14; u > -162; u -= 12)
        {
            if (Free(u)) LevelBuildKit.Place("Street Lamp", u, 0, propParent);
            if (Free(u - 6)) LevelBuildKit.Place("Tree", u - 6, 0, propParent);
        }
        foreach (int u in new[] { -52, -114, -161 })
        {
            if (Free(u)) LevelBuildKit.Place("Hydrant", u, 0, propParent);
        }
        foreach (int u in new[] { -17, -70, -137 })
        {
            if (Free(u)) LevelBuildKit.Place("Trash Can", u, 0, propParent);
        }
        (int u, int y)[] clouds = { (-20, 30), (-70, 26), (-120, 33), (-170, 28), (-220, 31) };
        foreach (var cloud in clouds)
        {
            LevelBuildKit.Place("Cloud", cloud.u, cloud.y, propParent);
        }
    }
}
