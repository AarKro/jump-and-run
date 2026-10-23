using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelGoal : MonoBehaviour
{
    [Header("Levelwechsel")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool loadNextSceneByBuildIndex = true;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
        {
            return;
        }

        triggered = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.FinishLevel();
        }

        LoadNextLevel();
    }

    private void LoadNextLevel()
    {
        if (!loadNextSceneByBuildIndex && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Debug.Log("Letztes Level erreicht. Keine weitere Szene in den Build Settings.");
        }
    }
}
