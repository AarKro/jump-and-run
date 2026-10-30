using UnityEngine;

// Fußgänger als Kulisse ohne Laufbahn (Piazza Pisa in der Stadt-Szene, dort gibt es keinen LanePath): läuft auf einem
// festen Weg aus Weltpunkten, entweder hin und her (Pause an den Enden) oder im Kreis (Pause an jedem Punkt, z.B. die
// Reisegruppe). Ohne Physik und ohne Collider, also kein Hindernis.
// Mitläufer (leader gesetzt) gehen followDistance hinter dem Anführer auf demselben Weg und bleiben stehen, wenn er
// stehen bleibt: So läuft eine Reisegruppe als Pulk hinter der Führerin her, statt dass jeder für sich an den Ecken wartet.
// Animiert wird die Figur von ihrem CrowdFigure (aus der Positionsänderung). Pillen-Figuren schauen bei Drehung 0 nach -Z.
public class PathWalker : MonoBehaviour
{
    [Tooltip("Wegpunkte in Weltkoordinaten (Höhe = Füße)")]
    [SerializeField] private Vector3[] points;
    [Tooltip("An = Rundweg (vom letzten Punkt zurück zum ersten), aus = hin und her")]
    [SerializeField] private bool loop;
    [SerializeField] private float speed = 1f;
    [Tooltip("Pause an den Enden (hin und her) bzw. an jedem Punkt (Rundweg)")]
    [SerializeField] private float waitTime = 2f;
    [Tooltip("Startet so weit (Meter) auf dem Weg, damit nicht alle gleichzeitig umdrehen")]
    [SerializeField] private float startDistance;
    [Tooltip("Beim Warten umdrehen (die Reiseführerin schaut zu ihrer Gruppe)")]
    [SerializeField] private bool turnBackWhenWaiting;
    [SerializeField] private float turnSharpness = 6f;

    [Header("Mitläufer")]
    [Tooltip("Läuft hinter diesem Walker her (gleicher Weg), statt selbst zu laufen")]
    [SerializeField] private PathWalker leader;
    [SerializeField] private float followDistance = 2f;
    [Tooltip("Seitlich versetzt zum Weg (Meter, + = rechts in Wegrichtung)")]
    [SerializeField] private float sideOffset;

    // Strecke auf einer Periode: Rundweg = einmal herum, hin und her = hin und zurück (doppelte Weglänge)
    public float Distance { get; private set; }
    public bool Waiting => leader != null ? leader.Waiting : wait > 0f;

    private float[] cumulative;
    private Vector3[] path;
    private float length;
    private float period;
    private float wait;
    private float yaw;

    private void Start()
    {
        // Rundweg: der erste Punkt kommt hinten noch einmal dran, dann ist das Schlussstück ein normales Segment
        int count = points.Length + (loop ? 1 : 0);
        path = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            path[i] = points[i % points.Length];
        }
        cumulative = new float[count];
        for (int i = 1; i < count; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }
        length = cumulative[count - 1];
        period = loop ? length : 2f * length;
        Distance = Mathf.Repeat(startDistance, period);
        Evaluate(Distance, out _, out Vector3 heading);
        yaw = YawFor(heading);
        Place();
    }

    private void Update()
    {
        if (period <= 0f)
        {
            return;
        }
        float dt = Time.deltaTime;
        if (leader != null)
        {
            Distance = Mathf.Repeat(leader.Distance - followDistance, period);
        }
        else if (wait > 0f)
        {
            wait -= dt;
        }
        else
        {
            // Bis zum nächsten Haltepunkt laufen, dort stehen bleiben
            float next = Distance + speed * dt;
            float stop = NextStop(Distance);
            if (waitTime > 0f && next >= stop)
            {
                next = stop;
                wait = waitTime;
            }
            Distance = Mathf.Repeat(next, period);
        }
        Place();
    }

    private void Place()
    {
        Vector3 position = Evaluate(Distance, out Vector3 along, out Vector3 heading);
        Vector3 right = Vector3.Cross(Vector3.up, along);
        transform.position = position + right * sideOffset;

        // Läuft: in Laufrichtung schauen. Wartet: Führerin dreht sich um, Mitläufer schauen zum Anführer
        float target = YawFor(heading);
        if (Waiting)
        {
            if (leader != null)
            {
                Vector3 toLeader = leader.transform.position - transform.position;
                target = toLeader.sqrMagnitude > 0.01f ? YawFor(toLeader) : yaw;
            }
            else
            {
                target = turnBackWhenWaiting ? YawFor(-heading) : yaw;
            }
        }
        float dt = Time.deltaTime;
        yaw = dt > 0f ? Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-turnSharpness * dt)) : target;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Punkt auf dem Weg bei Strecke s. along = Wegrichtung (für die Seite), heading = tatsächliche Laufrichtung
    private Vector3 Evaluate(float s, out Vector3 along, out Vector3 heading)
    {
        float d = s;
        float direction = 1f;
        if (!loop && s > length)
        {
            d = 2f * length - s;
            direction = -1f;
        }
        int i = 1;
        while (i < cumulative.Length - 1 && cumulative[i] < d)
        {
            i++;
        }
        Vector3 a = path[i - 1], b = path[i];
        float segment = cumulative[i] - cumulative[i - 1];
        float t = segment > 0f ? Mathf.Clamp01((d - cumulative[i - 1]) / segment) : 0f;
        along = b - a;
        along.y = 0f;
        along = along.sqrMagnitude > 0f ? along.normalized : Vector3.forward;
        heading = along * direction;
        return Vector3.Lerp(a, b, t);
    }

    // Nächster Haltepunkt hinter s: hin und her = die beiden Enden, Rundweg = jeder Wegpunkt
    private float NextStop(float s)
    {
        if (!loop)
        {
            return s < length - 0.0001f ? length : period;
        }
        for (int i = 1; i < cumulative.Length; i++)
        {
            if (cumulative[i] > s + 0.0001f)
            {
                return cumulative[i];
            }
        }
        return period;
    }

    // Die Figur schaut bei Drehung 0 nach -Z: Für Blickrichtung d also LookRotation(-d)
    private static float YawFor(Vector3 direction)
    {
        return Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;
    }
}
