using UnityEngine;

// Bodengegner (z.B. der Hund): läuft hin und her und dreht an Kanten und Wänden um, ganz ohne Wegpunkte.
// Sieht den Spieler nur in Blickrichtung (Reichweite + Höhe + freie Sichtlinie) und verfolgt ihn dann,
// bis er ihn eine Weile nicht mehr sieht. Fällt nie von einer Kante: Beim Verfolgen bleibt er dort stehen.
// Umdrehen dauert: erst abbremsen, dann im Stand umdrehen (Optik dreht sich über die Kamera-Seite), dann wieder
// beschleunigen. Wer über den Hund springt, hat so einen Moment Vorsprung.
// Bewegt wird ein kinematischer Rigidbody entlang der Laufbahn (Lane, Koordinate u). Die Höhe bleibt, wie sie im
// Level gesetzt ist. Ohne LanePath in der Szene ist die Bahn die X-Achse (altes Verhalten).
[RequireComponent(typeof(Rigidbody))]
public class GroundPatrol : MonoBehaviour
{
    [Header("Laufen")]
    [SerializeField] private float patrolSpeed = 1.7f;
    [SerializeField] private float chaseSpeed = 5f;
    [Tooltip("Blickrichtung beim Start: 1 = rechts (+X), -1 = links")]
    [SerializeField] private float startDirection = 1f;
    [Tooltip("Näher als das läuft er beim Verfolgen nicht mehr weiter (verhindert Zittern direkt unter dem Spieler)")]
    [SerializeField] private float stoppingDistance = 0.3f;
    [Tooltip("Beschleunigung in m/s². 20 = von 0 auf Verfolgungstempo (5 m/s) in 0.25 s")]
    [SerializeField] private float acceleration = 20f;
    [Tooltip("Bremsen in m/s². 25 = aus Verfolgungstempo in 0.2 s zum Stehen")]
    [SerializeField] private float deceleration = 25f;
    [Tooltip("So lange dauert das Umdrehen im Stand. Mit dem Abbremsen davor ~0.5 s: Zeit, nach einem Sprung " +
             "über den Hund wegzulaufen, bevor er hinterherkommt")]
    [SerializeField] private float turnDuration = 0.3f;

    [Header("Patrouillen-Bereich")]
    [Tooltip("Nur auf Patrouille: dreht an den Grenzen dieses Bereichs um, auch ohne Wand oder Kante (Enten im Park). " +
             "Beim Verfolgen darf er ihn verlassen und kehrt danach zurück")]
    [SerializeField] private bool usePatrolLimits;
    [Tooltip("Grenzen relativ zur Startposition entlang der Bahn (x = Richtung -u, y = Richtung +u), in Metern")]
    [SerializeField] private Vector2 patrolLimits = new Vector2(-10f, 10f);
    [Tooltip("So lange bleibt er an einer Bereichsgrenze stehen, bevor er umdreht (Passanten schauen sich einen Stand an)")]
    [SerializeField] private float waitAtLimits;

    [Header("Boden und Wände")]
    [SerializeField] private LayerMask groundLayer = 1 << 3;
    [Tooltip("So weit vor der Körperkante wird nach Boden gesucht. Kein Boden = Kante, umdrehen")]
    [SerializeField] private float ledgeLookAhead = 0.1f;
    [SerializeField] private float ledgeCheckDepth = 0.4f;

    [Header("Sehen")]
    [SerializeField] private float sightRange = 6f;
    [Tooltip("So viel höher oder tiefer darf der Spieler sein, damit der Gegner ihn noch sieht")]
    [SerializeField] private float sightHeight = 1.5f;
    [Tooltip("Was die Sicht verdeckt (Plattformen, Wände)")]
    [SerializeField] private LayerMask sightBlockers = 1 << 3;
    [Tooltip("So lange ohne Sichtkontakt, bis er die Verfolgung aufgibt")]
    [SerializeField] private float loseSightTime = 1.5f;

    [Header("Flattern (Enten)")]
    [Tooltip("Statt an einer Kante umzudrehen bzw. stehen zu bleiben, flattert er über die Lücke, wenn dahinter " +
             "innerhalb von maxFlapGap wieder Boden kommt (auch beim Verfolgen: über einen Bach entkommt man nicht)")]
    [SerializeField] private bool flapOverGaps;
    [SerializeField] private float maxFlapGap = 4f;
    [Tooltip("Höhe des Flatter-Bogens")]
    [SerializeField] private float flapHeight = 0.8f;
    [Tooltip("Mindesttempo beim Flattern (aus dem Stand hebt er nicht mit 0 m/s ab)")]
    [SerializeField] private float flapSpeed = 4f;

