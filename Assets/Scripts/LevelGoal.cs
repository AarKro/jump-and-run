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
        yield return WaitForJump(continueInputDelay);

        // Letztes Level: erst der Endbildschirm mit dem Trinkgeld aller Levels, dann geht es mit Level 1 von vorne los
        if (loadNextSceneByBuildIndex && LevelFlow.NextLevelIndex() < 0)
        {
            PizzaHud hud = FindAnyObjectByType<PizzaHud>();
            if (hud != null)
            {
                hud.ShowEndScreen();
                // Springen zählt erst, wenn alle Zeilen aufgeploppt sind und "Press Jump to play again" dasteht
                while (!hud.EndScreenReady)
                {
                    yield return null;
                }
                yield return WaitForJump(0f);
            }
        }

        LoadNextLevel();
    }

    private static IEnumerator WaitForJump(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        while (!ContinuePressed())
        {
            yield return null;
        }
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
            return;
        }

        // Nach dem letzten Level: neues Spiel ab Level 1, mit Kamerafahrt und Trinkgeld wieder bei 0
        DeliveryLog.Clear();
        LevelFlow.Load(LevelFlow.FirstLevelIndex());
    }
}
