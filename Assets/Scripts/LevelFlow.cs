using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Levelwechsel und Neustart. Ist die Stadt geladen, bleibt sie es: Nur die Level-Szene wird entladen und die nächste
// additiv dazugeladen. Ohne Stadt (Level 2) wie gehabt mit SceneManager.LoadScene.
// Die Coroutine braucht ein Objekt, das den Szenenwechsel überlebt (DontDestroyOnLoad), denn das Level, das den
// Wechsel auslöst, wird dabei selbst entladen.
public class LevelFlow : MonoBehaviour
{
    public const string CitySceneName = "City";

    private static LevelFlow runner;
    // Das gerade geladene Level ist ein Neustart (Pizza kalt): dann ohne Kamerafahrt
    private static bool restartPending;

    // Mit ausgeschaltetem Domain Reload (Enter Play Mode Options) behalten statische Felder ihre Werte vom letzten Play.
    // Diese Methode läuft bei jedem Play-Start ganz früh und setzt sie zurück
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        runner = null;
        restartPending = false;
    }

    public static void Restart()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        Load(current);
        restartPending = true;
    }

    // Einmal abfragbar: true, wenn dieses Level per Restart() geladen wurde
    public static bool ConsumeRestart()
    {
        bool wasRestart = restartPending;
        restartPending = false;
        return wasRestart;
    }

    // Nächstes Level in den Build Settings, die Stadt-Szene wird übersprungen. -1 = es gibt keins mehr
    public static int NextLevelIndex()
    {
        for (int i = SceneManager.GetActiveScene().buildIndex + 1; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (!IsCity(i))
            {
                return i;
            }
        }
        return -1;
    }

    // Erstes Level in den Build Settings (für "nochmal spielen" nach dem letzten Level)
    public static int FirstLevelIndex()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (!IsCity(i))
            {
                return i;
            }
        }
        return -1;
    }

    // Nummer des Levels, wie der Spieler sie zählt (1, 2, ...): Build-Index ohne die Stadt-Szene davor
    public static int LevelNumber(int buildIndex)
    {
        int number = 0;
        for (int i = 0; i <= buildIndex && i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (!IsCity(i))
            {
                number++;
            }
        }
        return number;
    }

    public static int CurrentLevelNumber()
    {
        return LevelNumber(SceneManager.GetActiveScene().buildIndex);
    }

    // Wie viele Levels es insgesamt gibt (alle Szenen in den Build Settings außer der Stadt)
    public static int LevelCount()
    {
        return LevelNumber(SceneManager.sceneCountInBuildSettings - 1);
    }

    private static bool IsCity(int buildIndex)
    {
        return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(buildIndex)) == CitySceneName;
    }

    public static void Load(int buildIndex)
    {
        restartPending = false;
        if (!SceneManager.GetSceneByName(CitySceneName).isLoaded)
        {
            SceneManager.LoadScene(buildIndex);
            return;
        }
        if (runner == null)
        {
            runner = new GameObject("Level Flow").AddComponent<LevelFlow>();
            DontDestroyOnLoad(runner.gameObject);
        }
        runner.StartCoroutine(SwapLevel(buildIndex));
    }

    private static IEnumerator SwapLevel(int buildIndex)
    {
        // Erst das alte Level weg (sonst gäbe es kurz zwei GameManager), dann das neue dazu und aktiv machen.
        // Die aktive Szene bekommt neu erzeugte Objekte, also muss das Level aktiv sein, nicht die Stadt
        Scene current = SceneManager.GetActiveScene();
        if (current.isLoaded && current.name != CitySceneName)
        {
            yield return SceneManager.UnloadSceneAsync(current);
        }
        yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(buildIndex));

        // Level ohne Stadt (z.B. Level 2 aus Plattformen): Stadt wieder entladen
        if (LevelCity.Instance == null)
        {
            yield return SceneManager.UnloadSceneAsync(CitySceneName);
        }
    }
}
