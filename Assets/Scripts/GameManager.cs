using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Wird bei jedem Respawn ausgelöst: Gegner, Power-Ups und Plattformen setzen sich darauf hin zurück.
    // Static, damit sich Objekte im Awake anmelden können, egal ob der GameManager schon existiert
    public static event System.Action LevelReset;

    // Pizza abgeliefert, mit Trinkgeld in Münzen (1-3). Das HUD zeigt darauf das Ergebnis an
    public static event System.Action<int> PizzaDelivered;
    // Wärme ist auf 0: Pizza ist kalt, kurz darauf startet das Level neu
    public static event System.Action PizzaCold;

    // Ab dieser Restwärme (Anteil 0-1) gibt es 2 bzw. 3 Münzen Trinkgeld, darunter (aber über 0) 1 Münze
    public const float TwoCoinHeat = 1f / 3f;
    public const float ThreeCoinHeat = 2f / 3f;

    [Header("Player und Start")]
    [SerializeField] private GameObject player;
    [SerializeField] private Transform startPoint;

    [Header("Wärme (Pizza wird kalt)")]
    [Tooltip("So viele Sekunden bleibt die Pizza warm. Bei 0 ist sie kalt und das Level startet neu.")]
    [SerializeField] private float levelTime = 120f;
    [Tooltip("So lange steht \"Pizza's cold!\" da, bevor das Level neu startet")]
    [SerializeField] private float coldRestartDelay = 1.5f;

    public float RemainingTime { get; private set; }
    // Restwärme als Anteil: 1 = frisch aus dem Ofen, 0 = kalt. Kann durch Wärme-Power-Ups über 1 gehen
    public float HeatPercent => levelTime > 0f ? RemainingTime / levelTime : 0f;
    // Geliefert oder kalt: Wärme läuft nicht mehr weiter, Respawns werden ignoriert
    public bool LevelFinished { get; private set; }

    private PlayerController playerController;

    private Transform currentRespawnPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RemainingTime = levelTime;
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }
    }

    private void Start()
    {
        currentRespawnPoint = startPoint;

        if (player != null && currentRespawnPoint != null)
        {
            PlacePlayerAt(currentRespawnPoint);
        }
    }

    private void Update()
    {
        if (LevelFinished)
        {
            return;
        }

        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);

        if (RemainingTime <= 0f)
        {
            HandleTimeUp();
        }
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        currentRespawnPoint = checkpoint;
    }

    public void RespawnPlayer()
    {
        // Nach dem Abliefern (oder bei kalter Pizza) soll z.B. ein Hund den Spieler nicht mehr zurücksetzen
        if (LevelFinished)
        {
            return;
        }

        if (player == null || currentRespawnPoint == null)
        {
            Debug.LogWarning("GameManager: Player oder Respawn Point fehlt.");
            return;
        }

        PlacePlayerAt(currentRespawnPoint);
        LevelReset?.Invoke();
    }

    // Wärme-Bonus. Bewusst nicht auf levelTime begrenzt: Die Wärme darf als Puffer über 100% gehen
    public void AddTime(float seconds)
    {
        RemainingTime += seconds;
    }

    // Vom Ziel aufgerufen. Gibt false zurück, wenn das Level schon vorbei ist (z.B. Pizza gerade kalt geworden)
    public bool DeliverPizza()
    {
        if (LevelFinished)
        {
            return false;
        }

        LevelFinished = true;
        FreezePlayer();
        PizzaDelivered?.Invoke(CoinsForHeat(HeatPercent));
        return true;
    }

    public static int CoinsForHeat(float heat)
    {
        if (heat >= ThreeCoinHeat)
        {
            return 3;
        }
        if (heat >= TwoCoinHeat)
        {
            return 2;
        }
        return heat > 0f ? 1 : 0;
    }

    public void RestartCurrentLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void PlacePlayerAt(Transform point)
    {
        Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.position = point.position;
            playerRigidbody.rotation = point.rotation;
        }
        else
        {
            player.transform.SetPositionAndRotation(point.position, point.rotation);
        }
    }

    private void FreezePlayer()
    {
        if (playerController != null)
        {
            playerController.Freeze();
        }
    }

    private void HandleTimeUp()
    {
        LevelFinished = true;
        FreezePlayer();
        PizzaCold?.Invoke();
        StartCoroutine(RestartAfterDelay());
    }

    private System.Collections.IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(coldRestartDelay);
        RestartCurrentLevel();
    }
}
