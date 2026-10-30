using UnityEngine;

// Der Pizzakarton, den der Spieler trägt. Sitzt als Kind am Player und folgt dem holdPoint zwischen den Händen des Modells
// (das sich selbst in Laufrichtung dreht).
// Der Dampf zeigt, wie warm die Pizza noch ist.
public class PizzaCarton : MonoBehaviour
{
    [Header("Dampf")]
    [Tooltip("Partikelsystem als Kind des Kartons. Rate und Deckkraft im Inspector = Werte bei voller Wärme")]
    [SerializeField] private ParticleSystem steam;

    [Header("Tragen")]
    [Tooltip("Punkt zwischen den Händen des Spieler-Modells (setzt PlayerModelSetup)")]
    [SerializeField] private Transform holdPoint;

    private float fullSteamRate;
    private float fullSteamAlpha;

    private void Awake()
    {
        if (steam != null)
        {
            fullSteamRate = steam.emission.rateOverTime.constant;
            fullSteamAlpha = steam.main.startColor.color.a;
        }
    }

    private void Update()
    {
        if (steam == null)
        {
            return;
        }

        // Über 100% Wärme gibt es nicht mehr Dampf, nur bis "voll"
        float heat = GameManager.Instance != null ? Mathf.Clamp01(GameManager.Instance.HeatPercent) : 1f;

        // Die Module (emission, main, ...) sind nur "Griffe" auf das Partikelsystem: in eine Variable holen
        // und darauf setzen ändert das System direkt. Gilt nur für neue Partikel, alte verblassen normal.
        ParticleSystem.EmissionModule emission = steam.emission;
        emission.rateOverTime = fullSteamRate * heat;

        ParticleSystem.MainModule main = steam.main;
        Color color = main.startColor.color;
        color.a = fullSteamAlpha * heat;
        main.startColor = color;
    }

    // Kurzer Dampfstoß, z.B. wenn eine Chili die Pizza aufwärmt. Emit erzeugt Partikel sofort, unabhängig von der Rate
    public void PuffSteam(int count)
    {
        if (steam != null)
        {
            steam.Emit(count);
        }
    }

    // LateUpdate: läuft nach allen Updates, also nach DeliveryGuy.Update, die Hände sind schon in der Pose dieses Frames
    private void LateUpdate()
    {
        if (holdPoint != null)
        {
            transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);
        }
    }
}
