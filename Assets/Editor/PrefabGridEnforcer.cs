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

        // Gibt es eine Laufbahn mit Kurven, wird entlang dieser Bahn eingerastet statt auf X/Z = 0
        LanePath lane = Object.FindAnyObjectByType<LanePath>();

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

            // Die Stadt-Szene hat ihre festen Gebäude an festen Weltpositionen (auch quer zur Laufbahn eines Levels)
            if (currentObject.scene.path == CityBuilder.ScenePath)
                continue;

            GameObject prefabRoot =
                PrefabUtility.GetOutermostPrefabInstanceRoot(
                    currentObject
                );

            // Nur das Root der Prefab-Instanz verschieben.
            if (prefabRoot != currentObject)
                continue;

            // UI (z.B. HUD) liegt nicht in der Welt
            if (currentTransform is RectTransform)
                continue;

            Vector3 currentPosition = currentTransform.position;

            if (lane != null)
            {
                SnapToLane(currentTransform, lane);
                continue;
            }

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

    // Auf die Bahn: Stelle u auf ganze Meter runden, Position auf dem Pfad, Drehung in den Lane-Rahmen.
    // Auf dem geraden Stück entlang +X ist das genau dasselbe wie vorher (u = X, Z = 0, keine Drehung)
    private static void SnapToLane(Transform target, LanePath lane)
    {
        float u = SnapToGrid(lane.Project(target.position), GridSizeX);
        Vector3 snappedPosition = lane.PointAt(u) + Vector3.up * SnapToGrid(target.position.y, GridSizeY);
        Vector3 back = Vector3.Cross(lane.TangentAt(u), Vector3.up);
        Quaternion snappedRotation = Quaternion.LookRotation(back, Vector3.up);

        // Mit kleiner Toleranz vergleichen: Positionen nach einer Kurve sind krumme Werte (z.B. 13.7168...), die in der
        // Szenen-Datei mit weniger Stellen gespeichert werden. Ohne Toleranz würde jedes Laden die Szene "ändern"
        if ((target.position - snappedPosition).sqrMagnitude < 0.0005f * 0.0005f &&
            Quaternion.Angle(target.rotation, snappedRotation) < 0.05f)
        {
            return;
        }

        target.SetPositionAndRotation(snappedPosition, snappedRotation);
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
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