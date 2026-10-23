using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Baut alles für die Pizza-Wärme auf:
// - HUD-Prefab (Wärmebalken, "Delivered!"-Panel mit Trinkgeld-Münzen, "Pizza's cold!"-Panel)
// - HUD in die geöffnete Szene setzen
// - Dampf-Partikel am Pizzakarton im Player-Prefab
// Menü: Tools > Jump and Run > Pizza Heat. Alles mehrfach ausführbar, vorhandenes wird ersetzt.
public static class PizzaHeatSetup
{
    private const string HudPrefabPath = "Assets/Prefabs/HUD.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    // Schrift-Material mit schwarzem Rand aus den TMP Essentials: Text bleibt lesbar, auch ohne dunklen Hintergrund
    private const string OutlineFontMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

    // Referenzauflösung: Alle Größen unten sind in Pixeln bei 1920x1080, der CanvasScaler rechnet sie um
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [MenuItem("Tools/Jump and Run/Pizza Heat/Create HUD Prefab")]
    public static void CreateHudPrefab()
    {
        // Ohne TMP Essential Resources gibt es keine Standard-Schrift. TMP öffnet dann selbst das Import-Fenster
        if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro fehlt",
                "Bitte zuerst Window > TextMeshPro > Import TMP Essential Resources ausführen, dann das Menü nochmal aufrufen.",
                "OK");
            return;
        }

        GameObject root = BuildHud();
        PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"PizzaHeatSetup: HUD-Prefab gespeichert unter {HudPrefabPath}.");
    }

    [MenuItem("Tools/Jump and Run/Pizza Heat/Add HUD to Active Scene")]
    public static void AddHudToActiveScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("HUD-Prefab fehlt", "Bitte zuerst \"Create HUD Prefab\" ausführen.", "OK");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (Object.FindAnyObjectByType<PizzaHud>() != null)
        {
            Debug.Log($"PizzaHeatSetup: {scene.name} hat schon ein HUD.");
            return;
        }

        // InstantiatePrefab (statt Object.Instantiate) behält die Verbindung zum Prefab:
        // Änderungen am Prefab kommen automatisch in allen Levels an
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Add HUD");
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;

        Debug.Log($"PizzaHeatSetup: HUD in {scene.name} eingesetzt und Szene gespeichert.");
    }

    [MenuItem("Tools/Jump and Run/Pizza Heat/Add Steam to Player Prefab")]
    public static void AddSteamToPlayerPrefab()
    {
        // Prefab-Inhalt laden wie im Prefab Mode, ändern, zurückspeichern
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var carton = root.GetComponentInChildren<PizzaCarton>();
            if (carton == null)
            {
                Debug.LogError("PizzaHeatSetup: Im Player-Prefab gibt es keinen PizzaCarton.");
                return;
            }

            Transform old = carton.transform.Find("Steam");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            ParticleSystem steam = BuildSteam(carton.transform);

            // Private [SerializeField]-Felder setzt man im Editor über SerializedObject,
            // genau so wie der Inspector es macht (inkl. Undo und Prefab-Speicherung)
            var serializedCarton = new SerializedObject(carton);
            serializedCarton.FindProperty("steam").objectReferenceValue = steam;
            serializedCarton.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("PizzaHeatSetup: Dampf am Pizzakarton im Player-Prefab eingerichtet.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- HUD ----------

    private static GameObject BuildHud()
    {
        var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // Scale With Screen Size: Das HUD sieht auf jeder Auflösung gleich aus (0.5 = Breite und Höhe halb/halb gewichtet)
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        var hud = root.AddComponent<PizzaHud>();
        var serializedHud = new SerializedObject(hud);

        BuildHeatBar(root.transform, serializedHud);
        BuildDeliveredPanel(root.transform, serializedHud);
        BuildColdPanel(root.transform, serializedHud);

        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void BuildHeatBar(Transform parent, SerializedObject hud)
    {
        // Oben mittig verankert, Pivot an der Oberkante: y = -40 heißt 40 px unter dem Bildschirmrand
        RectTransform bar = CreateImage("Heat Bar", parent, new Color(0.08f, 0.06f, 0.06f, 0.85f));
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(640f, 36f);
        bar.anchoredPosition = new Vector2(0f, -40f);

        // Innenbereich mit 4 px Rand, darin wachsen die Füllungen über ihre Anker
        RectTransform area = CreateRect("Fill Area", bar);
        Stretch(area, 4f);

        RectTransform fill = CreateImage("Heat Fill", area, Color.white);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        RectTransform overheat = CreateImage("Overheat Fill", area, new Color(1f, 0.95f, 0.6f, 0.85f));
        overheat.anchorMin = Vector2.zero;
        overheat.anchorMax = new Vector2(0f, 1f);
        overheat.offsetMin = overheat.offsetMax = Vector2.zero;

        // Striche an den Münz-Grenzen, nach den Füllungen erzeugt, damit sie darüber liegen
        CreateTick(area, GameManager.TwoCoinHeat);
        CreateTick(area, GameManager.ThreeCoinHeat);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.35f, 0.6f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.5f),
                new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        hud.FindProperty("heatFill").objectReferenceValue = fill;
        hud.FindProperty("heatFillImage").objectReferenceValue = fill.GetComponent<Image>();
        hud.FindProperty("heatColors").gradientValue = gradient;
        hud.FindProperty("overheatFill").objectReferenceValue = overheat;
        hud.FindProperty("overheatImage").objectReferenceValue = overheat.GetComponent<Image>();
    }

    private static void CreateTick(RectTransform area, float heat)
    {
        RectTransform tick = CreateImage("Tick " + Mathf.RoundToInt(heat * 100f) + "%", area, new Color(1f, 1f, 1f, 0.6f));
        tick.anchorMin = new Vector2(heat, 0f);
        tick.anchorMax = new Vector2(heat, 1f);
        tick.sizeDelta = new Vector2(3f, 0f);
        tick.anchoredPosition = Vector2.zero;
    }

    private static void BuildDeliveredPanel(Transform parent, SerializedObject hud)
    {
        // Kompakt im oberen Drittel, ohne Abdunkeln: Die Bildmitte bleibt frei für den tanzenden Kunden.
        // Oben mittig verankert, direkt unter dem Wärmebalken (der endet bei y = -76)
        RectTransform panel = CreateRect("Delivered Panel", parent);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(1400f, 400f);
        panel.anchoredPosition = new Vector2(0f, -100f);

        var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineFontMaterialPath);
        // Positionen relativ zur Panel-Mitte (200 px unter der Panel-Oberkante)
        CreateText("Title", panel, "Delivered!", 84f, Color.white, new Vector2(0f, 130f), outline);
        CreateText("Tip Label", panel, "Tip", 32f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, 60f), outline);

        // Eingebautes Kreis-Sprite aus Unitys UI-Ressourcen als Münze
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        SerializedProperty coins = hud.FindProperty("coins");
        coins.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            RectTransform coin = CreateImage("Coin " + (i + 1), panel, Color.white);
            coin.GetComponent<Image>().sprite = circle;
            coin.sizeDelta = new Vector2(80f, 80f);
            coin.anchoredPosition = new Vector2((i - 1) * 110f, -15f);
            coins.GetArrayElementAtIndex(i).objectReferenceValue = coin.GetComponent<Image>();
        }

        CreateText("Continue Hint", panel, "Press Jump to continue", 30f, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -110f), outline);

        hud.FindProperty("deliveredPanel").objectReferenceValue = panel.gameObject;
        // Im Prefab ausgeschaltet speichern, sonst verdeckt das Panel im Edit Mode die Game-Ansicht
        panel.gameObject.SetActive(false);
    }

    private static void BuildColdPanel(Transform parent, SerializedObject hud)
    {
        RectTransform panel = CreateImage("Cold Panel", parent, new Color(0.1f, 0.2f, 0.4f, 0.45f));
        Stretch(panel, 0f);

        CreateText("Title", panel, "Pizza's cold!", 120f, new Color(0.75f, 0.9f, 1f), Vector2.zero);

        hud.FindProperty("coldPanel").objectReferenceValue = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // ---------- Dampf ----------

    private static ParticleSystem BuildSteam(Transform carton)
    {
        var go = new GameObject("Steam");
        go.transform.SetParent(carton, false);
        go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        // Der Cone strahlt entlang lokal +Z ab, -90° um X dreht ihn nach oben
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var steam = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = steam.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startColor = new Color(1f, 1f, 1f, 0.45f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 60;
        // World: Einmal ausgestoßene Puffs bleiben in der Welt und ziehen beim Laufen hinter dem Karton her.
        // Bei Local würden sie wie festgeklebt mitwandern.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = steam.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = steam.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.12f;

        ParticleSystem.SizeOverLifetimeModule size = steam.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

        // Weich ein- und ausblenden. Multipliziert sich mit der Startfarbe, die PizzaCarton nach Wärme setzt
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = steam.colorOverLifetime;
        color.enabled = true;
        color.color = fade;

        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (particleRenderer.sharedMaterial == null)
        {
            Debug.LogWarning($"PizzaHeatSetup: Partikel-Material nicht gefunden ({ParticleMaterialPath}), Dampf wird pink.");
        }

        return steam;
    }

    // ---------- Hilfen ----------

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        // Reine Anzeige: keine Maus-Treffer, damit das HUD nie Klicks abfängt
        image.raycastTarget = false;
        return rect;
    }

    private static void CreateText(string name, Transform parent, string text, float fontSize, Color color, Vector2 position, Material fontMaterial = null)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(1400f, fontSize * 1.4f);
        rect.anchoredPosition = position;

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        if (fontMaterial != null)
        {
            label.fontSharedMaterial = fontMaterial;
        }
    }

    // Anker auf die Ecken des Elternteils: Das Rechteck füllt es komplett aus, abzüglich Rand
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
