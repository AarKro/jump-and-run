using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD für die Pizza-Wärme: Balken oben, Ergebnis beim Abliefern (Trinkgeld-Münzen), Meldung bei kalter Pizza.
// Der Balken erklärt sich selbst: Pizza-Symbol links, unter jedem Drittel die Münzen, die es dort Trinkgeld gibt
// (fällt die Wärme unter eine Stufe, wird deren Münzgruppe grau und wackelt), eingesammelte Chilis fliegen als
// Flamme in den Balken, und zu Levelbeginn steht kurz "Keep the pizza hot!" darunter.
// Wird von Tools > Jump and Run > Pizza Heat > Create HUD Prefab aufgebaut, die Referenzen setzt das Tool.
public class PizzaHud : MonoBehaviour
{
    [Header("Intro")]
    [Tooltip("Wärmebalken: im Level-Intro ausgeblendet, danach eingeblendet")]
    [SerializeField] private CanvasGroup gameplayGroup;
    [Tooltip("\"Deliver to\" + Adresse, während die Kamera zum Kunden fliegt")]
    [SerializeField] private CanvasGroup introTitle;
    [SerializeField] private TextMeshProUGUI introAddress;

    [Header("Wärmebalken")]
    [Tooltip("Füllung des Balkens. Die Breite kommt über anchorMax.x (0-1), nicht über die Größe")]
    [SerializeField] private RectTransform heatFill;
    [SerializeField] private Image heatFillImage;
    [Tooltip("Farbe nach Wärme: links kalt, rechts heiß")]
    [SerializeField] private Gradient heatColors;

    [Tooltip("Der ganze Balken, für den kleinen Ruck, wenn eine Chili-Flamme ankommt")]
    [SerializeField] private RectTransform heatBar;

