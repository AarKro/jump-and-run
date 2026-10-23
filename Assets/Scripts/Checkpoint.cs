using UnityEngine;

public class Checkpoint : MonoBehaviour
{
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
            GameManager.Instance.SetCheckpoint(respawnPoint != null ? respawnPoint : transform);
        }

        UpdateVisuals(true);
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
