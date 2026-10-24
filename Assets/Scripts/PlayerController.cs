using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Beschleunigen beim Loslaufen in m/s². 30 = in gut 0.2 s auf voller Geschwindigkeit")]
    [SerializeField] private float acceleration = 30f;
    [Tooltip("Abbremsen beim Loslassen oder Umdrehen in m/s². Hoch = sofortiger Stopp, kaum Nachrutschen")]
    [SerializeField] private float deceleration = 150f;

    [Header("Sprung")]
    [Tooltip("Maximale Sprunghöhe in Metern bei gehaltener Taste")]
    [SerializeField] private float jumpHeight = 3.2f;
    [Tooltip("Sekunden bis zum höchsten Punkt. Kleiner = knackiger, größer = schwebender")]
    [SerializeField] private float timeToApex = 0.4f;
    [Tooltip("Schwerkraft-Faktor beim Fallen: schneller runter als rauf")]
    [SerializeField] private float fallGravityMultiplier = 1.6f;
    [Tooltip("Schwerkraft-Faktor, wenn die Taste beim Aufsteigen losgelassen wird (kurzer Hüpfer)")]
    [SerializeField] private float jumpCutGravityMultiplier = 3f;
    [Tooltip("Unterhalb dieser vertikalen Geschwindigkeit gilt der Player als am Scheitelpunkt")]
    [SerializeField] private float apexThreshold = 2f;
    [Tooltip("Schwerkraft-Faktor am Scheitelpunkt bei gehaltener Taste: kurzer Hang-Moment zum Zielen")]
    [SerializeField] private float apexGravityMultiplier = 0.5f;
    [SerializeField] private float maxFallSpeed = 20f;
    [Tooltip("Nach dem Verlassen einer Kante darf noch so lange gesprungen werden")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Ein zu früh gedrückter Sprung wird so lange gemerkt und bei der Landung ausgeführt")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [Tooltip("Abprall beim Draufspringen auf Gegner, als Anteil der Sprunggeschwindigkeit. Sprungtaste halten = hoher Abprall")]
    [SerializeField] private float stompBounceFraction = 0.75f;

    [Header("Bodenprüfung")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody body;
    private Vector2 moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private readonly Collider[] groundHits = new Collider[4];
    private MovingPlatform currentPlatform;
    // Horizontaler Schwung von einer beweglichen Plattform. Bleibt in der Luft erhalten,
    // damit ein Sprung von der Plattform deren Bewegung mitnimmt
    private Vector3 carryVelocity;
    private float groundVerticalVelocity;
    private float groundLockTimer;

    // So lange nach einem Sprung zählt der Boden nicht, weil die Bodenprüfung direkt nach dem Absprung noch kurz anschlägt
    private const float GroundLockAfterJump = 0.1f;
    private float coyoteTimer;
    private float jumpBufferTimer;

    // +1 = schaut nach rechts (+X, Levelrichtung), -1 = nach links. Kommt aus der Eingabe, nicht aus der
    // Geschwindigkeit, damit eine Plattform den Spieler nicht umdreht. Bleibt im Stehen beim letzten Wert.
    public float FacingDirection { get; private set; } = 1f;
    // Steht auf dem Boden (für die Lauf-/Sprung-Animation des Modells)
    public bool IsGrounded => isGrounded;

    // Ab diesem Eingabe-Anteil in X zählt es als Umdrehen (filtert leichtes Stick-Wackeln)
    private const float FacingInputThreshold = 0.1f;

    // Aus bei Lieferung oder kalter Pizza. Wird mit dem Neuladen der Szene automatisch wieder true
    private bool controlEnabled = true;
    // Nach Bounce(): solange er steigt, kein Jump Cut, damit die Höhe nicht von der Sprungtaste abhängt
    private bool ignoreJumpCut;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        GameManager.LevelReset += ResetState;
        // Schwerkraft kommt aus FixedUpdate, damit Steigen und Fallen unterschiedlich stark sein können
        body.useGravity = false;

        // Reibungsfrei: Die Geschwindigkeit wird im Code gesetzt, Reibung würde nur dagegen bremsen
        // (und mit der stärkeren Sprung-Schwerkraft entsprechend stärker). Verhindert auch Hängenbleiben an Wänden.
        PhysicsMaterial noFriction = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        GetComponent<Collider>().material = noFriction;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetState;
    }

    // Beim Respawn: Plattform-Schwung und gepufferte Eingaben vergessen
    private void ResetState()
    {
        carryVelocity = Vector3.zero;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        ignoreJumpCut = false;
    }

    // Aus Höhe und Steigzeit abgeleitet, damit man im Inspector in verständlichen Größen tunen kann
    private float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
    private float JumpVelocity => 2f * jumpHeight / timeToApex;

    private void Update()
    {
        moveInput = Vector2.zero;
        bool jumpPressed = false;
        bool held = false;

        if (!controlEnabled)
        {
            jumpHeld = false;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            moveInput = new Vector2(x, y);
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            held = keyboard.spaceKey.isPressed;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > moveInput.sqrMagnitude)
            {
                moveInput = stick;
            }
            jumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
            held |= gamepad.buttonSouth.isPressed;
        }

        jumpHeld = held;
        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
        }
    }

    private void FixedUpdate()
    {
        // Während Tod/Respawn bewegt PlayerLife den Spieler (kinematisch). Geschwindigkeiten auf einem
        // kinematischen Rigidbody zu setzen ist nicht erlaubt, also hier gar nichts tun
        if (body.isKinematic)
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            groundCheck.position,
            groundCheckRadius,
            groundHits,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
        isGrounded = hitCount > 0;

        currentPlatform = null;
        for (int i = 0; i < hitCount && currentPlatform == null; i++)
        {
            currentPlatform = groundHits[i].GetComponentInParent<MovingPlatform>();
        }
        Vector3 platformVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector3.zero;

        // Auf der Laufbahn halten: Statt Z einzufrieren (geht bei einer Kurve nicht mehr) wird der Spieler jeden
        // Physik-Schritt auf den Pfad zurückgesetzt und in den Lane-Rahmen gedreht. Ohne LanePath ist das die X-Achse
        float u = Lane.Project(body.position);
        Vector3 tangent = Lane.Tangent(u);
        SnapToLane(u);

        // Rechts/links heißt in 2.5D immer "vorwärts/rückwärts entlang der Bahn", unabhängig davon, wohin die Kamera
        // gerade schaut (in Kurven dreht sie nach). Hoch/runter (W/S) bewegt nicht, man kann nicht aus der Bahn laufen
        float along = moveInput.x;
        Vector3 desiredDirection = Mathf.Abs(along) > 0.01f ? tangent * Mathf.Sign(along) : Vector3.zero;
        if (Mathf.Abs(along) > FacingInputThreshold)
        {
            FacingDirection = Mathf.Sign(along);
        }
        Vector3 desiredVelocity = desiredDirection * moveSpeed;
        // Nur die eigene Bewegung steuern, der Plattform-Anteil kommt am Ende obendrauf
        Vector3 horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z) - carryVelocity;
        // Seitliche Anteile (quer zur Bahn) verwerfen, z.B. nach einer Kurve
        horizontalVelocity = tangent * Vector3.Dot(horizontalVelocity, tangent);

        // Loslaufen geht gleichmäßig mit "acceleration" (kein Sprung auf Startgeschwindigkeit mehr, das wirkte hektisch).
        // Bremsen geht deutlich schneller: Taste losgelassen, zu schnell, oder Umdrehen. Beim Umdrehen erst bis zum
        // Stillstand bremsen, danach beschleunigt er normal in die neue Richtung, so fühlt sich die Wende nicht träge an
        float speedInDirection = Vector3.Dot(horizontalVelocity, desiredDirection);
        bool turning = desiredDirection != Vector3.zero && speedInDirection < 0f;
        bool braking = desiredDirection == Vector3.zero || turning || horizontalVelocity.sqrMagnitude > desiredVelocity.sqrMagnitude;
        Vector3 newHorizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            turning ? Vector3.zero : desiredVelocity,
            (braking ? deceleration : acceleration) * Time.fixedDeltaTime
        );

        // Am Boden übernimmt der Player die Geschwindigkeit des Untergrunds (fester Boden = 0).
        // In der Luft bleibt der letzte Plattform-Schwung erhalten
        if (isGrounded)
        {
            carryVelocity = new Vector3(platformVelocity.x, 0f, platformVelocity.z);
        }
        newHorizontalVelocity += carryVelocity;

        body.linearVelocity = new Vector3(newHorizontalVelocity.x, body.linearVelocity.y, newHorizontalVelocity.z);

        UpdateJump(platformVelocity.y);
    }

    // Quer zur Bahn auf den Pfad zurücksetzen (kleine Korrektur pro Schritt) und in Laufrichtung drehen.
    // Die Rotation ist für die Physik gesperrt (Constraints), direkt setzen geht trotzdem
    private void SnapToLane(float u)
    {
        Vector3 back = Lane.Back(u);
        float sideways = Vector3.Dot(body.position - Lane.Point(u), back);
        if (Mathf.Abs(sideways) > 0.0001f)
        {
            body.position -= back * sideways;
        }
        body.rotation = Lane.Frame(u);
    }

    private void UpdateJump(float platformVerticalVelocity)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 velocity = body.linearVelocity;

        // Nur beim Stehen auffüllen, nicht direkt nach dem Absprung.
        // Bewusst ohne Geschwindigkeitsvergleich: auf beweglichen Plattformen weicht die Geschwindigkeit
        // des Players immer leicht von der Plattform ab, und der Sprung würde verschluckt
        groundLockTimer -= dt;
        if (isGrounded && groundLockTimer <= 0f)
        {
            coyoteTimer = coyoteTime;
            groundVerticalVelocity = platformVerticalVelocity;
        }
        else
        {
            coyoteTimer -= dt;
        }
        jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            // Eine aufsteigende Plattform gibt dem Sprung zusätzlichen Schwung mit
            velocity.y = JumpVelocity + Mathf.Max(groundVerticalVelocity, 0f);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            groundLockTimer = GroundLockAfterJump;
        }

        if (velocity.y <= 0f)
        {
            ignoreJumpCut = false;
        }

        float gravityMultiplier = 1f;
        if (velocity.y < 0f)
        {
            gravityMultiplier = fallGravityMultiplier;
        }
        else if (velocity.y > 0f && !jumpHeld && !ignoreJumpCut)
        {
            gravityMultiplier = jumpCutGravityMultiplier;
        }
        if (jumpHeld && Mathf.Abs(velocity.y) < apexThreshold && !isGrounded)
        {
            gravityMultiplier = apexGravityMultiplier;
        }

        velocity.y -= Gravity * gravityMultiplier * dt;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        body.linearVelocity = velocity;
    }

    // Keine Eingabe mehr und eigene Laufbewegung sofort stoppen. Schwerkraft und Plattform-Schwung laufen weiter,
    // damit der Spieler nicht in der Luft hängen bleibt oder von einer fahrenden Plattform rutscht
    public void Freeze()
    {
        controlEnabled = false;
        moveInput = Vector2.zero;
        jumpBufferTimer = 0f;
        if (!body.isKinematic)
        {
            Vector3 velocity = body.linearVelocity;
            body.linearVelocity = new Vector3(carryVelocity.x, velocity.y, carryVelocity.z);
        }
    }

    // Steuerung nach Freeze() wieder freigeben (z.B. nach dem Respawn, wenn er aus der Tür gelaufen ist)
    public void Unfreeze()
    {
        controlEnabled = true;
        jumpBufferTimer = 0f;
        carryVelocity = Vector3.zero;
    }

    // Abprallen nach dem Draufspringen auf einen Gegner. Wie beim Sprung entscheidet die gehaltene
    // Sprungtaste über die Höhe (ohne Taste greift die Jump-Cut-Schwerkraft und es wird ein kleiner Hüpfer)
    public void StompBounce()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = JumpVelocity * stompBounceFraction;
        body.linearVelocity = velocity;

        coyoteTimer = 0f;
        groundLockTimer = GroundLockAfterJump;
    }

    // Federndes Abprallen (Markise): immer gleich hoch, egal ob die Sprungtaste gehalten wird.
    // Höhe = v² / (2 * Schwerkraft), bei 40 m/s² und 20 m/s also etwa 5 m
    public void Bounce(float upwardVelocity)
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = upwardVelocity;
        body.linearVelocity = velocity;

        ignoreJumpCut = true;
        coyoteTimer = 0f;
        groundLockTimer = GroundLockAfterJump;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
