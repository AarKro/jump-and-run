using System.Collections;
using UnityEngine;

// Der Kunde am Ziel: wartet in seinem Haus (oder sitzt auf einer Picknickdecke), kommt heraus bzw. steht auf, wenn der
// Spieler nahe ist, winkt, nimmt beim Abliefern den Karton
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

    [Header("Picknick (ohne Haus)")]
    [Tooltip("Sitzt auf einer Picknickdecke statt im Haus zu warten: steht auf, wenn der Spieler nahe ist, und setzt sich " +
             "nach einem Respawn (und nach dem Intro) wieder hin")]
    [SerializeField] private bool sitsOnBlanket;
    [Tooltip("So weit sinkt der Körper im Sitzen ein (die Figur hat keine Beine)")]
    [SerializeField] private float sitDrop = 0.45f;
    [SerializeField] private float standUpDuration = 0.5f;

    [Header("Stahlträger (Level 4, ohne Haus)")]
    [Tooltip("Sitzt auf einem Stahlträger und bleibt sitzen: winkt im Sitzen, nimmt den Karton im Sitzen. Erst beim Tanz mit " +
             "3 Münzen steht er auf dem Träger auf. sitDrop dafür auf 0 (die Wurzel liegt auf dem Träger)")]
    [SerializeField] private bool seatedOnBeam;
    [Tooltip("Beine, die vom Sitz herunterbaumeln (nur auf dem Träger, die Figur hat sonst keine)")]
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

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
    // Während des Level-Intros: steht draußen und winkt in die Kamera, egal wo der Spieler ist
    private bool introWave;
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

        // Hat das Intro ihn schon nach draußen gestellt (kann im selben Frame vor diesem Start passieren), dort lassen
        if ((homeDoor != null || sitsOnBlanket || seatedOnBeam) && !introWave)
        {
            GoBackInside();
        }
    }

    // Blickpunkt für die Kamera im Intro: etwa Brusthöhe
    public Vector3 IntroFocus => transform.position + Vector3.up * 1.1f;

    // Level-Intro: wartet schon draußen vor der Tür und winkt, damit man sieht, wohin die Pizza muss
    public void ShowForIntro()
    {
        StopAllCoroutines();
        if (homeDoor != null)
        {
            homeDoor.SnapClosed();
            transform.position = homeDoor.OutsidePosition;
        }
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.identity;
        if (seatedOnBeam)
        {
            SitPose();
        }
        introWave = true;
        state = State.Waiting;
    }

    // Intro vorbei (der Bildschirm ist gerade schwarz): wieder rein, er kommt heraus, wenn der Bote wirklich da ist
    public void EndIntro()
    {
        introWave = false;
        GoBackInside();
    }

    // Startzustand mit Haus: drinnen hinter der geschlossenen Tür (unsichtbar im dunklen Raum). Beim Picknick: sitzt auf
    // der Decke. Auch beim Respawn des Spielers, solange noch nicht geliefert wurde
    private void GoBackInside()
    {
        if ((homeDoor == null && !sitsOnBlanket && !seatedOnBeam) || state == State.Receiving || state == State.Dancing)
        {
            return;
        }
        StopAllCoroutines();
        if (homeDoor != null)
        {
            homeDoor.SnapClosed();
            transform.position = homeDoor.InsidePosition;
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
        }
        else
        {
            SitPose();
        }
        state = State.Inside;
    }

    // Sitzen: Körper eingesunken und leicht nach hinten gelehnt (Drehung um X kippt den Kopf von der Kamera weg),
    // Hände liegen vorne auf den "Knien"
    private void SitPose()
    {
        rig.localPosition = Vector3.down * sitDrop;
        rig.localRotation = Quaternion.Euler(8f, 0f, 0f);
        SetHoldPose(35f, 10f);
        SetLegs(true, Mathf.Sin(Time.time * 2f) * 15f);
    }

    // Baumelnde Beine (nur auf dem Träger): hängen nach unten, schwingen gegengleich nach vorn und hinten (Grad)
    private void SetLegs(bool visible, float swing)
    {
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            if (leg != null && leg.gameObject.activeSelf != visible)
            {
                leg.gameObject.SetActive(visible);
            }
        }
        if (leftLeg != null)
        {
            leftLeg.localRotation = Quaternion.Euler(20f + swing, 0f, 0f);
        }
        if (rightLeg != null)
        {
            rightLeg.localRotation = Quaternion.Euler(20f - swing, 0f, 0f);
        }
    }

    // Picknick statt Tür: aufstehen (mit kleinem Schwung über die Standhöhe hinaus), dann warten und winken
    private IEnumerator StandUp()
    {
        state = State.ComingOut;
        for (float t = 0f; t < standUpDuration; t += Time.deltaTime)
        {
            float k = t / standUpDuration;
            float y = Mathf.Lerp(-sitDrop, 0f, Mathf.SmoothStep(0f, 1f, k)) + Mathf.Sin(k * Mathf.PI) * 0.12f;
            rig.localPosition = Vector3.up * y;
            rig.localRotation = Quaternion.Slerp(Quaternion.Euler(8f, 0f, 0f), Quaternion.identity, k);
            yield return null;
        }
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.identity;
        state = State.Waiting;
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
                if (seatedOnBeam)
                {
                    // Bleibt sitzen, baumelt mit den Beinen. Kommt der Bote, winkt er im Sitzen (Waiting)
                    SitPose();
                    if (player != null && FlatDistanceToPlayer() < comeOutDistance)
                    {
                        state = State.Waiting;
                    }
                }
                else if (player != null && FlatDistanceToPlayer() < comeOutDistance)
                {
                    StartCoroutine(homeDoor != null ? ComeOut() : StandUp());
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
        // Ungeduldig von einem Fuß auf den anderen (auf dem Träger: sitzen bleiben, Beine baumeln)
        float t = Time.time;
        if (seatedOnBeam)
        {
            SitPose();
        }
        else
        {
            rig.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * swaySpeed) * swayAngle);
        }

        bool playerClose = introWave || (player != null && FlatDistanceToPlayer() < waveDistance);
        // Winkel um Z: 0 = Arm hängt runter, -X-Arm geht mit negativen Winkeln nach außen/oben
        float leftAngle = playerClose ? -150f + Mathf.Sin(t * waveSpeed) * 25f : -8f;
        leftArm.localRotation = Quaternion.Euler(0f, 0f, leftAngle);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, 8f);
    }

    // Abstand ohne Höhe (Luftlinie am Boden). Funktioniert auf jeder Bahnrichtung, auch nach der Kurve
    private float FlatDistanceToPlayer()
    {
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void OnPizzaDelivered(int earnedCoins)
    {
        coins = earnedCoins;
        // Noch drinnen oder unterwegs (Spieler war sehr schnell): sofort draußen hinstellen bzw. aufstehen
        if (state == State.Inside || state == State.ComingOut)
        {
            StopAllCoroutines();
            if (homeDoor != null)
            {
                transform.position = homeDoor.OutsidePosition;
                homeDoor.SnapClosed();
            }
            if (!seatedOnBeam)
            {
                rig.localPosition = Vector3.zero;
            }
        }
        StartCoroutine(ReceiveCarton());
    }

    private IEnumerator ReceiveCarton()
    {
        state = State.Receiving;
        rig.localRotation = Quaternion.identity;
        if (seatedOnBeam)
        {
            rig.localPosition = Vector3.down * sitDrop;
        }
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
        if (seatedOnBeam)
        {
            UpdateBeamDance(t);
            return;
        }

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

    // Auf dem Träger: 1–2 Münzen im Sitzen (wippen, strampeln, Pizza im Takt), 3 Münzen aufstehen (Beine weg) und wie
    // gewohnt hüpfen und drehen, die Pizza hoch über dem Kopf
    private void UpdateBeamDance(float t)
    {
        Vector3 seat = Vector3.down * sitDrop;
        switch (coins)
        {
            case 3:
                SetLegs(false, 0f);
                rig.localPosition = seat + Vector3.up * (hopHeightThreeCoins * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.Euler(0f, danceTime * spinSpeed, 0f);
                SetHoldPose(125f + Mathf.Sin(t * 2f) * 15f);
                break;
            case 2:
                SetLegs(true, Mathf.Sin(t * 1.5f) * 35f);
                rig.localPosition = seat + Vector3.up * (0.08f * Mathf.Abs(Mathf.Sin(t)));
                rig.localRotation = Quaternion.Euler(6f, 0f, 0f);
                SetHoldPose(75f + Mathf.Sin(t) * 25f);
                break;
            default:
                SetLegs(true, Mathf.Sin(t * 0.6f) * 20f);
                rig.localPosition = seat;
                rig.localRotation = Quaternion.Euler(6f, 0f, Mathf.Sin(t * 0.6f) * 6f);
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
