using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// HUD für die Pizza-Wärme: Balken oben, Ergebnis beim Abliefern (Trinkgeld-Münzen), Meldung bei kalter Pizza.
// Wird von Tools > Jump and Run > Pizza Heat > Create HUD Prefab aufgebaut, die Referenzen setzt das Tool.
public class PizzaHud : MonoBehaviour
{
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

    private void Awake()
    {
        GameManager.PizzaDelivered += ShowDelivered;
        GameManager.PizzaCold += ShowCold;

        deliveredPanel.SetActive(false);
        coldPanel.SetActive(false);
        overheatBaseAlpha = overheatImage.color.a;
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= ShowDelivered;
        GameManager.PizzaCold -= ShowCold;
    }

    private void Update()
    {
        float heat = GameManager.Instance != null ? GameManager.Instance.HeatPercent : 0f;

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
