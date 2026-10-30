using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Feueratem im Park: Aufladen am Maul, Flammenstrom, Feuerwalze, umgekippter Eiswagen als Deckung
public static partial class Level5Builder
{
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
}
