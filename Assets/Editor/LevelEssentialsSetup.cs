using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Macht Player und GameManager aus Level 1 zu Prefabs und setzt sie in Level 2 ein,
// inklusive GameManager-Referenzen und ThirdPersonCamera auf der Main Camera.
// Menü: Tools > Jump and Run > Setup Player in Level 2
public static class LevelEssentialsSetup
{
    private const string Level1ScenePath = "Assets/Scenes/levels/level01.unity";
    private const string Level2ScenePath = "Assets/Scenes/levels/level02.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";

    [MenuItem("Tools/Jump and Run/Setup Player in Level 2")]
    public static void SetupPlayerInLevel2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!CreatePrefabsFromLevel1())
        {
            return;
        }

        var level2 = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
        SetupScene();
        EditorSceneManager.SaveScene(level2);

        Debug.Log("LevelEssentialsSetup: Player, GameManager und Kamera in Level 2 eingerichtet.");
    }

    private static bool CreatePrefabsFromLevel1()
    {
        bool hasPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null;
        bool hasGameManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath) != null;

        if (hasPlayerPrefab && hasGameManagerPrefab)
        {
            return true;
        }

        var level1 = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var gameManager = Object.FindFirstObjectByType<GameManager>();

        if (player == null || gameManager == null)
        {
            Debug.LogError("LevelEssentialsSetup: In Level 1 fehlt der Player (Tag 'Player') oder der GameManager.");
            return false;
        }

        // Die Level-1-Objekte werden direkt mit den neuen Prefabs verbunden.
        if (!hasPlayerPrefab)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPrefabPath, InteractionMode.AutomatedAction);
        }

        if (!hasGameManagerPrefab)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(gameManager.gameObject, GameManagerPrefabPath, InteractionMode.AutomatedAction);
        }

        // Szenenreferenzen landen nicht im Prefab, daher in Level 1 sicherheitshalber neu setzen.
        WireGameManager(gameManager, player, GameObject.Find("StartPoint"));
        EditorSceneManager.SaveScene(level1);
        return true;
    }

    private static void SetupScene()
    {
        GameObject startPoint = GameObject.Find("StartPoint");
        Vector3 spawnPosition = startPoint != null ? startPoint.transform.position : Vector3.up;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "player";
            player.transform.position = spawnPosition;
        }

        var gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "GameManager";
            gameManager = instance.GetComponent<GameManager>();
        }

        WireGameManager(gameManager, player, startPoint);

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("LevelEssentialsSetup: Keine Main Camera gefunden, Kamera nicht eingerichtet.");
            return;
        }

        var follow = camera.GetComponent<ThirdPersonCamera>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<ThirdPersonCamera>();
        }

        var serializedFollow = new SerializedObject(follow);
        serializedFollow.FindProperty("target").objectReferenceValue = player.transform;
        serializedFollow.ApplyModifiedPropertiesWithoutUndo();

        // Kamera direkt hinter den Player setzen, damit sie beim Start nicht quer durchs Level fährt.
        camera.transform.position = spawnPosition + new Vector3(0f, 5f, -8f);
        camera.transform.LookAt(spawnPosition + Vector3.up);
    }

    private static void WireGameManager(GameManager gameManager, GameObject player, GameObject startPoint)
    {
        var serialized = new SerializedObject(gameManager);
        serialized.FindProperty("player").objectReferenceValue = player;
        if (startPoint != null)
        {
            serialized.FindProperty("startPoint").objectReferenceValue = startPoint.transform;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
    }
}
