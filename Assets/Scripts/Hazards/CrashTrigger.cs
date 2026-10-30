using UnityEngine;

// Trigger, der den Spieler vom Skateboard wirft (PlayerLife.BoardCrash), z.B. die Keulen der Jongleure auf dem Markt.
// Zu Fuß passiert nichts. Gegenstück zur KillZone, nur ohne Respawn
public class CrashTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || !other.TryGetComponent(out PlayerController controller) || !controller.IsSkating)
        {
            return;
        }
        if (other.TryGetComponent(out PlayerLife life))
        {
            life.BoardCrash();
        }
    }
}
