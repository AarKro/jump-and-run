using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyPlatformPatrol : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private float waypointTolerance = 0.05f;

    [Header("Platform Detection")]
    [SerializeField] private LayerMask platformLayer;
    [SerializeField] private float enemyRayDistance = 5f;
    [SerializeField] private float playerRayDistance = 3f;

    [Header("Waypoint Names")]
    [SerializeField] private string waypointAName = "WaypointA";
    [SerializeField] private string waypointBName = "WaypointB";

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Visuals")]
    [SerializeField] private bool flipEnemy = true;
    [Tooltip("Dieses Objekt wird zum Umdrehen gespiegelt (scale.x). Leer = der Gegner selbst. " +
             "Besser ein Kind mit der Optik, dann bleiben Collider/Rigidbody ungespiegelt")]
    [SerializeField] private Transform flipTarget;

    // Für Erweiterungen wie den Hund: Ist der Spieler gerade auf derselben Plattform?
    public bool IsChasing => isChasing;
    // Solange true, bleibt der Gegner stehen (schaut aber zum Spieler), z.B. während der Hund bellt
    public bool HoldPosition { get; set; }

    private Rigidbody rb;
    private Transform player;

    private Transform platform;
    private Transform waypointA;
    private Transform waypointB;
    private Transform currentPatrolTarget;

    private Vector3 originalScale;
    private Vector3 startPosition;

    private bool routeFound;
    private bool isChasing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (flipTarget == null)
        {
            flipTarget = transform;
        }
        originalScale = flipTarget.localScale;
        startPosition = transform.position;
        GameManager.LevelReset += ResetPatrol;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetPatrol;
    }

    // Zurück zur Startposition, Verfolgung abbrechen
    private void ResetPatrol()
    {
        transform.position = startPosition;
        rb.position = startPosition;
        flipTarget.localScale = originalScale;
        isChasing = false;
        HoldPosition = false;

        if (routeFound)
        {
            currentPatrolTarget = GetNearestWaypoint();
        }
    }

    private void Start()
    {
        FindPlayer();
        FindPlatformAndWaypoints();
    }

    private void FixedUpdate()
    {
        if (!routeFound)
            return;

        if (player == null)
        {
            FindPlayer();
            Patrol();
            return;
        }

        isChasing = IsPlayerOnSamePlatform();

        if (HoldPosition)
        {
            if (isChasing && flipEnemy)
            {
                SetFacingDirection(Mathf.Sign(player.position.x - rb.position.x));
            }
            return;
        }

        if (isChasing)
            ChasePlayer();
        else
            Patrol();
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Kein GameObject mit Tag " +
                $"'{playerTag}' gefunden.",
                this
            );
        }
    }

    private void FindPlatformAndWaypoints()
    {
        Vector3 rayStart =
            transform.position + Vector3.up * 0.1f;

        if (!Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                enemyRayDistance,
                platformLayer))
        {
            Debug.LogWarning(
                $"{name}: Keine Plattform gefunden.",
                this
            );

            return;
        }

        platform = FindWaypointContainer(hit.transform);

        if (platform == null)
        {
            Debug.LogWarning(
                $"{name}: Plattform besitzt keine Children " +
                $"namens {waypointAName} und {waypointBName}.",
                this
            );

            return;
        }

        waypointA = FindChildByName(
            platform,
            waypointAName
        );

        waypointB = FindChildByName(
            platform,
            waypointBName
        );

        if (waypointA == null || waypointB == null)
        {
            Debug.LogWarning(
                $"{name}: WaypointA oder WaypointB " +
                "wurde nicht gefunden.",
                this
            );

            return;
        }

        currentPatrolTarget = GetNearestWaypoint();
        routeFound = true;

        Debug.Log(
            $"{name}: Patrouillenroute auf " +
            $"{platform.name} gefunden.",
            this
        );
    }

    private bool IsPlayerOnSamePlatform()
    {
        Vector3 rayStart =
            player.position + Vector3.up * 0.1f;

        if (!Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                playerRayDistance,
                platformLayer))
        {
            return false;
        }

        Transform hitTransform = hit.transform;

        // Der Collider kann auf der Plattform selbst
        // oder auf einem Child der Plattform liegen.
        return hitTransform == platform ||
               hitTransform.IsChildOf(platform);
    }

    private void Patrol()
    {
        if (currentPatrolTarget == null)
            return;

        float targetX = currentPatrolTarget.position.x;

        MoveTowardsX(targetX, patrolSpeed);

        if (Mathf.Abs(
                targetX - rb.position.x) <=
            waypointTolerance)
        {
            SwitchWaypoint();
        }
    }

    private void ChasePlayer()
    {
        float minimumX = Mathf.Min(
            waypointA.position.x,
            waypointB.position.x
        );

        float maximumX = Mathf.Max(
            waypointA.position.x,
            waypointB.position.x
        );

        // Der Enemy bleibt innerhalb seiner Plattform.
        float targetX = Mathf.Clamp(
            player.position.x,
            minimumX,
            maximumX
        );

        float distance =
            Mathf.Abs(targetX - rb.position.x);

        if (distance <= stoppingDistance)
            return;

        MoveTowardsX(targetX, chaseSpeed);
    }

    private void MoveTowardsX(
        float targetX,
        float speed)
    {
        float direction =
            Mathf.Sign(targetX - rb.position.x);

        Vector3 nextPosition = rb.position;

        nextPosition.x = Mathf.MoveTowards(
            rb.position.x,
            targetX,
            speed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);

        if (flipEnemy)
            SetFacingDirection(direction);
    }

    private void SwitchWaypoint()
    {
        if (currentPatrolTarget == waypointA)
            currentPatrolTarget = waypointB;
        else
            currentPatrolTarget = waypointA;
    }

    private Transform GetNearestWaypoint()
    {
        float distanceToA = Mathf.Abs(
            transform.position.x -
            waypointA.position.x
        );

        float distanceToB = Mathf.Abs(
            transform.position.x -
            waypointB.position.x
        );

        return distanceToA <= distanceToB
            ? waypointA
            : waypointB;
    }

    private Transform FindWaypointContainer(
        Transform hitObject)
    {
        Transform current = hitObject;

        while (current != null)
        {
            Transform foundA =
                FindChildByName(
                    current,
                    waypointAName
                );

            Transform foundB =
                FindChildByName(
                    current,
                    waypointBName
                );

            if (foundA != null && foundB != null)
                return current;

            current = current.parent;
        }

        return null;
    }

    private Transform FindChildByName(
        Transform parent,
        string childName)
    {
        Transform[] children =
            parent.GetComponentsInChildren<Transform>(
                true
            );

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void SetFacingDirection(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        Vector3 newScale = originalScale;

        newScale.x =
            Mathf.Abs(originalScale.x) * direction;

        flipTarget.localScale = newScale;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 enemyRayStart =
            transform.position + Vector3.up * 0.1f;

        Gizmos.DrawLine(
            enemyRayStart,
            enemyRayStart +
            Vector3.down * enemyRayDistance
        );

        if (player != null)
        {
            Gizmos.color = Color.cyan;

            Vector3 playerRayStart =
                player.position + Vector3.up * 0.1f;

            Gizmos.DrawLine(
                playerRayStart,
                playerRayStart +
                Vector3.down * playerRayDistance
            );
        }

        if (waypointA != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(
                waypointA.position,
                0.12f
            );
        }

        if (waypointB != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(
                waypointB.position,
                0.12f
            );
        }
    }
}