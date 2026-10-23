using System.Collections;
using UnityEngine;

// Der Kunde am Ziel: wartet in seinem Haus, kommt heraus, wenn der Spieler nahe ist, winkt, nimmt beim Abliefern den Karton
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
    [Tooltip("Länge eines Arms vom Schulter-Drehpunkt bis zur Hand (Arm-Kapsel ist 0.56 lang)")]
    [SerializeField] private float armLength = 0.56f;
    [Tooltip("Der Karton sitzt so weit vor den Händen (Richtung Kamera), damit er beim Hochheben nicht im Kopf steckt")]
    [SerializeField] private float holdForward = 0.18f;
    [SerializeField] private float holdAbove = 0.06f;

    [Header("Haus")]
    [Tooltip("Haustür, aus der der Kunde herauskommt. Leer = steht von Anfang an draußen")]
    [SerializeField] private SpawnDoor homeDoor;
    [Tooltip("Ab diesem Abstand (in X) öffnet er die Tür und kommt heraus")]
    [SerializeField] private float comeOutDistance = 8f;
    [SerializeField] private float walkOutDuration = 0.8f;

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

    private enum State { Inside, ComingOut, Waiting, Receiving, Dancing }

    private State state = State.Waiting;
    private int coins;
    private float danceTime;
    private Transform player;
    // Der gehaltene Karton: Seine Position wird jeden Frame zwischen die Hände gesetzt
    private Transform heldCarton;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        GameManager.PizzaDelivered += OnPizzaDelivered;
        GameManager.LevelReset += GoBackInside;
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= OnPizzaDelivered;
        GameManager.LevelReset -= GoBackInside;
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

        if (homeDoor != null)
        {
            GoBackInside();
        }
    }

    // Startzustand mit Haus: drinnen hinter der geschlossenen Tür (unsichtbar im dunklen Raum).
    // Auch beim Respawn des Spielers, solange noch nicht geliefert wurde
    private void GoBackInside()
    {
        if (homeDoor == null || state == State.Receiving || state == State.Dancing)
        {
            return;
        }
        StopAllCoroutines();
        homeDoor.SnapClosed();
        transform.position = homeDoor.InsidePosition;
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.identity;
        state = State.Inside;
    }

    private IEnumerator ComeOut()
    {
        state = State.ComingOut;
        yield return homeDoor.Open();

        Vector3 from = homeDoor.InsidePosition;
        Vector3 to = homeDoor.OutsidePosition;
        for (float t = 0f; t < walkOutDuration; t += Time.deltaTime)
        {
            float k = t / walkOutDuration;
            transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * 0.06f);
            yield return null;
        }
        transform.position = to;
        StartCoroutine(homeDoor.Close());
        state = State.Waiting;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Inside:
                if (player != null && Mathf.Abs(player.position.x - transform.position.x) < comeOutDistance)
                {
                    StartCoroutine(ComeOut());
                }
                break;
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
        // Noch drinnen oder unterwegs (Spieler war sehr schnell): sofort draußen hinstellen
        if (homeDoor != null && (state == State.Inside || state == State.ComingOut))
        {
            StopAllCoroutines();
            transform.position = homeDoor.OutsidePosition;
            homeDoor.SnapClosed();
        }
        StartCoroutine(ReceiveCarton());
    }

    private IEnumerator ReceiveCarton()
    {
        state = State.Receiving;
        rig.localRotation = Quaternion.identity;
        // Beide Arme nach vorne strecken, wie zum Entgegennehmen
        SetHoldPose(70f);

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
                Vector3 position = Vector3.Lerp(from, HoldPoint(), k);
                position.y += 4f * handoffArcHeight * k * (1f - k);
                cartonTransform.position = position;
                yield return null;
            }

            // Ab jetzt gehört der Karton dem Kunden, LateUpdate hält ihn zwischen den Händen
            cartonTransform.SetParent(rig, true);
            heldCarton = cartonTransform;
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
                // Wild: hoch hüpfen, im Kreis drehen, die Pizza wie einen Pokal vor dem Gesicht hochstemmen
                rig.localPosition = Vector3.up * (hopHeightThreeCoins * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.Euler(0f, danceTime * spinSpeed, 0f);
                SetHoldPose(125f + Mathf.Sin(t * 2f) * 15f);
                break;
            case 2:
                // Fröhlich wippen, die Pizza im Takt auf und ab
                rig.localPosition = Vector3.up * (hopHeightTwoCoins * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.identity;
                SetHoldPose(75f + Mathf.Sin(t) * 25f);
                break;
            default:
                // Kurz hin und her wiegen, die Pizza ruhig vor dem Bauch
                rig.localPosition = Vector3.zero;
                rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f) * 8f);
                SetHoldPose(70f + Mathf.Sin(t * 0.6f) * 5f);
                break;
        }
    }

    // Beide Arme gleich weit nach vorne/oben (Drehung um X: 0 = hängt, 90 = waagerecht nach vorne, 180 = senkrecht hoch)
    // und leicht nach innen (um Z), damit die Hände unter dem Karton zusammenkommen
    private void SetHoldPose(float lift, float inward = 20f)
    {
        leftArm.localRotation = Quaternion.Euler(lift, 0f, inward);
        rightArm.localRotation = Quaternion.Euler(lift, 0f, -inward);
    }

    // Mitte zwischen den beiden Handspitzen, ein Stück darüber und davor.
    // TransformPoint rechnet "Armlänge nach unten im Arm-Raum" in Weltkoordinaten um, egal wie der Arm gerade steht
    private Vector3 HoldPoint()
    {
        Vector3 leftHand = leftArm.TransformPoint(Vector3.down * armLength);
        Vector3 rightHand = rightArm.TransformPoint(Vector3.down * armLength);
        // Der Kunde schaut zur Kamera (-Z), "vorne" ist also Vector3.back im Rig-Raum
        return (leftHand + rightHand) * 0.5f + rig.up * holdAbove + rig.TransformDirection(Vector3.back) * holdForward;
    }

    // LateUpdate: nach Update, also nachdem die Arme für diesen Frame gestellt wurden
    private void LateUpdate()
    {
        if (heldCarton == null)
        {
            return;
        }
        heldCarton.position = HoldPoint();
        heldCarton.rotation = rig.rotation;
    }
}
