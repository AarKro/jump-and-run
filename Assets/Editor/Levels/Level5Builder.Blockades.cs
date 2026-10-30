using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Blockaden: gestürzte Gründer-Statue am Founder's Circle, versperrter Parkweg zum Brunnen, umgestürztes Hochhaus östlich der Pizzeria
public static partial class Level5Builder
{
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
        PrimitiveKit.Cylinder("Crater", blockade, new Vector3(-36f, CityLayout.Surface + 0.02f, craterZ), 5f, 0.1f, scorched);
        PrimitiveKit.Cylinder("Crater Rim", blockade, new Vector3(-36f, CityLayout.Surface + 0.1f, craterZ), 6.2f, 0.25f, rubble);
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

    // ---------- Park: Weg zum Brunnen versperrt ----------

    // Warum die Bahn hinter dem Eiswagen vom Südost-Weg auf den Rasen abbiegt (2026-10-30, vorher lief der Weg frei weiter
    // zum Brunnen und die Abbiegung wirkte willkürlich): Gleich hinter der Abzweigung liegt brennender Schutt auf dem Weg,
    // weiter zum Brunnen hin steckt ein Hochhaus-Stück, das die Echse aus dem Viertel geschleudert hat, schräg im Rasen quer
    // über dem Weg (das untere Ende im Krater, das obere mit der Bruchkante hoch in der Luft, brennende Fenster, Rauch).
    // Kamera: Auf dem Rasen-Stück steht sie ~8 m westlich der Bahn (x ~-87). Hohes darf dort nicht stehen, darum ist der
    // Schutt an der Abzweigung niedrig und das Turmstück steht weiter zum Brunnen hin, sein vergrabenes Ende zeigt nach
    // Nordosten (zur Kamera), das hohe nach Südwesten. Dazu etwas Verwüstung im Park (Brandflecken, Schutt, kleine Feuer).
    // Kulisse ohne Collider, die Bahn biegt ohnehin ab
    private const float ParkTowerS = 18f;          // Abstand vom Brunnen, wo das Turmstück den Weg kreuzt
    private const float ParkTowerTilt = 22f;       // so steil steckt es im Boden (Grad)
    private const float ParkTowerLength = 16f, ParkTowerBuried = 5f, ParkTowerWidth = 7f, ParkTowerThickness = 6f;
    //   Schutt auf dem Weg hinter der Abzweigung: s vom Brunnen, seitlich (+ = Nordost), Radius. Nicht näher als s 30, dort
    //   beginnt der Bogen der Bahn
    private static readonly (float s, float side, float radius)[] PathRubble =
        { (30f, 0.5f, 1.4f), (27.5f, -1.5f, 2f), (25f, 1.2f, 2.2f), (22.5f, -2.5f, 1.8f) };
    private static readonly (float s, float side)[] PathFires = { (27f, 0.3f), (24.5f, -1f) };
    //   Verwüstung im Park: Welt x, z, Brandfleck-Radius, Feuer. Östlich vom Rasen-Stück (hinter der Bahn, dorthin schaut die
    //   Kamera) und nordöstlich des Südost-Wegs vor dem Eiswagen (nicht an ihm, dort liegt die Deckung)
    private static readonly (float x, float z, float radius, bool fire)[] ParkDamage =
    {
        (-73f, 167f, 3f, true), (-69f, 181f, 2.5f, false), (-74f, 195f, 3f, true), (-63f, 174f, 2f, false),
        (-65f, 155f, 2.5f, true), (-59f, 153f, 2f, false),
    };

