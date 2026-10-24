using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD für die Pizza-Wärme: Balken oben, Ergebnis beim Abliefern (Trinkgeld-Münzen), Meldung bei kalter Pizza.
// Wird von Tools > Jump and Run > Pizza Heat > Create HUD Prefab aufgebaut, die Referenzen setzt das Tool.
public class PizzaHud : MonoBehaviour
{
    [Header("Intro")]
    [Tooltip("Wärmebalken und Stoppuhr: im Level-Intro ausgeblendet, danach eingeblendet")]
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

    [Header("Wärme über 100%")]
    [Tooltip("Zweite Füllung über dem Balken für den Puffer über 100% (100-200% Wärme = 0-1 Breite)")]
    [SerializeField] private RectTransform overheatFill;
    [SerializeField] private Image overheatImage;
    [SerializeField] private float overheatPulseSpeed = 5f;

    [Header("Stoppuhr")]
    [Tooltip("Lieferzeit neben dem Wärmebalken")]
    [SerializeField] private TextMeshProUGUI stopwatchText;

    [Header("Geliefert")]
    [SerializeField] private GameObject deliveredPanel;
    [Tooltip("Lieferzeit im Ergebnis-Panel")]
    [SerializeField] private TextMeshProUGUI deliveredTimeText;
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

    private void Awake()
    {
        GameManager.PizzaDelivered += ShowDelivered;
        GameManager.PizzaCold += ShowCold;

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

    public void FadeInGameplay(float duration)
    {
        gameplayFade = RestartFade(gameplayFade, gameplayGroup, 1f, duration);
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
    }

    private void Update()
    {
        float heat = GameManager.Instance != null ? GameManager.Instance.HeatPercent : 0f;
        if (stopwatchText != null && GameManager.Instance != null)
        {
            stopwatchText.text = FormatTime(GameManager.Instance.ElapsedTime);
        }

        // Anker statt Breite: anchorMax.x = 0.5 heißt "bis zur Hälfte des Eltern-Rechtecks",
        // egal wie groß der Balken auf dem Bildschirm ist (CanvasScaler, andere Auflösung)
        float normalHeat = Mathf.Clamp01(heat);
        heatFill.anchorMax = new Vector2(normalHeat, 1f);
        heatFillImage.color = heatColors.Evaluate(normalHeat);

        float extraHeat = Mathf.Clamp01(heat - 1f);
        overheatFill.anchorMax = new Vector2(extraHeat, 1f);
        Color glow = overheatImage.color;
        glow.a = overheatBaseAlpha * (0.6f + 0.4f * Mathf.Sin(Time.time * overheatPulseSpeed));
        overheatImage.color = glow;
    }

    // 83.4 Sekunden → "1:23.4"
    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        return $"{minutes}:{(seconds - minutes * 60f).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private void ShowDelivered(int earnedCoins)
    {
        deliveredPanel.SetActive(true);
        if (deliveredTimeText != null && GameManager.Instance != null)
        {
            deliveredTimeText.text = "Time  " + FormatTime(GameManager.Instance.ElapsedTime);
        }
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
