using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Umgebung der Stadt: Meer im Süden und Westen (Strand, Kaimauer mit Jachthafen), Containerhafen im Nordwesten,
// Wiese und Berge im Norden (hinter dem Kaiju-Kampf), Felder mit Windrädern im Osten.
// Alles reicht weit über die Stadt hinaus: Ab ~900 m verschwindet es im Nebel, so sieht man nirgends eine Kante.
public static partial class CityBuilder
{
    private const string MeshFolder = "Assets/Meshes";
    private const float SeaLevel = -0.5f;
    private const float Far = 1200f;

    private static void BuildSurroundings()
    {
        Transform parent = Group("Surroundings");
        BuildSeaAndLand(parent);
        BuildMarina(parent);
        BuildPort(Group("Port"));
        BuildMountains(parent);
        BuildFields(parent);
    }

    // ---------- Meer, Strand, Land ----------

    private static void BuildSeaAndLand(Transform parent)
    {
        Material sea = Mat("Sea", new Color(0.18f, 0.42f, 0.6f), 0.85f);
        Material sand = Mat("Sand", new Color(0.9f, 0.82f, 0.6f), 0.05f);
        Material wetSand = Mat("Wet Sand", new Color(0.72f, 0.64f, 0.46f), 0.3f);
        Material quay = Mat("Port Concrete", new Color(0.62f, 0.62f, 0.6f));

        // Ein großes Meer unter allem, das Land liegt als Platten darauf.
        // Meer, Land und Strand liegen auch unter der Stadt: CityGround, damit Baugruben eines Levels auch hier ausgeschnitten
        // werden (sonst sähe man in der Grube Wasser, Wiese oder Sand)
        Slab("Sea", parent, W - Far, -Far, E + Far, N + Far, SeaLevel - 6f, SeaLevel, sea).gameObject.AddComponent<CityGround>();
        Slab("Land", parent, W - 22f, -11.5f, E + Far, N + Far, -4f, 0.2f, Grass).gameObject.AddComponent<CityGround>();
        // Strand vor der Margherita Avenue, vorne eine Stufe aus nassem Sand ins Wasser
        Slab("Beach", parent, W - 22f, -30f, E + Far, -12f, -4f, 0.15f, sand).gameObject.AddComponent<CityGround>();
        Slab("Shore", parent, W - 22f, -42f, E + Far, -30f, -4f, SeaLevel + 0.2f, wetSand);
        // Kaimauer an der Westseite (Marinara Promenade)
        Slab("Quay", parent, W - 22f, -42f, W - 12f, N + 12f, -4f, 0.4f, quay);
    }

