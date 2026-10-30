using UnityEngine;

// Freund(in) des Kunden beim Picknick (Level 3): sitzt auf der Decke und winkt, wenn der Pizzabote nahe ist. Wird die
// Pizza geliefert, springt sie auf und jubelt mit beiden Armen, solange der Kunde tanzt. Nach einem Respawn wieder sitzen.
// Gleiche Figur wie der Kunde (Kapsel-Körper, Arme mit Drehpunkt an der Schulter), aber ohne Liefer-Logik.
public class PicnicFriend : MonoBehaviour
{
    [Header("Farben")]
    [SerializeField] private Color shirtColor = new Color(0.95f, 0.8f, 0.2f);
    [SerializeField] private Color skinColor = new Color(0.78f, 0.56f, 0.4f);
    [SerializeField] private Renderer[] shirtRenderers;
    [SerializeField] private Renderer[] skinRenderers;

    [Header("Körperteile")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;

    [Header("Verhalten")]
    [SerializeField] private float sitDrop = 0.45f;
    [Tooltip("Ab diesem Abstand winkt sie dem Pizzaboten zu")]
    [SerializeField] private float waveDistance = 7f;
    [SerializeField] private float waveSpeed = 8f;
    [SerializeField] private float cheerSpeed = 9f;
    [SerializeField] private float cheerHopHeight = 0.25f;

    private Transform player;
    private bool cheering;
    private float cheerTime;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        GameManager.PizzaDelivered += OnPizzaDelivered;
        GameManager.LevelReset += OnLevelReset;
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= OnPizzaDelivered;
        GameManager.LevelReset -= OnLevelReset;
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    private void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }

    // Wie beim Kunden: Farbe pro Renderer über einen MaterialPropertyBlock, alle teilen sich Customer.mat
    private void ApplyColors()
    {
        SetColor(shirtRenderers, shirtColor);
        SetColor(skinRenderers, skinColor);
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

    private void OnPizzaDelivered(int coins)
    {
        cheering = true;
        cheerTime = 0f;
    }

    // Nach dem Abliefern bleibt sie stehen (das Level ist dann vorbei), sonst wieder hinsetzen
    private void OnLevelReset()
    {
        cheering = false;
    }

    private void Update()
    {
        float t = Time.time;
        if (cheering)
        {
            // Aufgesprungen: hüpft im Takt, beide Arme abwechselnd hoch über dem Kopf
            cheerTime += Time.deltaTime;
            float beat = cheerTime * cheerSpeed;
            float standUp = Mathf.Clamp01(cheerTime / 0.3f);
            rig.localPosition = Vector3.up * (Mathf.Lerp(-sitDrop, 0f, standUp) + cheerHopHeight * Mathf.Abs(Mathf.Sin(beat)) * standUp);
            rig.localRotation = Quaternion.identity;
            // Um Z: 0 = Arm hängt, der -X-Arm geht mit negativen Winkeln nach oben, der +X-Arm mit positiven
            leftArm.localRotation = Quaternion.Euler(0f, 0f, -150f + Mathf.Sin(beat) * 20f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(beat + Mathf.PI) * 20f);
            return;
        }

        // Sitzen, leicht zurückgelehnt; winkt mit dem rechten Arm, wenn der Pizzabote kommt
        rig.localPosition = Vector3.down * sitDrop;
        rig.localRotation = Quaternion.Euler(8f, 0f, 0f);
        bool close = player != null && FlatDistance() < waveDistance;
        leftArm.localRotation = Quaternion.Euler(35f, 0f, 10f);
        rightArm.localRotation = close ? Quaternion.Euler(0f, 0f, 140f + Mathf.Sin(t * waveSpeed) * 25f) : Quaternion.Euler(35f, 0f, -10f);
    }

    private float FlatDistance()
    {
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }
}
