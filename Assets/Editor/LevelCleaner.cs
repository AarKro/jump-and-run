using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Räumt die aktive Szene für einen Neuaufbau ab: Plattformen, Gegner, Checkpoints, Power-Ups und
// Wegpunkte beweglicher Plattformen werden gelöscht. Player, StartPoint, GameManager,
// Kamera, Licht, Goal und KillZone bleiben erhalten. Rückgängig machen mit Strg/Cmd+Z.
// Menü: Tools > Jump and Run > Clear Level (active scene)
public static class LevelCleaner
{
    private const string CourseRootName = "ObstacleCourse";

    private static readonly string[] PlatformPrefabPaths =
    {
        "Assets/Prefabs/Platform Big.prefab",
        "Assets/Prefabs/Platform Medium.prefab",
        "Assets/Prefabs/Platform Small.prefab",
        "Assets/Prefabs/Moving Platform Small.prefab",
        "Assets/Prefabs/Dog.prefab",
        "Assets/Prefabs/Flying Enemy.prefab",
        "Assets/Prefabs/Checkpoint.prefab",
        "Assets/Prefabs/PowerUp.prefab",
    };

    [MenuItem("Tools/Jump and Run/Clear Level (active scene)")]
    private static void ClearActiveLevel()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog(
                "Level leeren",
                $"Alle Plattformen, Gegner, Checkpoints und Power-Ups in \"{scene.name}\" löschen?\n(Rückgängig mit Strg/Cmd+Z)",
                "Löschen",
                "Abbrechen"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Level");

        var prefabSet = new HashSet<GameObject>();
        foreach (string path in PlatformPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabSet.Add(prefab);
            }
        }

        var toDelete = new HashSet<GameObject>();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            // Generierter Parcours: KillZone retten, Rest komplett löschen
            if (rootObject.name == CourseRootName)
            {
                foreach (KillZone killZone in rootObject.GetComponentsInChildren<KillZone>(true))
                {
                    Undo.SetTransformParent(killZone.transform, null, "Keep KillZone");
                }
                toDelete.Add(rootObject);
                continue;
            }

            // Lose platzierte Objekte irgendwo sonst in der Szene
            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go);

                bool isCourseObject =
                    (source != null && prefabSet.Contains(source)) ||
                    go.GetComponent<PointToPointMover>() != null ||
                    go.GetComponent<Checkpoint>() != null ||
                    go.GetComponent<PowerUp>() != null ||
                    go.GetComponent<EnemyPlatformPatrol>() != null ||
                    go.GetComponent<EnemyDamage>() != null;

                if (isCourseObject)
                {
                    toDelete.Add(PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go);
                }
            }
        }

        int count = 0;
        foreach (GameObject go in toDelete)
        {
            // Kann schon mit einem Elternobjekt gelöscht worden sein
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"LevelCleaner: {count} Objekte aus \"{scene.name}\" entfernt. Szene speichern nicht vergessen.");
    }
}
