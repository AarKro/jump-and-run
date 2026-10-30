using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Feuerwehr am unteren Mozzarella Blvd und die Feuerwache als Checkpoint
public static partial class Level5Builder
{
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

    // ---------- Checkpoint: Feuerwache ----------
    //   Gleich nach dem Feuerwehr-Abschnitt, vor der Crust Street, auf dem Eckgrundstück an der Fassade (dasselbe Grundstück
    //   wie der Bauwagen in Level 3: 8 m entlang der Bahn, 10.8 m tief). Prefab aus FireSetup, ersetzt das Stadthaus dort
    //   per Cutout. Der einzige Checkpoint von Level 5 (2026-10-29): alles danach (Feueratem, Roboter, Brocken, Finale,
    //   Hickser) beginnt nach einem Tod von hier aus neu
    private const float FireStationZ = 98f;

    private static void BuildFireStation()
    {
        LevelBuildKit.Cutout(GameObject.Find("City Link").transform, "Fire Station Lot", MozzarellaX + 3f, MozzarellaX + 3f + 10.8f,
            FireStationZ - 4f, FireStationZ + 4f);
        LevelBuildKit.PlaceAsset($"{FireSetup.PrefabFolder}/Fire Station.prefab", Mathf.Round(LowerMozzarellaU(FireStationZ)), 0, gameplayParent);
    }

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
}
