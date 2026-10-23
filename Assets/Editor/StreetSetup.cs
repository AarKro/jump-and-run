using UnityEditor;
using UnityEngine;

// Baut das Prefab "Street Crossing": eine echte Querstraße mit 2 Spuren, die zwischen den Häusern hervorkommt.
// Liegt als Auflage auf einem Gehweg (Sidewalk-Prefab der Stadt): Hinter der Laufbahn läuft sie auf Straßenhöhe weit
// nach hinten (+Z) in den Nebel, mit Gehwegen und Bordsteinen an beiden Seiten. Vorne kreuzt sie die Hauptstraße
// (die gehört zum Sidewalk-Prefab) und läuft unter der Kamera aus. Wo der Spieler läuft, ist ein erhöhter Zebrastreifen
// auf Gehweghöhe mit kleinen Rampen: Die Laufbahn bleibt eben, und die Autos rollen über die Schwelle (Höhenkurve).
// Kein eigener Boden-Collider, der Gehweg darunter bleibt der Boden.
// Erwartet eine 10 m breite Lücke in der Häuserzeile (OuterHalfWidth). Die Häuser daneben baut CityLevelBuilder.
// Menü: Tools > Jump and Run > Build Street Crossing Prefab. Mehrfach ausführbar, das Prefab wird ersetzt.
// Braucht die Stadt-Materialien (erst Tools > Jump and Run > City > Build City Prefabs).
public static class StreetSetup
{
    private const string PrefabPath = "Assets/Prefabs/Street Crossing.prefab";
    private const string AsphaltMaterialPath = "Assets/Materials/Asphalt.mat";
    private const string MarkingMaterialPath = "Assets/Materials/Road Marking.mat";
    private const string CarMaterialPath = "Assets/Materials/Car.mat";
    private const string WheelMaterialPath = "Assets/Materials/Wheel.mat";
    private const string HeadlightMaterialPath = "Assets/Materials/Headlight.mat";
    private const string CityMaterials = "Assets/Materials/City";

    private const float LaneWidth = 3f;
    private const float RoadHalfWidth = LaneWidth + 0.2f;
    // Halbe Breite der ganzen Querstraße mit Gehwegen = halbe Lücke in der Häuserzeile
    public const float OuterHalfWidth = 5f;
    // Vorne läuft sie noch ein Stück über die Hauptstraße hinaus (die endet bei Z = -14), hinten bis in den Nebel
    private const float RoadStartZ = -16f;
    public const float RoadEndZ = 104f;
    private const float MainRoadFrontZ = -14f;
    private const float MainRoadLineZ = -8f;
    // Gehweg der Laufbahn (aus dem Sidewalk-Prefab): von Z = -2 bis 3, Oberkante 0.5. Die Straße liegt 0.2 tiefer
    private const float WalkFrontZ = -2f;
    private const float WalkBackZ = 3f;
    private const float RampLength = 1.5f;
    // Wurzel = Mitte einer 1 m dicken Plattform. Der PrefabGridEnforcer rastet Prefab-Wurzeln auf ganze Meter ein;
    // die Plattformmitten liegen auf ganzen Metern, ihre Oberkanten 0.5 darüber
    private const float SurfaceHeight = 0.5f;
    private const float RoadHeight = 0.3f;
    // Hier tauchen die Autos auf: am Ende der Anchovy Alley (Calzone Road), seit die Stadt ohne dichten Nebel sichtbar ist.
    // 270 m weiter hinten als ursprünglich (40): Das sind bei 12 m/s genau 9 Takte à 2.5 s,
    // am Zebrastreifen kommen sie also im selben Rhythmus an wie vorher
    private const float CarSpawnZ = 310f;

