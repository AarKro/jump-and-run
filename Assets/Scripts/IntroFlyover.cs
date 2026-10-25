using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Intro zum Levelstart, wie in Mario Sunshine: Erst ein Blick über die Stadt, dann schwingt die Kamera im Bogen hinunter
// zum Kunden, der vor seinem Haus wartet und winkt (die Lieferadresse wird eingeblendet). Eine Kreis-Blende (Iris)
// schließt sich um ihn, kurz schwarz, dann öffnet sie sich um die Tür der Pizzeria und der Pizzabote kommt heraus.
// Springen überspringt den Flug (die Iris läuft trotzdem). Nach "Pizza's cold!" gibt es nur Schwarz → Iris auf.
// Der GameManager ruft Prepare() (sofort schwarz, HUD aus) und dann Play() auf. Solange ist die ThirdPersonCamera aus.
[RequireComponent(typeof(ThirdPersonCamera))]
public class IntroFlyover : MonoBehaviour
{
    [Header("Übersicht (pro Level)")]
    [Tooltip("Startposition der Kamera relativ zum Kunden, in Welt-Achsen (z.B. hoch über dem Meer im Süden)")]
    [SerializeField] private Vector3 overviewOffset = new Vector3(20f, 90f, -110f);
    [Tooltip("Blickpunkt am Anfang so weit über dem Kunden: Die Kamera schaut flacher, man sieht mehr Stadt und Horizont")]
    [SerializeField] private float overviewLookAbove = 20f;
    [Tooltip("So lange schaut die Kamera über die Stadt, bevor sie losfliegt. Die Iris öffnet sich in dieser Zeit")]
    [SerializeField] private float overviewDuration = 1f;

    [Header("Anflug")]
    [Tooltip("Kamera am Ende, relativ zum Kunden im Rahmen des Ziels (x seitlich, y hoch, z nach hinten; negativ = vor dem Haus)")]
    [SerializeField] private Vector3 closeOffset = new Vector3(-2.5f, 0.8f, -6.5f);
    [SerializeField] private float swoopDuration = 3f;
    [Tooltip("Die letzten Meter fliegt die Kamera fast gerade auf den Kunden zu, so kommt sie schräg von der Seite statt von oben")]
    [SerializeField] private float approachLength = 30f;
    [Tooltip("So lange bleibt die Kamera beim winkenden Kunden, bevor sich die Iris schließt")]
    [SerializeField] private float holdDuration = 0.6f;

    [Header("Vorspann (optional, z.B. Level 5)")]
    [Tooltip("Wird vor dem Flug zum Kunden gezeigt (z.B. die Echse, die die Stadt verwüstet). Leer = kein Vorspann")]
    [SerializeField] private MonoBehaviour exposeActor;
    [Tooltip("Kamera gleitet in Welt-Koordinaten von hier nach dort und schaut dabei auf exposeLook. Danach fliegt sie von dort aus " +
             "zum Kunden (der Bogen bleibt auf dieser Höhe, bis er hinunterstößt)")]
    [SerializeField] private Vector3 exposeCameraFrom;
    [SerializeField] private Vector3 exposeCameraTo;
    [SerializeField] private Vector3 exposeLook;
    [SerializeField] private float exposeDuration = 4.5f;

    [Header("Lieferadresse")]
    [SerializeField] private string deliveryAddress = "No. 42, Pepperoni Parkway";
    [SerializeField] private float titleFadeDuration = 0.4f;

    [Header("Iris und HUD")]
    [SerializeField] private float irisOpenDuration = 0.5f;
    [SerializeField] private float irisCloseDuration = 0.5f;
    [Tooltip("So lange bleibt es zwischen Kunde und Pizzeria schwarz")]
    [SerializeField] private float blackDuration = 0.5f;
    [Tooltip("Nach einem Neustart (kalte Pizza) ohne Flug: nur so lange schwarz, dann geht die Iris an der Pizzeria auf")]
    [SerializeField] private float restartBlackDuration = 0.2f;
    [SerializeField] private float hudFadeDuration = 0.4f;

    [Tooltip("Erst nach dieser Zeit zählt Springen als Überspringen")]
    [SerializeField] private float skipDelay = 0.3f;

    private ThirdPersonCamera follow;
    private Camera view;
    private ScreenIris iris;
    private PizzaHud hud;

    // Sofort schwarz und HUD aus. Läuft im Start des GameManagers, also bevor das erste Bild gezeichnet wird:
    // So sieht man auch das Laden der Stadt nicht
    public void Prepare()
    {
        follow = GetComponent<ThirdPersonCamera>();
        view = GetComponent<Camera>();
        iris = FindAnyObjectByType<ScreenIris>();
        hud = FindAnyObjectByType<PizzaHud>();

        if (iris != null)
        {
            iris.SetClosed();
        }
        if (hud != null)
        {
            hud.SetGameplayVisible(false);
        }
    }

    // Ohne Tür kein Intro: Blende einfach weg und HUD an
    public void Release()
    {
        if (iris != null)
        {
            iris.SetOpen();
        }
        if (hud != null)
        {
            hud.SetGameplayVisible(true);
        }
    }

