using UnityEditor;
using UnityEngine;

// Bausteine für die Schutt-Kletterei in Level 5: Schuttstufen, schräg liegende Deckenplatten, herausragende Stahlträger und
// brennende Krater. Alle Stücke liegen auf geraden Bahn-Stücken im Lane-Rahmen (lokal X = +u, +Z = hinten, -Z = Kamera).
// Höhen sind Welt-Höhen (Oberkante), "ground" ist die Höhe des Bodens dort (Gehweg 0.5, Park-Laufboden 0.54).
//
// Physik und Optik sind getrennt: Der begehbare Teil ist ein sauberer Quader-Collider (Layer Ground), damit man nicht an
// Zacken hängen bleibt. Die Optik drumherum sind schräg liegende Brocken ohne Collider, oben nur knapp über der Kante.
public static class RubbleKit
{
    private const int GroundLayer = 3;
    // Tiefe des begehbaren Teils quer zur Bahn: Die Bahn hält den Spieler auf lokal z 0, 2.2 m reichen
    private const float WalkDepth = 2.2f;

    private static Material rubble, facade, concrete, rebar, steel, scorch;

    private static void LoadMaterials()
    {
        rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        facade = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Facade.mat");
        concrete = PrimitiveKit.Material("Assets/Materials/City/Broken Concrete.mat", PrimitiveKit.LitShader, new Color(0.62f, 0.6f, 0.57f), 0.1f);
        rebar = PrimitiveKit.Material("Assets/Materials/City/Rebar.mat", PrimitiveKit.LitShader, new Color(0.35f, 0.2f, 0.12f), 0.3f);
        steel = PrimitiveKit.Material("Assets/Materials/City/Steel Beam Primer.mat", PrimitiveKit.LitShader, new Color(0.62f, 0.22f, 0.14f), 0.3f);
        scorch = PrimitiveKit.Material("Assets/Materials/City/Scorch.mat", PrimitiveKit.LitShader, new Color(0.08f, 0.07f, 0.07f), 0.05f);
    }

    private static Vector3 Point(float u) => LevelBuildKit.Lane.PointAt(u);
    private static Quaternion Frame(float u) => LevelBuildKit.Frame(u);

    // Neues Objekt in der Mitte zwischen uA und uB, in der Höhe y, im Lane-Rahmen gedreht
    private static Transform Piece(Transform parent, string name, float uA, float uB, float y, int layer)
    {
        float u = (uA + uB) / 2f;
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(Point(u) + Vector3.up * y, Frame(u));
        return go.transform;
    }

    // Schuttstufe: massiv vom Boden bis zur Oberkante. Kern (Bruchbeton oder Fassade = Stück eines Hochhauses), an den
    // Kanten schräge Brocken, hinten ein höherer Schutthaufen und ein paar Bewehrungseisen
    public static Transform Step(Transform parent, float uA, float uB, float ground, float top, int seed, bool towerChunk = false)
    {
        LoadMaterials();
        float length = Mathf.Abs(uB - uA), height = top - ground;
        Transform t = Piece(parent, "Rubble Step", uA, uB, (ground + top) / 2f, GroundLayer);
        PrimitiveKit.AddBoxCollider(t.gameObject, Vector3.zero, new Vector3(length, height, WalkDepth));
        PrimitiveKit.Box("Core", t, new Vector3(0f, -0.03f, 0.1f), new Vector3(length - 0.05f, height - 0.05f, WalkDepth + 0.2f), towerChunk ? facade : concrete);
        var random = new System.Random(seed);
        Chunks(t, random, length, height / 2f, 1f, towerChunk);
        // Hinten (zum Haus hin, außerhalb der Bahn) darf es höher sein: der Schutt türmt sich an der Fassade
        for (int i = 0; i < Mathf.CeilToInt(length / 2f); i++)
        {
            float x = Lerp(random, -length / 2f, length / 2f);
            float size = Lerp(random, 0.8f, 1.6f);
            Renderer chunk = PrimitiveKit.Box("Back Chunk", t, new Vector3(x, height / 2f - Lerp(random, 0f, 0.6f), Lerp(random, 1.6f, 2.4f)),
                new Vector3(size * 1.4f, size, size), random.NextDouble() < 0.4 ? facade : rubble);
            chunk.transform.localRotation = RandomTilt(random, 30f);
        }
        Rebar(t, random, length, height / 2f, 2);
        return t;
    }

