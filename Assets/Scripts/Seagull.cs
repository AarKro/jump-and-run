using UnityEngine;

// Möwe (Signatur-Gefahr von Level 2): wartet unsichtbar über dem Strand hinter der Kamera. Ist der Spieler in ihrem
// Revier, fliegt sie in festen Abständen heran, schwebt kreischend über ihm ("SKREE!", ein Schatten am Boden markiert
// die Stelle) und stößt dann im Sturzflug genau auf diese Stelle herab. Wer noch dort steht, wird getroffen (Respawn wie
// bei Hund und Auto). Die Stelle wird beim Kreischen festgelegt: Weiterlaufen oder Wegspringen rettet einen.
// Danach zieht sie über die Dächer davon und kommt nach einer Pause wieder.
//
// Ablauf als kleine Zustandsmaschine (State): Away → Approach → Warn → Dive → Leave → Away.
// Positionen werden im Lane-Rahmen am Spieler berechnet (x entlang der Bahn, y hoch, z nach hinten, negativ = Kameraseite),
// so funktioniert das auch nach einer Kurve. Gebaut von Tools > Jump and Run > Build Seagull Prefab.
[RequireComponent(typeof(Rigidbody))]
public class Seagull : MonoBehaviour
{
    private enum State { Away, Approach, Warn, Dive, Leave }

    [Header("Revier")]
    [Tooltip("Lauf-Koordinate u: Nur wenn der Spieler zwischen diesen Werten ist, greift die Möwe an")]
    [SerializeField] private float territoryFrom = -170f;
    [SerializeField] private float territoryTo = -20f;

    [Header("Takt")]
    [Tooltip("Pause nach Levelstart bzw. Respawn, bevor die erste Attacke kommt")]
    [SerializeField] private float firstDelay = 2f;
    [Tooltip("Pause zwischen zwei Attacken")]
    [SerializeField] private float cooldown = 4f;

    [Header("Flug (Lane-Rahmen relativ zum Spieler)")]
    [SerializeField] private Vector3 approachFrom = new Vector3(-6f, 12f, -16f);
    [Tooltip("Hier schwebt sie beim Kreischen: über der angepeilten Stelle, in der oberen Bildhälfte")]
    [SerializeField] private Vector3 hoverOffset = new Vector3(0f, 4.5f, -0.5f);
    [SerializeField] private Vector3 leaveOffset = new Vector3(6f, 9f, 10f);
    [SerializeField] private float approachDuration = 0.9f;
    [Tooltip("So lange kreischt sie, bevor sie zustößt: die Warnung für den Spieler")]
    [SerializeField] private float warnDuration = 0.8f;
    [SerializeField] private float diveDuration = 0.3f;
    [SerializeField] private float leaveDuration = 0.8f;

    [Header("Teile")]
    [SerializeField] private Transform model;
    [SerializeField] private Transform wingLeft;
    [SerializeField] private Transform wingRight;
    [SerializeField] private GameObject skreeBubble;
    [Tooltip("Schatten am Boden an der angepeilten Stelle. Wird beim Start abgekoppelt, damit er nicht mitfliegt")]
    [SerializeField] private Transform shadow;
    [Tooltip("Durchmesser des Schattens am Ende der Warnung. Größer als der Spieler (1 m), sonst verdeckt er ihn")]
    [SerializeField] private float shadowSize = 2.2f;
    [SerializeField] private float flapSpeed = 14f;
    [SerializeField] private float flapAngle = 35f;
    [SerializeField] private LayerMask groundLayer = 1 << 3;

