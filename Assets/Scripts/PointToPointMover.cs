using UnityEngine;

// Bewegt einen kinematischen Rigidbody mit Easing zwischen zwei Punkten hin und her und wartet an den Enden.
// Startet von der Position im Editor aus. Basis für MovingPlatform, wird direkt vom Flying Enemy benutzt.
[RequireComponent(typeof(Rigidbody))]
public class PointToPointMover : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [Tooltip("Durchschnittsgeschwindigkeit in m/s. Durch das Easing ist das Objekt in der Mitte schneller und an den Enden langsamer")]
    [SerializeField] private float speed = 2f;
    [Tooltip("Wartezeit in Sekunden an jedem Endpunkt")]
    [SerializeField] private float waitTime = 0.5f;
    [Tooltip("Stärke des Anfahrens/Abbremsens. 1 = linear, 1.5 ≈ Unitys EaseInOut, höher = stärker. Spitzengeschwindigkeit in der Mitte = speed × Wert")]
    [Range(1f, 5f)]
    [SerializeField] private float easingStrength = 2.5f;

    private Rigidbody body;
    private Vector3 positionA;
    private Vector3 positionB;
    private Vector3 fromPosition;
    private Vector3 toPosition;
    private float progress;
    private float waitTimer;
    private Vector3 startPosition;
    private bool initialized;

    // Aktuelle Geschwindigkeit. Bei Plattformen liest der Player sie aus und addiert sie zu seiner
    // eigenen Bewegung, statt verschoben zu werden. So nimmt er den Schwung auch in den Sprung mit.
    public Vector3 Velocity { get; private set; }

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        GameManager.LevelReset += ResetToStart;
    }

    protected virtual void OnDestroy()
    {
        GameManager.LevelReset -= ResetToStart;
    }

    protected virtual void Start()
    {
        positionA = pointA.position;
        positionB = pointB.position;
        startPosition = body.position;
        initialized = true;
        StartFrom(startPosition);
    }

    // Zurück an die Startposition aus dem Editor, als wäre das Level neu geladen
    private void ResetToStart()
    {
        if (!initialized)
        {
            return;
        }
        StartFrom(startPosition);
        // Auch das Transform direkt setzen, sonst sieht man einen Frame lang die Interpolation "durchs Level rutschen"
        transform.position = body.position;
    }

    private void StartFrom(Vector3 position)
    {
        fromPosition = positionA;
        toPosition = positionB;

        // Von der Position im Editor aus loslaufen: Anteil der Strecke A->B bestimmen
        // und den passenden Kurven-Fortschritt dazu berechnen (Umkehrung von Ease)
        Vector3 route = positionB - positionA;
        float fraction = route.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector3.Dot(position - positionA, route) / route.sqrMagnitude)
            : 0f;

        if (fraction >= 0.999f)
        {
            // Steht schon am Ende: zurück Richtung A
            (fromPosition, toPosition) = (toPosition, fromPosition);
            fraction = 0f;
        }

        progress = InverseEase(fraction);
        body.position = Vector3.LerpUnclamped(fromPosition, toPosition, Ease(progress));

        // Steht das Objekt an einem Endpunkt, erst warten (Plattform: damit der Player aufspringen kann)
        waitTimer = fraction <= 0.001f ? waitTime : 0f;
        Velocity = Vector3.zero;
    }

    protected virtual void FixedUpdate()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            Velocity = Vector3.zero;
            return;
        }

        // Fortschritt linear in der Zeit hochzählen, die Kurve macht daraus die geglättete Position
        float duration = Vector3.Distance(fromPosition, toPosition) / Mathf.Max(speed, 0.01f);
        progress = Mathf.Min(progress + Time.fixedDeltaTime / Mathf.Max(duration, 0.01f), 1f);

        Vector3 nextPosition = Vector3.LerpUnclamped(
            fromPosition,
            toPosition,
            Ease(progress)
        );

        Velocity = (nextPosition - body.position) / Time.fixedDeltaTime;
        body.MovePosition(nextPosition);

        if (progress >= 1f)
        {
            // Richtung umdrehen und am Endpunkt warten
            (fromPosition, toPosition) = (toPosition, fromPosition);
            progress = 0f;
            waitTimer = waitTime;
        }
    }

    // Ease-in-out: t^p / (t^p + (1-t)^p). Symmetrisch, startet und endet mit Geschwindigkeit 0
    private float Ease(float t)
    {
        float a = Mathf.Pow(t, easingStrength);
        float b = Mathf.Pow(1f - t, easingStrength);
        return a / (a + b);
    }

    // Umkehrung von Ease: zu welchem Zeitpunkt t erreicht die Kurve den Wert y?
    private float InverseEase(float y)
    {
        if (y <= 0f)
        {
            return 0f;
        }
        if (y >= 1f)
        {
            return 1f;
        }
        float ratio = Mathf.Pow(y / (1f - y), 1f / easingStrength);
        return ratio / (1f + ratio);
    }

    // Strecke im Editor anzeigen, wenn das Objekt ausgewählt ist
    protected virtual void OnDrawGizmosSelected()
    {
        if (pointA == null || pointB == null)
        {
            return;
        }

        // Im Play Mode bewegen sich die Punkte als Children mit, daher die gemerkten Positionen nehmen
        Vector3 a = Application.isPlaying ? positionA : pointA.position;
        Vector3 b = Application.isPlaying ? positionB : pointB.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.15f);
        Gizmos.DrawWireSphere(b, 0.15f);
    }
}
