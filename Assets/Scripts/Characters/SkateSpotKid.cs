using UnityEngine;

// Kind im Skate-Spot an der Oregano Street (Level 4, Kulisse vor dem Board-Shop: zeigt schon mal, was das Skateboard kann).
// Ohne Physik, alles in lokalen Koordinaten des Skate-Spots (lokal X = entlang der Straße):
// - QuarterPipe: fährt über die flache Fläche hin und her, am +X-Ende die Rampe hinauf (Viertelkreis) und wieder hinunter,
//   dreht oben um. Bewegung wie ein Pendel (schnell in der Mitte, langsam an den Umkehrpunkten).
// - Ollies: rollt hin und her, springt in regelmäßigen Abständen mit dem Board hoch.
// - Watching: sitzt auf der Rampenkante, klatscht und wippt mit.
public class SkateSpotKid : MonoBehaviour
{
    public enum Trick { QuarterPipe, Ollies, Watching }

    [SerializeField] private Trick trick = Trick.QuarterPipe;
    [Tooltip("Körper (wird geneigt und gedreht) und Arme")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [Tooltip("Board unter den Füßen (fehlt beim Zuschauen)")]
    [SerializeField] private Transform board;

    [Header("Weg (lokal zum Skate-Spot)")]
    [Tooltip("Flache Strecke von x bis zum Rampenfuß")]
    [SerializeField] private float flatFromX = -4f;
    [SerializeField] private float rampStartX = 3f;
    [SerializeField] private float rampRadius = 2.4f;
    [Tooltip("So weit die Rampe hinauf (Grad des Viertelkreises, 90 = senkrecht)")]
    [SerializeField] private float rampAngle = 75f;
    [SerializeField] private float period = 3.2f;
    [SerializeField] private float ollieHeight = 0.6f;

    private Vector3 basePosition;
    private float offset;

    private void Awake()
    {
        basePosition = transform.localPosition;
        offset = Mathf.Repeat(basePosition.x * 1.7f + basePosition.z, 10f);
    }

    private void Update()
    {
        float t = Time.time + offset;
        switch (trick)
        {
            case Trick.QuarterPipe:
                QuarterPipe(t);
                break;
            case Trick.Ollies:
                Ollies(t);
                break;
            default:
                // Sitzt auf der Kante: wippt, klatscht über dem Kopf
                float clap = Mathf.Sin(t * 7f);
                leftArm.localRotation = Quaternion.Euler(150f, 0f, 15f + clap * 15f);
                rightArm.localRotation = Quaternion.Euler(150f, 0f, -15f - clap * 15f);
                rig.localRotation = Quaternion.Euler(Mathf.Sin(t * 3.5f) * 5f, 0f, 0f);
                break;
        }
    }

    // Weg: flach von flatFromX bis rampStartX, dann der Viertelkreis (Mittelpunkt über dem Rampenfuß). s = 0 … 1 entlang des
    // ganzen Wegs, als Pendel hin und her
    private void QuarterPipe(float t)
    {
        float flat = rampStartX - flatFromX;
        float arc = rampRadius * rampAngle * Mathf.Deg2Rad;
        float s = (1f - Mathf.Cos(t / period * Mathf.PI * 2f)) / 2f * (flat + arc);
        Vector3 position;
        float tilt;
        if (s <= flat)
        {
            position = new Vector3(flatFromX + s, 0f, 0f);
            tilt = 0f;
        }
        else
        {
            float angle = (s - flat) / rampRadius;
            position = new Vector3(rampStartX + Mathf.Sin(angle) * rampRadius, rampRadius - Mathf.Cos(angle) * rampRadius, 0f);
            tilt = angle * Mathf.Rad2Deg;
        }
        transform.localPosition = basePosition + position;
        // Auf der Rampe kippt er mit der Fläche (Drehung um Z: das +X-Ende hebt sich = positive Drehung kippt +Y nach -X)
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        // Fahrtrichtung aus der Ableitung des Pendels: vorwärts (+X) in der ersten Hälfte des Zyklus
        bool forward = Mathf.Sin(t / period * Mathf.PI * 2f) > 0f;
        rig.localRotation = Quaternion.Euler(0f, forward ? -90f : 90f, 0f);
        Arms(t);
    }

    private void Ollies(float t)
    {
        float x = Mathf.Sin(t / period * Mathf.PI * 2f) * 3f;
        float hopPhase = Mathf.Repeat(t / (period / 2f), 1f);
        float hop = hopPhase < 0.3f ? Mathf.Sin(hopPhase / 0.3f * Mathf.PI) * ollieHeight : 0f;
        transform.localPosition = basePosition + new Vector3(x, hop, 0f);
        bool forward = Mathf.Cos(t / period * Mathf.PI * 2f) > 0f;
        rig.localRotation = Quaternion.Euler(0f, forward ? -90f : 90f, 0f);
        // Beim Ollie hebt die Nase
        if (board != null)
        {
            board.localRotation = Quaternion.Euler(0f, 0f, hop > 0.05f ? (forward ? 12f : -12f) : 0f);
        }
        Arms(t);
    }

    // Arme seitlich ausgestreckt zum Balancieren, leicht schwankend
    private void Arms(float t)
    {
        leftArm.localRotation = Quaternion.Euler(0f, 0f, -60f + Mathf.Sin(t * 2.3f) * 15f);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, 60f + Mathf.Sin(t * 2.7f) * 15f);
    }
}
