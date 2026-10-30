using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Baut alles für die Pizza-Wärme auf:
// - HUD-Prefab (Wärmebalken, Lieferschein im Intro, "Delivered!"-Panel mit Trinkgeld-Münzen, "Pizza's cold!"-Panel,
//   Endbildschirm nach dem letzten Level)
// - HUD in die geöffnete Szene setzen
// - Dampf-Partikel am Pizzakarton im Player-Prefab
// Menü: Tools > Jump and Run > Pizza Heat. Alles mehrfach ausführbar, vorhandenes wird ersetzt.
public static class PizzaHeatSetup
{
    private const string HudPrefabPath = "Assets/Prefabs/HUD.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string IrisMaterialPath = "Assets/Materials/Iris.mat";
    // Fredoka (Google Fonts, SIL Open Font License, Lizenz liegt daneben): rund und kräftig, passt zum Comic-Look
    private const string FontFolder = "Assets/Fonts";

    // Lieferschein-Look für alle Texte zu Levelbeginn und -ende: cremefarbenes Papier, farbiges Band, dunkle Schrift
    private static readonly Color PaperColor = new Color(1f, 0.96f, 0.86f);
    private static readonly Color PizzeriaRed = new Color(0.8f, 0.13f, 0.1f);
    private static readonly Color ColdBlue = new Color(0.22f, 0.48f, 0.82f);
    private static readonly Color InkColor = new Color(0.27f, 0.13f, 0.08f);
    private static readonly Color FadedInkColor = new Color(0.55f, 0.4f, 0.3f);

    private static TMP_FontAsset boldFont;
    private static TMP_FontAsset semiBoldFont;

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

        boldFont = LoadOrCreateFont("Fredoka-Bold");
        semiBoldFont = LoadOrCreateFont("Fredoka-SemiBold");

        var hud = root.AddComponent<PizzaHud>();
        var serializedHud = new SerializedObject(hud);

        // Wärmebalken in einer eigenen Gruppe: Das Intro blendet sie zusammen aus und ein
        RectTransform gameplay = CreateRect("Gameplay", root.transform);
        Stretch(gameplay, 0f);
        var gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
        gameplayGroup.interactable = false;
        gameplayGroup.blocksRaycasts = false;
        serializedHud.FindProperty("gameplayGroup").objectReferenceValue = gameplayGroup;

        BuildHeatBar(gameplay, serializedHud);
        BuildDeliveredPanel(root.transform, serializedHud);
        BuildColdPanel(root.transform, serializedHud);
        BuildEndPanel(root.transform, serializedHud);
        BuildIntroTitle(root.transform, serializedHud);
        // Zuletzt erzeugt = zuletzt gezeichnet: Die Iris liegt über allem anderen im HUD
        BuildIris(root.transform);

        serializedHud.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void BuildHeatBar(Transform parent, SerializedObject hud)
    {
        // Dunkle Platte hinter Pizza-Symbol, Balken und Münzen: lesbar vor jeder Fassade (Kontrast).
        // Als Erstes erzeugt, liegt sie hinter allem anderen. Reicht von x -400 bis 335, y -24 bis -122
        Plate("Heat Plate", parent, new Vector2(0.5f, 1f), new Vector2(-32.5f, -73f), new Vector2(735f, 98f));

        // Oben mittig verankert, Pivot an der Oberkante: y = -40 heißt 40 px unter dem Bildschirmrand
        // Heller Rahmen hinter dem Balken (2 px größer), damit er sich von der dunklen Platte abhebt.
        // Kein Outline-Effekt: der zeichnet Kopien des Bildes, die durch einen halbtransparenten Balken durchscheinen
        RectTransform frame = CreateImage("Heat Bar Frame", parent, new Color(1f, 1f, 1f, 0.6f));
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.sizeDelta = new Vector2(644f, 40f);
        frame.anchoredPosition = new Vector2(0f, -38f);

        RectTransform bar = CreateImage("Heat Bar", parent, new Color(0.08f, 0.06f, 0.06f, 1f));
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
        hud.FindProperty("heatBar").objectReferenceValue = bar;

        BuildPizzaIcon(parent);
        BuildTipGroups(parent, hud);
        BuildFlame(parent, hud);
        BuildHeatHint(parent, hud);
    }

