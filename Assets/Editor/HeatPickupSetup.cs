using UnityEditor;
using UnityEngine;

// Baut das Prefab "Heat Pickup": eine glühende Chili-Schote, die schwebt, sich dreht und kleine Flammen sprüht.
// Einsammeln = ein Stück Wärme zurück (HeatPickup). Nur Primitive: Die Schote besteht aus Kapseln, die einem Bogen
// folgen und zur Spitze dünner werden.
// Menü: Tools > Jump and Run > Build Heat Pickup Prefab. Mehrfach ausführbar, das Prefab wird ersetzt (GUID bleibt).
public static class HeatPickupSetup
{
    private const string PrefabPath = "Assets/Prefabs/Heat Pickup.prefab";
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // Mittellinie der Schote (lokal im Modell, X = lang, Y = hoch) und Radius an jedem Punkt: vom Stiel zur Spitze
    private static readonly Vector2[] Spine =
    {
        new Vector2(-0.32f, 0.06f), new Vector2(-0.08f, 0.03f), new Vector2(0.14f, -0.06f),
        new Vector2(0.3f, -0.19f), new Vector2(0.39f, -0.33f),
    };
    private static readonly float[] Radius = { 0.17f, 0.16f, 0.13f, 0.09f, 0.04f };

    [MenuItem("Tools/Jump and Run/Build Heat Pickup Prefab")]
    public static void Build()
    {
        Material red = PrimitiveKit.Material("Assets/Materials/Chili Red.mat", PrimitiveKit.LitShader, new Color(0.85f, 0.08f, 0.04f), 0.85f);
        // Leichtes Eigenleuchten, damit die Chili auch im Schatten der Häuser auffällt
        red.EnableKeyword("_EMISSION");
        red.SetColor("_EmissionColor", new Color(0.45f, 0.04f, 0f));
        EditorUtility.SetDirty(red);
        Material green = PrimitiveKit.Material("Assets/Materials/Chili Green.mat", PrimitiveKit.LitShader, new Color(0.2f, 0.55f, 0.15f), 0.4f);

        var root = new GameObject("Heat Pickup");
        Transform model = PrimitiveKit.Pivot("Model", root.transform, Vector3.zero);
        // Schräg gestellt, wie eine hingeworfene Schote
        model.localRotation = Quaternion.Euler(0f, 0f, 20f);

        for (int i = 0; i < Spine.Length - 1; i++)
        {
            Segment($"Pod {i + 1}", model, Spine[i], Spine[i + 1], (Radius[i] + Radius[i + 1]) / 2f, red);
        }
        // Kelch: flache grüne Scheibe am dicken Ende, darüber der gebogene Stiel
        PrimitiveKit.Part("Calyx", PrimitiveType.Sphere, model, new Vector3(-0.47f, 0.07f, 0f), new Vector3(0.1f, 0.3f, 0.3f), green);
        Segment("Stem", model, new Vector2(-0.5f, 0.08f), new Vector2(-0.6f, 0.26f), 0.035f, green);

        ParticleSystem flames = BuildFlames(model);
        ParticleSystem burst = BuildBurst(root.transform);

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.7f;

        var pickup = root.AddComponent<HeatPickup>();
        var serialized = new SerializedObject(pickup);
        serialized.FindProperty("model").objectReferenceValue = model;
        serialized.FindProperty("flames").objectReferenceValue = flames;
        serialized.FindProperty("burst").objectReferenceValue = burst;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrimitiveKit.SavePrefab(root, PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"HeatPickupSetup: Prefab gespeichert unter {PrefabPath}.");
    }

    // Kapsel von a nach b. Die Unity-Kapsel ist bei Skalierung 1 zwei Einheiten hoch (entlang Y) mit Radius 0.5:
    // Breite = 2r, Y-Skalierung = halbe Gesamtlänge inklusive der runden Enden
    private static void Segment(string name, Transform parent, Vector2 a, Vector2 b, float radius, Material material)
    {
        Vector2 direction = b - a;
        float length = direction.magnitude;
        Renderer part = PrimitiveKit.Part(name, PrimitiveType.Capsule, parent, (a + b) / 2f,
            new Vector3(radius * 2f, (length + radius * 2f) / 2f, radius * 2f), material);
        // Kapsel-Achse (lokal Y) auf die Richtung a → b drehen
        part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
    }

    // Ständiges Flämmchen: kleine orange Partikel steigen von der Schote auf
    private static ParticleSystem BuildFlames(Transform parent)
    {
        ParticleSystem flames = CreateParticles("Flames", parent, new Vector3(0f, 0.1f, 0f));
        ParticleSystem.MainModule main = flames.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.05f), new Color(1f, 0.85f, 0.2f));
        main.gravityModifier = -0.2f;
        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 22f;
        ParticleSystem.ShapeModule shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        // Werden beim Aufsteigen kleiner, wie Flammenzungen
        ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        return flames;
    }

    // Glut-Ladung beim Einsammeln (nur Emit, keine Dauer-Emission)
    private static ParticleSystem BuildBurst(Transform parent)
    {
        ParticleSystem burst = CreateParticles("Burst", parent, Vector3.zero);
        ParticleSystem.MainModule main = burst.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.9f, 0.3f));
        main.gravityModifier = 0.4f;
        ParticleSystem.EmissionModule emission = burst.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        return burst;
    }

    private static ParticleSystem CreateParticles(string name, Transform parent, Vector3 localPosition)
    {
        var particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = localPosition;
        var particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.maxParticles = 60;
        // Welt-Raum: Partikel bleiben, wo sie entstanden sind, und drehen sich nicht mit der Schote mit
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        particleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        return particles;
    }
}
