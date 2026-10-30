using UnityEngine;

// Rollschuhfahrer im Pesto Park (Level 3): fährt mit GroundPatrol auf einem festen Stück des Brunnenrings hin und her
// (sieht den Spieler nie, verfolgt also nicht). Seitlich angefahren = Respawn. Draufgesprungen: Der Spieler federt ab,
// der Skater rudert eine Weile mit den Armen ("WHOA!") und fährt dann weiter. Solange er rudert, ist er harmlos.
// Animiert wird prozedural wie beim Hund: Tempo aus der Positionsänderung, weil der Rigidbody kinematisch ist.
// Die Figur steht im Profil (schaut nach lokal +X des Körpers), so sieht das Umdrehen von GroundPatrol (Spiegeln) gleich aus.
[RequireComponent(typeof(GroundPatrol))]
public class Skater : MonoBehaviour
{
    [Header("Teile (setzt ParkSetup)")]
    [Tooltip("Rig der Figur: Vorlage, Schwanken, Rudern")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftSkate;
    [SerializeField] private Transform rightSkate;
    [SerializeField] private GameObject whoaBubble;

    [Header("Farben")]
    [SerializeField] private Color shirtColor = new Color(0.2f, 0.75f, 0.85f);
    [SerializeField] private Color skinColor = new Color(0.95f, 0.75f, 0.6f);
    [SerializeField] private Color helmetColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private Renderer[] shirtRenderers;
    [SerializeField] private Renderer[] skinRenderers;
    [SerializeField] private Renderer[] helmetRenderers;

    [Header("Fahren")]
    [Tooltip("So viele Meter pro Doppelschritt (links + rechts abstoßen)")]
    [SerializeField] private float strideLength = 3.2f;
    [Tooltip("Vorlage bei vollem Tempo in Grad")]
    [SerializeField] private float lean = 14f;
    [SerializeField] private float fullSpeed = 6f;

    [Header("Draufspringen")]
    [SerializeField] private float stompHeightTolerance = 0.2f;
    [Tooltip("So lange rudert er mit den Armen und ist harmlos")]
    [SerializeField] private float wobbleDuration = 0.9f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Collider ownCollider;
    private Vector3 lastPosition;
    private float phase;
    // Sekunden seit dem Draufspringen, < 0 = rudert gerade nicht
    private float wobbleTime = -1f;

    private void Awake()
    {
        ownCollider = GetComponent<Collider>();
        lastPosition = transform.position;
        ApplyColors();
        GameManager.LevelReset += ResetSkater;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetSkater;
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    private void ResetSkater()
    {
        wobbleTime = -1f;
        phase = 0f;
        lastPosition = transform.position;
        if (whoaBubble != null)
        {
            whoaBubble.SetActive(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player != null && EnemyDamage.IsStomp(player, ownCollider, stompHeightTolerance))
        {
            player.StompBounce();
            wobbleTime = 0f;
            if (whoaBubble != null)
            {
                whoaBubble.SetActive(true);
            }
            return;
        }
        if (wobbleTime < 0f && GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float distance = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        float speed01 = Mathf.Clamp01(distance / dt / fullSpeed);
        phase = Mathf.Repeat(phase + distance / strideLength * Mathf.PI * 2f, Mathf.PI * 2f);

        if (wobbleTime >= 0f)
        {
            wobbleTime += Time.deltaTime;
            if (wobbleTime >= wobbleDuration)
            {
                wobbleTime = -1f;
                if (whoaBubble != null)
                {
                    whoaBubble.SetActive(false);
                }
            }
        }

        // Figur-Raum: vorne = -Z, Arme hängen nach unten. Drehung um X: negativ = Oberkörper nach vorn, bei den Armen
        // positiv = Arm nach vorn. Drehung um Z: Schwanken zur Seite
        float stroke = Mathf.Sin(phase);
        if (wobbleTime >= 0f)
        {
            // Rudern: Oberkörper kippt immer schwächer vor und zurück, Arme kreisen gegeneinander
            float k = 1f - wobbleTime / wobbleDuration;
            float rock = Mathf.Sin(wobbleTime * 18f) * 22f * k;
            rig.localRotation = Quaternion.Euler(rock, 0f, Mathf.Sin(wobbleTime * 11f) * 10f * k);
            float spin = wobbleTime * 900f;
            leftArm.localRotation = Quaternion.Euler(0f, 0f, -25f) * Quaternion.Euler(spin, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, 25f) * Quaternion.Euler(spin + 180f, 0f, 0f);
        }
        else
        {
            rig.localRotation = Quaternion.Euler(-lean * speed01, 0f, stroke * 6f * speed01);
            leftArm.localRotation = Quaternion.Euler(-stroke * 40f * speed01, 0f, -8f);
            rightArm.localRotation = Quaternion.Euler(stroke * 40f * speed01, 0f, 8f);
        }

        // Abstoßen: Der Schuh, der gerade drückt, gleitet nach hinten und etwas nach außen
        SetSkate(leftSkate, -1f, Mathf.Max(0f, stroke) * speed01);
        SetSkate(rightSkate, 1f, Mathf.Max(0f, -stroke) * speed01);
    }

    private static void SetSkate(Transform skate, float side, float push)
    {
        if (skate == null)
        {
            return;
        }
        Vector3 position = skate.localPosition;
        position.x = side * (0.13f + push * 0.12f);
        position.z = push * 0.35f;
        skate.localPosition = position;
        skate.localRotation = Quaternion.Euler(0f, side * push * 20f, 0f);
    }

    // Wie beim Kunden: Farbe pro Renderer über einen MaterialPropertyBlock, alle teilen sich Customer.mat
    private void ApplyColors()
    {
        SetColor(shirtRenderers, shirtColor);
        SetColor(skinRenderers, skinColor);
        SetColor(helmetRenderers, helmetColor);
    }

    private static void SetColor(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }
        var block = new MaterialPropertyBlock();
        foreach (Renderer target in renderers)
        {
            if (target == null)
            {
                continue;
            }
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }
}