    // Draufsicht einer Pizza links neben dem Balken (Rand, Käse, drei Salami): "dieser Balken ist die Pizza"
    private static void BuildPizzaIcon(Transform parent)
    {
        RectTransform pizza = PizzaIcon(parent, 56f);
        pizza.anchorMin = pizza.anchorMax = new Vector2(0.5f, 1f);
        // Balken: 640 breit, mittig, y -40 bis -76. Das Symbol sitzt links daneben, auf halber Höhe
        pizza.anchoredPosition = new Vector2(-362f, -58f);
    }

    // Pizza aus Kreisen, Maße für 56 px Durchmesser entworfen und auf size umgerechnet
    private static RectTransform PizzaIcon(Transform parent, float size)
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        float k = size / 56f;
        RectTransform pizza = CreateRect("Pizza Icon", parent);
        pizza.sizeDelta = new Vector2(size, size);
        Circle("Crust", pizza, Vector2.zero, 56f * k, new Color(0.8f, 0.5f, 0.22f), circle);
        Circle("Cheese", pizza, Vector2.zero, 44f * k, new Color(1f, 0.8f, 0.3f), circle);
        Color salami = new Color(0.78f, 0.16f, 0.1f);
        Circle("Salami 1", pizza, new Vector2(-9f, 8f) * k, 12f * k, salami, circle);
        Circle("Salami 2", pizza, new Vector2(10f, 4f) * k, 12f * k, salami, circle);
        Circle("Salami 3", pizza, new Vector2(0f, -11f) * k, 12f * k, salami, circle);
        return pizza;
    }

    // Unter jedem Drittel des Balkens die Münzen, die es in diesem Bereich als Trinkgeld gibt (1, 2, 3)
    private static void BuildTipGroups(Transform parent, SerializedObject hud)
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        SerializedProperty groups = hud.FindProperty("tipGroups");
        groups.arraySize = 3;
        // Innere Balkenbreite 632 (4 px Rand), Drittel-Mitten bei -210.7, 0, +210.7
        const float third = 632f / 3f;
        for (int i = 0; i < 3; i++)
        {
            RectTransform group = CreateRect($"Tip {i + 1}", parent);
            group.anchorMin = group.anchorMax = new Vector2(0.5f, 1f);
            group.sizeDelta = new Vector2(96f, 30f);
            group.anchoredPosition = new Vector2((i - 1) * third, -98f);
            int count = i + 1;
            for (int c = 0; c < count; c++)
            {
                // Münzen nebeneinander, die Gruppe mittig unter dem Drittel
                Circle($"Coin {c + 1}", group, new Vector2((c - (count - 1) / 2f) * 31f, 0f), 28f, new Color(1f, 0.8f, 0.15f), circle);
            }
            groups.GetArrayElementAtIndex(i).objectReferenceValue = group;
        }
    }

    // Vorlage der Chili-Flamme (orange mit gelbem Kern). PizzaHud kopiert sie pro eingesammelter Chili
    private static void BuildFlame(Transform parent, SerializedObject hud)
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        RectTransform flame = CreateRect("Flame Template", parent);
        flame.sizeDelta = new Vector2(40f, 40f);
        Circle("Glow", flame, Vector2.zero, 40f, new Color(1f, 0.4f, 0.05f, 0.9f), circle);
        Circle("Core", flame, Vector2.zero, 20f, new Color(1f, 0.9f, 0.4f), circle);
        flame.gameObject.SetActive(false);
        hud.FindProperty("flameTemplate").objectReferenceValue = flame;
    }

    // "Keep the pizza hot!" als rotes Band auf Papier unter der Wärme-Platte, nur kurz zu Levelbeginn (PizzaHud blendet ein und aus)
    private static void BuildHeatHint(Transform parent, SerializedObject hud)
    {
        RectTransform hint = CreateRect("Heat Hint", parent);
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 1f);
        hint.pivot = new Vector2(0.5f, 1f);
        // Wärme-Platte endet bei y -122
        hint.anchoredPosition = new Vector2(0f, -136f);
        var group = hint.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform paper = Ticket("Ticket", hint, new Vector2(0.5f, 1f), 0f);
        RectTransform band = Band("Band", paper, PizzeriaRed, 30f, 8f);
        TicketText("Text", band, "Keep the pizza hot!", 44f, Color.white, boldFont);
        hud.FindProperty("heatHint").objectReferenceValue = group;
    }

    // Abgerundete dunkle Platte (eingebautes UI-Sprite, 9-Slice: die Ecken bleiben rund, egal wie groß).
    // Fast deckendes Schwarz mit etwas Wärme, darauf haben Weiß, Gelb und Gold hohen Kontrast. 92%, weil das Projekt im
    // Linear-Farbraum mischt: Dort wirkt eine halbtransparente dunkle Fläche vor hellen Fassaden viel heller (82% sah aus wie 50%)
    private static RectTransform Plate(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform plate = CreateImage(name, parent, new Color(0.07f, 0.05f, 0.05f, 0.92f));
        var image = plate.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        // Kleiner Multiplikator = größere Ecken
        image.pixelsPerUnitMultiplier = 0.35f;
        plate.anchorMin = plate.anchorMax = anchor;
        plate.sizeDelta = size;
        plate.anchoredPosition = position;
        return plate;
    }

    private static void Circle(string name, Transform parent, Vector2 position, float size, Color color, Sprite sprite)
    {
        RectTransform rect = CreateImage(name, parent, color);
        rect.GetComponent<Image>().sprite = sprite;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;
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
        // Kompakt oben, ohne Abdunkeln: Die Bildmitte bleibt frei für den tanzenden Kunden.
        // Oben mittig verankert, direkt unter der Wärme-Platte (endet bei y -122)
        RectTransform panel = CreateRect("Delivered Panel", parent);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -140f);

        RectTransform paper = Ticket("Ticket", panel, new Vector2(0.5f, 1f), -1.5f);
        RectTransform band = Band("Band", paper, PizzeriaRed, 40f, 10f);
        AddLayoutSize(PizzaIcon(band, 60f), 60f, 60f);
        TicketText("Title", band, "Delivered!", 76f, Color.white, boldFont);

        RectTransform body = Body("Body", paper, 60f, 16f, 22f, 10f);
        TicketText("Tip Label", body, "TIP", 30f, FadedInkColor, semiBoldFont, 12f);

        // Münzen nebeneinander. Die Layout Group setzt nur Position und Größe, die Skalierung fürs Aufploppen bleibt frei
        RectTransform row = CreateRect("Coins", body);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 28f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(0, 0, 4, 8);

        // Eingebautes Kreis-Sprite aus Unitys UI-Ressourcen als Münze. Der dunkelgoldene Rand (Outline-Effekt: das Bild
        // nochmal in 4 Richtungen versetzt dahinter) trennt das Gold vom hellen Papier
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        SerializedProperty coins = hud.FindProperty("coins");
        coins.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            RectTransform coin = CreateImage("Coin " + (i + 1), row, Color.white);
            coin.GetComponent<Image>().sprite = circle;
            AddLayoutSize(coin, 80f, 80f);
            var rim = coin.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0.6f, 0.38f, 0.05f);
            rim.effectDistance = new Vector2(3f, -3f);
            coins.GetArrayElementAtIndex(i).objectReferenceValue = coin.GetComponent<Image>();
        }
        hud.FindProperty("coinMissingColor").colorValue = new Color(0.45f, 0.33f, 0.22f, 0.25f);

        TicketText("Continue Hint", body, "Press Jump to continue", 30f, FadedInkColor, semiBoldFont);

        hud.FindProperty("deliveredPanel").objectReferenceValue = panel.gameObject;
        // Im Prefab ausgeschaltet speichern, sonst verdeckt das Panel im Edit Mode die Game-Ansicht
        panel.gameObject.SetActive(false);
    }

    private static void BuildColdPanel(Transform parent, SerializedObject hud)
    {
        // Ganzer Bildschirm kühl-blau eingefärbt, mittig ein blaues Band auf Papier
        RectTransform panel = CreateImage("Cold Panel", parent, new Color(0.05f, 0.12f, 0.28f, 0.6f));
        Stretch(panel, 0f);
        RectTransform paper = Ticket("Ticket", panel, new Vector2(0.5f, 0.5f), 2f);
        RectTransform band = Band("Band", paper, ColdBlue, 60f, 18f);
        TicketText("Title", band, "Pizza's cold!", 110f, Color.white, boldFont);

        hud.FindProperty("coldPanel").objectReferenceValue = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // Endbildschirm nach dem letzten Level: großer Lieferschein mittig, eine Zeile pro Level (Nummer, Adresse, 3 Münzen),
    // darunter die Summe. Die Zeilen legt PizzaHud zur Laufzeit aus der ausgeschalteten Vorlage an, denn wie viele Levels
    // es gibt, steht erst in den Build Settings fest. Leicht abgedunkelt, damit der Zettel vor dem Tanz lesbar bleibt
    private static void BuildEndPanel(Transform parent, SerializedObject hud)
    {
        RectTransform panel = CreateImage("End Panel", parent, new Color(0f, 0f, 0f, 0.35f));
        Stretch(panel, 0f);
        RectTransform paper = Ticket("Ticket", panel, new Vector2(0.5f, 0.5f), -1f);
        RectTransform band = Band("Band", paper, PizzeriaRed, 50f, 12f);
        AddLayoutSize(PizzaIcon(band, 60f), 60f, 60f);
        TicketText("Title", band, "All pizzas delivered!", 68f, Color.white, boldFont);

        RectTransform body = Body("Body", paper, 60f, 22f, 24f, 10f);
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Zeilen in einer eigenen Spalte: Dort hängt die Vorlage, die Kopien landen daneben (gleicher Elternteil)
        RectTransform rows = CreateRect("Rows", body);
        var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 10f;
        rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        // Nummer | Adresse (nimmt den Rest der Breite, linksbündig) | Münzen: So stehen alle Münzreihen bündig untereinander
        RectTransform row = CreateRect("Row Template", rows);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 18f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        var rowGroup = row.gameObject.AddComponent<CanvasGroup>();
        rowGroup.interactable = false;
        rowGroup.blocksRaycasts = false;

        TextMeshProUGUI number = TicketText("Number", row, "1.", 40f, FadedInkColor, boldFont);
        number.alignment = TextAlignmentOptions.Right;
        number.gameObject.AddComponent<LayoutElement>().preferredWidth = 48f;
        TextMeshProUGUI address = TicketText("Address", row, "No. 42, Pepperoni Parkway", 40f, InkColor, semiBoldFont);
        address.alignment = TextAlignmentOptions.Left;
        address.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        RectTransform rowCoins = CreateRect("Coins", row);
        var coinsLayout = rowCoins.gameObject.AddComponent<HorizontalLayoutGroup>();
        coinsLayout.spacing = 12f;
        coinsLayout.childAlignment = TextAnchor.MiddleRight;
        coinsLayout.childControlWidth = coinsLayout.childControlHeight = true;
        coinsLayout.childForceExpandWidth = coinsLayout.childForceExpandHeight = false;
        coinsLayout.padding = new RectOffset(30, 0, 4, 4);
        for (int i = 0; i < 3; i++)
        {
            EndCoin("Coin " + (i + 1), rowCoins, circle, 46f);
        }
        row.gameObject.SetActive(false);

        // Trennlinie über der Summe
        RectTransform line = CreateImage("Divider", body, new Color(FadedInkColor.r, FadedInkColor.g, FadedInkColor.b, 0.5f));
        AddLayoutSize(line, 0f, 3f);
        line.GetComponent<LayoutElement>().flexibleWidth = 1f;

        RectTransform totalRow = CreateRect("Total", body);
        var totalLayout = totalRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        totalLayout.spacing = 18f;
        totalLayout.childAlignment = TextAnchor.MiddleCenter;
        totalLayout.childControlWidth = totalLayout.childControlHeight = true;
        totalLayout.childForceExpandWidth = totalLayout.childForceExpandHeight = false;
        totalLayout.padding = new RectOffset(0, 0, 6, 6);
        TicketText("Label", totalRow, "TOTAL TIP", 32f, FadedInkColor, semiBoldFont, 12f);
        EndCoin("Coin", totalRow, circle, 60f).color = new Color(1f, 0.8f, 0.15f);
        TextMeshProUGUI total = TicketText("Amount", totalRow, "0 / 15", 64f, InkColor, boldFont);

        TicketText("Thanks", body, "Thanks for playing!", 44f, InkColor, boldFont);
        TextMeshProUGUI hint = TicketText("Replay Hint", body, "Press Jump to play again", 30f, FadedInkColor, semiBoldFont);
        var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        hud.FindProperty("endPanel").objectReferenceValue = panel.gameObject;
        hud.FindProperty("endRowTemplate").objectReferenceValue = row;
        hud.FindProperty("endTotal").objectReferenceValue = total;
        hud.FindProperty("endHint").objectReferenceValue = hintGroup;
        panel.gameObject.SetActive(false);
    }

    // Münze wie auf der "Delivered!"-Karte: Kreis-Sprite mit dunkelgoldenem Rand
    private static Image EndCoin(string name, Transform parent, Sprite circle, float size)
    {
        RectTransform coin = CreateImage(name, parent, Color.white);
        var image = coin.GetComponent<Image>();
        image.sprite = circle;
        AddLayoutSize(coin, size, size);
        var rim = coin.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color(0.6f, 0.38f, 0.05f);
        rim.effectDistance = new Vector2(3f, -3f);
        return image;
    }

    // Lieferschein unten mittig, während die Kamera zum Kunden fliegt: rotes Band "DELIVERY ORDER", darunter die Adresse
    // und optional eine kleinere zweite Zeile. Startet unsichtbar (Alpha 0)
    private static void BuildIntroTitle(Transform parent, SerializedObject hud)
    {
        RectTransform group = CreateRect("Intro Title", parent);
        group.anchorMin = group.anchorMax = new Vector2(0.5f, 0f);
        group.pivot = new Vector2(0.5f, 0f);
        group.anchoredPosition = new Vector2(0f, 80f);
        var canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform paper = Ticket("Ticket", group, new Vector2(0.5f, 0f), 1.5f);
        RectTransform band = Band("Band", paper, PizzeriaRed, 28f, 8f);
        AddLayoutSize(PizzaIcon(band, 40f), 40f, 40f);
        TicketText("Label", band, "DELIVERY ORDER", 30f, Color.white, boldFont, 10f);

        RectTransform body = Body("Body", paper, 56f, 14f, 20f, 0f);
        TextMeshProUGUI address = TicketText("Address", body, "No. 42, Pepperoni Parkway", 64f, InkColor, boldFont);
        TextMeshProUGUI note = TicketText("Note", body, "", 38f, FadedInkColor, semiBoldFont);
        note.gameObject.SetActive(false);

        hud.FindProperty("introTitle").objectReferenceValue = canvasGroup;
        hud.FindProperty("introAddress").objectReferenceValue = address;
        hud.FindProperty("introNote").objectReferenceValue = note;
    }

    // ---------- Lieferschein ----------
    // Die Größen kommen aus dem Inhalt statt aus festen Zahlen: Jede Layout Group misst ihre Kinder (bei Text die Breite
    // der Zeile), der Content Size Fitter am Papier übernimmt das Ergebnis als eigene Größe. Längere Adresse = breiterer Zettel

    // Papier mit Schlagschatten, wächst mit seinem Inhalt um den Pivot. tilt = leicht schief (Grad), wirkt wie hingelegt
    private static RectTransform Ticket(string name, Transform parent, Vector2 pivot, float tilt)
    {
        RectTransform paper = CreateImage(name, parent, PaperColor);
        var image = paper.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.35f;
        paper.anchorMin = paper.anchorMax = pivot;
        paper.pivot = pivot;
        paper.anchoredPosition = Vector2.zero;
        paper.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Shadow-Effekt zeichnet das Papier versetzt und dunkel nochmal dahinter. Geht hier, weil das Papier deckend ist
        var shadow = paper.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(6f, -8f);

        // Band und Inhalt untereinander, 8 px Papierrand rundherum
        var layout = paper.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = paper.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return paper;
    }

    // Farbiges Band oben im Zettel, Inhalt (Symbol + Text) nebeneinander und mittig
    private static RectTransform Band(string name, Transform paper, Color color, float padX, float padY)
    {
        RectTransform band = CreateImage(name, paper, color);
        var image = band.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        // Etwas kleinere Ecken als das Papier, damit der Papierrand gleichmäßig breit wirkt
        image.pixelsPerUnitMultiplier = 0.5f;
        var layout = band.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), Mathf.RoundToInt(padY), Mathf.RoundToInt(padY));
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return band;
    }

    // Papierbereich unter dem Band, Zeilen untereinander
    private static RectTransform Body(string name, Transform paper, float padX, float padTop, float padBottom, float spacing)
    {
        RectTransform body = CreateRect(name, paper);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), Mathf.RoundToInt(padTop), Mathf.RoundToInt(padBottom));
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return body;
    }

    // Einzeilig (kein Umbruch): So meldet der Text der Layout Group seine volle Zeilenbreite und der Zettel wächst mit.
    // spacing = zusätzlicher Buchstabenabstand (für gesperrte Großbuchstaben wie "DELIVERY ORDER")
    private static TextMeshProUGUI TicketText(string name, Transform parent, string text, float fontSize, Color color, TMP_FontAsset font, float spacing = 0f)
    {
        RectTransform rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.characterSpacing = spacing;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // Feste Größe für ein Element in einer Layout Group (Symbol, Münze)
    private static void AddLayoutSize(RectTransform rect, float width, float height)
    {
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = width;
        element.preferredHeight = element.minHeight = height;
    }

    // SDF-Schrift-Asset aus der TTF erzeugen (einmalig, danach wird das gespeicherte geladen). Static mit allen druckbaren
    // ASCII-Zeichen: Der Atlas ändert sich dann zur Laufzeit nicht mehr (ein Dynamic-Atlas würde das Asset bei jedem neuen
    // Zeichen verändern). Fehlt doch mal ein Zeichen, springt Liberation Sans als Fallback ein
    private static TMP_FontAsset LoadOrCreateFont(string fileName)
    {
        string assetPath = $"{FontFolder}/{fileName} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{fileName}.ttf");
        // 72 pt Abtastgröße, 8 px Rand fürs Distanzfeld, 1024er Atlas: reicht für die ASCII-Zeichen in hoher Qualität
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        asset.name = fileName + " SDF";
        AssetDatabase.CreateAsset(asset, assetPath);
        // Atlas-Textur und Material entstehen nur im Speicher, als Unter-Assets kommen sie mit in die Datei
        asset.atlasTextures[0].name = fileName + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = fileName + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        var ascii = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++)
        {
            ascii.Append(c);
        }
        if (!asset.TryAddCharacters(ascii.ToString(), out string missing))
        {
            Debug.LogWarning($"PizzaHeatSetup: {fileName} fehlen Zeichen: {missing}");
        }
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        if (TMP_Settings.defaultFontAsset != null)
        {
            asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"PizzaHeatSetup: Schrift-Asset {assetPath} erzeugt.");
        return asset;
    }

    // Kreis-Blende über den ganzen Bildschirm. Image im Prefab aus, sonst ist die Game-Ansicht im Edit Mode schwarz
    private static void BuildIris(Transform parent)
    {
        RectTransform rect = CreateImage("Iris", parent, Color.black);
        Stretch(rect, 0f);
        var image = rect.GetComponent<Image>();
        image.material = LoadOrCreateIrisMaterial();
        image.enabled = false;
        rect.gameObject.AddComponent<ScreenIris>();
    }

    private static Material LoadOrCreateIrisMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(IrisMaterialPath);
        if (material != null)
        {
            return material;
        }
        material = new Material(Shader.Find("JumpAndRun/UI/Iris"));
        AssetDatabase.CreateAsset(material, IrisMaterialPath);
        return material;
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
        particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PrimitiveKit.ParticleMaterialPath);
        if (particleRenderer.sharedMaterial == null)
        {
            Debug.LogWarning($"PizzaHeatSetup: Partikel-Material nicht gefunden ({PrimitiveKit.ParticleMaterialPath}), Dampf wird pink.");
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

    // Anker auf die Ecken des Elternteils: Das Rechteck füllt es komplett aus, abzüglich Rand
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
