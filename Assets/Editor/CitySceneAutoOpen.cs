using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Öffnet im Editor die Stadt automatisch dazu (additiv), wenn ein Level mit LevelCity geöffnet wird.
// So sieht man beim Bearbeiten die Stadt drumherum, und Play startet mit beiden Szenen (LevelCity lädt sie dann nicht nochmal).
// Das Level bleibt die aktive Szene: Neue Objekte landen im Level, nicht in der Stadt.
// Hinweis: Die CityCutouts wirken erst im Play Mode, im Editor überlagern sich Level-Boden und Stadt-Boden.
[InitializeOnLoad]
public static class CitySceneAutoOpen
{
    static CitySceneAutoOpen()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (mode != OpenSceneMode.Single || EditorApplication.isPlayingOrWillChangePlaymode || !UsesCity(scene))
        {
            return;
        }
        if (SceneManager.GetSceneByPath(CityBuilder.ScenePath).isLoaded || !System.IO.File.Exists(CityBuilder.ScenePath))
        {
            return;
        }
        EditorSceneManager.OpenScene(CityBuilder.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
    }

    private static bool UsesCity(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<LevelCity>(true) != null)
            {
                return true;
            }
        }
        return false;
    }
}
