using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    // Dieser Checkpoint wurde gerade erreicht (z.B. für Effekte am Café)
    public event System.Action Activated;

    [Tooltip("Gebäude-Tür (z.B. Café): Beim Respawn läuft der Spieler aus dieser Tür")]
    [SerializeField] private SpawnDoor door;

    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player"))
        {
            return;
        }

        activated = true;

        if (GameManager.Instance != null)
        {
            if (door != null)
            {
                GameManager.Instance.SetCheckpoint(door);
            }
            else
            {
                Debug.LogWarning($"Checkpoint {name}: keine Tür gesetzt, der Respawn bleibt an der alten Tür.");
            }
        }

        Activated?.Invoke();
    }

    // Wieder unerreicht. Nötig, weil die Stadt mit dem Café über mehrere Level (und Neustarts) geladen bleibt
    public void ResetCheckpoint()
    {
        activated = false;
    }
}
