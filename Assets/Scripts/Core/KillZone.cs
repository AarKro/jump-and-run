using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }
}
