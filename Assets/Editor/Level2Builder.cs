using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Baut Level 2 (Leaning Tower of Pizza) komplett neu auf. Läuft in der gemeinsamen Stadt: Boden und Häuser kommen aus
// der Stadt-Szene, das Level bringt Laufbahn, Baugruben (mit CityCutouts), Überwege mit Autos, Spiel-Elemente, Möwen und Deko.
// Menü: Tools > Jump and Run > City > Rebuild Level 2 (Leaning Tower). Erneut ausführen überschreibt Handänderungen.
//
// Route (der Spieler läuft nach LINKS, also Richtung -u): aus der Pizzeria nach Westen die Margherita Avenue entlang
// (Strand hinter der Kamera), über Mozzarella Blvd und Oregano Street, Gelateria-Checkpoint an der Ecke, rechts in die
// Basil Street, dann links über die Straße auf die Piazza Pisa zum Kunden am Turm.
// Die Bahn ist in +u-Richtung definiert (Häuser links davon): Sie beginnt auf der Piazza und endet hinter der Pizzeria.
// Auf der Margherita Avenue ist u = Welt-X (wie in Level 1).
//
// Ablauf in Laufrichtung (Welt-X auf der Margherita Avenue, Gehweg-Oberkante 0.5, Sprung ~3.2 m hoch / ~5.5 m weit):
//   -7.5        Start aus der Pizzeria
//   -27..-23    Baugrube 1
//   -46..-34    Überweg Mozzarella Blvd mit Autos
//   -60..-56    Baugrube 2, danach Hund zwischen Container und Bank, Balkon-Kletterei mit Speed-Boost
//  -116..-104   Überweg Oregano Street mit Autos
//  -126..-130   federnde Markise → Balkon mit Wärme-Bonus, Baugrube 3, Gelateria-Checkpoint (Tür bei -165.5)
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
        LevelBuildKit.SetupEssentials(PiazzaU(-228f), "7", KeepWarmTime, new Vector3(-110f, -3f, 10f), new Vector3(280f, 1f, 90f));
        LevelBuildKit.Save(scene);
        Debug.Log("Level2Builder: Level 2 (Leaning Tower) gebaut.");
    }

    // ---------- Boden ----------

    private static void BuildGround(Transform link)
    {
        // Baugruben: Lücke von/bis in Welt-X. Das Prefab ist 1 m länger und steckt unter den Gehwegkanten;
        // der Cutout schneidet Gehweg, Straße und alles darunter genau in der Lücke aus
        Pit(link, -26.75f, -23.25f);
        Pit(link, -59.75f, -56.25f);
        Pit(link, -145.75f, -142.25f);

        // Überwege mit Verkehr: Die Autos kommen die Avenue herunter von der nächsten Kreuzung dahinter
        Crossing(-40, 118f);   // Mozzarella Blvd, hinter der Kreuzung mit der Crust Street
        Crossing(-110, 124f);  // Oregano Street, endet an der Crust Street

        // Über die Basil Street auf die Piazza: begehbare Platte auf Gehweghöhe über der Fahrbahn (dort fahren keine Autos)
        Material asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Asphalt.mat");
        Material marking = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road Marking.mat");
        Renderer table = PrimitiveKit.Box("Basil Crossing", groundParent, new Vector3(-180f, 0.41f, 31f), new Vector3(12f, 0.22f, 18f), asphalt);
        table.gameObject.layer = 3;
        table.gameObject.AddComponent<BoxCollider>();
        for (int i = 0; i < 7; i++)
        {
            PrimitiveKit.Box("Stripe " + (i + 1), table.transform.parent, new Vector3(-180f, 0.525f, 23.5f + i * 2.5f), new Vector3(10f, 0.005f, 1.1f), marking);
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
        // dann zwei Balkone als Kletterpartie mit Speed-Boost oben
        LevelBuildKit.Place("Dumpster", -66, 0, gameplayParent);
        LevelBuildKit.Dog(-76, 1f, gameplayParent);
        LevelBuildKit.Place("Bench", -86, 0, gameplayParent);
        LevelBuildKit.Place("Balcony", -92, 3, gameplayParent);
        LevelBuildKit.Place("Balcony", -96, 6, gameplayParent);
        LevelBuildKit.PowerUp(-96, 8, LevelBuildKit.PowerUpKind.Speed, true, gameplayParent);

        // Hinter der Oregano Street: federnde Markise (Oberkante 2.5) → Balkon (Oberkante 6.5) mit Wärme-Bonus
        LevelBuildKit.Place("Bouncy Awning", -126, 2, gameplayParent);
        LevelBuildKit.Place("Balcony", -130, 6, gameplayParent);
        LevelBuildKit.PowerUp(-130, 8, LevelBuildKit.PowerUpKind.TimeBonus, false, gameplayParent);

        // Basil Street: eine Bank als kleines Hindernis
        LevelBuildKit.Place("Bench", Mathf.Round(BasilU(14f)), 0, gameplayParent);

        // Piazza: Hund zwischen zwei Bänken vor dem Turm
        LevelBuildKit.Place("Bench", Mathf.Round(PiazzaU(-195f)), 0, gameplayParent);
        LevelBuildKit.Dog(Mathf.Round(PiazzaU(-205f)), 1f, gameplayParent);
        LevelBuildKit.Place("Bench", Mathf.Round(PiazzaU(-215f)), 0, gameplayParent);

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
            (-30f, -20f), (-48f, -32f), (-63f, -53f), (-118f, -102f), (-149f, -139f),
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
