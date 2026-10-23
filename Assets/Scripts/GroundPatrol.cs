using UnityEngine;

// Bodengegner (z.B. der Hund): läuft hin und her und dreht an Kanten und Wänden um, ganz ohne Wegpunkte.
// Sieht den Spieler nur in Blickrichtung (Reichweite + Höhe + freie Sichtlinie) und verfolgt ihn dann,
// bis er ihn eine Weile nicht mehr sieht. Fällt nie von einer Kante: Beim Verfolgen bleibt er dort stehen.
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

    [Header("Optik")]
    [Tooltip("Dieses Objekt wird zum Umdrehen gespiegelt (scale.x). Leer = der Gegner selbst. " +
             "Besser ein Kind mit der Optik, dann bleiben Collider/Rigidbody ungespiegelt")]
    [SerializeField] private Transform flipTarget;

    // Für den Hund: Verfolgt er gerade? Steigende Flanke = gerade entdeckt, dann bellt er
    public bool IsChasing { get; private set; }
    // Solange true, bleibt er stehen (schaut aber zum Spieler), z.B. während der Hund bellt
    public bool HoldPosition { get; set; }
    public float FacingDirection { get; private set; }

    private Rigidbody rb;
    private BoxCollider bodyCollider;
    private Transform player;
    private Vector3 startPosition;
    private Vector3 flipStartScale;
    private float lostSightTimer;
    // Position entlang der Bahn und feste Höhe
    private float u;
    private float startU;
    private float height;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<BoxCollider>();
        if (flipTarget == null)
        {
            flipTarget = transform;
        }
        flipStartScale = flipTarget.localScale;
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
        SetFacing(startDirection >= 0f ? 1f : -1f);
    }

    private void FixedUpdate()
    {
        UpdateSight();

        if (HoldPosition)
        {
            if (IsChasing)
            {
                FacePlayer();
            }
            return;
        }

        float direction = FacingDirection;
        float speed = patrolSpeed;
        if (IsChasing)
        {
            speed = chaseSpeed;
            float du = Lane.Project(player.position) - u;
            if (Mathf.Abs(du) <= stoppingDistance)
            {
                return;
            }
            direction = Mathf.Sign(du);
            SetFacing(direction);
        }

        if (!HasGroundAhead(direction) || HasWallAhead(direction))
        {
            // Beim Patrouillieren umdrehen. Beim Verfolgen an der Kante stehen bleiben und weiter zum Spieler schauen
            if (!IsChasing)
            {
                SetFacing(-direction);
            }
            return;
        }

        // Entlang der Bahn weiter, auch durch Kurven: neue Stelle u ausrechnen, Position und Drehung daraus
        u += direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(Lane.Point(u) + Vector3.up * height);
        rb.MoveRotation(Lane.Frame(u));
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

    private void FacePlayer()
    {
        float du = Lane.Project(player.position) - u;
        if (Mathf.Abs(du) > 0.05f)
        {
            SetFacing(Mathf.Sign(du));
        }
    }

    private void SetFacing(float direction)
    {
        FacingDirection = direction;
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
