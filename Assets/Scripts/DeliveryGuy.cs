using UnityEngine;

// Animiert das Modell des Pizzaboten (gebaut von PlayerModelSetup), ganz ohne Animator: nur Drehpunkte, die per Code
// gedreht werden ("prozedurale Animation"). Die Physik (Kugel-Collider, Rigidbody) weiß davon nichts, das Modell ist reine Optik.
//
// - Dreht sich ganz in Laufrichtung (±90°, reines Profil). Läuft er in die Tiefe (aus der Tür, zur Mikrowelle), schaut er in diese Richtung.
// - Laufzyklus: Beine schwingen gegengleich, der Körper wippt. Die Phase wächst mit der gelaufenen Strecke,
//   dadurch passen die Schritte zur Geschwindigkeit und rutschen nicht.
// - In der Luft: ein Bein angezogen, eins gestreckt.
// - Arme halten den Karton vor dem Bauch. Hat er ihn nicht (Mikrowelle, Kunde), hängen sie und schwingen mit.
//
// Die Geschwindigkeit kommt aus der Positionsänderung, nicht aus dem Rigidbody: Beim Rauslaufen aus der Tür ist er
// kinematisch und wird per MovePosition geschoben, dort ist body.linearVelocity 0.
public class DeliveryGuy : MonoBehaviour
{
    [Header("Teile (vom Setup gesetzt)")]
    [Tooltip("Dreht sich um die Hochachse in Blickrichtung")]
    [SerializeField] private Transform model;
    [Tooltip("Wippt beim Laufen")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform legLeft;
    [SerializeField] private Transform legRight;
    [SerializeField] private Transform armLeft;
    [SerializeField] private Transform armRight;
    [SerializeField] private PizzaCarton carton;

    [Header("Drehen")]
    [Tooltip("Wie weit er sich von der Kamera weg in Laufrichtung dreht. 90 = reines Profil, 0 = frontal")]
    [SerializeField] private float facingYaw = 90f;
    [SerializeField] private float turnSharpness = 15f;

    [Header("Laufen")]
    [Tooltip("Strecke in Metern für einen ganzen Schrittzyklus (links + rechts)")]
    [SerializeField] private float strideLength = 1.8f;
    [SerializeField] private float legSwing = 40f;
    [Tooltip("Ab dieser Geschwindigkeit schwingen die Beine voll aus")]
    [SerializeField] private float fullSwingSpeed = 5f;
    [SerializeField] private float bobHeight = 0.06f;

    [Header("Sprung")]
    [SerializeField] private float jumpLegForward = 45f;
    [SerializeField] private float jumpLegBack = -25f;

    [Header("Arme")]
    [Tooltip("Arme nach vorne gestreckt, um den Karton zu halten (Drehung um X, 0 = hängend)")]
    [SerializeField] private float holdAngle = 70f;
    [SerializeField] private float holdInward = 10f;
    [SerializeField] private float poseSharpness = 18f;

    private PlayerController player;
    private Rigidbody body;
    private Vector3 lastPosition;
    private Vector3 rigStart;
    private float speed;
    private float phase;
    private float yaw;
    private float legLeftAngle, legRightAngle;
    private float armAngle, armInward, armSwing;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
        lastPosition = transform.position;
        rigStart = rig.localPosition;
        yaw = -facingYaw;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // Geschwindigkeit im Bahn-Rahmen (lokal X = Laufrichtung, Z = Tiefe). Teleports (Respawn) ergeben einen
        // Ausreißer, darum begrenzen und glätten
        Vector3 velocity = Quaternion.Inverse(transform.rotation) * ((transform.position - lastPosition) / dt);
        lastPosition = transform.position;
        Vector2 flat = new Vector2(velocity.x, velocity.z);
        float targetSpeed = Mathf.Min(flat.magnitude, 15f);
        // Exponentielles Glätten: unabhängig von der Framerate gleich schnell (wie rotationSharpness der Kamera)
        float blend = 1f - Mathf.Exp(-12f * dt);
        speed = Mathf.Lerp(speed, targetSpeed, blend);

        UpdateFacing(flat, dt);

        // Kinematisch = Skript bewegt ihn (Tür, Tod), dort läuft keine Bodenprüfung, er gilt als am Boden
        bool grounded = body.isKinematic || player.IsGrounded;
        float swingAmount = Mathf.Clamp01(speed / fullSwingSpeed);
        float targetLeft, targetRight;
        if (grounded)
        {
            // 2π pro Schrittzyklus: Die Phase hängt an der Strecke, nicht an der Zeit
            phase = Mathf.Repeat(phase + speed / strideLength * 2f * Mathf.PI * dt, 2f * Mathf.PI);
            float swing = Mathf.Sin(phase) * legSwing * swingAmount;
            targetLeft = swing;
            targetRight = -swing;
            // Wippen: zweimal pro Zyklus hoch (bei jedem Schritt), darum der Betrag
            rig.localPosition = rigStart + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobHeight * swingAmount);
        }
        else
        {
            targetLeft = jumpLegForward;
            targetRight = jumpLegBack;
            rig.localPosition = Vector3.Lerp(rig.localPosition, rigStart, blend);
        }

        float pose = 1f - Mathf.Exp(-poseSharpness * dt);
        legLeftAngle = Mathf.Lerp(legLeftAngle, targetLeft, pose);
        legRightAngle = Mathf.Lerp(legRightAngle, targetRight, pose);
        legLeft.localRotation = Quaternion.Euler(legLeftAngle, 0f, 0f);
        legRight.localRotation = Quaternion.Euler(legRightAngle, 0f, 0f);

        UpdateArms(grounded ? -Mathf.Sin(phase) * legSwing * 0.6f * swingAmount : 0f, pose);
    }

    private void UpdateFacing(Vector2 flat, float dt)
    {
        float targetYaw = -facingYaw * player.FacingDirection;
        // Läuft er deutlich in die Tiefe, schaut er dorthin. Das Modell schaut bei Yaw 0 zur Kamera (-Z):
        // Richtung (x, z) entspricht Yaw = atan2(-x, -z)
        if (speed > 0.5f && Mathf.Abs(flat.y) > Mathf.Abs(flat.x))
        {
            targetYaw = Mathf.Atan2(-flat.x, -flat.y) * Mathf.Rad2Deg;
        }
        // LerpAngle nimmt den kürzeren Weg (z.B. von 170° nach -170° über 180°, nicht zurück über 0°)
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-turnSharpness * dt));
        model.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Arme: mit Karton nach vorne gestreckt (Hände unter dem Karton), ohne hängend und gegengleich zu den Beinen
    private void UpdateArms(float walkSwing, float pose)
    {
        bool holding = carton != null && carton.enabled;
        armAngle = Mathf.Lerp(armAngle, holding ? holdAngle : 0f, pose);
        armInward = Mathf.Lerp(armInward, holding ? holdInward : 0f, pose);
        armSwing = Mathf.Lerp(armSwing, holding ? 0f : walkSwing, pose);
        // Euler wendet erst Z (nach innen kippen), dann X (nach vorne heben) an. Der linke Arm sitzt bei -X,
        // nach innen ist für ihn +Z, für den rechten -Z
        armLeft.localRotation = Quaternion.Euler(armAngle + armSwing, 0f, armInward);
        armRight.localRotation = Quaternion.Euler(armAngle - armSwing, 0f, -armInward);
    }
}