    private static void BuildParkBlockade(Transform link)
    {
        Transform root = new GameObject("Park Blockade").transform;
        root.SetParent(propParent, false);
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        Material scorched = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Scorched.mat");
        var random = new System.Random(330);
        Vector2 center = CityLayout.ParkCenter;
        const float lawn = CityLayout.ParkLawnTop;
        var along = new Vector3(DirSE.x, 0f, DirSE.y);
        var toNE = new Vector3(-DirSE.y, 0f, DirSE.x);
        Vector3 OnPath(float s, float side) => new Vector3(center.x, lawn, center.y) + along * s + toNE * side;

        // Turmstück: Hochachse (lokal Y, dort liegen die Stockwerke) zeigt schräg nach Südwesten hinauf, lokal X entlang des
        // Wegs. Das Nordost-Ende steckt ParkTowerBuried tief (entlang der Achse) im Rasen
        Vector3 axis = (-toNE * Mathf.Cos(ParkTowerTilt * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(ParkTowerTilt * Mathf.Deg2Rad)).normalized;
        Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(along, axis), axis);
        Vector3 hit = OnPath(ParkTowerS, 0f);
        Renderer tower = PrimitiveKit.Box("Flung High-Rise", root, Vector3.zero, new Vector3(ParkTowerWidth, ParkTowerLength, ParkTowerThickness), facade);
        tower.transform.SetPositionAndRotation(hit + axis * (ParkTowerLength / 2f - ParkTowerBuried), rotation);
        BreakEdge(root, tower.transform, 14, random);
        // Krater, wo es eingeschlagen ist (am vergrabenen Ende), aufgeworfene Erde und Schutt ringsum
        Vector3 crater = hit - axis * ParkTowerBuried * Mathf.Cos(ParkTowerTilt * Mathf.Deg2Rad) * 0.6f;
        crater.y = lawn;
        PrimitiveKit.Cylinder("Crater", root, crater + Vector3.up * 0.02f, 6f, 0.1f, scorched);
        PrimitiveKit.Cylinder("Crater Rim", root, crater + Vector3.up * 0.05f, 7.2f, 0.2f, rubble);
        RubblePile(root, crater + toNE * 4f + along * 3f, 2.5f, rubble, facade, random);
        RubblePile(root, crater + toNE * 3f - along * 4f, 2.5f, rubble, facade, random);
        RubblePile(root, hit - toNE * 6f + along * 4f, 2f, rubble, facade, random);
        // Es brennt in Fenstern auf der Seite zur Abzweigung (+X, Südost), Rauch steigt von der Bruchkante
        foreach (float k in new[] { -0.15f, 0.2f })
        {
            HouseFire(root, tower.transform.TransformPoint(new Vector3(0.5f, k, 0.2f)) + along * 0.3f);
        }
        Smoke(root, tower.transform.TransformPoint(new Vector3(0f, 0.5f, 0f)));

        // Brennender Schutt auf dem Weg hinter der Abzweigung, mit Brandfleck
        PrimitiveKit.Cylinder("Path Scorch", root, OnPath(26.5f, 0f) + Vector3.up * 0.03f, 4f, 0.05f, scorched);
        foreach (var (s, side, radius) in PathRubble)
        {
            RubblePile(root, OnPath(s, side), radius, rubble, facade, random);
        }
        foreach (var (s, side) in PathFires)
        {
            HouseFire(root, OnPath(s, side) + Vector3.up * 0.3f);
        }

        // Verwüstung im Park
        int index = 0;
        foreach (var (x, z, radius, fire) in ParkDamage)
        {
            var at = new Vector3(x, lawn, z);
            PrimitiveKit.Cylinder("Scorch", root, at + Vector3.up * 0.03f, radius, 0.05f, scorched);
            RubblePile(root, at, radius * 0.6f, rubble, facade, random);
            if (fire)
            {
                HouseFire(root, at + Vector3.up * 0.3f);
            }
            // Bäume, die mitten darin stünden (nur sie, der Boden bleibt)
            LevelBuildKit.Cutout(link, $"Trees Park Damage {++index}", x - radius, x + radius, z - radius, z + radius, treesOnly: true);
        }
        // Bäume im Turmstück und im Krater
        Vector3 high = hit + axis * (ParkTowerLength - ParkTowerBuried);
        LevelBuildKit.Cutout(link, "Trees Flung High-Rise", Mathf.Min(high.x, crater.x) - 6f, Mathf.Max(high.x, crater.x) + 6f,
            Mathf.Min(high.z, crater.z) - 6f, Mathf.Max(high.z, crater.z) + 6f, treesOnly: true);
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
        BreakEdge(blockade, t, 12, random);
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

    // Abgebrochenes Ende eines liegenden Turmstücks (lokal Y = 0.5 ist die Bruchkante): Betonbrocken und Eisen
    private static void BreakEdge(Transform parent, Transform tower, int count, System.Random random)
    {
        Material concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Broken Concrete.mat");
        Material rebar = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rebar.mat");
        for (int i = 0; i < count; i++)
        {
            // Lokale Maße sind wegen der Skalierung Anteile: X und Z -0.5…0.5
            Vector3 local = new Vector3(((float)random.NextDouble() - 0.5f) * 0.9f, 0.5f + (float)random.NextDouble() * 0.03f, ((float)random.NextDouble() - 0.5f) * 0.9f);
            Vector3 world = tower.TransformPoint(local);
            Renderer piece = i % 3 == 0
                ? PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.08f, 0.8f, 0.08f), rebar)
                : PrimitiveKit.Box("Shard", parent, Vector3.zero, Vector3.one * (0.8f + (float)random.NextDouble()), concrete);
            piece.transform.SetPositionAndRotation(world, tower.rotation * Quaternion.Euler(random.Next(-40, 40), random.Next(360), random.Next(-40, 40)));
        }
    }
}