    // Schräg liegende Deckenplatte von (uA, yA) nach (uB, yB), Oberkante geht durch beide Punkte. Bis etwa 22° läuft man
    // hinauf, ohne abzurutschen (der Spieler bremst seine waagerechte Geschwindigkeit sofort ab, wenn man loslässt)
    public static Transform Slab(Transform parent, float uA, float yA, float uB, float yB, int seed)
    {
        LoadMaterials();
        const float thickness = 0.45f;
        // Immer von kleinem zu großem u rechnen: Sonst dreht der Winkel die Platte auf den Kopf
        if (uB < uA)
        {
            (uA, uB) = (uB, uA);
            (yA, yB) = (yB, yA);
        }
        float du = uB - uA, dy = yB - yA;
        float length = Mathf.Sqrt(du * du + dy * dy);
        float angle = Mathf.Atan2(dy, du) * Mathf.Rad2Deg;
        float u = (uA + uB) / 2f;
        var go = new GameObject("Fallen Slab") { layer = GroundLayer };
        go.transform.SetParent(parent, false);
        Quaternion rotation = Frame(u) * Quaternion.Euler(0f, 0f, angle);
        // Die Mitte der Oberkante liegt auf der Linie, der Quader hängt eine halbe Dicke darunter
        go.transform.SetPositionAndRotation(Point(u) + Vector3.up * ((yA + yB) / 2f) - rotation * Vector3.up * (thickness / 2f), rotation);
        Transform t = go.transform;
        PrimitiveKit.AddBoxCollider(go, Vector3.zero, new Vector3(length, thickness, WalkDepth + 0.4f));
        PrimitiveKit.Box("Slab", t, Vector3.zero, new Vector3(length, thickness, WalkDepth + 0.8f), concrete);
        // Abgerissene Kanten: Eisen ragen aus beiden Enden, am hinteren Rand ein Stück Fassade (Fenster), Brocken darunter
        var random = new System.Random(seed);
        foreach (float end in new[] { -1f, 1f })
        {
            for (int i = 0; i < 4; i++)
            {
                Renderer bar = PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, t, new Vector3(end * (length / 2f + 0.3f), 0f, Lerp(random, -1.2f, 1.2f)),
                    new Vector3(0.05f, 0.35f, 0.05f), rebar);
                bar.transform.localRotation = Quaternion.Euler(Lerp(random, -20f, 20f), 0f, 90f + Lerp(random, -25f, 25f));
            }
        }
        Renderer wall = PrimitiveKit.Box("Wall Piece", t, new Vector3(Lerp(random, -length / 4f, length / 4f), 0.7f, 1.55f), new Vector3(length * 0.5f, 1.2f, 0.3f), facade);
        wall.transform.localRotation = Quaternion.Euler(Lerp(random, -12f, 12f), 0f, Lerp(random, -6f, 6f));
        // Unter dem hohen Ende ein Schuttkeil (Optik), damit die Platte nicht schwebt
        float lowY = Mathf.Min(yA, yB), highY = Mathf.Max(yA, yB);
        float highU = yA > yB ? uA : uB;
        if (highY - lowY > 0.8f)
        {
            Transform wedge = Piece(parent, "Slab Support", highU - Mathf.Sign(highU - u) * 1.2f, highU, 0f, 0);
            var support = new System.Random(seed + 1);
            for (int i = 0; i < 4; i++)
            {
                float size = Lerp(support, 0.6f, 1.2f);
                Renderer chunk = PrimitiveKit.Box("Chunk", wedge, new Vector3(Lerp(support, -0.6f, 0.6f), Lerp(support, 0.3f, highY - 0.8f), Lerp(support, -0.5f, 0.8f)),
                    new Vector3(size, size, size), rubble);
                chunk.transform.localRotation = RandomTilt(support, 40f);
            }
        }
        return t;
    }

    // Umgestürztes Hochhaus-Stück: ein dicker Fassaden-Quader schräg entlang der Bahn, das Dach-Ende im Schutt vergraben,
    // das abgebrochene Ende auf einem Stumpf. Man läuft auf seiner Seitenwand hinauf (Fenster unter den Füßen). Nach hinten
    // ragt es weit in die Ruinen, nach vorne nur bis knapp vor die Bahn (die Kamera steht davor)
    public static Transform TowerPiece(Transform parent, float uA, float yA, float uB, float yB, int seed)
    {
        return BuildTower(parent, uA, yA, uB, yB, seed, false, facade);
    }

    // Wie TowerPiece, aber es steht anfangs aufrecht und kippt erst um, wenn der Spieler kommt (TopplingRuin): Drehpunkt am
    // unteren Ende (dort bricht es vom Fundament ab, das Dach liegt am oberen Ende auf dem Schutt). Fassade in einer einzigen
    // Farbe (RuinFacade), weil es sich bewegt. Gibt den Drehpunkt zurück (liegende Lage), dazu den Winkel um dessen lokale Z,
    // um den es stehend gedreht ist, und den begehbaren Collider
    public static Transform ToppledTower(Transform parent, float uLow, float yLow, float uHigh, float yHigh, int seed,
        out float standingTilt, out Collider solid)
    {
        Transform tower = BuildTower(parent, uLow, yLow, uHigh, yHigh, seed, true, RuinFacade());
        Transform pivot = new GameObject("Topple Pivot").transform;
        pivot.SetParent(parent, false);
        // Drehachse im Rahmen des Turms (Mitte), nicht am Fuß: Der kann schon in einer Kurve liegen (Level 5: 1.3 m), dann
        // stünde der Turm schief zur Seite gekippt
        pivot.SetPositionAndRotation(Point(uLow) + Vector3.up * yLow, Frame((uLow + uHigh) / 2f));
        tower.SetParent(pivot, true);
        // Liegend zeigt die Achse vom Drehpunkt zum hohen Ende schräg nach oben (Steigung a), stehend senkrecht: um 90 - a
        // zurückdrehen, und zwar zum Spieler hin (der kommt vom niedrigen Ende)
        float slope = Mathf.Atan2(yHigh - yLow, Mathf.Abs(uHigh - uLow)) * Mathf.Rad2Deg;
        standingTilt = (90f - slope) * Mathf.Sign(uHigh - uLow);
        solid = tower.GetComponent<Collider>();
        // Bruchkante am Fundament: ein paar Brocken um den Fuß (bleiben liegen)
        var random = new System.Random(seed + 7);
        for (int i = 0; i < 6; i++)
        {
            float size = Lerp(random, 0.6f, 1.3f);
            Renderer chunk = PrimitiveKit.Box("Base Chunk", pivot, new Vector3(Lerp(random, -0.5f, 2.5f) * -Mathf.Sign(uHigh - uLow), size * 0.3f - 0.2f, Lerp(random, 1.4f, 5.5f)),
                new Vector3(size * 1.4f, size, size), random.NextDouble() < 0.5 ? rubble : concrete);
            chunk.transform.localRotation = RandomTilt(random, 30f);
        }
        return pivot;
    }

    // Fassaden-Material in einer einzigen, rußigen Farbe, ohne helle Fenster: Der Fassaden-Shader wählt Farbe und Fenster aus
    // der Position des Würfels, bei einem bewegten Würfel flackerte sonst beides
    public static Material RuinFacade()
    {
        const string path = "Assets/Materials/City/Facade Ruin.mat";
        Material source = CityBuilder.FacadeMaterialAsset;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        material.CopyPropertiesFromMaterial(source);
        var soot = new Color(0.5f, 0.45f, 0.4f);
        for (int i = 0; i < 6; i++)
        {
            material.SetColor($"_Color{i}", soot);
        }
        material.SetFloat("_LitChance", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform BuildTower(Transform parent, float uA, float yA, float uB, float yB, int seed, bool roofAtHigh, Material wall)
    {
        LoadMaterials();
        const float thickness = 4.5f, front = -1.1f, back = 6f;
        if (uB < uA)
        {
            (uA, uB) = (uB, uA);
            (yA, yB) = (yB, yA);
        }
        float du = uB - uA, dy = yB - yA;
        float length = Mathf.Sqrt(du * du + dy * dy) + 3f;   // ragt an beiden Enden etwas in den Schutt
        float angle = Mathf.Atan2(dy, du) * Mathf.Rad2Deg;
        float u = (uA + uB) / 2f;
        var go = new GameObject("Fallen Tower") { layer = GroundLayer };
        go.transform.SetParent(parent, false);
        Quaternion rotation = Frame(u) * Quaternion.Euler(0f, 0f, angle);
        go.transform.SetPositionAndRotation(Point(u) + Vector3.up * ((yA + yB) / 2f) - rotation * Vector3.up * (thickness / 2f)
            + Frame(u) * Vector3.forward * ((front + back) / 2f), rotation);
        Transform t = go.transform;
        PrimitiveKit.AddBoxCollider(go, Vector3.zero, new Vector3(length, thickness, back - front));
        // Das Fassaden-Shader zeichnet Fenster nur auf Seitenflächen, oben und unten ist Dach. Die Lauffläche ist eine
        // Seitenwand des Turms: den Quader um 90° kippen (seine lokale Z zeigt nach oben), Tiefe und Dicke tauschen
        Renderer tower = PrimitiveKit.Box("Tower", t, Vector3.zero, new Vector3(length, back - front, thickness), wall);
        tower.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var random = new System.Random(seed);
        // Dachkante am unteren Ende, Antenne abgeknickt; am oberen Ende Bruchkante mit Eisen und Brocken
        float low = yA < yB ? -1f : 1f;
        if (roofAtHigh)
        {
            low = -low;
        }
        // Dachkante: ragt am Ende heraus, aber nicht über die Lauffläche (sonst stünde dort eine Schwelle)
        PrimitiveKit.Box("Roof Edge", t, new Vector3(low * (length / 2f + 0.15f), -0.1f, 0f), new Vector3(0.3f, thickness - 0.2f, back - front + 0.3f), concrete);
        for (int i = 0; i < 10; i++)
        {
            Renderer bar = PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, t, new Vector3(-low * (length / 2f + 0.3f), Lerp(random, -thickness / 2f, thickness / 2f), Lerp(random, -2.5f, 3f)),
                new Vector3(0.06f, 0.5f, 0.06f), rebar);
            bar.transform.localRotation = Quaternion.Euler(Lerp(random, -25f, 25f), 0f, 90f + Lerp(random, -30f, 30f));
        }
        for (int i = 0; i < 8; i++)
        {
            float size = Lerp(random, 0.8f, 1.8f);
            Renderer shard = PrimitiveKit.Box("Shard", t, new Vector3(-low * (length / 2f + Lerp(random, -0.4f, 0.5f)), Lerp(random, -thickness / 2f, thickness / 2f - 0.6f), Lerp(random, -0.8f, 3.5f)),
                new Vector3(size * 0.6f, size, size), random.NextDouble() < 0.5 ? concrete : wall);
            shard.transform.localRotation = RandomTilt(random, 35f);
        }
        return t;
    }

    // Stahlträger (Doppel-T), der aus den Trümmern ragt: Oberkante top, gestützt von einer abgeknickten Stütze hinter der Bahn
    // bei supportU (bis zum Boden)
    public static Transform Beam(Transform parent, float uA, float uB, float top, float supportU, float ground, int seed)
    {
        LoadMaterials();
        float length = Mathf.Abs(uB - uA);
        const float height = 0.45f;
        Transform t = Piece(parent, "Steel Beam", uA, uB, top - height / 2f, GroundLayer);
        PrimitiveKit.AddBoxCollider(t.gameObject, Vector3.zero, new Vector3(length, height, 1.6f));
        var random = new System.Random(seed);
        Quaternion tilt = Quaternion.Euler(0f, Lerp(random, -4f, 4f), 0f);
        Transform shape = PrimitiveKit.Pivot("I-Beam", t, Vector3.zero);
        shape.localRotation = tilt;
        PrimitiveKit.Box("Top Flange", shape, new Vector3(0f, height / 2f - 0.04f, 0f), new Vector3(length, 0.08f, 0.6f), steel);
        PrimitiveKit.Box("Web", shape, Vector3.zero, new Vector3(length, height - 0.1f, 0.08f), steel);
        PrimitiveKit.Box("Bottom Flange", shape, new Vector3(0f, -height / 2f + 0.04f, 0f), new Vector3(length, 0.08f, 0.6f), steel);
        // Ein Stück Beton klebt noch dran
        PrimitiveKit.Box("Concrete Lump", shape, new Vector3(Lerp(random, -length / 3f, length / 3f), -0.1f, 0.35f), new Vector3(0.9f, 0.5f, 0.5f), concrete)
            .transform.localRotation = RandomTilt(random, 20f);

        // Stütze hinter der Bahn: senkrechter Träger vom Boden bis unter den Querträger, oben ein schräges Verbindungsstück
        float columnHeight = top - height - ground;
        if (columnHeight > 0.5f)
        {
            Transform column = Piece(parent, "Beam Column", supportU, supportU, ground + columnHeight / 2f, 0);
            column.position += Frame(supportU) * Vector3.forward * 1.3f;
            PrimitiveKit.Box("Column", column, Vector3.zero, new Vector3(0.4f, columnHeight, 0.4f), steel);
            PrimitiveKit.Box("Brace", column, new Vector3(0f, columnHeight / 2f - 0.1f, -0.65f), new Vector3(0.12f, 0.2f, 1.3f), steel);
        }
        return t;
    }

    // Bruchplatte (CrumblingSlab): waagerecht, Oberkante top, von uA bis uB. Hält nach dem Betreten nur kurz, dann fällt sie.
    // Risse oben, Eisen an den Enden, damit man sieht, dass sie nicht hält
    public static Transform CrumblingSlab(Transform parent, float uA, float uB, float top, int seed)
    {
        LoadMaterials();
        const float thickness = 0.5f;
        float length = Mathf.Abs(uB - uA);
        Transform t = Piece(parent, "Crumbling Slab", uA, uB, top - thickness / 2f, GroundLayer);
        PrimitiveKit.AddBoxCollider(t.gameObject, Vector3.zero, new Vector3(length, thickness, WalkDepth + 0.4f));
        var body = t.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.None;
        Transform visual = PrimitiveKit.Pivot("Visual", t, Vector3.zero);
        PrimitiveKit.Box("Slab", visual, Vector3.zero, new Vector3(length, thickness, WalkDepth + 0.8f), concrete);
        var random = new System.Random(seed);
        // Risse: dunkle, dünne Streifen knapp über der Oberkante, schräg über die Platte
        for (int i = 0; i < 4; i++)
        {
            Renderer crack = PrimitiveKit.Box("Crack", visual, new Vector3(Lerp(random, -length / 2.5f, length / 2.5f), thickness / 2f + 0.005f, Lerp(random, -0.8f, 0.8f)),
                new Vector3(Lerp(random, 0.8f, 1.6f), 0.01f, 0.07f), scorch);
            crack.transform.localRotation = Quaternion.Euler(0f, Lerp(random, -70f, 70f), 0f);
        }
        foreach (float end in new[] { -1f, 1f })
        {
            for (int i = 0; i < 3; i++)
            {
                Renderer bar = PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, visual, new Vector3(end * (length / 2f + 0.25f), 0f, Lerp(random, -1.1f, 1.1f)),
                    new Vector3(0.05f, 0.3f, 0.05f), rebar);
                bar.transform.localRotation = Quaternion.Euler(Lerp(random, -20f, 20f), 0f, 90f + Lerp(random, -35f, 35f));
            }
        }
        // Darunter hängen ein paar Brocken (fallen mit)
        for (int i = 0; i < 3; i++)
        {
            float size = Lerp(random, 0.3f, 0.6f);
            Renderer chunk = PrimitiveKit.Box("Hanging Chunk", visual, new Vector3(Lerp(random, -length / 3f, length / 3f), -thickness / 2f - size * 0.3f, Lerp(random, -0.8f, 0.8f)),
                new Vector3(size * 1.4f, size, size), rubble);
            chunk.transform.localRotation = RandomTilt(random, 25f);
        }
        ParticleSystem dust = Dust(t, "Dust", new Vector3(0f, -thickness / 2f, 0f), length);
        var data = new SerializedObject(t.gameObject.AddComponent<global::CrumblingSlab>());
        data.FindProperty("visual").objectReferenceValue = visual;
        data.FindProperty("dust").objectReferenceValue = dust;
        data.ApplyModifiedPropertiesWithoutUndo();
        return t;
    }

    // Brocken aus dem Kampf (FallingDebris), landet bei u auf dem Boden (ground). Liegt danach als Stufe da (size.y hoch)
    public static Transform FallingChunk(Transform parent, float u, float ground, float triggerU, int seed)
    {
        LoadMaterials();
        var size = new Vector3(2.2f, 1.2f, 2.2f);
        Transform t = Piece(parent, "Falling Debris", u, u, ground, 0);
        // Brocken: ein Stück Fassade (eine Farbe, er bewegt sich) mit Beton und Eisen daran
        var chunk = new GameObject("Chunk") { layer = GroundLayer };
        chunk.transform.SetParent(t, false);
        chunk.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
        chunk.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);
        BoxCollider solid = PrimitiveKit.AddBoxCollider(chunk, Vector3.zero, size);
        PrimitiveKit.Box("Facade Piece", chunk.transform, new Vector3(0f, 0f, 0.15f), new Vector3(size.x, size.y, size.z - 0.3f), RuinFacade());
        var random = new System.Random(seed);
        for (int i = 0; i < 4; i++)
        {
            float s = Lerp(random, 0.4f, 0.8f);
            Renderer piece = PrimitiveKit.Box("Concrete", chunk.transform, new Vector3(Lerp(random, -0.8f, 0.8f), Lerp(random, -0.3f, 0.45f), Lerp(random, -1f, 1f)),
                new Vector3(s * 1.3f, s, s), concrete);
            piece.transform.localRotation = RandomTilt(random, 25f);
        }
        for (int i = 0; i < 3; i++)
        {
            Renderer bar = PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, chunk.transform, new Vector3(Lerp(random, -0.8f, 0.8f), size.y / 2f + 0.3f, Lerp(random, -0.8f, 0.8f)),
                new Vector3(0.05f, 0.45f, 0.05f), rebar);
            bar.transform.localRotation = Quaternion.Euler(Lerp(random, -35f, 35f), 0f, Lerp(random, -35f, 35f));
        }
        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        ParticleSystem trail = Level5Builder.Particles("Trail", chunk.transform, smokeMaterial, new Color(0.2f, 0.19f, 0.18f, 0.6f), new Color(0.35f, 0.33f, 0.3f, 0.45f),
            lifetime: 1.4f, speed: 0.5f, size: new Vector2(1.2f, 2.2f), rate: 35f, radius: 0.6f, growth: 1.2f);
        trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule trailMain = trail.main;
        trailMain.playOnAwake = false;
        // Schatten wie bei der Möwe: dunkle Scheibe knapp über dem Boden, wächst im Fall
        Material shadowMaterial = PrimitiveKit.Material("Assets/Materials/Gull Shadow.mat", PrimitiveKit.UnlitShader, new Color(0.12f, 0.12f, 0.14f));
        Transform shadow = PrimitiveKit.Pivot("Shadow", t, new Vector3(0f, 0.02f, 0f));
        shadow.localScale = new Vector3(size.x * 1.15f, 0.01f, size.z * 1.15f);
        PrimitiveKit.Part("Disc", PrimitiveType.Cylinder, shadow, Vector3.zero, Vector3.one, shadowMaterial);
        ParticleSystem impact = Dust(t, "Impact Dust", Vector3.zero, size.x * 1.5f);
        chunk.SetActive(false);
        shadow.gameObject.SetActive(false);
        var data = new SerializedObject(t.gameObject.AddComponent<FallingDebris>());
        data.FindProperty("triggerU").floatValue = triggerU;
        data.FindProperty("halfSize").vector2Value = new Vector2(size.x / 2f, size.z / 2f);
        data.FindProperty("chunk").objectReferenceValue = chunk.transform;
        data.FindProperty("solid").objectReferenceValue = solid;
        data.FindProperty("shadow").objectReferenceValue = shadow;
        data.FindProperty("impactDust").objectReferenceValue = impact;
        data.FindProperty("trail").objectReferenceValue = trail;
        data.ApplyModifiedPropertiesWithoutUndo();
        return t;
    }

    // Staubwolke, die nur per Emit ausgelöst wird (keine laufende Emission): graubraun, quillt auf und sinkt etwas
    public static ParticleSystem Dust(Transform parent, string name, Vector3 localPosition, float width)
    {
        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        ParticleSystem dust = Level5Builder.Particles(name, parent, smokeMaterial, new Color(0.55f, 0.5f, 0.44f, 0.75f), new Color(0.7f, 0.66f, 0.6f, 0.6f),
            lifetime: 1.6f, speed: 3f, size: new Vector2(0.8f, 1.8f), rate: 0f, radius: width / 2f, growth: 1.6f);
        dust.transform.localPosition = localPosition;
        ParticleSystem.MainModule main = dust.main;
        main.gravityModifier = 0.15f;
        main.maxParticles = 200;
        return dust;
    }

    // Krater mit brennendem Grund: fällt man hinein, ist es vorbei (Respawn). Dunkle Brandfläche, Brocken am Rand, Feuer
    public static Transform Crater(Transform parent, float uA, float uB, float ground, int seed)
    {
        LoadMaterials();
        float length = Mathf.Abs(uB - uA);
        Transform t = Piece(parent, "Burning Crater", uA, uB, ground, 0);
        PrimitiveKit.Box("Scorch", t, new Vector3(0f, 0.01f, 0.3f), new Vector3(length + 0.6f, 0.03f, 3.2f), scorch);
        GameObject fire = FireSetup.BurningGround("Crater Fire", length - 0.2f, 1f);
        fire.transform.SetParent(t, false);
        // Die Feuer-Prefabs liegen auf "Bodenplatte mit Oberkante +0.5"
        fire.transform.localPosition = Vector3.down * 0.5f;
        var random = new System.Random(seed);
        for (int i = 0; i < 6; i++)
        {
            float size = Lerp(random, 0.5f, 1f);
            Renderer chunk = PrimitiveKit.Box("Rim Chunk", t, new Vector3(Lerp(random, -length / 2f, length / 2f), size * 0.25f, Lerp(random, 1.3f, 2.2f) * (i % 2 == 0 ? 1f : -0.7f)),
                new Vector3(size * 1.3f, size * 0.6f, size), rubble);
            chunk.transform.localRotation = RandomTilt(random, 25f);
        }
        return t;
    }

    // Schräge Brocken an Vorder- und Hinterkante der Oberseite (y = halbe Höhe, relativ zur Mitte). Sie ragen höchstens
    // ein paar Zentimeter über die Kante, sonst sähe der Spieler aus, als stünde er im Schutt
    private static void Chunks(Transform t, System.Random random, float length, float topY, float density, bool towerChunk)
    {
        int count = Mathf.CeilToInt(length * 1.6f * density);
        for (int i = 0; i < count; i++)
        {
            float size = Lerp(random, 0.35f, 0.9f);
            bool front = i % 2 == 0;
            float z = front ? Lerp(random, -1.25f, -0.9f) : Lerp(random, 0.9f, 1.4f);
            var position = new Vector3(Lerp(random, -length / 2f + 0.2f, length / 2f - 0.2f), topY - size * 0.45f + 0.05f, z);
            Renderer chunk = PrimitiveKit.Box("Chunk", t, position, new Vector3(size * 1.5f, size, size),
                towerChunk && random.NextDouble() < 0.5 ? facade : random.NextDouble() < 0.5 ? rubble : concrete);
            chunk.transform.localRotation = RandomTilt(random, 25f);
        }
        // Auch an den Stirnseiten ein paar Brocken, damit die Stufe keine glatte Kante hat
        foreach (float end in new[] { -1f, 1f })
        {
            float size = Lerp(random, 0.5f, 0.9f);
            Renderer chunk = PrimitiveKit.Box("End Chunk", t, new Vector3(end * (length / 2f + 0.1f), topY - size * 0.8f, Lerp(random, -0.6f, 0.6f)),
                new Vector3(size, size, size * 1.4f), rubble);
            chunk.transform.localRotation = RandomTilt(random, 30f);
        }
    }

    private static void Rebar(Transform t, System.Random random, float length, float topY, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Renderer bar = PrimitiveKit.Part("Rebar", PrimitiveType.Cylinder, t, new Vector3(Lerp(random, -length / 2f, length / 2f), topY + 0.5f, Lerp(random, 1.2f, 1.8f)),
                new Vector3(0.05f, 0.6f, 0.05f), rebar);
            bar.transform.localRotation = Quaternion.Euler(Lerp(random, -30f, 30f), 0f, Lerp(random, -30f, 30f));
        }
    }

    private static float Lerp(System.Random random, float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());

    private static Quaternion RandomTilt(System.Random random, float max) =>
        Quaternion.Euler(Lerp(random, -max, max), Lerp(random, 0f, 360f), Lerp(random, -max, max));
}
