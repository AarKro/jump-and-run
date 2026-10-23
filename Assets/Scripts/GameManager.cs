using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Wird bei jedem Respawn ausgelöst: Gegner, Power-Ups und Plattformen setzen sich darauf hin zurück.
    // Static, damit sich Objekte im Awake anmelden können, egal ob der GameManager schon existiert
    public static event System.Action LevelReset;

    [Header("Player und Start")]
    [SerializeField] private GameObject player;
    [SerializeField] private Transform startPoint;

    [Header("Zeit")]
    [SerializeField] private float levelTime = 120f;
    [SerializeField] private bool restartLevelWhenTimeIsUp = true;

    public float RemainingTime { get; private set; }
    public bool LevelFinished { get; private set; }

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
        if (player == null || currentRespawnPoint == null)
        {
            Debug.LogWarning("GameManager: Player oder Respawn Point fehlt.");
            return;
        }

        PlacePlayerAt(currentRespawnPoint);
        LevelReset?.Invoke();
    }

    public void AddTime(float seconds)
    {
        RemainingTime += seconds;
    }

    public void FinishLevel()
    {
        LevelFinished = true;
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

    private void HandleTimeUp()
    {
        if (restartLevelWhenTimeIsUp)
        {
            RestartCurrentLevel();
        }
        else
        {
            RemainingTime = levelTime;
            RespawnPlayer();
        }
    }
}
