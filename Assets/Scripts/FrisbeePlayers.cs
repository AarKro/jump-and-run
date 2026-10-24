using UnityEngine;

// Zwei Leute auf dem Parkweg (Level 3), die sich eine Frisbee zuwerfen: die Spezial-Gefahr von Level 3.
// Die Leute sind feste Hindernisse (Collider, man kann auf ihnen stehen), die Frisbee ist ein Trigger mit KillZone
// (Berührung = Respawn). Sie fliegt flach auf Brusthöhe hin und her, dazwischen hält der Fänger sie kurz.
//
// Der Takt ist deterministisch wie bei den Autos: Aus der Uhr seit dem Levelstart wird direkt ausgerechnet, wo die
// Frisbee gerade ist. Nach jedem Respawn startet die Uhr neu, man sieht also immer dieselbe Situation.
// Aufgebaut von Tools > Jump and Run > City > Build Park Prefabs.
public class FrisbeePlayers : MonoBehaviour
{
    [Header("Takt")]
    [Tooltip("Flugzeit von einer Hand zur anderen")]
    [SerializeField] private float flightTime = 1f;
    [Tooltip("So lange hält der Fänger die Frisbee, bevor er zurückwirft")]
    [SerializeField] private float catchPause = 0.7f;
    [Tooltip("Verschiebt den Takt (Sekunden), damit zwei Paare nicht im Gleichschritt werfen")]
    [SerializeField] private float startOffset;
    [Tooltip("So hoch steigt die Frisbee in der Mitte über die gerade Linie zwischen den Händen")]
    [SerializeField] private float arcHeight = 0.3f;

    [Header("Teile")]
    [Tooltip("Hand-Punkte (lokal): Abwurf und Fang. A steht bei -X, B bei +X")]
    [SerializeField] private Vector3 handA = new Vector3(-3.5f, 1.7f, 0f);
    [SerializeField] private Vector3 handB = new Vector3(3.5f, 1.7f, 0f);
    [Tooltip("Die Frisbee: kinematischer Rigidbody, Trigger-Collider mit KillZone")]
    [SerializeField] private Rigidbody frisbee;
    [Tooltip("Die Scheibe selbst (dreht sich im Flug)")]
    [SerializeField] private Transform disc;
    [Tooltip("Wurfarm von A und B (Drehpunkt an der Schulter, Drehung um X: 0 = hängt, 90 = nach vorne)")]
    [SerializeField] private Transform armA;
    [SerializeField] private Transform armB;
    [SerializeField] private float spinSpeed = 900f;

    [Header("Farben")]
    [SerializeField] private Color shirtA = new Color(0.95f, 0.55f, 0.1f);
    [SerializeField] private Color shirtB = new Color(0.45f, 0.3f, 0.75f);
    [SerializeField] private Color skinA = new Color(0.96f, 0.78f, 0.62f);
    [SerializeField] private Color skinB = new Color(0.55f, 0.36f, 0.24f);
    [SerializeField] private Renderer[] shirtRenderersA;
    [SerializeField] private Renderer[] shirtRenderersB;
    [SerializeField] private Renderer[] skinRenderersA;
    [SerializeField] private Renderer[] skinRenderersB;

    private float clock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private float Period => 2f * (flightTime + catchPause);

    private void Awake()
    {
        GameManager.LevelReset += ResetClock;
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetClock;
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    private void ResetClock()
    {
        clock = 0f;
        frisbee.position = FrisbeeWorldPosition(0f);
        frisbee.transform.position = frisbee.position;
    }

    // Wie beim Kunden: Farbe pro Renderer über einen MaterialPropertyBlock, alle teilen sich Customer.mat
    private void ApplyColors()
    {
        SetColor(shirtRenderersA, shirtA);
        SetColor(shirtRenderersB, shirtB);
        SetColor(skinRenderersA, skinA);
        SetColor(skinRenderersB, skinB);
    }

    private static void SetColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }
        var block = new MaterialPropertyBlock();
        foreach (Renderer target in renderers)
        {
            if (target == null)
            {
                continue;
            }
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }

    // Bewegt wird in FixedUpdate (Physik), damit der Trigger die Frisbee dort trifft, wo sie gezeichnet wird
    private void FixedUpdate()
    {
        clock += Time.fixedDeltaTime;
        frisbee.MovePosition(FrisbeeWorldPosition(clock));
    }

    // Ein Takt: A wirft (Flug), B hält (Pause), B wirft zurück (Flug), A hält (Pause)
    private Vector3 FrisbeeWorldPosition(float time)
    {
        float t = Mathf.Repeat(time + startOffset, Period);
        Vector3 local;
        if (t < flightTime)
        {
            local = Flight(handA, handB, t / flightTime);
        }
        else if (t < flightTime + catchPause)
        {
            local = handB;
        }
        else if (t < 2f * flightTime + catchPause)
        {
            local = Flight(handB, handA, (t - flightTime - catchPause) / flightTime);
        }
        else
        {
            local = handA;
        }
        return transform.TransformPoint(local);
    }

    // Gerade Linie plus flacher Bogen (Parabel: 0 an den Enden, arcHeight in der Mitte)
    private Vector3 Flight(Vector3 from, Vector3 to, float k)
    {
        return Vector3.Lerp(from, to, k) + Vector3.up * (4f * arcHeight * k * (1f - k));
    }

    // Arme und Drehung der Scheibe: nur Optik, darum in Update (jeder Frame)
    private void Update()
    {
        float t = Mathf.Repeat(clock + startOffset, Period);
        bool flying = t < flightTime || (t >= flightTime + catchPause && t < 2f * flightTime + catchPause);
        if (flying)
        {
            disc.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        // A wirft bei t = 0 und fängt bei t = Period - catchPause ... Zeit relativ zum eigenen Wurf ausrechnen
        armA.localRotation = Quaternion.Euler(ArmAngle(t), 0f, 0f);
        armB.localRotation = Quaternion.Euler(ArmAngle(Mathf.Repeat(t - flightTime - catchPause, Period)), 0f, 0f);
    }

    // Armwinkel für eine Person, s = Zeit seit ihrem eigenen Wurf (0 = Abwurf). Ablauf: Wurf (Arm schwingt nach vorne
    // und sinkt wieder), warten, Frisbee kommt (Arm streckt sich zum Fangen), halten, ausholen (Arm nach hinten), Wurf
    private float ArmAngle(float s)
    {
        float catchAt = flightTime + catchPause + flightTime;   // dann kommt die eigene Frisbee zurück
        float throwAgain = Period;
        if (s < 0.3f)
        {
            return Mathf.Lerp(100f, 20f, s / 0.3f);                       // Nachschwingen nach dem Wurf
        }
        if (s < catchAt - 0.4f)
        {
            return 10f;                                                   // Hand unten, schaut zu
        }
        if (s < catchAt)
        {
            return Mathf.Lerp(10f, 85f, (s - (catchAt - 0.4f)) / 0.4f);   // zum Fangen nach vorne strecken
        }
        if (s < throwAgain - 0.25f)
        {
            return Mathf.Lerp(85f, -40f, Mathf.InverseLerp(catchAt, throwAgain - 0.25f, s));   // fangen, dann ausholen
        }
        return Mathf.Lerp(-40f, 100f, (s - (throwAgain - 0.25f)) / 0.25f);                  // Wurf
    }
}
