using UnityEngine;

// Holzrampe ("Kicker") vor einer Baugrube (Level 4, Crust Street): Fährt der Spieler auf dem Skateboard darüber, katapultiert
// ihn die Kante hoch und nach vorn, ohne dass er springen muss. Die Weite kommt aus seinem Tempo: Mit vollem Schwung
// (14 m/s) fliegt er über die Grube, wer davor gebremst hat, fällt hinein. Mit gehaltener Sprungtaste geht es höher.
// Zu Fuß ist sie nur eine Schräge. Der Trigger sitzt an der Kante (lokal -X, dort ist die Rampe hoch); die Rampe zeigt
// in Laufrichtung (-u), also nur auslösen, wenn er in diese Richtung fährt.
public class KickerRamp : MonoBehaviour
{
    [Tooltip("Abwurf nach oben in m/s. Höhe = v² / (2 · Schwerkraft): 17 m/s bei 40 m/s² ≈ 3.6 m. Flug bei 14 m/s ≈ 11 m " +
             "(0.43 s hoch, 0.37 s runter mit 1.6-facher Fall-Schwerkraft): 1 m Reserve über der 10-m-Grube")]
    [SerializeField] private float launchVelocity = 17f;
    [Tooltip("Mit gehaltener Sprungtaste")]
    [SerializeField] private float boostedVelocity = 19.5f;
    [Tooltip("Langsamer als das (in Rampenrichtung) löst sie nicht aus, z.B. wenn er rückwärts darüber rollt")]
    [SerializeField] private float minSpeed = 2f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || !other.TryGetComponent(out PlayerController controller) || !controller.IsSkating)
        {
            return;
        }
        Rigidbody body = other.attachedRigidbody;
        Vector3 rampDirection = -transform.right;
        if (body == null || Vector3.Dot(body.linearVelocity, rampDirection) < minSpeed)
        {
            return;
        }
        controller.Bounce(controller.JumpHeld ? boostedVelocity : launchVelocity);
    }
}
