using UnityEngine;

// Ein Auto auf der Querstraße. Bewegt wird es von TrafficLane, hier nur Farbe und tödliche Berührung.
// Der Collider ist ein Trigger (wie bei der KillZone): Das Auto schubst den Spieler nie, Berührung = sofort Respawn.
[RequireComponent(typeof(Rigidbody))]
public class Car : MonoBehaviour
{
    [Header("Lack")]
    [Tooltip("Diese Renderer bekommen die zufällige Lackfarbe (Karosserie, Kabine)")]
    [SerializeField] private Renderer[] paintRenderers;
    [SerializeField] private Color[] palette =
    {
        new Color(0.85f, 0.15f, 0.12f), // Rot
        new Color(0.15f, 0.35f, 0.85f), // Blau
        new Color(0.95f, 0.8f, 0.15f),  // Gelb
        new Color(0.2f, 0.65f, 0.3f),   // Grün
        new Color(0.92f, 0.92f, 0.92f)  // Weiß
    };

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public Rigidbody Body { get; private set; }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
    }

    // Von TrafficLane aufgerufen, wenn das Auto aus dem Pool neu losfährt
    public void Repaint(int seed)
    {
        if (palette.Length == 0)
        {
            return;
        }

        // Aus der Startnummer abgeleitet statt Random: Nach einem Respawn kommen dieselben Farben in derselben Reihenfolge
        Color color = palette[Mathf.Abs(seed * 7 + 3) % palette.Length];
        var block = new MaterialPropertyBlock();
        foreach (Renderer target in paintRenderers)
        {
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }
}
