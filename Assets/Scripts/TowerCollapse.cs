using System.Collections;
using UnityEngine;

// Hochhaus, das die Echse im Intro von Level 5 zerschlägt (KaijuBattle.PlayExpose): Nach dem Schlag fliegen Brocken von der
// Spitze, dann sackt der Turm in sich zusammen. Der Trick: Er sinkt einfach senkrecht in den Boden (der Boden verdeckt den
// Teil darunter), immer schneller, ruckelt dabei, und am Fuß quillt eine Staubwolke auf, die den Rand verdeckt. Nicht kippen:
// Der Fassaden-Shader nimmt Farbe und Fenster aus der Lage (X/Z) der Würfelmitte, beim Kippen wechselte der Turm die Farbe.
// Zurück bleibt ein Stumpf mit Schutt ("Aftermath"), der die ganze Zeit im Turm steckt und erst auftaucht, wenn der Turm
// tiefer ist als er.
// Grundzustand ist "eingestürzt": Läuft das Intro nicht (Neustart nach "Pizza's cold!"), ist der Turm schon weg.
public class TowerCollapse : MonoBehaviour
{
    [Tooltip("Der Turm selbst (wird nach unten bewegt)")]
    [SerializeField] private Transform body;
    [SerializeField] private float height = 52f;
    [Tooltip("Stumpf und Schutt, die nach dem Einsturz stehen bleiben")]
    [SerializeField] private GameObject aftermath;
    [Tooltip("Brocken, die beim Schlag von der Spitze fliegen (sind aus, bis es so weit ist)")]
    [SerializeField] private Transform[] debris;
    [SerializeField] private ParticleSystem impactDust;
    [SerializeField] private ParticleSystem baseDust;

    [Header("Ablauf")]
    [SerializeField] private float sinkDelay = 0.35f;
    [SerializeField] private float sinkDuration = 3f;

    private Vector3[] debrisVelocity;
    private Vector3[] debrisSpin;
    private Coroutine running;

    private void Awake()
    {
        SetCollapsed();
    }

    public void SetStanding()
    {
        StopRunning();
        body.gameObject.SetActive(true);
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        aftermath.SetActive(false);
        HideDebris();
    }

    public void SetCollapsed()
    {
        StopRunning();
        body.gameObject.SetActive(false);
        aftermath.SetActive(true);
        HideDebris();
    }

    // Der Schlag trifft die Spitze: Brocken fliegen, dann sackt er zusammen
    public void Smash(Vector3 hitDirection)
    {
        StopRunning();
        running = StartCoroutine(Collapse(hitDirection));
    }

    private IEnumerator Collapse(Vector3 hitDirection)
    {
        Vector3 top = transform.position + Vector3.up * height;
        if (impactDust != null)
        {
            impactDust.transform.position = top;
            impactDust.Emit(80);
        }
        LaunchDebris(top, hitDirection);
        CameraShake.Shake(0.5f, 0.8f);

        yield return new WaitForSeconds(sinkDelay);
        // Ab jetzt steckt der Stumpf im Turm und taucht beim Einsinken auf
        aftermath.SetActive(true);
        float emitted = 0f;
        for (float t = 0f; t < sinkDuration; t += Time.deltaTime)
        {
            float k = t / sinkDuration;
            // Immer schneller (Fall), ruckelt dabei (Stockwerke geben nacheinander nach), am Ende ganz im Boden
            body.localPosition = Vector3.down * (height * k * k + Mathf.Abs(Mathf.Sin(t * 30f)) * 0.6f);
            // Staub am Fuß, gleichmäßig über die Zeit
            emitted += Time.deltaTime * 70f;
            if (baseDust != null && emitted >= 1f)
            {
                baseDust.Emit((int)emitted);
                emitted -= (int)emitted;
            }
            yield return null;
        }
        body.gameObject.SetActive(false);
        if (baseDust != null)
        {
            baseDust.Emit(120);
        }
        CameraShake.Shake(0.8f, 1.2f);
        running = null;
    }

    private void LaunchDebris(Vector3 top, Vector3 hitDirection)
    {
        if (debris == null)
        {
            return;
        }
        debrisVelocity = new Vector3[debris.Length];
        debrisSpin = new Vector3[debris.Length];
        Vector3 away = new Vector3(hitDirection.x, 0f, hitDirection.z).normalized;
        for (int i = 0; i < debris.Length; i++)
        {
            debris[i].gameObject.SetActive(true);
            debris[i].position = top + new Vector3(Random.Range(-5f, 5f), Random.Range(-4f, 0f), Random.Range(-5f, 5f));
            debrisVelocity[i] = away * Random.Range(4f, 11f) + new Vector3(Random.Range(-4f, 4f), Random.Range(-2f, 7f), Random.Range(-4f, 4f));
            debrisSpin[i] = Random.insideUnitSphere * 300f;
        }
    }

    private void Update()
    {
        if (debrisVelocity == null)
        {
            return;
        }
        float dt = Time.deltaTime;
        for (int i = 0; i < debris.Length; i++)
        {
            Transform piece = debris[i];
            if (!piece.gameObject.activeSelf)
            {
                continue;
            }
            debrisVelocity[i] += Physics.gravity * dt;
            piece.position += debrisVelocity[i] * dt;
            piece.rotation = Quaternion.Euler(debrisSpin[i] * dt) * piece.rotation;
            // Im Boden: weg (der Staub verdeckt den Aufschlag)
            if (piece.position.y < 0f)
            {
                piece.gameObject.SetActive(false);
            }
        }
    }

    private void HideDebris()
    {
        debrisVelocity = null;
        if (debris == null)
        {
            return;
        }
        foreach (Transform piece in debris)
        {
            piece.gameObject.SetActive(false);
        }
    }

    private void StopRunning()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
    }
}
