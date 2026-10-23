using UnityEngine;

// Bodengegner (z.B. der Hund): läuft hin und her und dreht an Kanten und Wänden um, ganz ohne Wegpunkte.
// Sieht den Spieler nur in Blickrichtung (Reichweite + Höhe + freie Sichtlinie) und verfolgt ihn dann,
// bis er ihn eine Weile nicht mehr sieht. Fällt nie von einer Kante: Beim Verfolgen bleibt er dort stehen.
// Bewegt wird ein kinematischer Rigidbody, nur in X (die Höhe bleibt, wie sie im Level gesetzt ist).
[RequireComponent(typeof(Rigidbody))]
public class GroundPatrol : MonoBehaviour
{
    [Header("Laufen")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 6f;
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
    private Collider bodyCollider;
    private Transform player;
    private Vector3 startPosition;
    private Vector3 flipStartScale;
    private float lostSightTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        if (flipTarget == null)
        {
            flipTarget = transform;
        }
        flipStartScale = flipTarget.localScale;
        startPosition = transform.position;
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
            float dx = player.position.x - rb.position.x;
            if (Mathf.Abs(dx) <= stoppingDistance)
            {
                return;
            }
            direction = Mathf.Sign(dx);
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

        rb.MovePosition(rb.position + Vector3.right * (direction * speed * Time.fixedDeltaTime));
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
        Vector3 toPlayer = player.position - rb.position;
        if (Mathf.Abs(toPlayer.x) > sightRange || Mathf.Abs(toPlayer.y) > sightHeight)
        {
            return false;
        }
        if (requireFacing && Mathf.Abs(toPlayer.x) > 0.1f && Mathf.Sign(toPlayer.x) != FacingDirection)
        {
            return false;
        }

        // Sichtlinie: Liegt eine Plattform oder Wand dazwischen, sieht er nichts
        Vector3 eye = rb.position + Vector3.up * 0.2f;
        return !Physics.Linecast(eye, player.position, sightBlockers, QueryTriggerInteraction.Ignore);
    }

    private bool HasGroundAhead(float direction)
    {
        Bounds bounds = bodyCollider.bounds;
        // Knapp vor der vorderen Körperkante, ein Stück über der Unterkante nach unten schauen
        Vector3 origin = new Vector3(
            bounds.center.x + direction * (bounds.extents.x + ledgeLookAhead),
            bounds.min.y + 0.1f,
            rb.position.z);
        return Physics.Raycast(origin, Vector3.down, 0.1f + ledgeCheckDepth, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private bool HasWallAhead(float direction)
    {
        Bounds bounds = bodyCollider.bounds;
        Vector3 origin = bounds.center;
        return Physics.Raycast(origin, Vector3.right * direction, bounds.extents.x + 0.05f, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void FacePlayer()
    {
        float dx = player.position.x - rb.position.x;
        if (Mathf.Abs(dx) > 0.05f)
        {
            SetFacing(Mathf.Sign(dx));
        }
    }

    private void SetFacing(float direction)
    {
        FacingDirection = direction;
        Vector3 scale = flipStartScale;
        scale.x = Mathf.Abs(flipStartScale.x) * direction;
        flipTarget.localScale = scale;
    }

    // Im Editor (Objekt ausgewählt) sichtbar machen, was der Gegner prüft: Sichtweite, Kanten-Strahl
    private void OnDrawGizmosSelected()
    {
        Collider gizmoCollider = GetComponent<Collider>();
        if (gizmoCollider == null)
        {
            return;
        }

        float facing = Application.isPlaying ? FacingDirection : (startDirection >= 0f ? 1f : -1f);
        Bounds bounds = gizmoCollider.bounds;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
        Vector3 sightCenter = transform.position + Vector3.right * (facing * sightRange / 2f);
        Gizmos.DrawCube(sightCenter, new Vector3(sightRange, sightHeight * 2f, 0.1f));

        Gizmos.color = Color.cyan;
        Vector3 ledgeOrigin = new Vector3(bounds.center.x + facing * (bounds.extents.x + ledgeLookAhead), bounds.min.y + 0.1f, transform.position.z);
        Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector3.down * (0.1f + ledgeCheckDepth));
    }
}