    // Kleiner Jachthafen an der Westküste: Stege mit Booten
    private static void BuildMarina(Transform parent)
    {
        Material wood = Mat("Door Wood");
        Transform marina = new GameObject("Marina").transform;
        marina.SetParent(parent, false);
        var random = new System.Random(31);
        foreach (float z in new[] { 30f, 70f, 150f, 200f })
        {
            Slab("Jetty", marina, W - 52f, z - 1.5f, W - 22f, z + 1.5f, SeaLevel - 1f, 0.3f, wood);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    Boat(marina, new Vector3(W - 30f - i * 13f, SeaLevel, z + side * 5.5f), 9f + (float)random.NextDouble() * 4f, random);
                }
            }
        }
    }

    private static void Boat(Transform parent, Vector3 at, float length, System.Random random)
    {
        Material hull = random.Next(2) == 0 ? Mat("Boat White", new Color(0.95f, 0.95f, 0.93f), 0.5f) : Mat("Boat Blue", new Color(0.2f, 0.35f, 0.6f), 0.5f);
        Transform boat = new GameObject("Boat").transform;
        boat.SetParent(parent, false);
        boat.position = at;
        boat.rotation = Quaternion.Euler(0f, 90f, 0f);
        PrimitiveKit.Box("Hull", boat, new Vector3(0f, 0.4f, 0f), new Vector3(3.2f, 1.6f, length), hull);
        Renderer bow = PrimitiveKit.Box("Bow", boat, new Vector3(0f, 0.4f, length / 2f), new Vector3(2.26f, 1.6f, 2.26f), hull);
        bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        PrimitiveKit.Box("Cabin", boat, new Vector3(0f, 1.8f, -length * 0.1f), new Vector3(2.4f, 1.4f, length * 0.35f), Mat("Boat White", new Color(0.95f, 0.95f, 0.93f), 0.5f));
    }

    // ---------- Containerhafen (Nordwesten) ----------

    // Kai-Fläche westlich und nördlich der Stadtecke, an der Westkante Containerbrücken über zwei Frachtern,
    // dahinter Containerstapel und zwei Lagerhallen, an der Nordspitze ein Leuchtturm
    private static void BuildPort(Transform port)
    {
        Material concrete = Mat("Port Concrete", new Color(0.62f, 0.62f, 0.6f));
        float quayX = W - 130f;
        Slab("Port Ground", port, quayX, N - 70f, W - 12f, N + 70f, -4f, 0.6f, concrete);
        Slab("Quay Edge", port, quayX - 0.5f, N - 70f, quayX + 1f, N + 70f, -4f, 0.9f, Mat("Curb"));

        var random = new System.Random(77);
        int crane = 0;
        foreach (float z in new[] { N - 45f, N - 5f, N + 35f })
        {
            Crane(port, new Vector3(quayX + 12f, 0.6f, z), crane++ % 2 == 0 ? Mat("Crane Yellow", new Color(0.95f, 0.75f, 0.15f), 0.4f) : Mat("Crane Red", new Color(0.8f, 0.2f, 0.15f), 0.4f));
        }
        Ship(port, new Vector3(quayX - 16f, SeaLevel, N - 30f), 100f, Mat("Ship Hull Red", new Color(0.55f, 0.12f, 0.1f), 0.3f), random);
        Ship(port, new Vector3(quayX - 16f, SeaLevel, N + 62f), 70f, Mat("Ship Hull Blue", new Color(0.15f, 0.25f, 0.45f), 0.3f), random);

        // Containerstapel: Reihen aus 6 Stellplätzen nebeneinander, je 1–4 hoch
        for (float x = quayX + 30f; x < W - 40f; x += 22f)
        {
            for (float z = N - 62f; z < N + 55f; z += 16f)
            {
                for (int column = 0; column < 6; column++)
                {
                    int height = 1 + random.Next(4);
                    for (int level = 0; level < height; level++)
                    {
                        PrimitiveKit.Box("Container", port, new Vector3(x + column * 2.7f, 0.6f + 1.3f + level * 2.6f, z + 6f),
                            new Vector3(2.5f, 2.55f, 12f), ContainerMaterial(random));
                    }
                }
            }
        }

        Material shed = Mat("Shed", new Color(0.72f, 0.74f, 0.76f));
        Material shedRoof = Mat("Shed Roof", new Color(0.25f, 0.4f, 0.55f));
        foreach (float z in new[] { N - 60f, N + 40f })
        {
            Slab("Warehouse", port, W - 38f, z, W - 18f, z + 22f, 0.6f, 9f, shed);
            Slab("Warehouse Roof", port, W - 38.5f, z - 0.5f, W - 17.5f, z + 22.5f, 9f, 9.6f, shedRoof);
        }

        // Leuchtturm an der Nordspitze des Hafens, rot-weiß geringelt
        Transform lighthouse = new GameObject("Lighthouse").transform;
        lighthouse.SetParent(port, false);
        lighthouse.position = new Vector3(quayX + 6f, 0.6f, N + 64f);
        for (int ring = 0; ring < 7; ring++)
        {
            Cylinder($"Ring {ring + 1}", lighthouse, new Vector3(0f, ring * 3f + 1.5f, 0f), 2.6f - ring * 0.12f, 3f, ring % 2 == 0 ? Mat("Crane Red") : Mat("Awning White"));
        }
        Cylinder("Lamp", lighthouse, new Vector3(0f, 22.5f, 0f), 1.5f, 3f, Mat("Lamp Glow"));
        Cylinder("Cap", lighthouse, new Vector3(0f, 24.6f, 0f), 2f, 1.2f, Mat("Crane Red"));
    }

    private static readonly Color[] ContainerColors =
    {
        new Color(0.75f, 0.2f, 0.15f), new Color(0.15f, 0.35f, 0.65f), new Color(0.2f, 0.55f, 0.3f),
        new Color(0.9f, 0.6f, 0.15f), new Color(0.55f, 0.55f, 0.58f), new Color(0.6f, 0.15f, 0.45f)
    };

    private static Material ContainerMaterial(System.Random random)
    {
        int index = random.Next(ContainerColors.Length);
        return Mat($"Container {index + 1}", ContainerColors[index], 0.3f);
    }

    // Containerbrücke: vier Beine, Querträger oben, lang über das Wasser ragender Ausleger mit Kabine
    private static void Crane(Transform parent, Vector3 at, Material paint)
    {
        Transform crane = new GameObject("Crane").transform;
        crane.SetParent(parent, false);
        crane.position = at;
        const float legHeight = 38f;
        foreach (float x in new[] { -8f, 8f })
        {
            foreach (float z in new[] { -7f, 7f })
            {
                PrimitiveKit.Box("Leg", crane, new Vector3(x, legHeight / 2f, z), new Vector3(1.6f, legHeight, 1.6f), paint);
            }
            PrimitiveKit.Box("Side Beam", crane, new Vector3(x, legHeight, 0f), new Vector3(1.8f, 2f, 16f), paint);
            PrimitiveKit.Box("Low Beam", crane, new Vector3(x, 12f, 0f), new Vector3(1.2f, 1.2f, 14f), paint);
        }
        // Ausleger entlang X: weit nach Westen über die Schiffe, ein Stück nach Osten über den Hof
        PrimitiveKit.Box("Boom", crane, new Vector3(-22f, legHeight + 2f, 0f), new Vector3(76f, 2.4f, 3f), paint);
        PrimitiveKit.Box("Cabin", crane, new Vector3(-30f, legHeight - 0.5f, 0f), new Vector3(3f, 2.5f, 3f), Mat("Window Glass"));
        PrimitiveKit.Box("Tower", crane, new Vector3(0f, legHeight + 9f, 0f), new Vector3(3f, 16f, 3f), paint);
    }

    // Frachter längs am Kai (Bug nach Norden), Brücke hinten, Container an Deck
    private static void Ship(Transform parent, Vector3 at, float length, Material hullMaterial, System.Random random)
    {
        Transform ship = new GameObject("Cargo Ship").transform;
        ship.SetParent(parent, false);
        ship.position = at;
        const float width = 22f;
        PrimitiveKit.Box("Hull", ship, new Vector3(0f, 1f, 0f), new Vector3(width, 10f, length), hullMaterial);
        Renderer bow = PrimitiveKit.Box("Bow", ship, new Vector3(0f, 1f, length / 2f), new Vector3(width * 0.707f, 10f, width * 0.707f), hullMaterial);
        bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        PrimitiveKit.Box("Deck", ship, new Vector3(0f, 6.05f, 0f), new Vector3(width - 1f, 0.1f, length - 1f), Mat("Port Concrete"));
        // Brücke mit Fenstern: der Fassaden-Shader macht aus dem Würfel ein Haus mit Fensterreihen
        PrimitiveKit.Box("Bridge", ship, new Vector3(0f, 6f + 8f, -length / 2f + 9f), new Vector3(width - 2f, 16f, 10f), facade);
        PrimitiveKit.Box("Funnel", ship, new Vector3(0f, 6f + 19f, -length / 2f + 4f), new Vector3(4f, 7f, 4f), hullMaterial);
        for (float z = -length / 2f + 18f; z < length / 2f - 12f; z += 12.5f)
        {
            for (int column = -3; column <= 3; column++)
            {
                int height = 1 + random.Next(3);
                for (int level = 0; level < height; level++)
                {
                    PrimitiveKit.Box("Container", ship, new Vector3(column * 2.7f, 6f + 1.3f + level * 2.6f, z), new Vector3(2.5f, 2.55f, 12f), ContainerMaterial(random));
                }
            }
        }
    }

    // ---------- Berge (Norden) ----------

    // Low-Poly-Berge aus selbst erzeugten Meshes, mit Schneekappe. Stehen hinter dem Kaiju-Kampf und rahmen den Norden ein
    private static void BuildMountains(Transform parent)
    {
        Material rock = Mat("Mountain Rock", new Color(0.52f, 0.5f, 0.47f), 0.05f);
        Material snow = Mat("Snow", new Color(0.96f, 0.97f, 1f), 0.2f);
        Mesh[] shapes = { MountainMesh(1), MountainMesh(2), MountainMesh(3) };
        Transform mountains = new GameObject("Mountains").transform;
        mountains.SetParent(parent, false);

        var random = new System.Random(9);
        // Zwei Reihen, die hintere größer. Startet östlich des Hafens
        for (int row = 0; row < 2; row++)
        {
            float x = W - 60f + (float)random.NextDouble() * 40f;
            while (x < E + 420f)
            {
                float height = (row == 0 ? 70f : 130f) + (float)random.NextDouble() * (row == 0 ? 60f : 90f);
                float radius = height * (1.2f + (float)random.NextDouble() * 0.4f);
                // Fuß der vorderen Reihe frühestens 100 m hinter der Calzone Road, damit Hafen und Stadtrand frei bleiben
                float z = N + (row == 0 ? 100f + radius : 220f + radius * 0.9f) + (float)random.NextDouble() * 60f;
                Mesh shape = shapes[random.Next(shapes.Length)];
                Transform mountain = MeshObject("Mountain", mountains, shape, new Vector3(x, 0f, z), new Vector3(radius, height, radius), rock).transform;
                mountain.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

                // Schneekappe: dasselbe Mesh kleiner, Spitze auf Spitze. Minimal breiter, damit die Flächen nicht flackern (Z-Fighting)
                const float cap = 0.32f;
                Vector3 apex = mountain.TransformPoint(MountainApex(shape));
                Transform snowCap = MeshObject("Snow Cap", mountains, shape, Vector3.zero, new Vector3(radius * cap * 1.04f, height * cap, radius * cap * 1.04f), snow).transform;
                snowCap.rotation = mountain.rotation;
                snowCap.position = apex - (snowCap.TransformPoint(MountainApex(shape)) - snowCap.position) + Vector3.up * 0.3f;

                x += radius * (row == 0 ? 1.3f : 1.1f);
            }
        }
    }

    private static Vector3 MountainApex(Mesh mesh)
    {
        Vector3 apex = Vector3.zero;
        foreach (Vector3 vertex in mesh.vertices)
        {
            if (vertex.y > apex.y)
            {
                apex = vertex;
            }
        }
        return apex;
    }

    // Kegel mit Zacken: unterer Ring (Radius ~1), mittlerer Ring (~0.6 auf halber Höhe), Spitze bei y = 1.
    // Jedes Dreieck bekommt eigene Ecken, so berechnet RecalculateNormals harte Kanten (Low-Poly-Look statt glatt).
    // Die Dreiecke sind im Uhrzeigersinn (von außen gesehen): Das ist in Unity die Vorderseite
    private static Mesh MountainMesh(int seed)
    {
        PrimitiveKit.EnsureFolder("Assets", "Meshes");
        string path = $"{MeshFolder}/Mountain {seed}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            return existing;
        }

        var random = new System.Random(seed * 101);
        const int sides = 11;
        var lower = new Vector3[sides];
        var middle = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = (i + (float)random.NextDouble() * 0.4f) * Mathf.PI * 2f / sides;
            float r = 0.85f + (float)random.NextDouble() * 0.3f;
            lower[i] = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            float r2 = 0.5f + (float)random.NextDouble() * 0.2f;
            middle[i] = new Vector3(Mathf.Sin(angle) * r2, 0.42f + (float)random.NextDouble() * 0.12f, Mathf.Cos(angle) * r2);
        }
        var apex = new Vector3((float)random.NextDouble() * 0.16f - 0.08f, 1f, (float)random.NextDouble() * 0.16f - 0.08f);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add(vertices.Count); vertices.Add(a);
            triangles.Add(vertices.Count); vertices.Add(b);
            triangles.Add(vertices.Count); vertices.Add(c);
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Triangle(lower[i], middle[j], middle[i]);
            Triangle(lower[i], lower[j], middle[j]);
            Triangle(middle[i], middle[j], apex);
        }

        var mesh = new Mesh { name = $"Mountain {seed}" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
    {
        var meshObject = new GameObject(name);
        meshObject.transform.SetParent(parent, false);
        meshObject.transform.position = position;
        meshObject.transform.localScale = scale;
        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return meshObject;
    }

    // ---------- Felder und Windräder (Osten) ----------

    private static void BuildFields(Transform parent)
    {
        Material[] crops =
        {
            Mat("Field Wheat", new Color(0.86f, 0.76f, 0.4f), 0.05f),
            Mat("Field Green", new Color(0.5f, 0.68f, 0.3f), 0.05f),
            Mat("Field Soil", new Color(0.55f, 0.42f, 0.28f), 0.05f)
        };
        Transform fields = new GameObject("Fields").transform;
        fields.SetParent(parent, false);
        var random = new System.Random(55);

        // Streifen quer zur Stadt, dazwischen schmale Feldwege (die Wiese darunter)
        for (float x = E + 16f; x < E + 500f; )
        {
            float width = 60f + (float)random.NextDouble() * 50f;
            for (float z = -10f; z < N + 80f; )
            {
                float depth = 30f + (float)random.NextDouble() * 30f;
                Slab("Field", fields, x, z, x + width - 3f, z + depth - 3f, 0f, 0.26f, crops[random.Next(crops.Length)]);
                z += depth;
            }
            x += width;
        }

        // Bauernhof
        Material barn = Mat("Barn Red", new Color(0.62f, 0.16f, 0.12f));
        Slab("Barn", fields, E + 120f, 130f, E + 134f, 150f, 0.2f, 8f, barn);
        Renderer roof = PrimitiveKit.Box("Barn Roof", fields, new Vector3(E + 127f, 8f, 140f), new Vector3(10.6f, 10.6f, 20.4f), Mat("Shed Roof"));
        roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // Windräder, die Rotoren schauen zur Stadt
        Vector2[] spots = { new Vector2(80f, 40f), new Vector2(160f, 110f), new Vector2(110f, 210f), new Vector2(230f, 270f), new Vector2(260f, 60f), new Vector2(320f, 180f) };
        foreach (Vector2 spot in spots)
        {
            WindTurbine(fields, new Vector3(E + spot.x, 0.2f, spot.y), 20f + (float)random.NextDouble() * 15f);
        }
    }

    private static void WindTurbine(Transform parent, Vector3 at, float speed)
    {
        Material white = Mat("Turbine White", new Color(0.93f, 0.94f, 0.95f), 0.4f);
        Transform turbine = new GameObject("Wind Turbine").transform;
        turbine.SetParent(parent, false);
        turbine.position = at;
        Vector3 toCity = new Vector3(-90f, 0f, 160f) - at;
        turbine.rotation = Quaternion.LookRotation(new Vector3(toCity.x, 0f, toCity.z));

        const float height = 60f;
        Cylinder("Tower", turbine, new Vector3(0f, height / 2f, 0f), 1.6f, height, white);
        PrimitiveKit.Box("Nacelle", turbine, new Vector3(0f, height + 1.5f, -1f), new Vector3(3f, 3f, 8f), white);

        // Rotor dreht sich um seine lokale Z-Achse (zeigt zur Stadt)
        Transform rotor = PrimitiveKit.Pivot("Rotor", turbine, new Vector3(0f, height + 1.5f, 3.2f));
        PrimitiveKit.Part("Hub", PrimitiveType.Sphere, rotor, Vector3.zero, Vector3.one * 2.2f, white);
        for (int blade = 0; blade < 3; blade++)
        {
            Transform arm = PrimitiveKit.Pivot($"Blade {blade + 1}", rotor, Vector3.zero);
            arm.localRotation = Quaternion.Euler(0f, 0f, blade * 120f);
            PrimitiveKit.Box("Blade", arm, new Vector3(0f, 14f, 0f), new Vector3(1.6f, 26f, 0.4f), white);
        }
        Spinner spinner = rotor.gameObject.AddComponent<Spinner>();
        var serialized = new SerializedObject(spinner);
        serialized.FindProperty("degreesPerSecond").floatValue = speed;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