    // goal: Ziel mit Kunde (null = kein Flug), door: Pizzeria-Tür, aus der der Spieler gleich kommt
    public IEnumerator Play(Transform goal, SpawnDoor door, bool withFlight)
    {
        if (withFlight && goal != null)
        {
            yield return FlyToCustomer(goal);
        }
        else
        {
            yield return new WaitForSeconds(restartBlackDuration);
        }

        // Während es schwarz ist, springt die Spielkamera an die Pizzeria. Der Kreis geht dort auf, wo der Bote gleich steht
        follow.enabled = true;
        follow.SnapToTarget();
        if (iris != null)
        {
            yield return iris.Open(view, door.OutsidePosition + Vector3.up * 0.8f, irisOpenDuration);
        }
        if (hud != null)
        {
            hud.FadeInGameplay(hudFadeDuration);
        }
    }

    private IEnumerator FlyToCustomer(Transform goal)
    {
        follow.enabled = false;

        Customer customer = goal.GetComponentInChildren<Customer>();
        if (customer != null)
        {
            customer.ShowForIntro();
        }
        Vector3 focus = customer != null ? customer.IntroFocus : goal.position + Vector3.up;

        // Iris geht auf, sobald die erste Einstellung steht (beim Vorspann über diesem, sonst über der Stadt am Kunden).
        // Läuft nebenher weiter, während die Kamera schon fliegt
        Coroutine opening = null;
        bool skipped = false;
        Vector3 flightStart = focus + overviewOffset;
        float overview = overviewDuration;
        if (exposeActor is IIntroExpose expose)
        {
            transform.SetPositionAndRotation(exposeCameraFrom, Quaternion.LookRotation(exposeLook - exposeCameraFrom));
            opening = iris != null ? StartCoroutine(iris.Open(view, exposeLook, irisOpenDuration)) : null;
            expose.PlayExpose();
            for (float t = 0f; t < exposeDuration; t += Time.deltaTime)
            {
                if (t > skipDelay && SkipPressed())
                {
                    skipped = true;
                    break;
                }
                float k = Mathf.SmoothStep(0f, 1f, t / exposeDuration);
                transform.position = Vector3.Lerp(exposeCameraFrom, exposeCameraTo, k);
                transform.rotation = Quaternion.LookRotation(exposeLook - transform.position);
                yield return null;
            }
            // Der Flug zum Kunden beginnt dort, wo der Vorspann aufhört, ohne Übersichts-Pause
            flightStart = transform.position;
            overview = 0f;
        }
        else
        {
            opening = iris != null ? StartCoroutine(iris.Open(view, focus, irisOpenDuration)) : null;
        }

        // Kurve als kubische Bézier-Kurve aus 4 Punkten: Start und Ende liegen auf der Kurve, die zwei Punkte
        // dazwischen ziehen sie nur in ihre Richtung. Kontrollpunkt 2 liegt auf der Verlängerung der Endposition
        // (vom Kunden weg), dadurch kommt die Kamera zum Schluss gerade und schräg von vorne herein.
        // Kontrollpunkt 1 liegt in Starthöhe darüber: erst oben bleiben und herumschwenken, dann hinunterstoßen.
        Vector3 start = flightStart;
        // Von vorne auf den Kunden, also in seiner Drehung (nicht der des Ziels: Level 3 dreht das Picknick um,
        // die Wurzel des Ziels bleibt aber im Lane-Rahmen)
        Quaternion facing = customer != null ? customer.transform.rotation : goal.rotation;
        Vector3 end = focus + facing * closeOffset;
        Vector3 approach = end + (end - focus).normalized * approachLength;
        Vector3 high = new Vector3(approach.x, start.y, approach.z);

        float elapsed = 0f;
        bool titleShown = false;
        float total = skipped ? 0f : overview + swoopDuration + holdDuration;
        // Vom Vorspann kommend: Der Blick schwenkt erst vom Vorspann-Ziel auf den Kunden, statt zu springen
        Quaternion startRotation = transform.rotation;

        while (elapsed < total)
        {
            if (elapsed > skipDelay && SkipPressed())
            {
                break;
            }

            float swoop = Mathf.Clamp01((elapsed - overview) / swoopDuration);
            if (hud != null && swoop > 0f && !titleShown)
            {
                titleShown = true;
                hud.ShowIntroTitle(deliveryAddress, titleFadeDuration);
            }
            // SmootherStep (Ken Perlin): wie SmoothStep, aber noch weicher an beiden Enden. Losfliegen und Ankommen ohne Ruck
            float k = swoop * swoop * swoop * (swoop * (swoop * 6f - 15f) + 10f);

            transform.position = Bezier(start, high, approach, end, k);
            // Immer den Kunden im Blick, am Anfang etwas darüber (flacherer Blick über die Stadt)
            Vector3 lookPoint = focus + Vector3.up * (overviewLookAbove * (1f - k));
            Quaternion look = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = overview > 0f ? look : Quaternion.Slerp(startRotation, look, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(swoop * 3f)));

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Übersprungen, während die Iris noch aufging: das Öffnen anhalten, sonst kämpfen beide um den Radius
        if (opening != null)
        {
            StopCoroutine(opening);
        }

        if (hud != null)
        {
            hud.HideIntroTitle(irisCloseDuration);
        }
        if (iris != null)
        {
            yield return iris.Close(view, focus, irisCloseDuration);
        }
        // Im Dunkeln wieder rein, damit er herauskommt, wenn der Bote wirklich ankommt
        if (customer != null)
        {
            customer.EndIntro();
        }
        yield return new WaitForSeconds(blackDuration);
    }

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    private static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }
}
