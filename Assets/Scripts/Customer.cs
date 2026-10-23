using System.Collections;
using UnityEngine;

// Der Kunde am Ziel: wartet, winkt wenn der Spieler nahe ist, nimmt beim Abliefern den Karton
// und tanzt je nach Trinkgeld (1-3 Münzen) unterschiedlich wild. Alles prozedural im Code, ohne Animation-Clips.
// Aufgebaut von Tools > Jump and Run > Build Customer into Goal Prefab.
public class Customer : MonoBehaviour
{
    [Header("Farben")]
    [SerializeField] private Color shirtColor = new Color(0.1f, 0.6f, 0.6f);
    [SerializeField] private Color skinColor = new Color(0.96f, 0.78f, 0.62f);
    [SerializeField] private Renderer[] shirtRenderers;
    [SerializeField] private Renderer[] skinRenderers;

    [Header("Körperteile")]
    [Tooltip("Alles Sichtbare hängt hier dran. Nur dieses Objekt wird bewegt, die Wurzel bleibt am Ziel stehen")]
    [SerializeField] private Transform rig;
    [Tooltip("Arm auf der -X-Seite (zum ankommenden Spieler hin), Drehpunkt an der Schulter")]
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [Tooltip("Hier landet der Pizzakarton")]
    [SerializeField] private Transform handsPoint;

    [Header("Warten")]
    [Tooltip("Ab diesem Abstand (in X) winkt der Kunde dem Spieler zu")]
    [SerializeField] private float waveDistance = 6f;
    [SerializeField] private float swayAngle = 4f;
    [SerializeField] private float swaySpeed = 2f;
    [SerializeField] private float waveSpeed = 9f;

    [Header("Übergabe")]
    [SerializeField] private float handoffDuration = 0.4f;
    [SerializeField] private float handoffArcHeight = 1f;

    [Header("Tanz")]
    [SerializeField] private float danceSpeed = 7f;
    [Tooltip("Sprunghöhe beim Tanzen für 2 bzw. 3 Münzen")]
    [SerializeField] private float hopHeightTwoCoins = 0.2f;
    [SerializeField] private float hopHeightThreeCoins = 0.4f;
    [Tooltip("Drehung beim wildesten Tanz (Grad pro Sekunde)")]
    [SerializeField] private float spinSpeed = 270f;

    private enum State { Waiting, Receiving, Dancing }

    private State state = State.Waiting;
    private int coins;
    private float danceTime;
    private Transform player;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        GameManager.PizzaDelivered += OnPizzaDelivered;
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= OnPizzaDelivered;
    }

    // Läuft im Editor bei jeder Änderung im Inspector: So sieht man die Farben sofort, ohne Play zu drücken
    private void OnValidate()
    {
        ApplyColors();
    }

    // MaterialPropertyBlock: Farbe pro Renderer setzen, ohne das gemeinsame Material zu ändern oder zu kopieren.
    // Alle Kunden teilen sich Customer.mat und können trotzdem verschiedene Farben haben.
    private void ApplyColors()
    {
        SetColor(shirtRenderers, shirtColor);
        SetColor(skinRenderers, skinColor);
    }

    private static void SetColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        var block = new MaterialPropertyBlock();
        foreach (Renderer target in renderers)
        {
            if (target == null)
            {
                continue;
            }
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }

    private void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }

    private void Update()
    {
        switch (state)
        {
            case State.Waiting:
                UpdateWaiting();
                break;
            case State.Dancing:
                danceTime += Time.deltaTime;
                UpdateDance();
                break;
        }
    }

    private void UpdateWaiting()
    {
        // Ungeduldig von einem Fuß auf den anderen
        float t = Time.time;
        rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * swaySpeed) * swayAngle);

        bool playerClose = player != null && Mathf.Abs(player.position.x - transform.position.x) < waveDistance;
        // Winkel um Z: 0 = Arm hängt runter, -X-Arm geht mit negativen Winkeln nach außen/oben
        float leftAngle = playerClose ? -150f + Mathf.Sin(t * waveSpeed) * 25f : -8f;
        leftArm.localRotation = Quaternion.Euler(0f, 0f, leftAngle);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, 8f);
    }

    private void OnPizzaDelivered(int earnedCoins)
    {
        coins = earnedCoins;
        StartCoroutine(ReceiveCarton());
    }

    private IEnumerator ReceiveCarton()
    {
        state = State.Receiving;
        rig.localRotation = Quaternion.identity;
        // Arme leicht nach innen, wie zum Entgegennehmen
        leftArm.localRotation = Quaternion.Euler(0f, 0f, 25f);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, -25f);

        PizzaCarton carton = FindAnyObjectByType<PizzaCarton>();
        if (carton != null)
        {
            // Sonst setzt der Karton in LateUpdate seine Position wieder neben den Spieler
            carton.enabled = false;
            Transform cartonTransform = carton.transform;
            Vector3 from = cartonTransform.position;

            for (float t = 0f; t < handoffDuration; t += Time.deltaTime)
            {
                float k = t / handoffDuration;
                // Bogen: geradlinig von A nach B plus eine Parabel nach oben (0 an den Enden, Maximum in der Mitte)
                Vector3 position = Vector3.Lerp(from, handsPoint.position, k);
                position.y += 4f * handoffArcHeight * k * (1f - k);
                cartonTransform.position = position;
                yield return null;
            }

            // Umhängen: Ab jetzt ist der Karton Teil des Kunden und tanzt mit
            cartonTransform.SetParent(handsPoint, false);
            cartonTransform.localPosition = Vector3.zero;
        }

        state = State.Dancing;
        danceTime = 0f;
    }

    private void UpdateDance()
    {
        float t = danceTime * danceSpeed;

        switch (coins)
        {
            case 3:
                // Wild: hoch hüpfen, im Kreis drehen, Arme über dem Kopf
                rig.localPosition = Vector3.up * (hopHeightThreeCoins * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.Euler(0f, danceTime * spinSpeed, 0f);
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -160f + Mathf.Sin(t * 2f) * 20f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 160f - Mathf.Sin(t * 2f) * 20f);
                break;
            case 2:
                // Fröhlich wippen, Arme abwechselnd hoch
                rig.localPosition = Vector3.up * (hopHeightTwoCoins * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.identity;
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -110f + Mathf.Sin(t) * 40f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 110f + Mathf.Sin(t) * 40f);
                break;
            default:
                // Kurz hin und her wiegen, Arme nur ein bisschen
                rig.localPosition = Vector3.zero;
                rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f) * 8f);
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -20f + Mathf.Sin(t * 0.6f) * 10f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 20f + Mathf.Sin(t * 0.6f) * 10f);
                break;
        }
    }
}