    [MenuItem("Tools/Jump and Run/Build Street Crossing Prefab")]
    public static void BuildStreet()
    {
        Material asphalt = GetOrCreateMaterial(AsphaltMaterialPath, "Universal Render Pipeline/Lit", new Color(0.18f, 0.18f, 0.2f));
        Material marking = GetOrCreateMaterial(MarkingMaterialPath, "Universal Render Pipeline/Lit", new Color(0.95f, 0.95f, 0.95f));
        Material paint = GetOrCreateMaterial(CarMaterialPath, "Universal Render Pipeline/Lit", Color.white);
        Material wheel = GetOrCreateMaterial(WheelMaterialPath, "Universal Render Pipeline/Lit", new Color(0.08f, 0.08f, 0.08f));
        // Unlit: Scheinwerfer leuchten unabhängig vom Licht, so fallen entgegenkommende Autos früh auf
        Material headlight = GetOrCreateMaterial(HeadlightMaterialPath, "Universal Render Pipeline/Unlit", new Color(1f, 0.95f, 0.6f));

        // Dieselben Materialien wie Hauptstraße und Gehweg, damit die Kreuzung nahtlos aussieht
        Material road = LoadCityMaterial("Road");
        Material concrete = LoadCityMaterial("Sidewalk");
        Material curb = LoadCityMaterial("Curb");
        Material foundation = LoadCityMaterial("Foundation");
        if (road == null || concrete == null || curb == null || foundation == null)
        {
            Debug.LogError("StreetSetup: Stadt-Materialien fehlen, erst Tools > Jump and Run > City > Build City Prefabs ausführen.");
            return;
        }

        var root = new GameObject("Street Crossing");
        try
        {
            BuildRoad(root.transform, road, foundation, marking);
            BuildCrosswalk(root.transform, asphalt, marking);
            BuildSidewalks(root.transform, concrete, curb, foundation);

            // Zwei Spuren nebeneinander in X, beide fahren von hinten nach vorne, die zweite um ein halbes Intervall versetzt
            AnimationCurve height = CrosswalkHeightCurve();
            BuildLane("Lane 1", root.transform, -LaneWidth / 2f, 0f, height, paint, wheel, headlight);
            BuildLane("Lane 2", root.transform, LaneWidth / 2f, 1.25f, height, paint, wheel, headlight);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"StreetSetup: Prefab gespeichert unter {PrefabPath}.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material LoadCityMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{CityMaterials}/{name}.mat");

    // Fahrbahn auf Straßenhöhe: hinter der Laufbahn bis ganz nach hinten, vor der Hauptstraße noch ein Stück bis unter die Kamera.
    // Dazwischen liegt die Hauptstraße selbst (Teil des Gehweg-Prefabs), dort nur die gelbe Mittellinie abdecken
    private static void BuildRoad(Transform root, Material road, Material foundation, Material marking)
    {
        Transform parent = new GameObject("Road").transform;
        parent.SetParent(root, false);
        float width = RoadHalfWidth * 2f;

        RoadPiece("Back", parent, WalkBackZ, RoadEndZ, width, road, foundation);
        RoadPiece("Front", parent, RoadStartZ, MainRoadFrontZ, width, road, foundation);

        // Auf der Kreuzung keine gelbe Linie der Hauptstraße: ein dünnes Stück Asphalt knapp darüber
        PrimitiveKit.Box("Intersection Patch", parent, new Vector3(0f, RoadHeight + 0.012f, MainRoadLineZ), new Vector3(width, 0.01f, 0.4f), road);

        // Weiße Mittellinie nur auf den Straßenstücken, nicht auf der Kreuzung und nicht auf dem Zebrastreifen
        float backFrom = WalkBackZ + RampLength + 1f;
        PrimitiveKit.Box("Center Line Back", parent, new Vector3(0f, RoadHeight + 0.003f, (backFrom + RoadEndZ) / 2f), new Vector3(0.12f, 0.005f, RoadEndZ - backFrom), marking);
        PrimitiveKit.Box("Center Line Front", parent, new Vector3(0f, RoadHeight + 0.003f, (RoadStartZ + MainRoadFrontZ) / 2f), new Vector3(0.12f, 0.005f, MainRoadFrontZ - RoadStartZ), marking);
    }

    private static void RoadPiece(string name, Transform parent, float fromZ, float toZ, float width, Material road, Material foundation)
    {
        float centerZ = (fromZ + toZ) / 2f;
        float length = toZ - fromZ;
        PrimitiveKit.Box($"Asphalt {name}", parent, new Vector3(0f, RoadHeight / 2f, centerZ), new Vector3(width, RoadHeight, length), road);
        // Fundament nach unten (wie bei der Hauptstraße), damit man nie unter die Straße ins Leere schaut
        PrimitiveKit.Box($"Foundation {name}", parent, new Vector3(0f, -3.1f, centerZ), new Vector3(width, 6.2f, length), foundation);
    }

    // Erhöhter Zebrastreifen auf dem Gehweg der Laufbahn, vorne und hinten je eine Rampe hinunter auf Straßenhöhe
    private static void BuildCrosswalk(Transform root, Material asphalt, Material marking)
    {
        Transform crosswalk = new GameObject("Raised Crosswalk").transform;
        crosswalk.SetParent(root, false);
        float width = RoadHalfWidth * 2f;
        // Knapp über dem Gehweg, sonst flackern die Flächen gegeneinander (Z-Fighting)
        float top = SurfaceHeight + 0.02f;

        PrimitiveKit.Box("Table", crosswalk, new Vector3(0f, SurfaceHeight + 0.01f, (WalkFrontZ + WalkBackZ) / 2f), new Vector3(width, 0.02f, WalkBackZ - WalkFrontZ), asphalt);
        Ramp("Ramp Front", crosswalk, WalkFrontZ, -1f, top, width, asphalt);
        Ramp("Ramp Back", crosswalk, WalkBackZ, 1f, top, width, asphalt);

        // Zebrastreifen über die Laufbahn des Spielers (Z = 0): Balken in Fahrtrichtung, nebeneinander in X
        Transform zebra = new GameObject("Zebra Crossing").transform;
        zebra.SetParent(crosswalk, false);
        int stripes = 8;
        for (int i = 0; i < stripes; i++)
        {
            float x = -width / 2f + 0.5f + i * ((width - 1f) / (stripes - 1));
            PrimitiveKit.Box("Stripe " + (i + 1), zebra, new Vector3(x, top + 0.004f, 0f), new Vector3(0.4f, 0.005f, 2.4f), marking);
        }
    }

    // Schräge Platte von der Kante des Zebrastreifens (Höhe top) über RampLength hinunter auf Straßenhöhe.
    // Sie ist dicker als der Höhenunterschied und steckt unten in der Straße, so bleibt seitlich kein Spalt
    private static void Ramp(string name, Transform parent, float edgeZ, float outward, float top, float width, Material material)
    {
        const float thickness = 0.3f;
        float drop = top - RoadHeight;
        float angle = Mathf.Atan2(drop, RampLength) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(RampLength * RampLength + drop * drop);
        // Mitte der Oberseite liegt auf halber Höhe der Rampe, die Box-Mitte eine halbe Dicke (senkrecht zur Schräge) darunter
        float centerY = (top + RoadHeight) / 2f - thickness / 2f / Mathf.Cos(angle * Mathf.Deg2Rad);
        Renderer ramp = PrimitiveKit.Box(name, parent, new Vector3(0f, centerY, edgeZ + outward * RampLength / 2f), new Vector3(width, thickness, length), material);
        // Positiver Winkel um X senkt das +Z-Ende ab: hinten (outward +1) so, vorne andersherum
        ramp.transform.localRotation = Quaternion.Euler(outward * angle, 0f, 0f);
    }

    // Gehwege mit Bordstein links und rechts der Fahrbahn, von der Laufbahn bis ganz nach hinten.
    // Gleiche Höhe wie der Gehweg der Laufbahn, sie schließen hinten bündig an ihn an
    private static void BuildSidewalks(Transform root, Material concrete, Material curb, Material foundation)
    {
        Transform parent = new GameObject("Sidewalks").transform;
        parent.SetParent(root, false);
        float width = OuterHalfWidth - RoadHalfWidth;
        float length = RoadEndZ - WalkBackZ;
        float centerZ = (WalkBackZ + RoadEndZ) / 2f;

        foreach (float side in new[] { -1f, 1f })
        {
            string label = side < 0f ? "Left" : "Right";
            float centerX = side * (RoadHalfWidth + width / 2f);
            PrimitiveKit.Box($"Sidewalk {label}", parent, new Vector3(centerX, 0f, centerZ), new Vector3(width, 1f, length), concrete);
            PrimitiveKit.Box($"Foundation {label}", parent, new Vector3(centerX, -3.25f, centerZ), new Vector3(width, 5.5f, length), foundation);
            // Wie beim Gehweg-Prefab: Bordstein knapp innerhalb der Kante, ragt bis zur Straße hinunter
            PrimitiveKit.Box($"Curb {label}", parent, new Vector3(side * (RoadHalfWidth + 0.1f), 0.38f, centerZ), new Vector3(0.25f, 0.25f, length), curb);
        }
    }

    // Höhe der Fahrbahn über Z, relativ zur Spur (die liegt auf Straßenhöhe): flach, über die Rampe hoch auf den Zebrastreifen,
    // über die andere Rampe wieder runter. Lineare Tangenten, damit die Autos genau der Rampe folgen statt einer Welle
    private static AnimationCurve CrosswalkHeightCurve()
    {
        float bump = SurfaceHeight - RoadHeight;
        var curve = new AnimationCurve(
            new Keyframe(RoadStartZ - 10f, 0f),
            new Keyframe(WalkFrontZ - RampLength, 0f),
            new Keyframe(WalkFrontZ, bump),
            new Keyframe(WalkBackZ, bump),
            new Keyframe(WalkBackZ + RampLength, 0f),
            new Keyframe(RoadEndZ + 10f, 0f));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static void BuildLane(string name, Transform root, float x, float startOffset, AnimationCurve height,
        Material paint, Material wheel, Material headlight)
    {
        Transform lane = new GameObject(name).transform;
        lane.SetParent(root, false);
        lane.localPosition = new Vector3(x, RoadHeight, 0f);

        Car template = BuildCar(lane, paint, wheel, headlight);
        template.name = "Car Template";
        template.gameObject.SetActive(false);

        var trafficLane = lane.gameObject.AddComponent<TrafficLane>();
        var serialized = new SerializedObject(trafficLane);
        serialized.FindProperty("carTemplate").objectReferenceValue = template;
        serialized.FindProperty("startOffset").floatValue = startOffset;
        serialized.FindProperty("spawnZ").floatValue = CarSpawnZ;
        serialized.FindProperty("heightOverZ").animationCurveValue = height;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Auto fährt Richtung -Z (zur Kamera), vorne ist also -Z. Unterkante der Reifen auf y = 0
    private static Car BuildCar(Transform parent, Material paint, Material wheel, Material headlight)
    {
        var carObject = new GameObject("Car");
        carObject.transform.SetParent(parent, false);

        Renderer body = CreatePart("Body", PrimitiveType.Cube, carObject.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.7f, 4f), paint);
        Renderer cabin = CreatePart("Cabin", PrimitiveType.Cube, carObject.transform, new Vector3(0f, 1.2f, 0.3f), new Vector3(1.6f, 0.55f, 2f), paint);

        foreach (float wheelX in new[] { -0.9f, 0.9f })
        {
            foreach (float wheelZ in new[] { -1.3f, 1.3f })
            {
                // Zylinder steht hochkant (Achse Y), um Z gekippt zeigt die Achse nach X wie bei einem Rad
                CreatePart("Wheel", PrimitiveType.Cylinder, carObject.transform, new Vector3(wheelX, 0.3f, wheelZ), new Vector3(0.6f, 0.12f, 0.6f), wheel)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        CreatePart("Headlight L", PrimitiveType.Cube, carObject.transform, new Vector3(-0.6f, 0.65f, -2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);
        CreatePart("Headlight R", PrimitiveType.Cube, carObject.transform, new Vector3(0.6f, 0.65f, -2.01f), new Vector3(0.35f, 0.2f, 0.04f), headlight);

        // Ein Trigger für das ganze Auto. Bewegte Collider brauchen einen (kinematischen) Rigidbody,
        // sonst behandelt die Physik sie als statisch und muss bei jeder Bewegung neu rechnen
        var trigger = carObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.8f, 0f);
        trigger.size = new Vector3(1.8f, 1.4f, 4f);

        var rigidbody = carObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        var car = carObject.AddComponent<Car>();
        var serialized = new SerializedObject(car);
        SerializedProperty renderers = serialized.FindProperty("paintRenderers");
        renderers.arraySize = 2;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = body;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = cabin;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return car;
    }

    private static Renderer CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        // Nur Optik: Die Plattform ist der Boden, und am Auto zählt nur der eine Trigger
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static Material GetOrCreateMaterial(string path, string shaderName, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find(shaderName));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
