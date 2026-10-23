using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PrefabGridEnforcer
{
    private const float GridSizeX = 1f;
    private const float GridSizeY = 1f;
    private const float FixedZ = 0f;

    private const float CheckInterval = 0.2f;
    private static double nextCheckTime;

    static PrefabGridEnforcer()
    {
        EditorApplication.update += CheckPrefabPositions;
        EditorApplication.hierarchyChanged += EnforceImmediately;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void CheckPrefabPositions()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.timeSinceStartup < nextCheckTime)
            return;

        nextCheckTime =
            EditorApplication.timeSinceStartup + CheckInterval;

        SnapAllPrefabInstances();
    }

    private static void EnforceImmediately()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            SnapAllPrefabInstances();
    }

    private static void OnSceneOpened(
        Scene scene,
        OpenSceneMode mode)
    {
        SnapAllPrefabInstances();
    }

    private static void SnapAllPrefabInstances()
    {
        Transform[] transforms =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform currentTransform in transforms)
        {
            GameObject currentObject = currentTransform.gameObject;

            // Nur Objekte aus geöffneten Szenen berücksichtigen.
            if (!currentObject.scene.IsValid() ||
                !currentObject.scene.isLoaded)
            {
                continue;
            }

            // Nur Prefab-Instanzen bearbeiten.
            if (!PrefabUtility.IsPartOfPrefabInstance(currentObject))
                continue;

            GameObject prefabRoot =
                PrefabUtility.GetOutermostPrefabInstanceRoot(
                    currentObject
                );

            // Nur das Root der Prefab-Instanz verschieben.
            if (prefabRoot != currentObject)
                continue;

            Vector3 currentPosition = currentTransform.position;

            Vector3 snappedPosition = new Vector3(
                SnapToGrid(currentPosition.x, GridSizeX),
                SnapToGrid(currentPosition.y, GridSizeY),
                FixedZ
            );

            if (ApproximatelyEqual(
                    currentPosition,
                    snappedPosition))
            {
                continue;
            }

            currentTransform.position = snappedPosition;

            EditorUtility.SetDirty(currentTransform);

            EditorSceneManager.MarkSceneDirty(
                currentObject.scene
            );
        }
    }

    private static float SnapToGrid(
        float value,
        float gridSize)
    {
        if (gridSize <= 0f)
            return value;

        return Mathf.Round(value / gridSize) * gridSize;
    }

    private static bool ApproximatelyEqual(
        Vector3 first,
        Vector3 second)
    {
        return Mathf.Approximately(first.x, second.x) &&
               Mathf.Approximately(first.y, second.y) &&
               Mathf.Approximately(first.z, second.z);
    }
}