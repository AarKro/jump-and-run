using UnityEngine;

// Bauarbeiter auf dem Stahlträger im Hafen (Level 4, Kunden-Gruppe, Hommage an "Lunch atop a Skyscraper"): sitzt auf dem
// Träger, die Beine baumeln (jeder in seinem eigenen Takt), winkt, wenn der Pizzabote kommt. Wird geliefert, jubelt er
// nach Trinkgeld: 1–2 Münzen im Sitzen (Arme hoch, Beine strampeln), 3 Münzen steht er auf dem Träger auf und hüpft.
// Der Vorarbeiter in der Mitte ist der eigentliche Kunde (Customer, nimmt den Karton), die anderen sind BeamWorker.
// Gleiche Pillen-Figur wie der Kunde, dazu zwei Beine unter dem Sitz (fehlen der Figur sonst).
public class BeamWorker : MonoBehaviour
{
    [Header("Farben")]
    [SerializeField] private Color vestColor = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Color skinColor = new Color(0.9f, 0.7f, 0.55f);
    [SerializeField] private Renderer[] vestRenderers;
    [SerializeField] private Renderer[] skinRenderers;

    [Header("Körperteile")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    [Header("Verhalten")]
    [Tooltip("Ab diesem Abstand winkt er dem Pizzaboten zu")]
    [SerializeField] private float waveDistance = 9f;
    [Tooltip("Takt-Versatz, damit nicht alle gleichzeitig baumeln")]
    [SerializeField] private float phase;
    [SerializeField] private float cheerSpeed = 9f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Transform player;
    private int coins;
    private float cheerTime = -1f;

    private void Awake()
    {
        GameManager.PizzaDelivered += OnPizzaDelivered;
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.PizzaDelivered -= OnPizzaDelivered;
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

    private void OnPizzaDelivered(int earnedCoins)
    {
        coins = earnedCoins;
        cheerTime = 0f;
    }

    private void Update()
    {
        float t = Time.time + phase;
        if (cheerTime >= 0f)
        {
            cheerTime += Time.deltaTime;
            Cheer(cheerTime * cheerSpeed);
            return;
        }

        // Sitzen: Beine baumeln langsam, Oberkörper leicht zurückgelehnt, Hände auf den Knien. Winkt, wenn er kommt
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.Euler(6f, 0f, 0f);
        SetLegs(true, Mathf.Sin(t * 2.2f) * 18f);
        bool close = player != null && FlatDistance() < waveDistance;
        leftArm.localRotation = Quaternion.Euler(30f, 0f, 10f);
        rightArm.localRotation = close ? Quaternion.Euler(0f, 0f, 140f + Mathf.Sin(t * 8f) * 25f) : Quaternion.Euler(30f, 0f, -10f);
    }

    private void Cheer(float beat)
    {
        if (coins >= 3)
        {
            // Aufgestanden (Beine weg, er steht auf dem Träger), hüpft, beide Arme hoch
            SetLegs(false, 0f);
            rig.localPosition = Vector3.up * (0.25f * Mathf.Abs(Mathf.Sin(beat)));
            rig.localRotation = Quaternion.identity;
            leftArm.localRotation = Quaternion.Euler(0f, 0f, -150f + Mathf.Sin(beat) * 20f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(beat + Mathf.PI) * 20f);
            return;
        }
        // Im Sitzen: strampeln, Arme abwechselnd hoch (2 Münzen: beide gleichzeitig und schneller)
        rig.localPosition = Vector3.up * (0.06f * Mathf.Abs(Mathf.Sin(beat)));
        rig.localRotation = Quaternion.Euler(6f, 0f, 0f);
        SetLegs(true, Mathf.Sin(beat * 1.5f) * 35f);
        float other = coins >= 2 ? 0f : Mathf.PI;
        leftArm.localRotation = Quaternion.Euler(0f, 0f, -140f + Mathf.Sin(beat) * 25f);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, 140f + Mathf.Sin(beat + other) * 25f);
    }

    // Beine hängen vom Sitz nach unten und schwingen nach vorn/hinten (gegengleich); swing in Grad
    private void SetLegs(bool visible, float swing)
    {
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            if (leg != null && leg.gameObject.activeSelf != visible)
            {
                leg.gameObject.SetActive(visible);
            }
        }
        if (leftLeg != null)
        {
            leftLeg.localRotation = Quaternion.Euler(20f + swing, 0f, 0f);
        }
        if (rightLeg != null)
        {
            rightLeg.localRotation = Quaternion.Euler(20f - swing, 0f, 0f);
        }
    }

    private float FlatDistance()
    {
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void ApplyColors()
    {
        SetColor(vestRenderers, vestColor);
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
}
