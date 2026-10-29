using System.Collections.Generic;
using UnityEngine;

// Die beiden Riesenmonster im Hintergrund ringen in einer Endlosschleife. Die Füße bleiben stehen (man soll zwischen den
// Beinen durchlaufen können), es bewegen sich nur Oberkörper, Arme, Köpfe und der Schwanz der Echse.
// Alles aus Sinuswellen mit unterschiedlichen Phasen: Wenn die Echse nach vorne drückt, weicht der Roboter zurück und umgekehrt.
// Gebaut und verdrahtet von CityBuilder (Tools > Jump and Run > City > Rebuild City Scene).
public class KaijuFight : MonoBehaviour
{
    [Header("Echse")]
    [SerializeField] private Transform lizardUpper;
    [SerializeField] private Transform lizardHead;
    [SerializeField] private Transform lizardArmLeft;
    [SerializeField] private Transform lizardArmRight;
    [SerializeField] private Transform[] lizardTail;

    [Header("Roboter")]
    [SerializeField] private Transform robotUpper;
    [SerializeField] private Transform robotHead;
    [SerializeField] private Transform robotArmLeft;
    [SerializeField] private Transform robotArmRight;
    [SerializeField] private Renderer robotVisor;
    [SerializeField] private Color visorColor = new Color(0.4f, 0.95f, 1f);

    [Header("Bewegung")]
    [Tooltip("Sekunden für einmal Schieben hin und zurück")]
    [SerializeField] private float cycle = 7f;
    [Tooltip("Wie weit sich die Oberkörper nach vorne/hinten lehnen (Grad)")]
    [SerializeField] private float leanAngle = 6f;
    [Tooltip("Ausschlag der Arme (Grad)")]
    [SerializeField] private float armSwing = 16f;
    [Tooltip("Ausschlag pro Schwanzglied (Grad)")]
    [SerializeField] private float tailSwing = 8f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // Ausgangsdrehung jedes bewegten Teils: Die Animation dreht immer relativ dazu, so bleibt die gebaute Pose erhalten
    private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    private MaterialPropertyBlock visorBlock;

    private void Awake()
    {
        Remember(lizardUpper, lizardHead, lizardArmLeft, lizardArmRight, robotUpper, robotHead, robotArmLeft, robotArmRight);
        if (lizardTail != null)
        {
            Remember(lizardTail);
        }
        visorBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        float t = Time.time * Mathf.PI * 2f / cycle;
        float shove = Mathf.Sin(t);

        // Schieben: Echse lehnt nach vorne, Roboter nach hinten (beide schauen sich an, also gleiches Vorzeichen in ihrem eigenen Raum)
        Turn(lizardUpper, shove * leanAngle, Mathf.Sin(t * 0.5f) * 3f, Mathf.Sin(t * 0.8f) * 2f);
        Turn(robotUpper, -shove * leanAngle, Mathf.Sin(t * 0.5f + 1f) * 3f, Mathf.Sin(t * 0.6f) * 2f);

        // Echse brüllt jedes zweite Mal den Kopf nach oben, Roboter schaut sich um
        float roar = Mathf.Max(0f, Mathf.Sin(t * 0.5f));
        Turn(lizardHead, -roar * roar * 22f, Mathf.Sin(t * 0.9f) * 6f, 0f);
        Turn(robotHead, Mathf.Sin(t * 1.1f) * 4f, Mathf.Sin(t * 0.7f) * 10f, 0f);

        // Echse kratzt abwechselnd links/rechts, Roboter boxt mit kurzen, harten Stößen (Potenz macht die Spitze schmal)
        Turn(lizardArmLeft, Mathf.Sin(t * 2f) * armSwing, 0f, 0f);
        Turn(lizardArmRight, Mathf.Sin(t * 2f + Mathf.PI) * armSwing, 0f, 0f);
        Turn(robotArmLeft, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.5f)), 4f) * armSwing * 1.6f, 0f, 0f);
        Turn(robotArmRight, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.5f + Mathf.PI)), 4f) * armSwing * 1.6f, 0f, 0f);

        // Schwanz: Welle, die von der Wurzel zur Spitze läuft
        if (lizardTail != null)
        {
            for (int i = 0; i < lizardTail.Length; i++)
            {
                Turn(lizardTail[i], 0f, Mathf.Sin(t * 1.3f - i * 0.6f) * tailSwing, 0f);
            }
        }

        if (robotVisor != null)
        {
            // Visier pulsiert. MaterialPropertyBlock statt material.color: sonst legt Unity eine Kopie des Materials an.
            // Hier erst anlegen, falls nötig: Nach einem Neukompilieren mitten im Play Mode sind nicht gespeicherte Felder leer
            visorBlock ??= new MaterialPropertyBlock();
            robotVisor.GetPropertyBlock(visorBlock);
            visorBlock.SetColor(BaseColor, visorColor * (0.65f + 0.35f * Mathf.Sin(t * 4f)));
            robotVisor.SetPropertyBlock(visorBlock);
        }
    }

    // Die Schwanzhaltung als neue Ruhehaltung merken (Level 5 legt ihn nach dem Umdrehen flach hinter die Echse)
    public void RememberTail()
    {
        if (lizardTail != null)
        {
            Remember(lizardTail);
        }
    }

    private void Remember(params Transform[] parts)
    {
        foreach (Transform part in parts)
        {
            if (part != null)
            {
                rest[part] = part.localRotation;
            }
        }
    }

    private void Turn(Transform part, float pitch, float yaw, float roll)
    {
        if (part != null && rest.TryGetValue(part, out Quaternion restRotation))
        {
            part.localRotation = restRotation * Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
