using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Baut Hindernisparcours aus den drei Plattform-Prefabs.
// Der Parcours läuft entlang +X auf dem Raster von PrefabGridEnforcer (ganze X/Y, Z = 0).
// Menü: Tools > Jump and Run > ...
public static class ObstacleCourseBuilder
{
    private const string RootName = "ObstacleCourse";
    private const string BigPath = "Assets/Prefabs/Platform Big.prefab";
    private const string MediumPath = "Assets/Prefabs/Platform Medium.prefab";
    private const string SmallPath = "Assets/Prefabs/Platform Small.prefab";
    private const string GoalMaterialPath = "Assets/Materials/Goal.mat";
    private const string KillZoneMaterialPath = "Assets/Materials/KillZone.mat";
    private const string MovingMaterialPath = "Assets/Materials/Moving.mat";
    private const string EnemyPath = "Assets/Prefabs/Dog.prefab";

    private const string Level1ScenePath = "Assets/Scenes/levels/level01.unity";
    private const string Level2ScenePath = "Assets/Scenes/levels/level02.unity";
    private const string Level2SceneName = "level02";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private enum Size { Big, Medium, Small }
    private enum Extra { None, Checkpoint, Goal, Enemy, EnemyAndGoal }

    private struct Segment
    {
        public Size size;
        public float gap;       // Abstand von der Kante der vorherigen Plattform
        public int height;      // Y der Plattform-Mitte (Oberkante = height + 0.5)
        public Extra extra;
        public Vector2 travel;  // != 0: bewegliche Plattform, fährt so weit in X/Y
        public float speed;

        public Segment(Size size, float gap, int height, Extra extra = Extra.None)
            : this(size, gap, height, extra, Vector2.zero, 0f)
        {
        }

        public Segment(Size size, float gap, int height, Extra extra, Vector2 travel, float speed)
        {
            this.size = size;
            this.gap = gap;
            this.height = height;
            this.extra = extra;
            this.travel = travel;
            this.speed = speed;
        }
    }

    // Level 1: Aufwärmen -> Treppe -> Checkpoint -> kleine Hüpfer -> bewegliche Plattform
    // -> Checkpoint -> Aufstieg mit Abstieg -> Ziel.
    // Sprung des Players: ~3.2 m hoch, ~5.5 m weit (bei moveSpeed 7), daher Lücken 3-4 m und Stufen von 1 m.
    private static readonly Segment[] Level1Layout =
    {
        new Segment(Size.Big,    0f,   0),
        new Segment(Size.Medium, 3f,   0),
        new Segment(Size.Medium, 3f,   1),
        new Segment(Size.Small,  3f,   2),
        new Segment(Size.Small,  3f,   3),
        new Segment(Size.Big,    3f,   3, Extra.Checkpoint),
        new Segment(Size.Small,  3.5f, 3),
        new Segment(Size.Small,  3.5f, 2),
        new Segment(Size.Small,  4f,   2),
        new Segment(Size.Small,  3.5f, 3),
        new Segment(Size.Medium, 3f,   3),
        new Segment(Size.Small,  3f,   3, Extra.None, new Vector2(8f, 0f), 2.5f),
        new Segment(Size.Big,    3f,   3, Extra.Checkpoint),
        new Segment(Size.Medium, 3f,   4),
        new Segment(Size.Small,  3f,   5),
        new Segment(Size.Small,  3.5f, 6),
        new Segment(Size.Small,  4f,   5),
        new Segment(Size.Big,    3f,   5, Extra.Goal),
    };

    // Level 2 (schwerer): Lücken 4-5.5 m, Stufen bis 2 m, fast nur kleine Plattformen,
    // nur ein Checkpoint, schnelle bewegliche Plattform, Aufzug und patrouillierende Gegner.
    private static readonly Segment[] Level2Layout =
    {
        new Segment(Size.Big,    0f,   0),
        new Segment(Size.Small,  4f,   1),
        new Segment(Size.Small,  4.5f, 2),
        new Segment(Size.Small,  5f,   1),
        new Segment(Size.Medium, 4f,   1, Extra.Enemy),
        new Segment(Size.Small,  5f,   2),
        new Segment(Size.Small,  5.5f, 3),
        new Segment(Size.Big,    4f,   3, Extra.Checkpoint),
        new Segment(Size.Small,  4f,   3, Extra.None, new Vector2(10f, 0f), 3.5f),
        new Segment(Size.Small,  4f,   4),
        new Segment(Size.Small,  4f,   2, Extra.None, new Vector2(0f, 4f), 2f),
        new Segment(Size.Medium, 4f,   6),
        new Segment(Size.Small,  5f,   5),
        new Segment(Size.Small,  5.5f, 6),
        new Segment(Size.Small,  5f,   7),
        new Segment(Size.Big,    4f,   7, Extra.EnemyAndGoal),
    };

