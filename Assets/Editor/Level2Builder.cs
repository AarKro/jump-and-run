using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Baut Level 2 (Leaning Tower of Pizza) komplett neu auf. Läuft in der gemeinsamen Stadt: Boden und Häuser kommen aus
// der Stadt-Szene, das Level bringt Laufbahn, Baugruben (mit CityCutouts), Überwege mit Autos, Spiel-Elemente, Möwen und Deko.
// Menü: Tools > Jump and Run > City > Rebuild Level 2 (Leaning Tower). Erneut ausführen überschreibt Handänderungen.
//
// Route (der Spieler läuft nach LINKS, also Richtung -u): aus der Pizzeria nach Westen die Margherita Avenue entlang,
// über den Mozzarella Blvd, dann die Strandpromenade (die Stadt hat dort statt Fahrbahn Sand und Meer, Kamera über dem
// Wasser), Gelateria-Checkpoint an der Ecke, rechts in die Basil Street, dann links über die Straße auf die Piazza Pisa
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
//  -110         Rettungsturm (Plattform 3 m hoch) mit Chili, wo früher die Oregano Street kreuzte
//  -126..-130   federnder Sonnenschirm → Balkon mit Chili, Bachmündung 2, Gelateria-Checkpoint (Tür bei -165.5)
//   Ecke        rechts in die Basil Street (Ostseite, x -173), Bank, links über die Straße auf die Piazza
//   Piazza      Hund zwischen zwei Bänken, Kunde westlich des Turms (x ≈ -228)
// Möwen kreisen über dem ganzen Strand-Stück (u -165 bis -45).
public static class Level2Builder
{
    private const string ScenePath = "Assets/Scenes/levels/level02.unity";
    private const float KeepWarmTime = 100f;

    // Bahn: Piazza (Start bei x -236, z 37, Richtung Osten) → Rechtskurve über die Basil Street → nach Süden → Linkskurve
    // an der Gelateria-Ecke → Margherita Avenue nach Osten bis hinter die Pizzeria
    private const float PiazzaStartX = -236f;
    private const float PiazzaZ = 37f;
    private const float PiazzaStraight = 51f;          // bis x -185
    private const float CrossRadius = 12f;             // über die Basil Street, Kamera schwenkt innen über die Straße
    private const float BasilStraight = 21f;           // z 25 → 4 auf der Ostseite der Basil Street (x -173)
    private const float CornerRadius = 4f;             // um die Gelateria-Ecke, Kamera schwenkt außen
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

    private static void Pit(Transform link, float fromX, float toX)
    {
        float center = (fromX + toX) / 2f;
        LevelBuildKit.Stretched("Construction Pit", center, 0, toX - fromX + 1f, groundParent);
        LevelBuildKit.Place("Barrier", Mathf.Round(toX + 1f), 0, groundParent);
        LevelBuildKit.Place("Barrier", Mathf.Round(fromX - 1f), 0, groundParent);
        // Nur bis zur Strandkante (z -12): Davor liegt in der Stadt der Strand, er deckt das vordere Ende der Grube ab
        LevelBuildKit.Cutout(link, $"Pit {center:F0}", fromX, toX, -12f, 3f);

        // Seitenwände: Ohne sie sähe man in der Grube die Schnittkanten der Stadt-Platten darunter (Wiese, Straßenbelag).
        // Unter dem Gehweg bis zu dessen Oberkante, unter der Straße nur bis zu deren Oberkante (sonst stünde eine Kante vor).
        // Die Wände stehen 0.2 m in der Lücke: Läge ihre Fläche genau auf der Schnittkante, flackerten beide (Z-Fighting)
        Material foundation = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Foundation.mat");
        foreach (float x in new[] { fromX + 0.1f, toX - 0.1f })
        {
            PrimitiveKit.Box("Pit Wall Walk", groundParent, new Vector3(x, (-4f + 0.49f) / 2f, 0.5f), new Vector3(0.2f, 4.49f, 5f), foundation);
            PrimitiveKit.Box("Pit Wall Road", groundParent, new Vector3(x, (-4f + 0.29f) / 2f, -7f), new Vector3(0.2f, 4.29f, 10f), foundation);
        }
    }

