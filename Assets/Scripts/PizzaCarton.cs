using UnityEngine;

// Der Pizzakarton, den der Spieler trägt. Sitzt als Kind am Player und wechselt die Seite,
// je nachdem wohin der Spieler schaut. Der Dampf zeigt, wie warm die Pizza noch ist.
public class PizzaCarton : MonoBehaviour
{
    [Header("Dampf")]
    [Tooltip("Partikelsystem als Kind des Kartons. Rate und Deckkraft im Inspector = Werte bei voller Wärme")]
    [SerializeField] private ParticleSystem steam;

    private PlayerController player;
    // Abstand zur Körpermitte in X, aus der Position im Prefab gelesen (Vorzeichen egal)
    private float carryOffsetX;
    private float fullSteamRate;
    private float fullSteamAlpha;

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        carryOffsetX = Mathf.Abs(transform.localPosition.x);

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

    // LateUpdate: läuft nach allen Updates, also nachdem der Spieler seine Blickrichtung gesetzt hat
    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        // localPosition ist relativ zum Spieler, darum wandert der Karton automatisch mit.
        // Nur X wird gespiegelt, Höhe und Tiefe bleiben wie im Prefab eingestellt.
        Vector3 local = transform.localPosition;
        local.x = carryOffsetX * player.FacingDirection;
        transform.localPosition = local;
    }
}
