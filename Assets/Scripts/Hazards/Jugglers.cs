using UnityEngine;

// Zwei Jongleure auf dem Markt (Level 4), einer vor der Bahn (Kameraseite), einer dahinter. Sie werfen sich Keulen zu,
// die in einem flachen Bogen auf Brusthöhe über die Bahn fliegen und sich dabei überschlagen. Eine Keule trifft = vom
// Board fallen (CrashTrigger an der Keule). Man fährt durch eine Lücke im Strom oder springt drüber.
// Zeitlich fest wie der Verkehr und die Frisbees: eine Uhr, aus der sich jede Keulen-Position berechnet. Nach einem
// Respawn fängt sie wieder bei startOffset an, alles läuft also jedes Mal gleich.
// Eine Keule: Flug A→B (flightTime), kurz in der Hand bei B (holdTime), Flug B→A, kurz bei A. Die Keulen sind
// gleichmäßig über diesen Zyklus verteilt.
public class Jugglers : MonoBehaviour
{
    [Header("Keulen (setzt MarketSetup)")]
    [Tooltip("Kinematische Rigidbodies mit Trigger, bewegt per MovePosition")]
    [SerializeField] private Rigidbody[] clubs;
    [Tooltip("Optik jeder Keule, überschlägt sich im Flug")]
    [SerializeField] private Transform[] clubVisuals;
    [Tooltip("Hände der beiden Jongleure, lokal zur Wurzel")]
    [SerializeField] private Vector3 handA;
    [SerializeField] private Vector3 handB;
    [SerializeField] private Transform armA;
    [SerializeField] private Transform armB;

    [Header("Takt")]
    [SerializeField] private float flightTime = 0.9f;
    [SerializeField] private float holdTime = 0.4f;
    [Tooltip("So hoch steigt der Bogen über die Hände. Flach halten, sonst fliegt die Keule über den Spieler hinweg")]
    [SerializeField] private float arcHeight = 0.3f;
    [Tooltip("Uhr startet hier (versetzt mehrere Paare gegeneinander)")]
    [SerializeField] private float startOffset;
    [SerializeField] private float spinsPerFlight = 1.5f;

    private float clock;

    private float Cycle => 2f * (flightTime + holdTime);

    private void Awake()
    {
        clock = startOffset;
        GameManager.LevelReset += ResetClock;
        Place();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetClock;
    }

    private void ResetClock()
    {
        clock = startOffset;
        Place();
    }

    private void FixedUpdate()
    {
        clock += Time.fixedDeltaTime;
        for (int i = 0; i < clubs.Length; i++)
        {
            clubs[i].MovePosition(transform.TransformPoint(ClubPosition(i, out _)));
        }
    }

    // Sofort hinsetzen (ohne Interpolation), z.B. beim Zurücksetzen
    private void Place()
    {
        for (int i = 0; i < clubs.Length; i++)
        {
            Vector3 position = transform.TransformPoint(ClubPosition(i, out _));
            clubs[i].position = position;
            clubs[i].transform.position = position;
        }
    }

    // Lokale Position der Keule i. flight: 0–1 im Flug (für den Überschlag), -1 in der Hand
    private Vector3 ClubPosition(int i, out float flight)
    {
        float t = Mathf.Repeat(clock + i * Cycle / clubs.Length, Cycle);
        Vector3 from = handA, to = handB;
        if (t >= flightTime + holdTime)
        {
            t -= flightTime + holdTime;
            from = handB;
            to = handA;
        }
        if (t >= flightTime)
        {
            flight = -1f;
            return to;
        }
        flight = t / flightTime;
        return Vector3.Lerp(from, to, flight) + Vector3.up * (4f * arcHeight * flight * (1f - flight));
    }

    // LateUpdate: Die Figuren (CrowdFigure) setzen ihre Arme in Update, die Wurf-Arme hier überschreiben das danach
    private void LateUpdate()
    {
        float spin = 360f * spinsPerFlight;
        for (int i = 0; i < clubs.Length && i < clubVisuals.Length; i++)
        {
            ClubPosition(i, out float flight);
            // Überschlag um die Achse quer zur Flugrichtung (lokal X = entlang der Bahn)
            clubVisuals[i].localRotation = Quaternion.Euler(flight < 0f ? 0f : flight * spin, 0f, 0f);
        }
        // Wurf-Arme: heben sich im Takt der Würfe (einer pro Keule und Richtung)
        float beat = Mathf.Sin(clock / (Cycle / clubs.Length) * Mathf.PI * 2f);
        if (armA != null)
        {
            armA.localRotation = Quaternion.Euler(70f + beat * 25f, 0f, 0f);
        }
        if (armB != null)
        {
            armB.localRotation = Quaternion.Euler(70f - beat * 25f, 0f, 0f);
        }
    }
}