    [Header("Trinkgeld-Stufen am Balken")]
    [Tooltip("Münzgruppen unter den Balken-Dritteln, von links: 1, 2, 3 Münzen")]
    [SerializeField] private RectTransform[] tipGroups;
    [SerializeField] private Color tipLostColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);

    [Header("Chili-Flug")]
    [Tooltip("Ausgeschaltete Vorlage: fliegt von der Chili zum Ende der Füllung")]
    [SerializeField] private RectTransform flameTemplate;
    [SerializeField] private float flameFlightTime = 0.6f;

    [Header("Hinweis zu Levelbeginn")]
    [SerializeField] private CanvasGroup heatHint;
    [SerializeField] private float hintHoldTime = 3f;

    [Header("Wärme über 100%")]
    [Tooltip("Zweite Füllung über dem Balken für den Puffer über 100% (100-200% Wärme = 0-1 Breite)")]
    [SerializeField] private RectTransform overheatFill;
    [SerializeField] private Image overheatImage;
    [SerializeField] private float overheatPulseSpeed = 5f;

    [Header("Geliefert")]
    [SerializeField] private GameObject deliveredPanel;
    [SerializeField] private Image[] coins;
    [SerializeField] private Color coinEarnedColor = new Color(1f, 0.8f, 0.15f);
    [SerializeField] private Color coinMissingColor = new Color(1f, 1f, 1f, 0.2f);
    [Tooltip("Pause zwischen den einzelnen Münzen")]
    [SerializeField] private float coinPopInterval = 0.3f;
    [Tooltip("So groß ist eine Münze im ersten Moment, bevor sie auf normale Größe schrumpft")]
    [SerializeField] private float coinPopScale = 1.5f;

    [Header("Kalt")]
    [SerializeField] private GameObject coldPanel;

    private float overheatBaseAlpha;
    private Coroutine gameplayFade;
    private Coroutine titleFade;
    private Coroutine hintFade;
    // Wärme von Chilis, deren Flamme noch unterwegs ist: Der Balken zeigt sie erst, wenn die Flamme ankommt
    private float pendingHeat;
    // 1 = Füllung ganz weiß aufgeblitzt, klingt in Update ab
    private float flash;
    private Image[][] tipCoins;
    private Vector2[] tipGroupPositions;
    private int shownTier = -1;

    private void Awake()
    {
        GameManager.PizzaDelivered += ShowDelivered;
        GameManager.PizzaCold += ShowCold;
        HeatPickup.Collected += OnChiliCollected;

        tipCoins = new Image[tipGroups.Length][];
        tipGroupPositions = new Vector2[tipGroups.Length];
        for (int i = 0; i < tipGroups.Length; i++)
        {
            tipCoins[i] = tipGroups[i].GetComponentsInChildren<Image>(true);
            tipGroupPositions[i] = tipGroups[i].anchoredPosition;
        }
        if (heatHint != null)
        {
            heatHint.alpha = 0f;
        }
        deliveredPanel.SetActive(false);
        coldPanel.SetActive(false);
        if (introTitle != null)
        {
            introTitle.alpha = 0f;
        }
        overheatBaseAlpha = overheatImage.color.a;
    }

    // ---------- Intro ----------

    public void SetGameplayVisible(bool visible)
    {
        if (gameplayGroup != null)
        {
            gameplayGroup.alpha = visible ? 1f : 0f;
        }
    }

    // Levelbeginn (nach dem Intro, auch nach einem Neustart): HUD einblenden und kurz erklären, worum es geht
    public void FadeInGameplay(float duration)
    {
        gameplayFade = RestartFade(gameplayFade, gameplayGroup, 1f, duration);
        if (heatHint != null)
        {
            if (hintFade != null)
            {
                StopCoroutine(hintFade);
            }
            hintFade = StartCoroutine(ShowHint(duration));
        }
    }

    private IEnumerator ShowHint(float fadeIn)
    {
        yield return Fade(heatHint, 1f, fadeIn);
        yield return new WaitForSeconds(hintHoldTime);
        yield return Fade(heatHint, 0f, 0.6f);
    }

    public void ShowIntroTitle(string address, float duration)
    {
        if (introAddress != null)
        {
            introAddress.text = address;
        }
        titleFade = RestartFade(titleFade, introTitle, 1f, duration);
    }

    public void HideIntroTitle(float duration)
    {
        titleFade = RestartFade(titleFade, introTitle, 0f, duration);
    }

    // Eine laufende Blende derselben Gruppe erst anhalten, sonst ziehen zwei Coroutines am selben Alpha
    private Coroutine RestartFade(Coroutine running, CanvasGroup group, float target, float duration)
    {
        if (group == null)
        {
            return null;
        }
        if (running != null)
        {
            StopCoroutine(running);
        }
        return StartCoroutine(Fade(group, target, duration));
    }

    // CanvasGroup.alpha blendet alle Kinder auf einmal, ohne jede Farbe einzeln anzufassen
    private static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float from = group.alpha;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= ShowDelivered;
        GameManager.PizzaCold -= ShowCold;
        HeatPickup.Collected -= OnChiliCollected;
    }

    private void Update()
    {
        float heat = GameManager.Instance != null ? GameManager.Instance.HeatPercent : 0f;
        heat = Mathf.Max(0f, heat - pendingHeat);
        // Anker statt Breite: anchorMax.x = 0.5 heißt "bis zur Hälfte des Eltern-Rechtecks",
        // egal wie groß der Balken auf dem Bildschirm ist (CanvasScaler, andere Auflösung)
        float normalHeat = Mathf.Clamp01(heat);
        heatFill.anchorMax = new Vector2(normalHeat, 1f);
        heatFillImage.color = Color.Lerp(heatColors.Evaluate(normalHeat), Color.white, flash);
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
        UpdateTipTier(heat);

        float extraHeat = Mathf.Clamp01(heat - 1f);
        overheatFill.anchorMax = new Vector2(extraHeat, 1f);
        Color glow = overheatImage.color;
        glow.a = overheatBaseAlpha * (0.6f + 0.4f * Mathf.Sin(Time.time * overheatPulseSpeed));
        overheatImage.color = glow;
    }

    // ---------- Trinkgeld-Stufen ----------

    // Gruppe i (i + 1 Münzen) leuchtet, solange die Wärme mindestens in ihrem Drittel ist. Fällt die Wärme darunter,
    // wird sie grau und wackelt ("dieses Trinkgeld ist weg"), kommt sie zurück (Chili), ploppt sie wieder auf
    private void UpdateTipTier(float heat)
    {
        int tier = GameManager.CoinsForHeat(heat);
        if (tier == shownTier)
        {
            return;
        }
        for (int i = 0; i < tipGroups.Length; i++)
        {
            bool lit = i < tier;
            foreach (Image coin in tipCoins[i])
            {
                coin.color = lit ? coinEarnedColor : tipLostColor;
            }
            // Beim ersten Mal (Levelstart) ohne Animation
            if (shownTier < 0)
            {
                continue;
            }
            if (!lit && i < shownTier)
            {
                StartCoroutine(Shake(i));
            }
            else if (lit && i >= shownTier)
            {
                StartCoroutine(Pop(tipGroups[i], 1.4f, 0.15f));
            }
        }
        shownTier = tier;
    }

    private IEnumerator Shake(int group)
    {
        RectTransform rect = tipGroups[group];
        const float duration = 0.4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float strength = 1f - t / duration;
            rect.anchoredPosition = tipGroupPositions[group] + Vector2.right * (Mathf.Sin(t * 60f) * 8f * strength);
            yield return null;
        }
        rect.anchoredPosition = tipGroupPositions[group];
    }

    private static IEnumerator Pop(RectTransform rect, float startScale, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            rect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, t / duration);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    // ---------- Chili-Flug ----------

    private void OnChiliCollected(Vector3 worldPosition, float heatFraction)
    {
        if (flameTemplate == null)
        {
            return;
        }
        pendingHeat += heatFraction;
        StartCoroutine(FlyFlame(worldPosition, heatFraction));
    }

    // Die Flamme startet dort auf dem Bildschirm, wo die Chili war, und fliegt im Bogen zum Ende der Füllung.
    // Erst bei der Ankunft zeigt der Balken die neue Wärme, blitzt auf und ruckt kurz: Das verbindet Chili und Balken
    private IEnumerator FlyFlame(Vector3 worldPosition, float heatFraction)
    {
        var space = (RectTransform)flameTemplate.parent;
        RectTransform flame = Instantiate(flameTemplate, space);
        flame.gameObject.SetActive(true);

        // Welt → Bildschirmpunkt (Pixel) → Punkt im Rechteck des HUD. Overlay-Canvas: keine Kamera für die Umrechnung
        Vector2 start = Vector2.zero;
        Camera view = Camera.main;
        if (view != null)
        {
            Vector3 screen = view.WorldToScreenPoint(worldPosition);
            if (screen.z > 0f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out start);
            }
        }
        // Am Bildschirmrand starten, falls die Chili gerade außerhalb des Bildes war (z.B. Kamera schwenkt noch)
        Vector2 half = space.rect.size / 2f - new Vector2(40f, 40f);
        start = new Vector2(Mathf.Clamp(start.x, -half.x, half.x), Mathf.Clamp(start.y, -half.y, half.y));

        for (float t = 0f; t < flameFlightTime; t += Time.deltaTime)
        {
            // Ziel jedes Frame neu: Die Füllung schrumpft ja weiter, während die Flamme fliegt
            Vector2 end = FillEnd(space);
            Vector2 control = (start + end) / 2f + Vector2.up * 150f;
            // Erst langsam, dann schneller (Quadrat), auf einer Bézier-Kurve durch den Kontrollpunkt
            float k = t / flameFlightTime;
            k *= k;
            Vector2 a = Vector2.Lerp(start, control, k);
            Vector2 b = Vector2.Lerp(control, end, k);
            flame.anchoredPosition = Vector2.Lerp(a, b, k);
            flame.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t * 30f));
            yield return null;
        }

        Destroy(flame.gameObject);
        pendingHeat = Mathf.Max(0f, pendingHeat - heatFraction);
        flash = 1f;
        if (heatBar != null)
        {
            StartCoroutine(Pop(heatBar, 1.08f, 0.2f));
        }
    }

    // Rechte Kante der Füllung (Mitte), umgerechnet in das Rechteck, in dem die Flamme fliegt
    private Vector2 FillEnd(RectTransform space)
    {
        var corners = new Vector3[4];
        heatFill.GetWorldCorners(corners);
        // 2 = oben rechts, 3 = unten rechts
        Vector3 rightMiddle = (corners[2] + corners[3]) / 2f;
        return space.InverseTransformPoint(rightMiddle);
    }

    // ---------- Ergebnis ----------

    private void ShowDelivered(int earnedCoins)
    {
        deliveredPanel.SetActive(true);
        StartCoroutine(PopCoins(earnedCoins));
    }

    private void ShowCold()
    {
        coldPanel.SetActive(true);
    }

    // Erst alle Plätze grau zeigen, dann die verdienten Münzen nacheinander "aufploppen" lassen
    private IEnumerator PopCoins(int earnedCoins)
    {
        foreach (Image coin in coins)
        {
            coin.color = coinMissingColor;
        }

        for (int i = 0; i < earnedCoins && i < coins.Length; i++)
        {
            yield return new WaitForSeconds(coinPopInterval);

            Image coin = coins[i];
            coin.color = coinEarnedColor;

            float t = 0f;
            const float popDuration = 0.15f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(coinPopScale, 1f, t / popDuration);
                coin.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            coin.rectTransform.localScale = Vector3.one;
        }
    }
}
