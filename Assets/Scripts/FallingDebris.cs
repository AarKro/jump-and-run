using UnityEngine;

// Brocken aus dem Kampf der Monster (Level 5, nach der Landung des Roboters): Kommt der Spieler an triggerU vorbei (er läuft
// nach -u), fällt ein Fassaden-Brocken aus großer Höhe auf eine feste Stelle der Bahn (die Wurzel, auf dem Boden). Vorher
// kündigt er sich an wie die Möwe (2026-10-29, vorher kam nur der Schatten, der Brocken selbst war bis kurz vor dem Aufprall
// über dem Bildrand): warnTime lang rieseln Steinchen auf die Stelle, ein Schatten erscheint und ein orangener Ring zieht sich
// dort immer wieder zusammen. Dann fällt er, der Schatten wächst weiter. Trifft der Brocken den Spieler im Fall, ist es ein
// Respawn. Danach liegt er als Stufe auf der Bahn (Collider an), bis zum nächsten Respawn.
//
// Getroffen wird von Hand geprüft (Abstand und Höhe in FixedUpdate) statt mit einem bewegten Trigger: Der Brocken fällt in
// einer Sekunde 30 m, ein Trigger könnte den Spieler zwischen zwei Physik-Schritten überspringen.
public class FallingDebris : MonoBehaviour
{
    private enum State { Waiting, Warning, Falling, Landed }

    [Header("Auslöser")]
    [Tooltip("Sobald der Spieler hier vorbei ist (u kleiner), fällt der Brocken")]
    [SerializeField] private float triggerU;
    [Tooltip("Erst, wenn der Roboter gelandet ist (vorher kämpfen die Monster noch nicht)")]
    [SerializeField] private bool needsRobot = true;

    [Header("Warnung")]
    [Tooltip("So lange rieseln Steinchen und pulsiert der Ring, bevor der Brocken fällt (s)")]
    [SerializeField] private float warnTime = 1f;
    [Tooltip("Ring am Boden, zieht sich in der Warnung und im Fall immer wieder auf die Landestelle zusammen")]
    [SerializeField] private Transform warningRing;
    [Tooltip("Rieselnde Steinchen über der Stelle (Warnung und Fall)")]
    [SerializeField] private ParticleSystem pebbles;
    [Tooltip("Ringe pro Sekunde")]
    [SerializeField] private float ringPulseRate = 2.5f;

    [Header("Fall")]
    [SerializeField] private float dropHeight = 18f;
    [Tooltip("Dauer des Falls (s), nach der Warnung")]
    [SerializeField] private float fallTime = 0.9f;
    [Tooltip("Dreht sich im Fall (Grad pro Sekunde)")]
    [SerializeField] private Vector3 spin = new Vector3(160f, 70f, 110f);
    [Tooltip("Halbe Grundfläche des Brockens entlang und quer zur Bahn (m): Treffer- und Schattengröße")]
    [SerializeField] private Vector2 halfSize = new Vector2(1.1f, 1.1f);

    [Header("Teile")]
    [Tooltip("Optik und Collider des Brockens; seine Lage im Editor ist die Ruhelage nach dem Aufprall")]
    [SerializeField] private Transform chunk;
    [SerializeField] private Collider solid;
    [SerializeField] private Transform shadow;
    [SerializeField] private ParticleSystem impactDust;
    [Tooltip("Rauchspur im Fall (optional)")]
    [SerializeField] private ParticleSystem trail;

    private State state = State.Waiting;
    private float stateTime;
    private Vector3 chunkRest;
    private Quaternion chunkRestRotation;
    private Vector3 shadowScale;
    private Vector3 ringScale;
    private Transform player;
    private Collider playerCollider;
    private KaijuBattle battle;

