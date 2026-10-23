using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Kamerafahrt zum Levelstart, wie eine Streckenvorschau: Beginnt hoch über dem Kunden (da muss die Pizza hin),
// fliegt die Route rückwärts entlang der Laufbahn ab (Stadt, Turm und Monster im Hintergrund) und sinkt dabei herunter,
// bis sie genau in der Pose der Spielkamera an der Pizzeria-Tür ankommt. Springen überspringt sie.
// Der GameManager startet sie (nicht nach einem Neustart wegen kalter Pizza). Solange ist die ThirdPersonCamera aus.
[RequireComponent(typeof(ThirdPersonCamera))]
public class IntroFlyover : MonoBehaviour
{
    [SerializeField] private float duration = 5f;
    [Tooltip("Kamera-Versatz am Anfang, im Lane-Rahmen (x seitlich, y hoch, z nach hinten; negativ = vor der Laufbahn)")]
    [SerializeField] private Vector3 startOffset = new Vector3(0f, 40f, -60f);
    [Tooltip("Blickpunkt am Anfang so hoch über der Laufbahn: hoch = fast waagerechter Blick über die Stadt")]
    [SerializeField] private float startLookHeight = 25f;
    [Tooltip("Anteil der Fahrt, in dem die Kamera noch oben bleibt, bevor sie heruntersinkt")]
    [Range(0f, 0.9f)]
    [SerializeField] private float stayHigh = 0.35f;
    [Tooltip("Erst nach dieser Zeit zählt Springen als Überspringen")]
    [SerializeField] private float skipDelay = 0.3f;

    // from: Ziel (Kunde), to: wo die Spielkamera am Ende hinschaut (Spieler hinter der Tür)
    public IEnumerator Play(Vector3 from, Vector3 to)
    {
        var follow = GetComponent<ThirdPersonCamera>();
        follow.enabled = false;

        float uFrom = Lane.Project(from);
        float uTo = Lane.Project(to);
        // Abstand der beiden Punkte zur Laufbahn (z.B. Spieler steht hinter der Tür, nicht auf der Bahn):
        // wird mitgeblendet, damit die Fahrt exakt am Spieler endet
        Vector3 fromShift = from - Lane.Point(uFrom);
        Vector3 toShift = to - Lane.Point(uTo);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (t > skipDelay && SkipPressed())
            {
                break;
            }
            float k = t / duration;
            // Entlang der Route: weich anfahren und abbremsen
            float along = Mathf.SmoothStep(0f, 1f, k);
            // Herunter erst nach stayHigh: so sieht man am Anfang lange über die Stadt
            float down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - stayHigh) / (1f - stayHigh)));

            float u = Mathf.Lerp(uFrom, uTo, along);
            Vector3 anchor = Lane.Point(u) + Vector3.Lerp(fromShift, toShift, along);
            Quaternion frame = Lane.Frame(u);
            Vector3 offset = Vector3.Lerp(startOffset, follow.Offset, down);
            float lookHeight = Mathf.Lerp(startLookHeight, follow.LookHeight, down);

            transform.position = anchor + frame * offset;
            transform.rotation = frame * Quaternion.LookRotation(Vector3.up * lookHeight - offset);
            yield return null;
        }

        follow.enabled = true;
        follow.SnapToTarget();
    }

    private static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }
}
