using UnityEditor;
using UnityEngine;

// Baut die Prefabs für den Feuerwehr-Abschnitt in Level 5 (unterer Mozzarella Blvd): brennende Stelle mit Feuerwehrmann
// (FirePatch), Feuersäule (ebenso, hoch und schmal), breites Dauerfeuer, Löschfahrzeug, Drehleiter-Fahrzeug (LadderBoom) und den Rettungskorb (MovingPlatform).
// Dazu die Feuerwache (Checkpoint von Level 5, gleich nach dem Feuerwehr-Abschnitt).
// Menü: Tools > Jump and Run > City > Build Fire Prefabs. Mehrfach ausführbar, Prefabs werden ersetzt.
//
// Konvention wie bei den Stadt-Prefabs: Wurzel auf der Laufbahn (lokal +X = +u, +Z = hinten zu den Häusern, -Z = Kamera),
// auf der Mitte einer 1 m dicken Bodenplatte: Gehweg = +0.5, Fahrbahn = +0.3. Die Fahrzeuge stehen auf der Fahrbahn vor der
// Bahn, ihr Inhalt ist darum im Prefab nach -Z verschoben (die Wurzel bleibt auf der Bahn).
public static class FireSetup
{
    public const string PrefabFolder = "Assets/Prefabs/Fire";
    private const string MaterialFolder = "Assets/Materials/City";
    private const int GroundLayer = 3;
    private const float Surface = 0.5f;
    private const float Road = 0.3f;

    public const float PatchLength = 7f;
    public const float BedLength = 12f;
    // Feuersäule zwischen den Stahlträgern: Die Lücke ist 2.5 m, der Trigger 1.4 m (wer an der Trägerkante steht, ragt mit
    // seiner Kapsel 0.5 m hinein und soll nicht verbrennen). Er reicht bis 9 m: höher, als man von den Trägern (5 m) springt
    public const float ColumnLength = 1.4f;
    public const float ColumnHazardHeight = 8.5f;
    // Feuerwehrmann auf der Fahrbahn gleich hinter dem Bordstein (der liegt 1 m vor der Bahn). Die Fahrzeuge stehen nicht bei
    // den Stellen, sondern in den Lücken dazwischen (sonst verdecken sie die Feuerwehrleute), auch dicht am Bordstein
    public const float FirefighterZ = -2.6f;
    public const float TruckZ = -4.4f;
    public const float TruckLength = 8f;
    public const float TruckWidth = 2.5f;
    // Drehkranz der Drehleiter am hinteren Ende (lokal +X)
    public const float TurntableX = 3f;

    private static Material embers, red, white, chrome, glass, wheel, dark, helmet, reflective, tank, hose, brass, charred, wet,
        lightRed, lightBlue, lightOff, ladder, figure;
    private static Material fireMaterial, smokeMaterial;