    [Header("Optik")]
    [Tooltip("Dieses Objekt wird zum Umdrehen gespiegelt (scale.x). Leer = der Gegner selbst. " +
             "Besser ein Kind mit der Optik, dann bleiben Collider/Rigidbody ungespiegelt")]
    [SerializeField] private Transform flipTarget;

    // Für den Hund: Verfolgt er gerade? Steigende Flanke = gerade entdeckt, dann bellt er
    public bool IsChasing { get; private set; }
    // Solange true, bleibt er stehen (schaut aber zum Spieler), z.B. während der Hund bellt
    public bool HoldPosition { get; set; }
    public float FacingDirection { get; private set; }
    // Für die Ente: flattert gerade über eine Lücke, und wie hoch über der normalen Höhe sie gerade ist
    public bool IsFlapping { get; private set; }
    public float HopOffset { get; private set; }
    // Position entlang der Bahn und Startwerte (die Entenküken laufen dieselbe Spur nach)
    public float U => u;
    public float StartU => startU;
    public float StartFacing => startDirection >= 0f ? 1f : -1f;
    public float PatrolSpeed => patrolSpeed;

    private Rigidbody rb;
    private BoxCollider bodyCollider;
    private Transform player;
    private Vector3 startPosition;
    private Vector3 flipStartScale;
    private Quaternion flipStartRotation;
    // Aktuelles Tempo in Blickrichtung (nie negativ: rückwärts läuft er nicht, er dreht sich erst um)
    private float speed;
    // >= 0 = dreht sich gerade um (Sekunden seit Beginn), sonst -1
    private float turnTimer = -1f;
    private float lostSightTimer;
    // Position entlang der Bahn und feste Höhe
    private float u;
    private float startU;
    private float height;
    private float hopFromU;
    private float hopToU;
    // > 0 = steht gerade an einer Bereichsgrenze und wartet, danach dreht er um
    private float waitTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<BoxCollider>();
        if (flipTarget == null)
        {
            flipTarget = transform;
        }
        flipStartScale = flipTarget.localScale;
        flipStartRotation = flipTarget.localRotation;
        startPosition = transform.position;
        startU = Lane.Project(startPosition);
        u = startU;
        height = startPosition.y;
        SetFacing(startDirection >= 0f ? 1f : -1f);
        GameManager.LevelReset += ResetPatrol;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetPatrol;
    }

    private void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }

    private void ResetPatrol()
    {
        transform.position = startPosition;
        rb.position = startPosition;
        u = startU;
        IsChasing = false;
        HoldPosition = false;
        lostSightTimer = 0f;
        speed = 0f;
        turnTimer = -1f;
        waitTimer = 0f;
        IsFlapping = false;
        HopOffset = 0f;
        flipTarget.localRotation = flipStartRotation;
        SetFacing(startDirection >= 0f ? 1f : -1f);
    }

    private void FixedUpdate()
    {
        UpdateSight();

        if (IsFlapping)
        {
            Flap();
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f)
            {
                StartTurn();
            }
            return;
        }

        if (turnTimer >= 0f)
        {
            turnTimer += Time.fixedDeltaTime;
            if (turnTimer >= turnDuration)
            {
                turnTimer = -1f;
                SetFacing(-FacingDirection);
            }
            return;
        }

        // Wohin will er und wie schnell? Patrouille: weiter in Blickrichtung. Verfolgen: zum Spieler,
        // direkt unter ihm stehen bleiben. Beim Bellen (HoldPosition) steht er, schaut aber zum Spieler
        float wanted = FacingDirection;
        float targetSpeed = patrolSpeed;
        if (IsChasing)
        {
            targetSpeed = chaseSpeed;
            float du = Lane.Project(player.position) - u;
            if (Mathf.Abs(du) <= stoppingDistance)
            {
                targetSpeed = 0f;
            }
            else
            {
                wanted = Mathf.Sign(du);
            }
        }
        if (HoldPosition)
        {
            speed = 0f;
            targetSpeed = 0f;
        }

        if (wanted != FacingDirection)
        {
            // Spieler ist hinter ihm (z.B. drübergesprungen): erst abbremsen, im Stand umdrehen
            speed = Mathf.MoveTowards(speed, 0f, deceleration * Time.fixedDeltaTime);
            if (speed <= 0f)
            {
                StartTurn();
                return;
            }
        }
        else
        {
            float rate = targetSpeed > speed ? acceleration : deceleration;
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.fixedDeltaTime);
        }
        if (speed <= 0f)
        {
            return;
        }

        // Ende des Patrouillen-Bereichs erreicht (und läuft weiter hinaus): umdrehen wie an einer Wand
        if (!IsChasing && OutsidePatrolLimits(FacingDirection))
        {
            speed = 0f;
            if (waitAtLimits > 0f)
            {
                waitTimer = waitAtLimits;
            }
            else
            {
                StartTurn();
            }
            return;
        }

        if (flapOverGaps && !HasGroundAhead(FacingDirection) && !HasWallAhead(FacingDirection) && TryFindLanding(FacingDirection, out float landing))
        {
            // Über die Lücke flattern: Ziel so, dass der ganze Körper hinter der Kante landet
            IsFlapping = true;
            hopFromU = u;
            hopToU = u + FacingDirection * (landing + HalfLength + 0.2f);
            return;
        }

        if (!HasGroundAhead(FacingDirection) || HasWallAhead(FacingDirection))
        {
            // Sofort stehen (sonst rutscht er über die Kante). Beim Patrouillieren umdrehen,
            // beim Verfolgen an der Kante stehen bleiben (umdrehen nur, wenn der Spieler hinter ihm ist)
            speed = 0f;
            if (!IsChasing)
            {
                StartTurn();
            }
            return;
        }

        // Entlang der Bahn weiter, auch durch Kurven: neue Stelle u ausrechnen, Position und Drehung daraus
        u += FacingDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(Lane.Point(u) + Vector3.up * height);
        rb.MoveRotation(Lane.Frame(u));
    }

    // Im Flattern: in Blickrichtung weiter (mindestens flapSpeed), Höhe als Parabel über die Strecke, kein Umdrehen und
    // keine Kanten-Prüfung, bis er drüben ist
    private void Flap()
    {
        speed = Mathf.Max(speed, flapSpeed);
        u = Mathf.MoveTowards(u, hopToU, speed * Time.fixedDeltaTime);
        float k = Mathf.InverseLerp(hopFromU, hopToU, u);
        HopOffset = 4f * flapHeight * k * (1f - k);
        if (k >= 1f)
        {
            IsFlapping = false;
            HopOffset = 0f;
        }
        rb.MovePosition(Lane.Point(u) + Vector3.up * (height + HopOffset));
        rb.MoveRotation(Lane.Frame(u));
    }

    private bool OutsidePatrolLimits(float direction)
    {
        if (!usePatrolLimits)
        {
            return false;
        }
        return direction > 0f ? u >= startU + patrolLimits.y : u <= startU + patrolLimits.x;
    }

    // Kommt hinter der Kante (innerhalb von maxFlapGap) wieder Boden? landing = Abstand der vorderen Körperkante bis dorthin
    private bool TryFindLanding(float direction, out float landing)
    {
        for (float d = ledgeLookAhead + 0.25f; d <= maxFlapGap; d += 0.25f)
        {
            Vector3 origin = Lane.Point(u + direction * (HalfLength + d)) + Vector3.up * (height + BottomOffset + 0.1f);
            if (Physics.Raycast(origin, Vector3.down, 0.1f + ledgeCheckDepth, groundLayer, QueryTriggerInteraction.Ignore))
            {
                landing = d;
                return true;
            }
        }
        landing = 0f;
        return false;
    }

    private void StartTurn()
    {
        speed = 0f;
        turnTimer = 0f;
    }

    // Die Dreh-Optik läuft in Update statt in FixedUpdate: Physik tickt nur 50-mal pro Sekunde, gerendert wird öfter.
    // Den Winkel nur pro Physik-Schritt zu setzen, sähe ruckelig aus. Darum wird die Zeit seit dem letzten
    // Physik-Schritt (Time.time - Time.fixedTime) dazugerechnet, so dreht er in jedem Frame ein Stück weiter.
    private void Update()
    {
        if (turnTimer < 0f)
        {
            return;
        }

        float t = Mathf.Clamp01((turnTimer + (Time.time - Time.fixedTime)) / turnDuration);
        // Über die Kamera-Seite drehen: Die Optik schaut nach lokal +X (gespiegelt -X). +90° um Y dreht +X zur Kamera (-Z),
        // für die gespiegelte Optik braucht es -90°. Nach 180° sieht er genauso aus wie gespiegelt ohne Drehung
        // (der Hund ist links/rechts symmetrisch), darum übernimmt am Ende SetFacing ohne sichtbaren Sprung
        float angle = Mathf.SmoothStep(0f, 180f, t) * FacingDirection;
        flipTarget.localRotation = flipStartRotation * Quaternion.Euler(0f, angle, 0f);
    }

    private void UpdateSight()
    {
        if (player == null)
        {
            IsChasing = false;
            return;
        }

        // Zum Entdecken muss er in Blickrichtung schauen. Wenn er schon verfolgt, behält er den Spieler
        // in alle Richtungen im Auge (sonst würde er ihn verlieren, sobald man über ihn springt)
        bool sees = CanSeePlayer(requireFacing: !IsChasing);
        if (!IsChasing)
        {
            if (sees)
            {
                IsChasing = true;
                lostSightTimer = 0f;
            }
            return;
        }

        lostSightTimer = sees ? 0f : lostSightTimer + Time.fixedDeltaTime;
        if (lostSightTimer >= loseSightTime)
        {
            IsChasing = false;
        }
    }

    private bool CanSeePlayer(bool requireFacing)
    {
        // Abstand entlang der Bahn (nicht Luftlinie), Höhe wie gehabt
        float along = Lane.Project(player.position) - u;
        float up = player.position.y - rb.position.y;
        if (Mathf.Abs(along) > sightRange || Mathf.Abs(up) > sightHeight)
        {
            return false;
        }
        if (requireFacing && Mathf.Abs(along) > 0.1f && Mathf.Sign(along) != FacingDirection)
        {
            return false;
        }
        // Mitten im Umdrehen entdeckt er niemanden (er würde sonst fertig vom Spieler wegdrehen und gleich zurück)
        if (requireFacing && turnTimer >= 0f)
        {
            return false;
        }

        // Sichtlinie: Liegt eine Plattform oder Wand dazwischen, sieht er nichts
        Vector3 eye = rb.position + Vector3.up * 0.2f;
        return !Physics.Linecast(eye, player.position, sightBlockers, QueryTriggerInteraction.Ignore);
    }

    // Halbe Körperlänge entlang der Laufrichtung (aus der Box, nicht aus den Bounds: die sind achsparallel
    // und würden in einer Kurve falsch werden)
    private float HalfLength => bodyCollider.size.x * 0.5f;
    private float BottomOffset => bodyCollider.center.y - bodyCollider.size.y * 0.5f;

    private bool HasGroundAhead(float direction)
    {
        // Knapp vor der vorderen Körperkante, ein Stück über der Unterkante nach unten schauen
        Vector3 origin = Lane.Point(u + direction * (HalfLength + ledgeLookAhead)) + Vector3.up * (height + BottomOffset + 0.1f);
        return Physics.Raycast(origin, Vector3.down, 0.1f + ledgeCheckDepth, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private bool HasWallAhead(float direction)
    {
        Vector3 origin = Lane.Point(u) + Vector3.up * (height + bodyCollider.center.y);
        return Physics.Raycast(origin, Lane.Tangent(u) * direction, HalfLength + 0.05f, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void SetFacing(float direction)
    {
        FacingDirection = direction;
        flipTarget.localRotation = flipStartRotation;
        Vector3 scale = flipStartScale;
        scale.x = Mathf.Abs(flipStartScale.x) * direction;
        flipTarget.localScale = scale;
    }

    // Im Editor (Objekt ausgewählt) sichtbar machen, was der Gegner prüft: Sichtweite entlang der Bahn, Kanten-Strahl
    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
        {
            return;
        }

        float facing = Application.isPlaying ? FacingDirection : (startDirection >= 0f ? 1f : -1f);
        float gizmoU = Application.isPlaying ? u : Lane.Project(transform.position);
        float y = transform.position.y;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Vector3 previous = Lane.Point(gizmoU) + Vector3.up * y;
        for (float d = 0.5f; d <= sightRange; d += 0.5f)
        {
            Vector3 point = Lane.Point(gizmoU + facing * d) + Vector3.up * y;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.color = Color.cyan;
        float half = box.size.x * 0.5f;
        float bottom = box.center.y - box.size.y * 0.5f;
        Vector3 ledgeOrigin = Lane.Point(gizmoU + facing * (half + ledgeLookAhead)) + Vector3.up * (y + bottom + 0.1f);
        Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector3.down * (0.1f + ledgeCheckDepth));
    }
}