    private void Awake()
    {
        chunkRest = chunk.localPosition;
        chunkRestRotation = chunk.localRotation;
        if (shadow != null)
        {
            shadowScale = shadow.localScale;
        }
        if (warningRing != null)
        {
            ringScale = warningRing.localScale;
        }
        GameManager.LevelReset += ResetDebris;
        ResetDebris();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetDebris;
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
            playerCollider = controller.GetComponent<Collider>();
        }
        battle = FindAnyObjectByType<KaijuBattle>();
    }

    private void ResetDebris()
    {
        state = State.Waiting;
        stateTime = 0f;
        chunk.localPosition = chunkRest;
        chunk.localRotation = chunkRestRotation;
        chunk.gameObject.SetActive(false);
        if (solid != null)
        {
            solid.enabled = false;
        }
        if (trail != null)
        {
            trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        SetWarning(false);
        if (pebbles != null)
        {
            pebbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SetWarning(bool on)
    {
        if (warningRing != null)
        {
            warningRing.gameObject.SetActive(on);
        }
        if (shadow != null)
        {
            shadow.gameObject.SetActive(on);
        }
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            return;
        }
        GameManager game = GameManager.Instance;
        switch (state)
        {
            case State.Waiting:
                if (Lane.Project(player.position) > triggerU || (needsRobot && battle != null && !battle.RobotLanded)
                    || game == null || game.IsRespawning || game.InCutscene)
                {
                    return;
                }
                state = State.Warning;
                stateTime = 0f;
                SetWarning(true);
                if (pebbles != null)
                {
                    pebbles.Play();
                }
                break;

            case State.Warning:
                stateTime += Time.fixedDeltaTime;
                if (stateTime < warnTime)
                {
                    return;
                }
                state = State.Falling;
                stateTime = 0f;
                chunk.gameObject.SetActive(true);
                if (trail != null)
                {
                    trail.Play();
                }
                break;

            case State.Falling:
                stateTime += Time.fixedDeltaTime;
                if (stateTime >= fallTime)
                {
                    Land();
                    return;
                }
                if (HitsPlayer() && game != null && !game.IsRespawning && !game.LevelFinished && !game.InCutscene)
                {
                    game.RespawnPlayer();
                }
                break;
        }
    }

    // Wie hoch der Brocken gerade über seiner Ruhelage ist: schneller werdend (wie freier Fall, k²)
    private float Height => state == State.Falling ? dropHeight * (1f - Sq(Mathf.Clamp01(stateTime / fallTime))) : 0f;

    private static float Sq(float x) => x * x;

    // Steht der Spieler unter dem Brocken und hat ihn die Unterkante schon bis auf Kopfhöhe erreicht?
    private bool HitsPlayer()
    {
        if (playerCollider == null)
        {
            return false;
        }
        Bounds bounds = playerCollider.bounds;
        Vector3 local = transform.InverseTransformPoint(bounds.center);
        const float margin = 0.35f;   // ungefähr der Kapsel-Radius, etwas gnädiger
        if (Mathf.Abs(local.x) > halfSize.x + margin || Mathf.Abs(local.z) > halfSize.y + margin)
        {
            return false;
        }
        float chunkBottom = transform.position.y + Height;
        return chunkBottom < bounds.max.y;
    }

    private void Land()
    {
        state = State.Landed;
        chunk.localPosition = chunkRest;
        chunk.localRotation = chunkRestRotation;
        if (solid != null)
        {
            solid.enabled = true;
        }
        SetWarning(false);
        if (trail != null)
        {
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (pebbles != null)
        {
            pebbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (impactDust != null)
        {
            impactDust.Emit(60);
        }
        CameraShake.Shake(0.35f, 0.45f);
    }

    // Optik jeden Frame: In der Warnung erscheint der Schatten und wächst auf die Hälfte, im Fall auf die volle Größe; der
    // Brocken fällt und dreht sich (zur Ruhelage hin); der Ring zieht sich immer wieder vom Doppelten auf die Grundfläche
    // zusammen (wie ein Fadenkreuz, das einrastet)
    private void Update()
    {
        if (state != State.Warning && state != State.Falling)
        {
            return;
        }
        float time = stateTime + (Time.time - Time.fixedTime);
        float shadowSize;
        if (state == State.Warning)
        {
            shadowSize = Mathf.Lerp(0.1f, 0.5f, Mathf.Clamp01(time / warnTime));
        }
        else
        {
            float k = Mathf.Clamp01(time / fallTime);
            float height = dropHeight * (1f - k * k);
            chunk.localPosition = chunkRest + transform.InverseTransformDirection(Vector3.up) * height;
            // Die Drehung läuft auf die Ruhelage zu (bei k = 1 genau dort), sonst würde er beim Aufprall springen
            chunk.localRotation = chunkRestRotation * Quaternion.Euler(spin * (fallTime * (1f - k)));
            shadowSize = Mathf.Lerp(0.5f, 1f, k);
        }
        if (shadow != null)
        {
            shadow.localScale = Vector3.Scale(shadowScale, new Vector3(shadowSize, 1f, shadowSize));
        }
        if (warningRing != null)
        {
            float phase = Mathf.Repeat((state == State.Falling ? warnTime : 0f) + time, 1f / ringPulseRate) * ringPulseRate;
            float ease = 1f - (1f - phase) * (1f - phase);
            warningRing.localScale = ringScale * Mathf.Lerp(2f, 1f, ease);
        }
    }
}
