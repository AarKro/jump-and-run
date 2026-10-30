using UnityEngine;

// Hochhaus-Stück, das umkippt, wenn der Spieler kommt (Level 5, Four Cheese Avenue): Es steht anfangs aufrecht auf seinem
// Stumpf. Ist der Spieler an triggerU vorbei (er läuft nach -u), knarrt und wackelt es kurz, dann kippt es um den Drehpunkt
// (pivot, an der Bruchkante) von ihm weg und bleibt schräg auf dem Schuttberg liegen: Ab dann ist es die Rampe hinauf.
// Harmlos: Der Collider ist erst an, wenn es liegt, und es fällt vom Spieler weg. Nach einem Respawn steht es wieder.
//
// Die liegende Lage ist die, die der Pivot im Editor hat (so baut der Generator die Rampe), die stehende ist sie mal
// standingTilt um lokal Z (quer zur Bahn).
public class TopplingRuin : MonoBehaviour
{
    private enum State { Standing, Creaking, Toppling, Down }

    [SerializeField] private float triggerU;
    [Tooltip("Dreht sich um seine lokale Z-Achse; Lage im Editor = liegend")]
    [SerializeField] private Transform pivot;
    [Tooltip("Stehend = liegend mal so viel Grad um lokal Z")]
    [SerializeField] private float standingTilt = -70f;
    [Tooltip("Wackeln vor dem Kippen (s)")]
    [SerializeField] private float creakTime = 0.6f;
    [SerializeField] private float creakAngle = 2.5f;
    [Tooltip("Dauer des Kippens (s), schneller werdend")]
    [SerializeField] private float toppleTime = 1.3f;
    [Tooltip("Begehbare Collider: aus, solange es steht oder fällt")]
    [SerializeField] private Collider[] solids;
    [Tooltip("Staub am Fuß beim Knarren")]
    [SerializeField] private ParticleSystem baseDust;
    [Tooltip("Staubwolke beim Aufprall")]
    [SerializeField] private ParticleSystem impactDust;

    private State state = State.Standing;
    private float stateTime;
    private Quaternion lying;
    private Transform player;

    private Quaternion Standing => lying * Quaternion.Euler(0f, 0f, standingTilt);

    private void Awake()
    {
        lying = pivot.localRotation;
        GameManager.LevelReset += ResetRuin;
        ResetRuin();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetRuin;
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }

    private void ResetRuin()
    {
        state = State.Standing;
        stateTime = 0f;
        pivot.localRotation = Standing;
        SetSolid(false);
    }

    private void SetSolid(bool on)
    {
        foreach (Collider solid in solids)
        {
            if (solid != null)
            {
                solid.enabled = on;
            }
        }
    }

    private void Update()
    {
        switch (state)
        {
            case State.Standing:
                GameManager game = GameManager.Instance;
                if (player == null || game == null || game.IsRespawning || Lane.Project(player.position) > triggerU)
                {
                    return;
                }
                state = State.Creaking;
                stateTime = 0f;
                if (baseDust != null)
                {
                    baseDust.Emit(30);
                }
                return;

            case State.Creaking:
                stateTime += Time.deltaTime;
                // Schwankt ein paar Mal hin und her, immer weiter zur Fallrichtung
                float k = stateTime / creakTime;
                float wobble = Mathf.Sin(k * Mathf.PI * 5f) * creakAngle * (1f - k) + k * creakAngle;
                pivot.localRotation = Standing * Quaternion.Euler(0f, 0f, -Mathf.Sign(standingTilt) * wobble);
                if (stateTime >= creakTime)
                {
                    state = State.Toppling;
                    stateTime = 0f;
                }
                return;

            case State.Toppling:
                stateTime += Time.deltaTime;
                float t = Mathf.Clamp01(stateTime / toppleTime);
                // Wie ein umfallender Stab: anfangs langsam, dann immer schneller (t²)
                Quaternion start = Standing * Quaternion.Euler(0f, 0f, -Mathf.Sign(standingTilt) * creakAngle);
                pivot.localRotation = Quaternion.Slerp(start, lying, t * t);
                if (t >= 1f)
                {
                    state = State.Down;
                    pivot.localRotation = lying;
                    SetSolid(true);
                    if (impactDust != null)
                    {
                        impactDust.Emit(80);
                    }
                    CameraShake.Shake(0.6f, 0.8f);
                }
                return;
        }
    }
}
