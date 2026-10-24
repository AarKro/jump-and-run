using UnityEngine;

// Kleine Dauer-Animation für Kulisse auf dem Markt (Level 4):
// Bellows = der Balg eines Akkordeons geht auf und zu (Skalierung entlang X),
// Sway = ein Luftballon-Strauß wiegt sich im Wind (Drehung um die Aufhängung).
public class Squeeze : MonoBehaviour
{
    public enum Mode { Bellows, Sway }

    [SerializeField] private Mode mode = Mode.Bellows;
    [Tooltip("Zyklen pro Sekunde")]
    [SerializeField] private float speed = 0.8f;
    [Tooltip("Bellows: Anteil, um den der Balg auf- und zugeht. Sway: Grad")]
    [SerializeField] private float amount = 0.35f;

    private Vector3 startScale;
    private Quaternion startRotation;
    private float offset;

    private void Awake()
    {
        startScale = transform.localScale;
        startRotation = transform.localRotation;
        // Jede Instanz mit eigenem Takt (aus der Position), damit nicht alle gleichzeitig schwingen
        offset = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.61f, 10f);
    }

    private void Update()
    {
        float wave = Mathf.Sin((Time.time + offset) * speed * Mathf.PI * 2f);
        if (mode == Mode.Bellows)
        {
            transform.localScale = new Vector3(startScale.x * (1f + amount * wave), startScale.y, startScale.z);
        }
        else
        {
            transform.localRotation = startRotation * Quaternion.Euler(wave * amount * 0.5f, 0f, wave * amount);
        }
    }
}
