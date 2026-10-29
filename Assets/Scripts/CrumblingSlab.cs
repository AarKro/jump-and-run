using UnityEngine;

// Bruchplatte (Level 5, Schutt über dem brennenden Krater auf der Four Cheese Avenue): Landet der Spieler darauf, wackelt sie
// kurz (Staub rieselt), dann bricht sie weg und stürzt in die Tiefe. Wer nicht weiterspringt, fällt mit ihr. Nach jedem
// Respawn liegt sie wieder an ihrem Platz (nur deaktiviert, nie zerstört, wie alles Rücksetzbare).
//
// Die Wurzel trägt den Collider (Layer Ground) und einen kinematischen Rigidbody, bewegt wird sie in FixedUpdate mit
// MovePosition: So fällt der Spieler, der darauf steht, ganz normal mit seiner eigenen Schwerkraft hinterher. Gewackelt wird
// nur die Optik (Kind "visual"), sonst würde der Boden unter den Füßen ruckeln.
[RequireComponent(typeof(Rigidbody))]
public class CrumblingSlab : MonoBehaviour
{
    private enum State { Idle, Shaking, Falling, Gone }

    [Header("Ablauf")]
    [Tooltip("So lange nach dem Betreten hält sie noch (s)")]
    [SerializeField] private float crumbleDelay = 0.7f;
    [Tooltip("Wackeln kurz vor dem Bruch (m)")]
    [SerializeField] private float shakeAmount = 0.07f;
    [Tooltip("Fallbeschleunigung (m/s²). Kleiner als die des Spielers, damit er sauber abhebt, wenn er springt")]
    [SerializeField] private float fallGravity = 22f;
    [Tooltip("Nach so viel Fall verschwindet sie (m)")]
    [SerializeField] private float fallDistance = 6f;
    [Tooltip("Kippt beim Fallen (Grad pro Sekunde, um die Achse quer zur Bahn)")]
    [SerializeField] private float tumble = 40f;
    [Tooltip("So weit unter der Oberkante dürfen die Füße sein und es zählt trotzdem als 'oben drauf'")]
    [SerializeField] private float topTolerance = 0.25f;

    [Header("Teile")]
    [SerializeField] private Transform visual;
    [Tooltip("Staub: rieselt beim Wackeln, Wolke beim Bruch")]
    [SerializeField] private ParticleSystem dust;

    private State state = State.Idle;
    private float stateTime;
    private float fallSpeed;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 visualRest;
    private Rigidbody body;
    private Collider solid;
    private float nextDust;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        solid = GetComponent<Collider>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        if (visual != null)
        {
            visualRest = visual.localPosition;
        }
        GameManager.LevelReset += ResetSlab;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetSlab;
    }

    private void ResetSlab()
    {
        state = State.Idle;
        stateTime = 0f;
        fallSpeed = 0f;
        // Kinematisch: direkt setzen, MovePosition würde erst im nächsten Physik-Schritt (und interpoliert) ankommen
        body.position = startPosition;
        body.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
        solid.enabled = true;
        if (visual != null)
        {
            visual.localPosition = visualRest;
            visual.gameObject.SetActive(true);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryStart(collision);
    }

    // Auch Stay: Kommt er von der Seite und rutscht dann hinauf, zählt es ebenfalls
    private void OnCollisionStay(Collision collision)
    {
        TryStart(collision);
    }

    private void TryStart(Collision collision)
    {
        if (state != State.Idle || !collision.collider.CompareTag("Player"))
        {
            return;
        }
        // Nur von oben: Unterkante des Spielers auf Höhe der Oberkante (wie beim Stampfen auf Gegner)
        if (collision.collider.bounds.min.y < solid.bounds.max.y - topTolerance)
        {
            return;
        }
        state = State.Shaking;
        stateTime = 0f;
    }

    private void FixedUpdate()
    {
        if (state == State.Idle || state == State.Gone)
        {
            return;
        }
        stateTime += Time.fixedDeltaTime;
        if (state == State.Shaking)
        {
            if (stateTime >= crumbleDelay)
            {
                state = State.Falling;
                stateTime = 0f;
                if (dust != null)
                {
                    dust.Emit(40);
                }
                CameraShake.Shake(0.12f, 0.25f);
            }
            return;
        }

        // Fallen: schneller werdend, dabei leicht kippen (um lokal Z = quer zur Bahn, also nach vorne oder hinten über)
        fallSpeed += fallGravity * Time.fixedDeltaTime;
        body.MovePosition(body.position + Vector3.down * (fallSpeed * Time.fixedDeltaTime));
        body.MoveRotation(body.rotation * Quaternion.Euler(0f, 0f, tumble * Time.fixedDeltaTime));
        if (startPosition.y - body.position.y > fallDistance)
        {
            state = State.Gone;
            solid.enabled = false;
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }
        }
    }

    // Wackeln nur in der Optik, jeden Frame (stärker, je näher der Bruch). Dazu rieselt Staub
    private void Update()
    {
        if (visual == null)
        {
            return;
        }
        if (state != State.Shaking)
        {
            visual.localPosition = visualRest;
            return;
        }
        float k = Mathf.Clamp01(stateTime / crumbleDelay);
        float amount = shakeAmount * (0.4f + 0.6f * k);
        visual.localPosition = visualRest + new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount) * 0.5f, Random.Range(-amount, amount));
        if (dust != null && Time.time >= nextDust)
        {
            nextDust = Time.time + 0.08f;
            dust.Emit(3);
        }
    }
}
