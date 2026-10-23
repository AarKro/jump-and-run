using System.Collections;
using UnityEngine;

// Federnde Ladenmarkise: Landet der Spieler von oben drauf, katapultiert sie ihn hoch (wie ein Trampolin).
// Damit erreicht man Stellen, die mit einem normalen Sprung zu hoch wären (Sprung ~3.2 m, Markise ~5 m).
public class BounceAwning : MonoBehaviour
{
    [Tooltip("Startgeschwindigkeit nach oben. Höhe = v² / (2 * Schwerkraft), mit 20 m/s etwa 5 m")]
    [SerializeField] private float bounceVelocity = 20f;
    [Tooltip("Spielraum wie beim Draufspringen auf Gegner: Unterkante Spieler gegen Oberkante Markise")]
    [SerializeField] private float topTolerance = 0.2f;
    [Tooltip("Der Stoff, der beim Abfedern kurz eingedrückt wird")]
    [SerializeField] private Transform canvas;
    [SerializeField] private float squashDuration = 0.2f;

    private Collider awningCollider;
    private Vector3 canvasScale;

    private void Awake()
    {
        awningCollider = GetComponent<Collider>();
        if (canvas != null)
        {
            canvasScale = canvas.localScale;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        Collider playerCollider = collision.collider;
        if (player == null || playerCollider.bounds.min.y < awningCollider.bounds.max.y - topTolerance)
        {
            // Seitlich oder von unten: einfach eine feste Fläche
            return;
        }

        player.Bounce(bounceVelocity);
        if (canvas != null)
        {
            StopAllCoroutines();
            StartCoroutine(Squash());
        }
    }

    private IEnumerator Squash()
    {
        for (float t = 0f; t < squashDuration; t += Time.deltaTime)
        {
            // Erst schnell eindrücken, dann zurückfedern: Sinus-Bogen von 0 über 1 zurück auf 0
            float dip = Mathf.Sin(t / squashDuration * Mathf.PI);
            canvas.localScale = new Vector3(canvasScale.x, canvasScale.y * (1f - 0.5f * dip), canvasScale.z);
            yield return null;
        }
        canvas.localScale = canvasScale;
    }
}
