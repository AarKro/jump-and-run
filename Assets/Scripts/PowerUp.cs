using UnityEngine;

public class PowerUp : MonoBehaviour
{
    public enum PowerUpType
    {
        Speed,
        Jump,
        TimeBonus
    }

    [SerializeField] private PowerUpType type = PowerUpType.Speed;
    [SerializeField] private float multiplier = 1.5f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float bonusSeconds = 10f;
    [Tooltip("Taucht nach dem Tod des Players wieder auf. Bei Zeit-Boni ausschalten, falls man sonst Zeit farmen kann")]
    [SerializeField] private bool respawnOnLevelReset = true;

    private void Awake()
    {
        GameManager.LevelReset += Respawn;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= Respawn;
    }

    private void Respawn()
    {
        if (respawnOnLevelReset)
        {
            gameObject.SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerController playerController = other.GetComponent<PlayerController>();

        switch (type)
        {
            case PowerUpType.Speed:
                if (playerController != null)
                {
                    playerController.ApplySpeedBoost(multiplier, duration);
                }
                break;

            case PowerUpType.Jump:
                if (playerController != null)
                {
                    playerController.ApplyJumpBoost(multiplier, duration);
                }
                break;

            case PowerUpType.TimeBonus:
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddTime(bonusSeconds);
                }
                break;
        }

        // Nur ausblenden statt zerstören, damit das Power-Up beim Respawn zurückkommen kann
        gameObject.SetActive(false);
    }
}
