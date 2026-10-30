using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Schutt zum Klettern (Margherita Avenue, unterer Mozzarella Blvd, Rasenstück im Park, Four Cheese Avenue)
public static partial class Level5Builder
{
    // ---------- Schutt zum Klettern ----------
    //   Margherita Avenue: kleiner Aufwärm-Hügel gleich hinter der Pizzeria (zwei Stufen hoch, Platte runter)
    //   Unterer Mozzarella Blvd: Schuttberg bis 5 m zwischen den beiden Feuerwehr-Stellen. Vom Gipfel über zwei Stahlträger,
    //     in der Lücke dazwischen die Feuersäule (BuildFirefighters, z 50): warten, bis gelöscht ist. Wer herunterfällt, steht
    //     unten vor derselben Säule. Landehaufen dahinter
    //   Rasen-Stück im Park: umgestürzter Baum (auf den Stamm, über die brennende Krone), zwei Hochhaus-Brocken, ein brennender Krater
    //     (hineinfallen = Respawn), Platte hinunter. Danach landet der Roboter
    //   Four Cheese Avenue: das Hochhaus-Stück, das vor dem Spieler umkippt, schräg hinauf bis 6.2 m, Stufen hinunter auf die
    //     Oregano Street (Finale), dabei fallen Brocken aus dem Kampf. Danach Balkone, ein Stahlträger, zwei Bruchplatten über
    //     einem brennenden Krater, Schuttberg und Platte hinunter, dort ein Doppelschlag fallender Brocken
    //   Basil Street: fallende Brocken (ein großer als Trittstein zu einer Chili, einer vor der Salami-Kreuzung)
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
        // Brocken aus dem Kampf (DebrisDrops): vor dem Finale auf der oberen Stufe und der Oregano-Kreuzung, danach auf der
        // Four Cheese Avenue hinter dem Krater und in der Basil Street
        foreach (var (street, spot, ground, trigger, big) in DebrisDrops)
        {
            float DropU(float at) => street == DebrisStreet.FourCheese ? F(at) : BasilU(at);
            // Ganze u: Die Chili über dem Trittstein sitzt auf ganzen Metern (PrefabGridEnforcer), der Brocken genau darunter
            float spotU = big ? Mathf.Round(DropU(spot)) : DropU(spot);
            RubbleKit.FallingChunk(rubble, spotU, ground, DropU(trigger), seed++, big);
            if (big)
            {
                LevelBuildKit.HeatPickup(spotU, StepChiliY, gameplayParent);
            }
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
    //   Brocken aus dem Kampf: Straße, Landestelle (Four Cheese: x, Basil: z), Boden dort, Auslöser (Spieler vorbei),
    //   großer Brocken. Vom Auslöser bis zum Aufprall vergehen 1.9 s (1 s Warnung + 0.9 s Fall), so weit (~13 m bei 7 m/s)
    //   liegt der Auslöser vor der Stelle: Wer ohne zu schauen durchläuft, kommt genau beim Aufprall an; wer die Warnung
    //   sieht, bleibt davor stehen (2026-10-29, vorher 1.3 s und 6–8 m ohne Warnung).
    //   2026-10-30 (der Nutzer mochte die Brocken, zwei wirkten aber wie eine Wegwerf-Mechanik): mehr davon nach dem Finale,
    //   in verschiedenen Mustern. Nach dem Sturz der Echse bröckeln die Fassaden weiter
    //   - Vor dem Finale: obere Stufe (Oberkante 4.3), Oregano-Kreuzung (ab x -110 läuft das Finale)
    //   - Doppelschlag hinter dem Krater: Der erste löst aus, sobald man auf der Stufe nach den Bruchplatten steht (dort kann
    //     man sicher warten), der zweite erst unten an der Platte. Wer nach dem ersten wieder losrennt und über ihn springt,
    //     läuft genau in den zweiten: zwischen beiden stehen bleiben (2.3 m Platz)
    //   - Basil Street: ein großer Brocken (2 m hoch) als Trittstein, darüber schwebt eine Chili, die man nur von ihm aus
    //     erreicht. Der Brocken, der einen fast erschlagen hätte, ist der Weg zu ihr. Dann einer kurz vor der Salami-Kreuzung
    private enum DebrisStreet { FourCheese, Basil }
    private static readonly (DebrisStreet street, float spot, float ground, float trigger, bool big)[] DebrisDrops =
    {
        (DebrisStreet.FourCheese, -101.25f, 4.3f, -88f, false),
        (DebrisStreet.FourCheese, -107.5f, CityLayout.Surface, -94.5f, false),
        (DebrisStreet.FourCheese, -159.5f, CityLayout.Surface, -147.5f, false),
        (DebrisStreet.FourCheese, -165f, CityLayout.Surface, -152f, false),
        (DebrisStreet.Basil, 238f, CityLayout.Surface, 225f, true),
        (DebrisStreet.Basil, 253.5f, CityLayout.Surface, 240.5f, false),
    };
    //   Chili über dem großen Brocken (Wurzel = Mitte des Triggers, Radius 0.7). Vom Gehweg (0.5) reicht der Kopf im Sprung
    //   bis 5.3, vom Brocken (Oberkante 2.5) bis 7.3: bei 7 nur von ihm aus erreichbar
    private const float StepChiliY = 7f;

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
}
