using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    // Dieser Checkpoint wurde gerade erreicht (z.B. für Effekte am Café)
    public event System.Action Activated;

    [Tooltip("Gebäude-Tür (z.B. Café): Beim Respawn läuft der Spieler aus dieser Tür. Hat Vorrang vor respawnPoint")]
    [SerializeField] private SpawnDoor door;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private GameObject inactiveVisual;
    [SerializeField] private GameObject activeVisual;

    private bool activated;

    private void Start()
    {
        UpdateVisuals(false);
    }

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
                GameManager.Instance.SetCheckpoint(respawnPoint != null ? respawnPoint : transform);
            }
        }

        UpdateVisuals(true);
        Activated?.Invoke();
    }

    // Wieder unerreicht. Nötig, weil die Stadt mit dem Café über mehrere Level (und Neustarts) geladen bleibt
    public void ResetCheckpoint()
    {
        activated = false;
        UpdateVisuals(false);
    }

    private void UpdateVisuals(bool isActive)
    {
        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!isActive);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(isActive);
        }
    }
}