    // Bach, der unter der Häuserreihe hervorkommt und über den Strand ins Meer fließt. Die Lücke im Plattenweg ist so breit
    // wie eine Baugrube; wer hineinfällt, landet im Wasser (Respawn). Der Cutout nimmt Weg, Kante, Sand und alles darunter
    // (auch das Meer) heraus, das Wasser füllt das Loch wieder auf
    private static void Creek(Transform link, float fromX, float toX)
    {
        float center = (fromX + toX) / 2f;
        float width = toX - fromX;
        const float front = CityLayout.SandFrontZ, back = CityLayout.PromenadeBackZ;
        const float waterTop = 0.05f;
        LevelBuildKit.Cutout(link, $"Creek {center:F0}", fromX, toX, front, back);

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Sea.mat");
        Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Curb.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Interior Dark.mat");

        // Wasser knapp unter dem Sand: Zum Strand hin sieht es aus, als flösse der Bach einfach hinaus
        PrimitiveKit.Box("Creek Water", groundParent, new Vector3(center, (waterTop - 3f) / 2f, (front + back) / 2f), new Vector3(width, waterTop + 3f, back - front), water);
        // Ufermauern unter dem Plattenweg, 0.1 m in der Lücke (lägen sie genau auf der Schnittkante, flackerten beide)
        foreach (float x in new[] { fromX + 0.1f, toX - 0.1f })
        {
            PrimitiveKit.Box("Creek Wall", groundParent, new Vector3(x, 0f, (CityLayout.PromenadeFrontZ + back) / 2f), new Vector3(0.2f, 1f, back - CityLayout.PromenadeFrontZ), stone);
        }
        // Auslass unter der Häuserreihe: dunkle Öffnung mit Steinrahmen
        PrimitiveKit.Box("Culvert", groundParent, new Vector3(center, 0.27f, back + 0.02f), new Vector3(width - 0.8f, 0.44f, 0.04f), dark);
        PrimitiveKit.Box("Culvert Lintel", groundParent, new Vector3(center, 0.53f, back - 0.05f), new Vector3(width - 0.4f, 0.12f, 0.12f), stone);

        // Rinnsal den Abhang hinunter bis ins Meer, knapp über der Schräge
        CityLayout.BeachSlope(out Vector3 surfaceCenter, out Quaternion rotation, out float length, out float waterlineZ);
        float stripLength = length / 2f - waterlineZ + 0.3f;
        Vector3 local = new Vector3(0f, 0.02f, length / 2f - stripLength / 2f);
        surfaceCenter.x = center;
        Renderer stream = PrimitiveKit.Box("Creek Stream", groundParent, surfaceCenter + rotation * local, new Vector3(width - 0.8f, 0.02f, stripLength), water);
        stream.transform.rotation = rotation;

        // Ins Wasser fallen = Respawn: Auslöser knapp über der Wasseroberfläche, nur unter dem Plattenweg
        var trap = new GameObject("Creek Kill");
        trap.transform.SetParent(groundParent, false);
        trap.transform.position = new Vector3(center, waterTop - 0.5f, (CityLayout.PromenadeFrontZ + back) / 2f);
        PrimitiveKit.AddBoxCollider(trap, Vector3.zero, new Vector3(width, 1.1f, back - CityLayout.PromenadeFrontZ), isTrigger: true);
        trap.AddComponent<KillZone>();
    }

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
        GameObject dog = LevelBuildKit.Dog(-76, 1f, gameplayParent);
        // Draufgesprungen läuft er zum Meer: von der Promenade 0.3 m hinunter auf den Sand (statt 0.2 auf die Straße)
        var dogSettings = new SerializedObject(dog.GetComponent<Dog>());
        dogSettings.FindProperty("curbDrop").floatValue = 0.5f - CityLayout.SandTop;
        dogSettings.ApplyModifiedPropertiesWithoutUndo();
        LevelBuildKit.Place("Bench", -86, 0, gameplayParent);
        LevelBuildKit.Place("Balcony", -92, 3, gameplayParent);
        LevelBuildKit.Place("Balcony", -96, 6, gameplayParent);
        LevelBuildKit.HeatPickup(-96, 8, gameplayParent);

        // Wo früher die Oregano Street kreuzte: Rettungsturm (Plattform-Oberkante 3.5) mit Chili
        LevelBuildKit.Place("Lifeguard Tower", -110, 3, gameplayParent);
        LevelBuildKit.HeatPickup(-110, 5, gameplayParent);

        // Federnder Sonnenschirm (Oberkante 2.5) → Balkon (Oberkante 6.5) mit Chili
        LevelBuildKit.Place("Bouncy Umbrella", -126, 2, gameplayParent);
        LevelBuildKit.Place("Balcony", -130, 6, gameplayParent);
        LevelBuildKit.HeatPickup(-130, 8, gameplayParent);

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
        // Voll vom Ende des Überwegs (x -54) bis kurz vor die Gelateria-Ecke, dort zurück auf die normale Kamera
        var beachZone = new SerializedObject(new GameObject("Beach Camera Zone", typeof(CameraZone)).GetComponent<CameraZone>());
        beachZone.FindProperty("fromU").floatValue = -158f;
        beachZone.FindProperty("toU").floatValue = -54f;
        beachZone.FindProperty("blend").floatValue = 8f;
        beachZone.FindProperty("offset").vector3Value = BeachCameraOffset;
        beachZone.FindProperty("lookHeight").floatValue = BeachCameraLookHeight;
        beachZone.ApplyModifiedPropertiesWithoutUndo();
        ((CameraZone)beachZone.targetObject).transform.SetParent(gameplayParent, false);

        // Möwen über dem Strand-Stück, zeitversetzt
        Seagull(-100, 2f);
        Seagull(-140, 4.5f);
    }

    private static void Seagull(float u, float firstDelay)
    {
        GameObject gull = LevelBuildKit.PlaceAsset("Assets/Prefabs/Seagull.prefab", u, 10, gameplayParent);
        var serialized = new SerializedObject(gull.GetComponent<Seagull>());
        serialized.FindProperty("territoryFrom").floatValue = -165f;
        serialized.FindProperty("territoryTo").floatValue = -45f;
        serialized.FindProperty("firstDelay").floatValue = firstDelay;
        serialized.FindProperty("cooldown").floatValue = 5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Deko ----------

    private static void BuildProps()
    {
        // Nicht in Baugruben, nicht auf Überwegen, nicht vor Türen und nicht unter Balkonen/Markise
        var blocked = new List<(float from, float to)>
        {
            (-30f, -20f), (-48f, -32f), (-63f, -53f), (-114f, -106f), (-149f, -139f),
            (-11f, -4f), (-170f, -160f), (-100f, -88f), (-134f, -122f)
        };
        bool Free(float u) => blocked.TrueForAll(zone => u < zone.from || u > zone.to);

        for (int u = -14; u > -162; u -= 12)
        {
            if (Free(u)) LevelBuildKit.Place("Street Lamp", u, 0, propParent);
            if (Free(u - 6)) LevelBuildKit.Place("Tree", u - 6, 0, propParent);
        }
        foreach (int u in new[] { -52, -114, -152 })
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
