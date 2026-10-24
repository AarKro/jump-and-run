using System.Collections.Generic;
using UnityEngine;

// Hindernis auf dem Markt (Level 4): Passant, Gruppe, Handkarren, Kistenstapel. Kein Respawn, stattdessen:
// - Seitlich angefahren, während der Spieler skatet: Er fällt vom Board (PlayerLife.BoardCrash), die Person ruft "HEY!".
// - Von oben: Bei Leuten federt er vom Kopf ab wie bei einem Gegner (bounceOnTop). Kisten und Karren sind dagegen
//   Plattformen (Layer Ground), auf denen er landet und weiterfährt.
// - Zu Fuß passiert nichts, sie sind dann einfach im Weg.
// Nach einem Sturz sind Leute und Karren kurz durchlässig (SetPassable), damit ein entgegenkommender Passant ihn nicht
// gleich noch einmal umwirft. Springt er über eine Person, duckt sie sich (duckers).
public class CrowdObstacle : MonoBehaviour
{
    // Alle aktiven Hindernisse, für SetPassable. Statisch: wird in OnDisable ausgetragen (kein Domain Reload)
    private static readonly List<CrowdObstacle> All = new List<CrowdObstacle>();

    [Tooltip("Leute: vom Kopf abfedern. Aus = Plattform zum Draufstehen (Kisten, Karren)")]
    [SerializeField] private bool bounceOnTop = true;
    [Tooltip("Nach einem Sturz kurz durchlässig (Leute, Karren). Kisten bleiben fest")]
    [SerializeField] private bool passableAfterCrash = true;
    [Tooltip("Sprechblase \"HEY!\", im Prefab aus")]
    [SerializeField] private GameObject heyBubble;
    [SerializeField] private float bubbleTime = 1f;
    [Tooltip("Figuren, die sich ducken, wenn der Spieler über sie springt")]
    [SerializeField] private CrowdFigure[] duckers;
    [Tooltip("Sprechblase beim Ducken (\"WHOA!\"), im Prefab aus. Leer = keine")]
    [SerializeField] private GameObject duckBubble;
    [Tooltip("Spielraum für 'von oben': Unterkante Spieler gegen Oberkante Hindernis")]
    [SerializeField] private float topTolerance = 0.25f;

    private Collider ownCollider;
    private PlayerController player;
    private Collider playerCollider;
    private float hideBubbleAt;

    private void Awake()
    {
        ownCollider = GetComponent<Collider>();
        if (heyBubble != null)
        {
            heyBubble.SetActive(false);
        }
        if (duckBubble != null)
        {
            duckBubble.SetActive(false);
        }
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerCollider = player.GetComponent<Collider>();
        }
    }

    // Spieler-Collider gegen alle durchlässigen Hindernisse ein- oder ausschalten (Physics.IgnoreCollision gilt nur für
    // dieses eine Paar von Collidern, alles andere kollidiert normal weiter)
    public static void SetPassable(Collider target, bool passable)
    {
        foreach (CrowdObstacle obstacle in All)
        {
            if (obstacle.passableAfterCrash && obstacle.ownCollider != null)
            {
                Physics.IgnoreCollision(target, obstacle.ownCollider, passable);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player") || !collision.gameObject.TryGetComponent(out PlayerController controller))
        {
            return;
        }
        if (EnemyDamage.IsStomp(controller, ownCollider, topTolerance))
        {
            if (bounceOnTop)
            {
                controller.StompBounce();
                ShowBubble();
            }
            return;
        }
        if (!controller.IsSkating)
        {
            return;
        }
        if (collision.gameObject.TryGetComponent(out PlayerLife life) && life.BoardCrash())
        {
            ShowBubble();
        }
    }

    private void ShowBubble()
    {
        if (heyBubble == null)
        {
            return;
        }
        heyBubble.SetActive(true);
        hideBubbleAt = Time.time + bubbleTime;
    }

    private void Update()
    {
        if (heyBubble != null && heyBubble.activeSelf && Time.time >= hideBubbleAt)
        {
            heyBubble.SetActive(false);
        }
        if (duckers == null || duckers.Length == 0 || player == null)
        {
            return;
        }
        // Ducken, solange der Spieler in der Luft über (oder knapp vor/hinter) ihnen ist
        float along = Mathf.Abs(Lane.Project(player.transform.position) - Lane.Project(transform.position));
        bool above = !player.IsGrounded && playerCollider.bounds.min.y > ownCollider.bounds.max.y - 0.6f;
        float duck = above && along < ownCollider.bounds.extents.magnitude + 1.5f ? 1f : 0f;
        foreach (CrowdFigure figure in duckers)
        {
            figure.Duck = duck;
        }
        if (duckBubble != null && duck > 0f && !duckBubble.activeSelf)
        {
            duckBubble.SetActive(true);
        }
        else if (duckBubble != null && duck <= 0f && duckBubble.activeSelf && player.IsGrounded)
        {
            duckBubble.SetActive(false);
        }
    }
}