    [MenuItem("Tools/Jump and Run/Build Level 1 Course (active scene)")]
    public static void BuildLevel1InActiveScene()
    {
        BuildCourse(Level1Layout, Level2SceneName);
    }

    [MenuItem("Tools/Jump and Run/Build Level 2 Course (active scene)")]
    public static void BuildLevel2InActiveScene()
    {
        BuildCourse(Level2Layout, null);
    }

    // Legt Level 2 als Kopie von Level 1 an, baut den schwereren Parcours hinein,
    // verlinkt das Ziel von Level 1 auf Level 2 und trägt beide Szenen in die Build Settings ein.
    // Auch per Batchmode aufrufbar: -executeMethod ObstacleCourseBuilder.SetupLevel2
    [MenuItem("Tools/Jump and Run/Setup Level 2")]
    public static void SetupLevel2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var level1 = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
        ConfigureGoal(FindOrCreateGoal(), Level2SceneName);
        EditorSceneManager.SaveScene(level1);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level2ScenePath) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Level2ScenePath));
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(Level1ScenePath, Level2ScenePath))
            {
                Debug.LogError($"ObstacleCourseBuilder: Konnte {Level2ScenePath} nicht anlegen.");
                return;
            }
        }

        var level2 = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
        BuildCourse(Level2Layout, null);

        // Der lose Platzhalter-Gegner aus Level 1 hat hier nichts verloren.
        GameObject placeholderEnemy = GameObject.Find("enemy");
        if (placeholderEnemy != null && placeholderEnemy.transform.parent == null)
        {
            Object.DestroyImmediate(placeholderEnemy);
        }

        EditorSceneManager.SaveScene(level2);

        AddLevelsToBuildSettings();

        EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
        Debug.Log("ObstacleCourseBuilder: Level 2 angelegt, Level 1 Ziel lädt jetzt Level 2.");
    }

    // LevelGoal kann nur Szenen laden, die in den Build Settings stehen.
    [MenuItem("Tools/Jump and Run/Add Levels to Build Settings")]
    public static void AddLevelsToBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Level1ScenePath, true),
            new EditorBuildSettingsScene(Level2ScenePath, true),
            new EditorBuildSettingsScene(SampleScenePath, false),
        };
        Debug.Log("ObstacleCourseBuilder: level01 (Index 0) und level02 (Index 1) in den Build Settings eingetragen.");
    }

    private static void BuildCourse(Segment[] layout, string nextSceneName)
    {
        var prefabs = new Dictionary<Size, GameObject>
        {
            { Size.Big, AssetDatabase.LoadAssetAtPath<GameObject>(BigPath) },
            { Size.Medium, AssetDatabase.LoadAssetAtPath<GameObject>(MediumPath) },
            { Size.Small, AssetDatabase.LoadAssetAtPath<GameObject>(SmallPath) },
        };

        foreach (var pair in prefabs)
        {
            if (pair.Value == null)
            {
                Debug.LogError($"ObstacleCourseBuilder: Prefab für {pair.Key} nicht gefunden.");
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Obstacle Course");
        int undoGroup = Undo.GetCurrentGroup();

        RemoveOldPlatforms(prefabs.Values);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create course root");

        float cursor = 0f;
        float minX = 0f;
        Vector3 startPosition = Vector3.zero;
        Vector3 goalPosition = Vector3.zero;
        int checkpointCount = 0;
        int enemyCount = 0;

        for (int i = 0; i < layout.Length; i++)
        {
            Segment segment = layout[i];
            GameObject prefab = prefabs[segment.size];
            float halfLength = prefab.transform.localScale.x * 0.5f;

            float centerX = i == 0 ? 0f : Mathf.Round(cursor + segment.gap + halfLength);
            if (i == 0)
            {
                minX = centerX - halfLength;
            }

            var platform = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            platform.transform.position = new Vector3(centerX, segment.height, 0f);
            platform.name = $"{i:00} {prefab.name}";

            float top = segment.height + 0.5f;

            if (i == 0)
            {
                startPosition = new Vector3(centerX - halfLength + 1.5f, top + 1f, 0f);
            }

            if (segment.travel != Vector2.zero)
            {
                MakeMoving(platform, root.transform, segment.travel, segment.speed);
                cursor = centerX + segment.travel.x + halfLength;
            }
            else
            {
                cursor = centerX + halfLength;
            }

            if (segment.extra == Extra.Checkpoint)
            {
                checkpointCount++;
                CreateCheckpoint(root.transform, checkpointCount, new Vector3(centerX - halfLength + 1.5f, top + 1f, 0f));
            }

            if (segment.extra == Extra.Enemy || segment.extra == Extra.EnemyAndGoal)
            {
                enemyCount++;
                CreatePatrollingEnemy(root.transform, platform, enemyCount, centerX, halfLength, top);
            }

            if (segment.extra == Extra.Goal || segment.extra == Extra.EnemyAndGoal)
            {
                goalPosition = new Vector3(centerX + halfLength - 1f, top, 0f);
            }
        }

        CreateKillZone(root.transform, minX, cursor);
        SetupStartAndPlayer(startPosition);

        GameObject goal = FindOrCreateGoal();
        Undo.RecordObject(goal.transform, "Move Goal");
        goal.transform.position = goalPosition;
        ConfigureGoal(goal, nextSceneName);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log($"ObstacleCourseBuilder: Parcours gebaut ({layout.Length} Plattformen, {checkpointCount} Checkpoints, {enemyCount} Gegner).");
    }

    // Entfernt einen alten Parcours und lose Plattform-Instanzen, damit nichts überlappt.
    private static void RemoveOldPlatforms(IEnumerable<GameObject> prefabs)
    {
        var prefabSet = new HashSet<GameObject>(prefabs);
        var scene = EditorSceneManager.GetActiveScene();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == RootName)
            {
                Undo.DestroyObjectImmediate(rootObject);
                continue;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(rootObject);
            if (source != null && prefabSet.Contains(source))
            {
                Undo.DestroyObjectImmediate(rootObject);
            }
        }
    }

    private static void MakeMoving(GameObject platform, Transform root, Vector2 travel, float speed)
    {
        Vector3 start = platform.transform.position;

        var pointA = new GameObject(platform.name + " Point A");
        pointA.transform.SetParent(root, false);
        pointA.transform.position = start;

        var pointB = new GameObject(platform.name + " Point B");
        pointB.transform.SetParent(root, false);
        pointB.transform.position = start + (Vector3)travel;

        var moving = platform.AddComponent<MovingPlatform>();
        var serialized = new SerializedObject(moving);
        serialized.FindProperty("pointA").objectReferenceValue = pointA.transform;
        serialized.FindProperty("pointB").objectReferenceValue = pointB.transform;
        serialized.FindProperty("speed").floatValue = speed;
        serialized.FindProperty("waitTime").floatValue = 0.75f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SetMaterial(platform, MovingMaterialPath);
    }

    private static void CreateCheckpoint(Transform root, int number, Vector3 position)
    {
        var checkpoint = new GameObject($"Checkpoint {number}");
        checkpoint.transform.SetParent(root, false);
        checkpoint.transform.position = position;

        var trigger = checkpoint.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.5f, 3f, 3f);
        trigger.center = new Vector3(0f, 0.5f, 0f);

        GameObject inactive = CreatePole(checkpoint.transform, "Inactive", null);
        GameObject active = CreatePole(checkpoint.transform, "Active", GoalMaterialPath);

        var component = checkpoint.AddComponent<Checkpoint>();
        var serialized = new SerializedObject(component);
        serialized.FindProperty("inactiveVisual").objectReferenceValue = inactive;
        serialized.FindProperty("activeVisual").objectReferenceValue = active;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreatePole(Transform parent, string name, string materialPath)
    {
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = name;
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.transform.SetParent(parent, false);
        pole.transform.localPosition = Vector3.zero;
        pole.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

        if (materialPath != null)
        {
            SetMaterial(pole, materialPath);
        }

        return pole;
    }

    // Gegner aus dem enemy-Prefab (EnemyPlatformPatrol + EnemyDamage): sucht per Raycast seine
    // Plattform und darauf die Children "WaypointA"/"WaypointB", zwischen denen er patrouilliert.
    // Als Prefab-Instanz wird er wie die Plattformen von PrefabGridEnforcer am Raster eingerastet.
    private static void CreatePatrollingEnemy(Transform root, GameObject platform, int number, float centerX, float halfLength, float top)
    {
        float patrolHalfWidth = halfLength - 1f;
        float y = top + 0.5f;

        var waypointA = new GameObject("WaypointA");
        waypointA.transform.SetParent(platform.transform, false);
        waypointA.transform.position = new Vector3(centerX - patrolHalfWidth, y, 0f);

        var waypointB = new GameObject("WaypointB");
        waypointB.transform.SetParent(platform.transform, false);
        waypointB.transform.position = new Vector3(centerX + patrolHalfWidth, y, 0f);

        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
        if (enemyPrefab == null)
        {
            Debug.LogError($"ObstacleCourseBuilder: Gegner-Prefab fehlt: {EnemyPath}");
            return;
        }

        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, root);
        enemy.name = $"Enemy {number}";
        // Auf dem Raster: centerX ist ganzzahlig, y = Plattform-Oberkante + Radius 0.5 = ganzzahlig
        enemy.transform.position = new Vector3(centerX, y, 0f);
    }

    private static void CreateKillZone(Transform root, float minX, float maxX)
    {
        var killZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        killZone.name = "KillZone";
        killZone.transform.SetParent(root, false);
        killZone.transform.position = new Vector3((minX + maxX) * 0.5f, -6f, 0f);
        killZone.transform.localScale = new Vector3(maxX - minX + 40f, 1f, 40f);
        killZone.GetComponent<Collider>().isTrigger = true;
        killZone.AddComponent<KillZone>();
        SetMaterial(killZone, KillZoneMaterialPath);
    }

    private static void SetupStartAndPlayer(Vector3 startPosition)
    {
        GameObject startPoint = GameObject.Find("StartPoint");
        if (startPoint == null)
        {
            startPoint = new GameObject("StartPoint");
            Undo.RegisterCreatedObjectUndo(startPoint, "Create StartPoint");
        }
        Undo.RecordObject(startPoint.transform, "Move StartPoint");
        startPoint.transform.position = startPosition;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Move Player");
            player.transform.position = startPosition;
        }

        var gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("ObstacleCourseBuilder: Kein GameManager in der Szene, Respawn funktioniert nicht.");
            return;
        }

        var serialized = new SerializedObject(gameManager);
        serialized.FindProperty("startPoint").objectReferenceValue = startPoint.transform;
        if (player != null)
        {
            serialized.FindProperty("player").objectReferenceValue = player;
        }
        serialized.ApplyModifiedProperties();
    }

    private static GameObject FindOrCreateGoal()
    {
        GameObject goal = GameObject.Find("Goal");
        if (goal == null)
        {
            goal = new GameObject("Goal");
            Undo.RegisterCreatedObjectUndo(goal, "Create Goal");
        }
        return goal;
    }

    // nextSceneName == null: Ziel lädt die nächste Szene nach Build-Index (letztes Level).
    private static void ConfigureGoal(GameObject goal, string nextSceneName)
    {
        var trigger = goal.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(goal);
        }
        trigger.isTrigger = true;
        trigger.size = new Vector3(2f, 3f, 3f);
        trigger.center = new Vector3(0f, 1.5f, 0f);

        var levelGoal = goal.GetComponent<LevelGoal>();
        if (levelGoal == null)
        {
            levelGoal = Undo.AddComponent<LevelGoal>(goal);
        }

        var serialized = new SerializedObject(levelGoal);
        serialized.FindProperty("nextSceneName").stringValue = nextSceneName ?? string.Empty;
        serialized.FindProperty("loadNextSceneByBuildIndex").boolValue = string.IsNullOrEmpty(nextSceneName);
        serialized.ApplyModifiedProperties();

        if (goal.transform.Find("Visual") == null)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(goal.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visual.transform.localScale = new Vector3(0.3f, 3f, 0.3f);
            SetMaterial(visual, GoalMaterialPath);
            Undo.RegisterCreatedObjectUndo(visual, "Create Goal visual");
        }

        EditorSceneManager.MarkSceneDirty(goal.scene);
    }

    private static void SetMaterial(GameObject target, string materialPath)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var renderer = target.GetComponent<Renderer>();
        if (material != null && renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