    private State state;
    private float stateTime;
    private float waitTime;
    private Transform player;
    private Vector3 flightStart;
    private Vector3 target;
    private Vector3 lastPosition;
    private Collider hitCollider;

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        GameManager.LevelReset += ResetGull;
        if (shadow != null)
        {
            shadow.SetParent(null, true);
        }
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetGull;
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
        ResetGull();
    }

    private void ResetGull()
    {
        GoAway(firstDelay);
    }

    private void GoAway(float wait)
    {
        state = State.Away;
        waitTime = wait;
        SetVisible(false);
        skreeBubble.SetActive(false);
        if (shadow != null)
        {
            shadow.gameObject.SetActive(false);
        }
        hitCollider.enabled = false;
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }
        if (state != State.Away && !GameRunning())
        {
            GoAway(firstDelay);
            return;
        }

        stateTime += Time.deltaTime;
        switch (state)
        {
            case State.Away:
                // Pause nur im Revier abzählen: Sonst wären bei mehreren Möwen alle Timer abgelaufen, wenn der Spieler
                // ankommt, und alle stürzen gleichzeitig
                bool ready = GameRunning() && InTerritory();
                if (ready)
                {
                    waitTime -= Time.deltaTime;
                }
                if (waitTime <= 0f && ready)
                {
                    flightStart = PlayerRelative(approachFrom);
                    transform.position = flightStart;
                    lastPosition = flightStart;
                    SetVisible(true);
                    Enter(State.Approach);
                }
                break;

            case State.Approach:
                // Folgt dem Spieler beim Anflug, damit sie sicher über ihm ankommt
                transform.position = Vector3.Lerp(flightStart, PlayerRelative(hoverOffset), Mathf.SmoothStep(0f, 1f, stateTime / approachDuration));
                if (stateTime >= approachDuration)
                {
                    // Jetzt wird die Stelle festgelegt: Ab hier zielt sie, wo der Spieler gerade ist
                    target = player.position;
                    PlaceShadow();
                    skreeBubble.SetActive(true);
                    Enter(State.Warn);
                }
                break;

            case State.Warn:
                float bob = Mathf.Sin(stateTime * 20f) * 0.08f;
                transform.position = target + Frame(target) * hoverOffset + Vector3.up * bob;
                if (shadow != null)
                {
                    shadow.localScale = new Vector3(1f, 0.01f, 1f) * Mathf.Lerp(0.6f, shadowSize, stateTime / warnDuration) + new Vector3(0f, 0.01f, 0f);
                }
                if (stateTime >= warnDuration)
                {
                    skreeBubble.SetActive(false);
                    flightStart = transform.position;
                    hitCollider.enabled = true;
                    Enter(State.Dive);
                }
                break;

            case State.Dive:
                // Quadratisch beschleunigt: langsam los, schnell unten
                float k = Mathf.Clamp01(stateTime / diveDuration);
                transform.position = Vector3.Lerp(flightStart, target, k * k);
                if (stateTime >= diveDuration)
                {
                    hitCollider.enabled = false;
                    if (shadow != null)
                    {
                        shadow.gameObject.SetActive(false);
                    }
                    flightStart = transform.position;
                    Enter(State.Leave);
                }
                break;

            case State.Leave:
                Vector3 exit = target + Frame(target) * leaveOffset;
                transform.position = Vector3.Lerp(flightStart, exit, Mathf.SmoothStep(0f, 1f, stateTime / leaveDuration));
                if (stateTime >= leaveDuration)
                {
                    GoAway(cooldown);
                }
                break;
        }

        Animate();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (state == State.Dive && other.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    private void Enter(State next)
    {
        state = next;
        stateTime = 0f;
    }

    private bool GameRunning()
    {
        GameManager game = GameManager.Instance;
        return game != null && !game.IsRespawning && !game.InCutscene && !game.LevelFinished;
    }

    private bool InTerritory()
    {
        float u = Lane.Project(player.position);
        return u >= Mathf.Min(territoryFrom, territoryTo) && u <= Mathf.Max(territoryFrom, territoryTo);
    }

    private static Quaternion Frame(Vector3 at) => Lane.Frame(Lane.Project(at));

    private Vector3 PlayerRelative(Vector3 offset) => player.position + Frame(player.position) * offset;

    // Schatten genau auf den Boden unter der angepeilten Stelle (Gehweg, Zebrastreifen, ...)
    private void PlaceShadow()
    {
        if (shadow == null)
        {
            return;
        }
        Vector3 ground = target;
        if (Physics.Raycast(target + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
        }
        shadow.position = ground + Vector3.up * 0.02f;
        shadow.gameObject.SetActive(true);
    }

    // Schnabel in Flugrichtung, Flügelschlag (beim Kreischen schneller), Sprechblase immer zur Kamera
    private void Animate()
    {
        Vector3 velocity = transform.position - lastPosition;
        lastPosition = transform.position;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(velocity.normalized, Vector3.up), 0.3f);
        }
        float speed = state == State.Warn ? flapSpeed * 1.8f : flapSpeed;
        float angle = Mathf.Sin(Time.time * speed) * flapAngle;
        wingLeft.localRotation = Quaternion.Euler(0f, 0f, angle);
        wingRight.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void SetVisible(bool visible)
    {
        model.gameObject.SetActive(visible);
    }
}
