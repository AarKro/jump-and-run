using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelGoal : MonoBehaviour
{
    [Header("Levelwechsel")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool loadNextSceneByBuildIndex = true;

    [Header("Weiter nach dem Abliefern")]
    [Tooltip("Erst nach dieser Zeit zählt Springen als \"Weiter\", damit ein Sprung beim Ankommen nicht sofort weiterschaltet")]
    [SerializeField] private float continueInputDelay = 0.5f;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
        {
            return;
        }

        // Ohne GameManager (z.B. Testszene) direkt weiter
        if (GameManager.Instance == null)
        {
            triggered = true;
            LoadNextLevel();
            return;
        }

        // false = Pizza ist schon kalt, dann zählt die Lieferung nicht
        if (!GameManager.Instance.DeliverPizza())
        {
            return;
        }

        triggered = true;
        StartCoroutine(WaitForContinue());
    }

    // Coroutine: läuft über mehrere Frames. "yield return null" wartet genau einen Frame,
    // so kann man auf einen Tastendruck warten, ohne Update mit Zustandsvariablen vollzustopfen
    private IEnumerator WaitForContinue()
    {
        yield return new WaitForSeconds(continueInputDelay);

        while (!ContinuePressed())
        {
            yield return null;
        }

        LoadNextLevel();
    }

    private static bool ContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }

    private void LoadNextLevel()
    {
        if (!loadNextSceneByBuildIndex && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        // Überspringt die Stadt-Szene in den Build Settings; mit Stadt wird nur die Level-Szene getauscht
        int nextIndex = LevelFlow.NextLevelIndex();

        if (nextIndex >= 0)
        {
            LevelFlow.Load(nextIndex);
        }
        else
        {
            Debug.Log("Letztes Level erreicht. Keine weitere Szene in den Build Settings.");
        }
    }
}