    [MenuItem("Tools/Jump and Run/City/Build Fire Prefabs")]
    public static void BuildAll()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Fire");
        CreateMaterials();
        BuildFirePatch();
        BuildFireColumn();
        BuildFireBed();
        BuildFireTruck();
        BuildLadderTruck();
        BuildRescueBasket();
        BuildFireStation();
        AssetDatabase.SaveAssets();
        Debug.Log("FireSetup: Feuerwehr-Prefabs gebaut.");
    }

    [MenuItem("Tools/Jump and Run/City/Build Fire Station Prefab")]
    public static void BuildFireStationOnly()
    {
        PrimitiveKit.EnsureFolder("Assets/Prefabs", "Fire");
        CreateMaterials();
        BuildFireStation();
        AssetDatabase.SaveAssets();
    }

    // ---------- Feuerwache (Checkpoint) ----------

    // Feuerwache "No. 5" auf dem Eckgrundstück vor der Crust Street (dasselbe wie der Bauwagen in Level 3: 8 m entlang der
    // Bahn, von der Fassadenlinie lokal z 3 bis z 13.8; ersetzt ein Stadthaus per Cutout und bringt eigenen Boden mit).
    // Zweistöckig aus rotem Backstein: rechts (lokal +X, dort kommt der Spieler her) ein offenes, leeres Fahrzeugtor (die
    // Wagen sind draußen beim Löschen), links die Tür mit SpawnDoor (dahinter ein dunkler Raum, dort wartet der Spieler vor
    // dem Respawn). Bis zum Checkpoint dunkel; danach gehen die Fenster an, die rote Rundumleuchte auf dem Dach dreht sich
    // und "Checkpoint unlocked!" steigt auf (SiteOfficeEffects, ohne Tor)
    private static void BuildFireStation()
    {
        const float facade = 3f, back = 13.8f, halfWidth = 4f, wall = 0.3f;
        const float groundTop = Surface + 3.8f, roof = Surface + 7f;
        const float bayLeft = 0f, bayRight = 3.6f;
        const float doorLeft = -2.9f, doorRight = -1.9f, doorHeight = 2.1f;
        Material brick = Lit("Station Brick", new Color(0.6f, 0.23f, 0.16f), 0.15f);
        Material stone = Lit("Station Stone", new Color(0.86f, 0.83f, 0.76f));
        Material concrete = Lit("Station Floor", new Color(0.5f, 0.5f, 0.48f));
        Material inside = Lit("Station Inside", new Color(0.62f, 0.6f, 0.56f));
        Material yellowLine = Lit("Station Line", new Color(0.95f, 0.78f, 0.15f));
        Material oil = Lit("Oil Stain", new Color(0.12f, 0.12f, 0.12f), 0.6f);
        Material windowDark = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Window Glass.mat");
        Material windowLit = PrimitiveKit.Material($"{MaterialFolder}/Window Lit.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.86f, 0.55f));
        Material room = PrimitiveKit.Material($"{MaterialFolder}/Interior Dark.mat", PrimitiveKit.UnlitShader, new Color(0.07f, 0.05f, 0.04f));
        Material beaconOff = Lit("Station Beacon Off", new Color(0.45f, 0.08f, 0.06f), 0.6f);
        Material beaconOn = Unlit("Station Beacon On", new Color(1f, 0.15f, 0.1f));

        var root = new GameObject("Fire Station");
        Transform t = root.transform;
        // Boden: Vorplatz und Halle aus Beton, Oberkante 5 mm unter dem Gehweg (keine flackernde Schnittkante)
        PrimitiveKit.Box("Floor", t, new Vector3(0f, (Surface - 0.005f - 6f) / 2f, (facade + back) / 2f),
            new Vector3(halfWidth * 2f, Surface - 0.005f + 6f, back - facade), concrete);

        // Außenwände: Vorderwand in Stücken um Tor und Tür, darüber das Obergeschoss in einem Stück
        float frontZ = facade + wall / 2f;
        void Front(string name, float x0, float x1, float y0, float y1) =>
            PrimitiveKit.Box(name, t, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, frontZ), new Vector3(x1 - x0, y1 - y0, wall), brick);
        Front("Pier Left", -halfWidth, doorLeft, Surface, groundTop);
        Front("Pier Middle", doorRight, bayLeft, Surface, groundTop);
        Front("Pier Right", bayRight, halfWidth, Surface, groundTop);
        Front("Over Door", doorLeft, doorRight, Surface + doorHeight, groundTop);
        Front("Upper Floor", -halfWidth, halfWidth, groundTop, roof);
        foreach (float x in new[] { -halfWidth + wall / 2f, halfWidth - wall / 2f })
        {
            PrimitiveKit.Box("Side Wall", t, new Vector3(x, (Surface + roof) / 2f, (facade + back) / 2f), new Vector3(wall, roof - Surface, back - facade), brick);
        }
        PrimitiveKit.Box("Back Wall", t, new Vector3(0f, (Surface + roof) / 2f, back - wall / 2f), new Vector3(halfWidth * 2f, roof - Surface, wall), brick);
        PrimitiveKit.Box("Roof", t, new Vector3(0f, roof + 0.15f, (facade + back) / 2f), new Vector3(halfWidth * 2f + 0.2f, 0.3f, back - facade + 0.2f), stone);
        PrimitiveKit.Box("Parapet", t, new Vector3(0f, roof + 0.5f, facade + 0.1f), new Vector3(halfWidth * 2f + 0.2f, 0.5f, 0.25f), stone);
        PrimitiveKit.Box("Base Course", t, new Vector3(0f, Surface + 0.2f, facade - 0.02f), new Vector3(halfWidth * 2f, 0.4f, 0.06f), stone);

        // Fahrzeughalle hinter dem Tor: Decke, Trennwand, leer; nur Spuren, wo der Wagen stand
        PrimitiveKit.Box("Bay Ceiling", t, new Vector3((bayLeft + bayRight) / 2f, groundTop + 0.15f, (facade + back) / 2f), new Vector3(bayRight - bayLeft, 0.3f, back - facade - 0.4f), inside);
        PrimitiveKit.Box("Bay Wall", t, new Vector3(bayLeft - 0.1f, (Surface + groundTop) / 2f, (facade + back) / 2f + 0.2f), new Vector3(0.2f, groundTop - Surface, back - facade - 0.4f), inside);
        PrimitiveKit.Box("Bay Back", t, new Vector3((bayLeft + bayRight) / 2f, (Surface + groundTop) / 2f, back - wall - 0.02f), new Vector3(bayRight - bayLeft, groundTop - Surface, 0.04f), inside);
        foreach (float x in new[] { bayLeft + 0.35f, bayRight - 0.35f })
        {
            PrimitiveKit.Box("Bay Line", t, new Vector3(x, Surface + 0.005f, (facade + back) / 2f), new Vector3(0.12f, 0.01f, back - facade - 1f), yellowLine);
        }
        PrimitiveKit.Box("Oil Stain", t, new Vector3((bayLeft + bayRight) / 2f + 0.2f, Surface + 0.006f, 8.5f), new Vector3(1.1f, 0.01f, 0.8f), oil);
        // Haken mit Helmen und eine aufgerollte Schlauchrolle an der Trennwand
        for (int i = 0; i < 3; i++)
        {
            PrimitiveKit.Part("Helmet", PrimitiveType.Sphere, t, new Vector3(bayLeft + 0.2f, Surface + 1.7f, 6.5f + i * 0.9f), new Vector3(0.36f, 0.26f, 0.36f), helmet);
        }
        Renderer reel = PrimitiveKit.Part("Hose Reel", PrimitiveType.Cylinder, t, new Vector3(bayLeft + 0.3f, Surface + 1f, 10.5f), new Vector3(1f, 0.15f, 1f), hose);
        reel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // Rolltor oben eingerollt, roter Rahmen ums Tor
        PrimitiveKit.Box("Rolled Door", t, new Vector3((bayLeft + bayRight) / 2f, groundTop - 0.2f, facade + 0.45f), new Vector3(bayRight - bayLeft, 0.4f, 0.4f), red);
        PrimitiveKit.Box("Bay Frame Top", t, new Vector3((bayLeft + bayRight) / 2f, groundTop - 0.06f, facade - 0.02f), new Vector3(bayRight - bayLeft + 0.3f, 0.12f, 0.08f), red);
        foreach (float x in new[] { bayLeft, bayRight })
        {
            PrimitiveKit.Box("Bay Frame Side", t, new Vector3(x, (Surface + groundTop) / 2f, facade - 0.02f), new Vector3(0.15f, groundTop - Surface, 0.08f), red);
        }

        // Tür mit dunklem Raum dahinter (Spieler wartet dort vor dem Respawn), Türflügel öffnet nach innen
        float doorCenter = (doorLeft + doorRight) / 2f, doorWidth = doorRight - doorLeft;
        PrimitiveKit.Box("Interior", t, new Vector3(doorCenter, Surface + doorHeight / 2f, facade + 0.3f + 1f), new Vector3(doorWidth, doorHeight, 2f), room);
        Transform hinge = PrimitiveKit.Pivot("Door Hinge", t, new Vector3(doorLeft, Surface, facade + 0.04f));
        PrimitiveKit.Box("Door Leaf", hinge, new Vector3(doorWidth / 2f, doorHeight / 2f, 0f), new Vector3(doorWidth, doorHeight, 0.06f), red);
        PrimitiveKit.Part("Door Handle", PrimitiveType.Sphere, hinge, new Vector3(doorWidth - 0.15f, doorHeight / 2f, -0.05f), Vector3.one * 0.08f, chrome);

        // Schild zwischen den Geschossen, Fenster: oben drei, unten eins neben der Tür (aus bis zum Checkpoint)
        PrimitiveKit.Box("Sign Board", t, new Vector3(0f, groundTop + 0.55f, facade - 0.04f), new Vector3(halfWidth * 2f - 0.8f, 0.75f, 0.06f), red);
        PrimitiveKit.Text("Sign Text", t, new Vector3(0f, groundTop + 0.55f, facade - 0.08f), new Vector2(halfWidth * 2f - 1.2f, 0.6f), "FIRE STATION No. 5", Color.white);
        var windows = new System.Collections.Generic.List<Renderer>();
        foreach (float x in new[] { -2.6f, 0f, 2.6f })
        {
            windows.Add(PrimitiveKit.Quad("Window", t, new Vector3(x, groundTop + 1.9f, facade - 0.01f), new Vector2(1.3f, 1.2f), windowDark));
            PrimitiveKit.Box("Sill", t, new Vector3(x, groundTop + 1.25f, facade - 0.06f), new Vector3(1.5f, 0.1f, 0.12f), stone);
        }
        windows.Add(PrimitiveKit.Quad("Window", t, new Vector3(-1f, Surface + 1.6f, facade - 0.01f), new Vector2(0.9f, 1f), windowDark));

        // Rote Rundumleuchte auf dem Dach über dem Tor: Glas aus/an, darin ein drehender Lichtkegel
        Vector3 beaconAt = new Vector3((bayLeft + bayRight) / 2f, roof + 0.3f, facade + 0.6f);
        PrimitiveKit.Part("Beacon Base", PrimitiveType.Cylinder, t, beaconAt - Vector3.up * 0.25f, new Vector3(0.45f, 0.05f, 0.45f), chrome);
        Renderer beaconGlass = PrimitiveKit.Part("Beacon Glass", PrimitiveType.Cylinder, t, beaconAt, new Vector3(0.36f, 0.2f, 0.36f), beaconOff);
        Transform rotor = PrimitiveKit.Pivot("Beacon Rotor", t, beaconAt);
        var lightObject = new GameObject("Beacon Light");
        lightObject.transform.SetParent(rotor, false);
        var spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.15f, 0.1f);
        spot.intensity = 45f;
        spot.range = 16f;
        spot.spotAngle = 35f;
        spot.shadows = LightShadows.None;
        rotor.gameObject.SetActive(false);

        Transform insidePoint = PrimitiveKit.Pivot("Inside Point", t, new Vector3(doorCenter, Surface + 0.5f, facade + 1.4f));
        Transform outsidePoint = PrimitiveKit.Pivot("Outside Point", t, new Vector3(doorCenter, Surface + 0.5f, 0f));
        var door = root.AddComponent<SpawnDoor>();
        var doorSettings = new SerializedObject(door);
        doorSettings.FindProperty("insidePoint").objectReferenceValue = insidePoint;
        doorSettings.FindProperty("outsidePoint").objectReferenceValue = outsidePoint;
        doorSettings.FindProperty("doorLeaf").objectReferenceValue = hinge;
        doorSettings.ApplyModifiedPropertiesWithoutUndo();

        TMPro.TextMeshPro unlocked = CityPropsSetup.BuildUnlockedText(t, new Vector3(0f, roof + 1.6f, facade - 0.6f));
        ParticleSystem sparkles = CityPropsSetup.BuildSparkles(t);

        // Auslöser auf der Bahn vor der Tür (wie beim Café)
        PrimitiveKit.AddBoxCollider(root, new Vector3(doorCenter, Surface + 1.5f, 0f), new Vector3(1.5f, 3f, 3f), true);
        var checkpoint = new SerializedObject(root.AddComponent<Checkpoint>());
        checkpoint.FindProperty("door").objectReferenceValue = door;
        checkpoint.ApplyModifiedPropertiesWithoutUndo();

        var fx = new SerializedObject(root.AddComponent<SiteOfficeEffects>());
        PrimitiveKit.SetReferences(fx, "windows", windows.ToArray());
        fx.FindProperty("windowsClosed").objectReferenceValue = windowDark;
        fx.FindProperty("windowsOpen").objectReferenceValue = windowLit;
        fx.FindProperty("beaconGlass").objectReferenceValue = beaconGlass;
        fx.FindProperty("beaconOff").objectReferenceValue = beaconOff;
        fx.FindProperty("beaconOn").objectReferenceValue = beaconOn;
        fx.FindProperty("beaconRotor").objectReferenceValue = rotor;
        fx.FindProperty("unlockedText").objectReferenceValue = unlocked;
        fx.FindProperty("sparkles").objectReferenceValue = sparkles;
        fx.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Fire Station.prefab");
    }

    private static Material Lit(string name, Color color, float smoothness = 0.25f) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.LitShader, color, smoothness);

    private static Material Unlit(string name, Color color) =>
        PrimitiveKit.Material($"{MaterialFolder}/{name}.mat", PrimitiveKit.UnlitShader, color);

    private static void CreateMaterials()
    {
        red = Lit("Fire Truck Red", new Color(0.78f, 0.07f, 0.05f), 0.55f);
        white = Lit("Fire Truck White", new Color(0.95f, 0.95f, 0.93f), 0.4f);
        chrome = Lit("Chrome", new Color(0.78f, 0.79f, 0.8f), 0.85f);
        glass = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Window Glass.mat");
        wheel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wheel.mat");
        dark = Lit("Truck Dark", new Color(0.12f, 0.12f, 0.13f));
        helmet = Lit("Firefighter Helmet", new Color(0.82f, 0.08f, 0.06f), 0.7f);
        reflective = Lit("Reflective Stripe", new Color(0.85f, 0.88f, 0.9f), 0.9f);
        tank = Lit("Air Tank", new Color(0.6f, 0.62f, 0.65f), 0.7f);
        hose = Lit("Fire Hose", new Color(0.88f, 0.84f, 0.7f));
        brass = Lit("Brass", new Color(0.8f, 0.62f, 0.25f), 0.8f);
        charred = Lit("Charred", new Color(0.07f, 0.06f, 0.06f), 0.05f);
        embers = Unlit("Ember Glow", new Color(0.95f, 0.3f, 0.05f));
        ladder = Lit("Ladder Silver", new Color(0.7f, 0.72f, 0.74f), 0.6f);
        lightRed = Unlit("Flash Red", new Color(1f, 0.1f, 0.08f));
        lightBlue = Unlit("Flash White", new Color(1f, 0.97f, 0.9f));
        lightOff = Lit("Flash Housing", new Color(0.35f, 0.05f, 0.05f), 0.6f);
        wet = TransparentMaterial("Wet Ground", new Color(0.04f, 0.05f, 0.08f, 0.55f));
        figure = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Customer.mat");

        smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        fireMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Fire Additive.mat");
        if (fireMaterial == null)
        {
            fireMaterial = smokeMaterial;
        }
    }

    // Halbdurchsichtig wie das Gitter des Bauzauns (ConstructionSetup): Im Code muss man alles setzen, was der Inspector beim
    // Umschalten auf "Transparent" selbst erledigt
    private static Material TransparentMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        material = new Material(Shader.Find(PrimitiveKit.LitShader));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.85f);   // nass glänzt
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---------- Brennende Stellen ----------

    // Das Feuer selbst: verkohlte Bretter, Flammen über die ganze Fläche (Box-Form), Rauch, Licht. Der Trigger liegt auf der
    // Wurzel (FirePatch prüft ihn), meist 1.2 m hoch: Wer drüberspringt, ist oben sicher, wer darin landet, nicht
    private static GameObject FireBase(string name, float length, float hazardHeight, out FirePatch patch, out SerializedObject data)
    {
        var root = new GameObject(name);
        Transform t = root.transform;
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface + hazardHeight / 2f, 0f), new Vector3(length, hazardHeight, 1.8f), isTrigger: true);

        // Verkohlter Schutt flach am Boden (Balken, Bretter) mit glühenden Stücken dazwischen
        var random = new System.Random(name.GetHashCode() & 0xffff);
        int planks = Mathf.RoundToInt(length * 0.6f);
        for (int i = 0; i < planks; i++)
        {
            float x = ((float)random.NextDouble() - 0.5f) * (length - 1f);
            float z = ((float)random.NextDouble() - 0.5f) * 1.2f;
            Renderer plank = PrimitiveKit.Box("Charred Beam", t, new Vector3(x, Surface + 0.09f, z),
                new Vector3(0.9f + (float)random.NextDouble() * 0.8f, 0.18f, 0.3f), charred);
            // Ungefähr entlang der Bahn: Quer liegende Balken sähe man von der schräg herabschauenden Kamera wie Pfähle
            plank.transform.localRotation = Quaternion.Euler(0f, random.Next(50) - 25f, 0f);
            // Glut: ein dünner glühender Streifen auf dem Balken
            Renderer glowStrip = PrimitiveKit.Box("Ember", plank.transform, new Vector3(0f, 0.51f, 0f), new Vector3(0.6f, 0.05f, 0.5f), embers);
            glowStrip.transform.localPosition = new Vector3(((float)random.NextDouble() - 0.5f) * 0.4f, 0.5f, 0f);
        }

        // Flammen schrumpfen beim Aufsteigen (growth < 0), so sehen die Teilchen wie Zungen aus
        // Zwei Schichten: große orange Flammen (halbdurchsichtig, damit sie auch vor hellem Hintergrund Körper haben) und ein
        // kleinerer gelber Kern (additiv, leuchtet)
        ParticleSystem fire = Level5Builder.Particles("Fire", t, smokeMaterial, new Color(1f, 0.35f, 0.05f, 0.9f), new Color(1f, 0.55f, 0.1f, 0.85f),
            lifetime: 1f, speed: 1.8f, size: new Vector2(2f, 3.2f), rate: 9f * length, radius: 1f, growth: -0.7f);
        BoxShape(fire, length, 1.3f);
        fire.transform.localPosition = new Vector3(0f, Surface + 0.1f, 0f);
        ParticleSystem core = Level5Builder.Particles("Fire Core", t, fireMaterial, new Color(1f, 0.75f, 0.25f, 0.9f), new Color(1f, 0.9f, 0.5f, 0.9f),
            lifetime: 0.7f, speed: 1.5f, size: new Vector2(1.2f, 2f), rate: 7f * length, radius: 1f, growth: -0.6f);
        BoxShape(core, length, 1f);
        core.transform.localPosition = new Vector3(0f, Surface + 0.1f, 0f);
        ParticleSystem smoke = Level5Builder.Particles("Smoke", t, smokeMaterial, new Color(0.16f, 0.15f, 0.15f, 0.55f), new Color(0.3f, 0.28f, 0.27f, 0.4f),
            lifetime: 3f, speed: 2.5f, size: new Vector2(1.4f, 2.6f), rate: 1.2f * length, radius: 1f, growth: 1.5f);
        BoxShape(smoke, length, 1f);
        smoke.transform.localPosition = new Vector3(0f, Surface + 1.4f, 0f);

        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(t, false);
        glowObject.transform.localPosition = new Vector3(0f, Surface + 1.2f, -0.5f);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.2f);
        glow.range = length * 0.6f + 3f;
        glow.intensity = 4f;
        glow.shadows = LightShadows.None;

        patch = root.AddComponent<FirePatch>();
        data = new SerializedObject(patch);
        data.FindProperty("fire").objectReferenceValue = fire;
        data.FindProperty("fireCore").objectReferenceValue = core;
        data.FindProperty("smoke").objectReferenceValue = smoke;
        data.FindProperty("glow").objectReferenceValue = glow;
        return root;
    }

    // Particles() emittiert aus einem Kreis nach oben (das Objekt ist um -90° um X gedreht): lokal X bleibt X, lokal Y wird
    // zur Tiefe. Eine Box mit Länge × Breite deckt die ganze Stelle ab
    private static void BoxShape(ParticleSystem system, float length, float width)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(length, width, 0.1f);
    }

    private static void BuildFirePatch()
    {
        GameObject root = BuildFirefighterFire("Fire Patch", PatchLength, 1.2f, out SerializedObject data);
        data.FindProperty("sweepHalf").floatValue = PatchLength / 2f - 0.7f;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Fire Patch.prefab");
    }

    // Feuersäule aus einer geborstenen Gasleitung in der Lücke zwischen zwei Stahlträgern (Level 5): schmal, aber so hoch,
    // dass man nicht drüberspringen kann (der Trigger reicht bis über die Sprunghöhe von den Trägern aus). Gleicher Takt mit
    // Feuerwehrmann wie die Stellen: Wer auf dem Träger steht, wartet, bis er sie gelöscht hat
    private static void BuildFireColumn()
    {
        GameObject root = BuildFirefighterFire("Fire Column", ColumnLength, ColumnHazardHeight, out SerializedObject data);
        Transform t = root.transform;
        // Hohe Flammen: schneller und länger nach oben, Rauch erst über den Trägern
        var fire = (ParticleSystem)data.FindProperty("fire").objectReferenceValue;
        var core = (ParticleSystem)data.FindProperty("fireCore").objectReferenceValue;
        var smoke = (ParticleSystem)data.FindProperty("smoke").objectReferenceValue;
        TallFlames(fire, 10f, 1.1f, 2.2f, 110f);
        TallFlames(core, 9f, 0.85f, 1.5f, 80f);
        BoxShape(fire, ColumnLength, 0.9f);
        BoxShape(core, ColumnLength * 0.7f, 0.6f);
        smoke.transform.localPosition = new Vector3(0f, Surface + 8f, 0f);
        var glow = (Light)data.FindProperty("glow").objectReferenceValue;
        glow.transform.localPosition = new Vector3(0f, Surface + 3f, -0.8f);
        glow.range = 9f;
        glow.intensity = 6f;
        // Geborstene Leitung: ein Rohrstumpf mit Flansch aus einem aufgerissenen Loch im Gehweg
        Material pipe = Lit("Gas Pipe", new Color(0.55f, 0.5f, 0.2f), 0.4f);
        PrimitiveKit.Box("Broken Slab", t, new Vector3(0f, Surface + 0.02f, 0f), new Vector3(2.2f, 0.06f, 1.9f), charred);
        PrimitiveKit.Part("Gas Pipe", PrimitiveType.Cylinder, t, new Vector3(0f, Surface + 0.25f, 0.1f), new Vector3(0.5f, 0.3f, 0.5f), pipe)
            .transform.localRotation = Quaternion.Euler(8f, 0f, -10f);
        PrimitiveKit.Part("Flange", PrimitiveType.Cylinder, t, new Vector3(0.05f, Surface + 0.55f, 0.15f), new Vector3(0.75f, 0.05f, 0.75f), pipe)
            .transform.localRotation = Quaternion.Euler(8f, 0f, -10f);
        data.FindProperty("sweepHalf").floatValue = 0.3f;
        // Er zielt in die Flammen, nicht nur auf den Boden (sonst sähe es aus, als lösche er daneben)
        data.FindProperty("sprayHeight").floatValue = 2.5f;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Fire Column.prefab");
    }

    // Dichte, hohe Flammen: viele große Teilchen, die beim Aufsteigen nur wenig schrumpfen (eine schmale Säule aus wenigen
    // Teilchen sah aus wie Funkenflug)
    private static void TallFlames(ParticleSystem system, float speed, float lifetime, float size, float rate)
    {
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(size, size * 1.6f);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        ParticleSystem.SizeOverLifetimeModule growth = system.sizeOverLifetime;
        growth.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.45f)));
        // Gleich sichtbar statt erst nach 15 % der Lebenszeit: Die Säule soll unten am Rohr anfangen
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.color = fade;
    }

    // Brennende Stelle mit Feuerwehrmann, Dampf und nassem Fleck (Stelle und Säule)
    private static GameObject BuildFirefighterFire(string name, float length, float hazardHeight, out SerializedObject data)
    {
        GameObject root = FireBase(name, length, hazardHeight, out _, out data);
        Transform t = root.transform;

        ParticleSystem steam = Level5Builder.Particles("Steam", t, smokeMaterial, new Color(0.92f, 0.93f, 0.95f, 0.5f), new Color(0.8f, 0.82f, 0.85f, 0.35f),
            lifetime: 1.8f, speed: 1.6f, size: new Vector2(0.9f, 1.7f), rate: 0f, radius: 1f, growth: 1.5f);
        BoxShape(steam, length, 1.4f);
        steam.transform.localPosition = new Vector3(0f, Surface + 0.1f, 0f);
        Renderer wetSpot = PrimitiveKit.Quad("Wet", t, new Vector3(0f, Surface + 0.012f, 0f), new Vector2(Mathf.Max(length, 2f) + 0.4f, 1.9f), wet);
        wetSpot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Feuerwehrmann: dieselbe Pillen-Figur wie der Kunde, gelbe Jacke mit Reflexstreifen, roter Helm mit langem Nackenschutz,
        // Atemluftflasche auf dem Rücken. Die Düse hängt an der rechten Hand
        ParkSetup.Figure f = ParkSetup.BuildFigure(t, "Firefighter", new Vector3(0f, Road, FirefighterZ), 180f);
        PrimitiveKit.Part("Helmet", PrimitiveType.Sphere, f.rig, new Vector3(0f, 1.48f, 0f), new Vector3(0.5f, 0.36f, 0.52f), helmet);
        PrimitiveKit.Part("Helmet Brim", PrimitiveType.Cylinder, f.rig, new Vector3(0f, 1.4f, 0.07f), new Vector3(0.62f, 0.015f, 0.72f), helmet);
        PrimitiveKit.Part("Helmet Badge", PrimitiveType.Cube, f.rig, new Vector3(0f, 1.5f, -0.25f), new Vector3(0.14f, 0.14f, 0.03f), brass);
        foreach (float y in new[] { 0.3f, 0.72f })
        {
            PrimitiveKit.Part("Stripe", PrimitiveType.Cylinder, f.rig, new Vector3(0f, y, 0f), new Vector3(0.62f, 0.03f, 0.62f), reflective);
        }
        foreach (Transform arm in new[] { f.leftArm, f.rightArm })
        {
            PrimitiveKit.Part("Stripe", PrimitiveType.Cylinder, arm, new Vector3(0f, -0.42f, 0f), new Vector3(0.155f, 0.025f, 0.155f), reflective);
        }
        PrimitiveKit.Part("Air Tank", PrimitiveType.Capsule, f.rig, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.26f, 0.3f, 0.26f), tank);
        Transform nozzle = PrimitiveKit.Pivot("Nozzle", f.rightArm, new Vector3(-0.05f, -0.58f, 0f));
        Renderer pipe = PrimitiveKit.Part("Nozzle Pipe", PrimitiveType.Cylinder, nozzle, new Vector3(0f, 0f, 0.15f), new Vector3(0.09f, 0.2f, 0.09f), brass);
        pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // Den Schlauch zum Löschfahrzeug legt der Level-Generator (das Fahrzeug steht in einer Lücke daneben)

        ParticleSystem jet = Jet(nozzle);
        ParticleSystem splash = Level5Builder.Particles("Splash", t, smokeMaterial, new Color(0.85f, 0.93f, 1f, 0.85f), new Color(1f, 1f, 1f, 0.6f),
            lifetime: 0.55f, speed: 4f, size: new Vector2(0.12f, 0.32f), rate: 45f, radius: 0.3f, growth: 0.8f);
        ParticleSystem.MainModule splashMain = splash.main;
        splashMain.gravityModifier = 1.2f;
        ParticleSystem.ShapeModule splashShape = splash.shape;
        splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
        splashShape.radius = 0.25f;

        data.FindProperty("steam").objectReferenceValue = steam;
        data.FindProperty("wet").objectReferenceValue = wetSpot;
        data.FindProperty("firefighter").objectReferenceValue = f.root;
        data.FindProperty("armLeft").objectReferenceValue = f.leftArm;
        data.FindProperty("armRight").objectReferenceValue = f.rightArm;
        data.FindProperty("nozzle").objectReferenceValue = nozzle;
        data.FindProperty("jet").objectReferenceValue = jet;
        data.FindProperty("splash").objectReferenceValue = splash;
        PrimitiveKit.SetReferences(data, "coatRenderers", f.body, f.leftArmMesh, f.rightArmMesh);
        PrimitiveKit.SetReferences(data, "skinRenderers", f.head);
        data.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    // Wasserstrahl: schmaler Kegel entlang lokal Z der Düse, die Tropfen fallen mit der Schwerkraft (Welt-Simulation, so
    // biegt sich der Strahl, wenn er schwenkt). FirePatch setzt Neigung, Geschwindigkeit und Lebensdauer jeden Frame
    private static ParticleSystem Jet(Transform nozzle)
    {
        var go = new GameObject("Jet");
        go.transform.SetParent(nozzle, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.8f;
        main.startSpeed = 13f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.88f, 1f, 0.85f), new Color(0.92f, 0.96f, 1f, 0.7f));
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 170f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 2.5f;
        shape.radius = 0.04f;
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 2.6f)));
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0.3f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
        system.Play();
        return system;
    }

    // Brennender Boden direkt in der Szene (z.B. Krater im Schutt): wie das Dauerfeuer, aber beliebig lang, kein Prefab
    public static GameObject BurningGround(string name, float length, float hazardHeight)
    {
        CreateMaterials();
        GameObject root = FireBase(name, length, hazardHeight, out _, out SerializedObject data);
        data.FindProperty("alwaysBurning").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    // Breites Feuer unter der Drehleiter: brennt immer, kein Feuerwehrmann. Der Trigger reicht nur bis 1.3 (0.8 über dem
    // Gehweg): Der Boden des Rettungskorbs liegt bei 1.5, wer darin steht, darf nicht hineinragen
    private static void BuildFireBed()
    {
        GameObject root = FireBase("Fire Bed", BedLength, 0.8f, out _, out SerializedObject data);
        data.FindProperty("alwaysBurning").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Fire Bed.prefab");
    }

    // ---------- Fahrzeuge ----------

    // Fahrgestell und Aufbau eines Feuerwehrautos (Kabine bei -X, schaut nach Norden). Ohne Collider: Der Spieler kommt nie
    // auf die Fahrbahn. Gibt den Aufbau zurück (Ursprung = Fahrbahn unter der Fahrzeugmitte)
    private static Transform Truck(Transform parent, out float roofY)
    {
        Transform body = PrimitiveKit.Pivot("Truck", parent, new Vector3(0f, Road, TruckZ));
        const float half = TruckLength / 2f, w = TruckWidth;
        const float bottom = 0.75f, top = 2.9f;
        roofY = Road + top;
        const float cabLength = 2.2f;
        float cabX = -half + cabLength / 2f;
        // Kabine: etwas höher als der Aufbau, Fenster vorne und an den Seiten
        PrimitiveKit.Box("Cab", body, new Vector3(cabX, (bottom + 3f) / 2f, 0f), new Vector3(cabLength, 3f - bottom, w), red);
        PrimitiveKit.Box("Windshield", body, new Vector3(-half - 0.01f, 2.3f, 0f), new Vector3(0.02f, 0.9f, w - 0.3f), glass);
        foreach (float side in new[] { -1f, 1f })
        {
            PrimitiveKit.Box("Cab Window", body, new Vector3(cabX - 0.3f, 2.3f, side * (w / 2f + 0.01f)), new Vector3(1.2f, 0.8f, 0.02f), glass);
        }
        PrimitiveKit.Box("Bumper", body, new Vector3(-half - 0.1f, 0.75f, 0f), new Vector3(0.25f, 0.35f, w), chrome);
        // Aufbau mit Geräteräumen (helle Rolltore) und weißem Streifen
        float boxLength = TruckLength - cabLength;
        float boxX = -half + cabLength + boxLength / 2f;
        PrimitiveKit.Box("Body", body, new Vector3(boxX, (bottom + top) / 2f, 0f), new Vector3(boxLength, top - bottom, w), red);
        PrimitiveKit.Box("Stripe", body, new Vector3(0f, 1.25f, 0f), new Vector3(TruckLength + 0.02f, 0.18f, w + 0.02f), white);
        for (int i = 0; i < 3; i++)
        {
            float x = boxX - boxLength / 2f + boxLength * (i + 0.5f) / 3f;
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box("Locker Door", body, new Vector3(x, 2.1f, side * (w / 2f + 0.01f)), new Vector3(boxLength / 3f - 0.25f, 1.1f, 0.02f), chrome);
            }
        }
        // Räder: vorne eine Achse, hinten zwei
        foreach (float x in new[] { -half + 1.2f, half - 2.1f, half - 1f })
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Renderer w2 = PrimitiveKit.Part("Wheel", PrimitiveType.Cylinder, body, new Vector3(x, 0.5f, side * (w / 2f - 0.2f)), new Vector3(1f, 0.18f, 1f), wheel);
                w2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        PrimitiveKit.Box("Chassis", body, new Vector3(0f, 0.6f, 0f), new Vector3(TruckLength - 0.4f, 0.3f, w - 0.6f), dark);

        // Blinklichter: Lichtbalken auf der Kabine, dazu je eins an den hinteren Ecken. Rot/Weiß im Wechsel
        PrimitiveKit.Box("Light Bar", body, new Vector3(cabX, 3.05f, 0f), new Vector3(0.35f, 0.1f, w - 0.3f), lightOff);
        var lightsA = new System.Collections.Generic.List<Renderer>();
        var lightsB = new System.Collections.Generic.List<Renderer>();
        foreach (float z in new[] { -0.8f, -0.3f, 0.3f, 0.8f })
        {
            bool a = z < 0f ? z < -0.5f : z > 0.5f;
            Renderer lamp = PrimitiveKit.Box("Flash", body, new Vector3(cabX, 3.15f, z), new Vector3(0.3f, 0.12f, 0.4f), a ? lightRed : lightBlue);
            (a ? lightsA : lightsB).Add(lamp);
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Renderer lamp = PrimitiveKit.Box("Flash", body, new Vector3(half - 0.1f, top - 0.15f, side * (w / 2f - 0.15f)), new Vector3(0.22f, 0.22f, 0.22f), lightRed);
            (side < 0f ? lightsB : lightsA).Add(lamp);
        }
        var flashing = new SerializedObject(parent.gameObject.AddComponent<FlashingLights>());
        PrimitiveKit.SetReferences(flashing, "groupA", lightsA.ToArray());
        PrimitiveKit.SetReferences(flashing, "groupB", lightsB.ToArray());
        flashing.ApplyModifiedPropertiesWithoutUndo();
        return body;
    }

    // Löschfahrzeug: Schlauchbett auf dem Dach, Haspel am Heck
    private static void BuildFireTruck()
    {
        var root = new GameObject("Fire Truck");
        Transform body = Truck(root.transform, out _);
        const float top = 2.9f;
        PrimitiveKit.Box("Hose Bed", body, new Vector3(1f, top + 0.15f, 0f), new Vector3(4.6f, 0.3f, TruckWidth - 0.4f), dark);
        for (int i = 0; i < 5; i++)
        {
            PrimitiveKit.Box("Folded Hose", body, new Vector3(1f, top + 0.33f, -0.8f + i * 0.4f), new Vector3(4.4f, 0.1f, 0.32f), hose);
        }
        Renderer reel = PrimitiveKit.Part("Hose Reel", PrimitiveType.Cylinder, body, new Vector3(TruckLength / 2f + 0.15f, 1.6f, 0f), new Vector3(0.9f, 0.15f, 0.9f), hose);
        reel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        PrimitiveKit.Box("Ladder Rack", body, new Vector3(0.5f, top + 0.5f, -TruckWidth / 2f + 0.15f), new Vector3(5f, 0.12f, 0.25f), ladder);
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Fire Truck.prefab");
    }

    // Drehleiter-Fahrzeug: Drehkranz am Heck, Leiter aus zwei Stücken (hinten fest, vorne Teleskop, LadderBoom streckt es bis
    // zum Korb), Stützen auf der Fahrbahn. Der Korb ist ein eigenes Prefab, die Verbindung setzt der Level-Generator
    private static void BuildLadderTruck()
    {
        var root = new GameObject("Ladder Truck");
        Transform body = Truck(root.transform, out float roofY);
        foreach (float x in new[] { -1.2f, 3.3f })
        {
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box("Outrigger", body, new Vector3(x, 0.5f, side * (TruckWidth / 2f + 0.5f)), new Vector3(0.25f, 0.2f, 1f), dark);
                PrimitiveKit.Box("Outrigger Pad", body, new Vector3(x, 0.05f, side * (TruckWidth / 2f + 0.9f)), new Vector3(0.6f, 0.1f, 0.6f), dark);
            }
        }

        Transform turntable = PrimitiveKit.Pivot("Turntable", root.transform, new Vector3(TurntableX, roofY, TruckZ));
        PrimitiveKit.Part("Ring", PrimitiveType.Cylinder, turntable, new Vector3(0f, 0.15f, 0f), new Vector3(1.8f, 0.15f, 1.8f), dark);
        PrimitiveKit.Box("Console", turntable, new Vector3(0.6f, 0.75f, -0.5f), new Vector3(0.5f, 0.9f, 0.5f), red);
        Transform boom = PrimitiveKit.Pivot("Boom", turntable, new Vector3(0f, 0.7f, 0f));
        // Hinteres Stück: zwei Holme mit Sprossen, Länge 4 entlang Z
        const float baseLength = 4f;
        foreach (float x in new[] { -0.28f, 0.28f })
        {
            PrimitiveKit.Box("Rail", boom, new Vector3(x, 0f, baseLength / 2f - 0.5f), new Vector3(0.1f, 0.3f, baseLength), ladder);
        }
        for (int i = 0; i < 8; i++)
        {
            PrimitiveKit.Box("Rung", boom, new Vector3(0f, 0.05f, -0.3f + i * 0.5f), new Vector3(0.5f, 0.05f, 0.05f), ladder);
        }
        PrimitiveKit.Box("Hinge Block", boom, new Vector3(0f, -0.15f, -0.4f), new Vector3(0.8f, 0.4f, 0.6f), red);
        // Vorderes Stück: ein Quader mit Länge 1, LadderBoom streckt ihn
        Transform fly = PrimitiveKit.Box("Fly Section", boom, new Vector3(0f, 0.05f, 5f), new Vector3(0.42f, 0.22f, 1f), ladder).transform;

        var boomData = new SerializedObject(root.AddComponent<LadderBoom>());
        boomData.FindProperty("turntable").objectReferenceValue = turntable;
        boomData.FindProperty("boom").objectReferenceValue = boom;
        boomData.FindProperty("flySection").objectReferenceValue = fly;
        boomData.FindProperty("flyStart").floatValue = baseLength - 0.8f;
        boomData.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Ladder Truck.prefab");
    }

    // Rettungskorb: MovingPlatform (Oberkante = Wurzel + 0.5 wie die Hebebühne), begehbarer Boden auf Layer Ground, niedrige
    // Geländer vorne und hinten (an den Enden offen, dort steigt man ein), unten zur Kamera die Halterung für die Leiter
    private static void BuildRescueBasket()
    {
        var root = new GameObject("Rescue Basket") { layer = GroundLayer };
        Transform t = root.transform;
        const float length = 2.4f, depth = 1.6f, railHeight = 0.75f;
        PrimitiveKit.Box("Floor", t, new Vector3(0f, Surface - 0.1f, 0f), new Vector3(length, 0.2f, depth), white);
        PrimitiveKit.Box("Trim", t, new Vector3(0f, Surface - 0.22f, 0f), new Vector3(length + 0.04f, 0.08f, depth + 0.04f), red);
        PrimitiveKit.AddBoxCollider(root, new Vector3(0f, Surface - 0.1f, 0f), new Vector3(length, 0.2f, depth));
        foreach (float side in new[] { -1f, 1f })
        {
            float z = side * (depth / 2f - 0.04f);
            PrimitiveKit.Box("Rail", t, new Vector3(0f, Surface + railHeight, z), new Vector3(length, 0.07f, 0.07f), red);
            PrimitiveKit.Box("Kick Plate", t, new Vector3(0f, Surface + 0.12f, z), new Vector3(length, 0.24f, 0.04f), red);
            foreach (float x in new[] { -length / 2f + 0.04f, 0f, length / 2f - 0.04f })
            {
                PrimitiveKit.Box("Post", t, new Vector3(x, Surface + railHeight / 2f, z), new Vector3(0.06f, railHeight, 0.06f), red);
            }
        }
        PrimitiveKit.Box("Mount", t, new Vector3(0f, Surface - 0.25f, -depth / 2f - 0.2f), new Vector3(0.6f, 0.4f, 0.4f), dark);

        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        Transform start = PrimitiveKit.Pivot("moving platform start", t, Vector3.zero);
        Transform end = PrimitiveKit.Pivot("moving platform end", t, Vector3.right * 8f);
        var platform = new SerializedObject(root.AddComponent<MovingPlatform>());
        platform.FindProperty("pointA").objectReferenceValue = start;
        platform.FindProperty("pointB").objectReferenceValue = end;
        platform.FindProperty("speed").floatValue = 3f;
        platform.FindProperty("waitTime").floatValue = 1f;
        platform.ApplyModifiedPropertiesWithoutUndo();
        PrimitiveKit.SavePrefab(root, $"{PrefabFolder}/Rescue Basket.prefab");
    }

    // Am Korb hängt die Leiter hier (lokal): unter dem Boden zur Kamera hin
    public static readonly Vector3 BasketMount = new Vector3(0f, Surface - 0.25f, -1.0f);
}
