using UnityEngine;

// Eine Person auf dem Markt (Level 4): Passant, Händler, Kunde am Stand, Gruppe. Dieselbe Pillen-Figur wie der Kunde
// (ParkSetup.BuildFigure), animiert ohne Animator:
// - Farben (Hemd, Haut, Haare) zufällig, aber fest aus der Startposition berechnet: Jede Person sieht nach einem Neustart
//   gleich aus, und alle teilen sich ein Material (Farbe per MaterialPropertyBlock).
// - Bewegt sich die Figur (Passant mit GroundPatrol), läuft sie: wippen, Arme schwingen gegengleich.
// - Steht sie, wiegt sie sich leicht und gestikuliert ab und zu (Unterhaltung, Ware zeigen).
// - Duck (0–1, von außen gesetzt, z.B. MarketGroup): duckt sich weg, wenn der Spieler über sie springt.
// Die Bewegung kommt aus der Positionsänderung, weil die Passanten kinematisch per MovePosition laufen.
public class CrowdFigure : MonoBehaviour
{
    [Header("Teile (setzt MarketSetup)")]
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Renderer[] shirtRenderers;
    [SerializeField] private Renderer[] skinRenderers;
    [Tooltip("Haare/Mütze, bekommen eine eigene Farbe")]
    [SerializeField] private Renderer[] hairRenderers;

    [Header("Bewegung")]
    [Tooltip("Strecke für einen Doppelschritt")]
    [SerializeField] private float strideLength = 1.2f;
    [SerializeField] private float bobHeight = 0.05f;
    [SerializeField] private float armSwing = 30f;
    [Tooltip("Steht still: so stark gestikuliert sie (0 = gar nicht, z.B. Händler hinter dem Tisch etwas weniger)")]
    [SerializeField] private float gesture = 1f;
    [Tooltip("Arme dauerhaft so weit nach vorn (Grad), z.B. der Händler, der seinen Karren schiebt")]
    [SerializeField] private float armsForward;
    [Header("Feste Kleidung (statt zufällig, z.B. der Eisverkäufer, der in Level 3 und 5 gleich aussehen soll)")]
    [SerializeField] private bool fixedOutfit;
    [SerializeField] private Color outfitShirt = Color.white;
    [SerializeField] private Color outfitSkin = new Color(0.96f, 0.8f, 0.66f);
    [SerializeField] private Color outfitHair = new Color(0.2f, 0.13f, 0.08f);

    [Tooltip("In Panik (Level 5): rennt vorgebeugt mit hochgerissenen, fuchtelnden Armen")]
    [SerializeField] private bool panic;

    // 0 = steht normal, 1 = ganz weggeduckt
    public float Duck { get; set; }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color[] Shirts =
    {
        new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.8f), new Color(0.95f, 0.75f, 0.2f), new Color(0.3f, 0.65f, 0.35f),
        new Color(0.6f, 0.35f, 0.7f), new Color(0.95f, 0.55f, 0.25f), new Color(0.25f, 0.7f, 0.75f), new Color(0.9f, 0.9f, 0.88f),
        new Color(0.3f, 0.3f, 0.35f), new Color(0.95f, 0.5f, 0.65f),
    };
    private static readonly Color[] Skins =
    {
        new Color(0.96f, 0.8f, 0.66f), new Color(0.88f, 0.68f, 0.52f), new Color(0.7f, 0.5f, 0.35f), new Color(0.5f, 0.34f, 0.24f),
        new Color(0.98f, 0.86f, 0.75f),
    };
    private static readonly Color[] Hairs =
    {
        new Color(0.2f, 0.13f, 0.08f), new Color(0.08f, 0.07f, 0.07f), new Color(0.75f, 0.6f, 0.3f), new Color(0.55f, 0.25f, 0.12f),
        new Color(0.7f, 0.7f, 0.72f),
    };

    private Vector3 lastPosition;
    private Vector3 rigStart;
    private float phase;
    private float speed;
    private float seed;
    private float duckAmount;

    private void Awake()
    {
        rigStart = rig.localPosition;
        lastPosition = transform.position;
    }

    private void Start()
    {
        ApplyColors();
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        // Ausreißer (Zurücksetzen beim Respawn) begrenzen und glätten wie beim Pizzaboten
        float targetSpeed = Mathf.Min(delta.magnitude / dt, 4f);
        speed = Mathf.Lerp(speed, targetSpeed, 1f - Mathf.Exp(-10f * dt));
        float walking = Mathf.Clamp01(speed / 1f);
        phase = Mathf.Repeat(phase + speed / strideLength * Mathf.PI * 2f * dt, Mathf.PI * 2f);

        // Stehen: langsames Wiegen und ab und zu eine Geste mit einem Arm. Jede Figur hat ihren eigenen Takt (seed)
        float time = Time.time + seed * 10f;
        float sway = Mathf.Sin(time * 1.3f) * 3f * (1f - walking);
        float talk = Mathf.Max(0f, Mathf.Sin(time * 0.9f)) * gesture * (1f - walking);
        float wave = Mathf.Sin(time * 6f) * 10f * talk;

        duckAmount = Mathf.Lerp(duckAmount, Duck, 1f - Mathf.Exp(-14f * dt));
        float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * walking;
        // Ducken: zusammensinken (Stauchen um den Drehpunkt an den Füßen, die bleiben also auf dem Boden) und nach vorne
        // beugen (Figur schaut bei Drehung 0 nach -Z, nach vorn = negative X-Drehung). Nicht absenken, sonst steckt sie im Boden
        rig.localPosition = rigStart + Vector3.up * bob;
        rig.localScale = new Vector3(1f + 0.08f * duckAmount, 1f - 0.3f * duckAmount, 1f + 0.08f * duckAmount);
        rig.localRotation = Quaternion.Euler(-20f * duckAmount, 0f, sway);

        float swing = Mathf.Sin(phase) * armSwing * walking;
        // Arme: Drehung um X, positiv = nach vorn (die Hand geht zur Kamera). Ducken: Arme schützend über den Kopf
        float cover = 150f * duckAmount;
        if (armsForward > 0f)
        {
            // Hält etwas fest (Karren): Arme bleiben vorn, schwingen kaum
            swing *= 0.15f;
            talk = 0f;
            wave = 0f;
        }
        if (panic)
        {
            // Arme über dem Kopf, fuchteln schnell gegengleich; Oberkörper nach vorn (negative X-Drehung)
            float flail = Mathf.Sin(time * 14f) * 25f;
            rig.localRotation = Quaternion.Euler(-12f * walking, 0f, sway);
            leftArm.localRotation = Quaternion.Euler(160f + flail, 0f, -20f);
            rightArm.localRotation = Quaternion.Euler(160f - flail, 0f, 20f);
            return;
        }
        leftArm.localRotation = Quaternion.Euler(armsForward + swing + cover, 0f, -6f);
        rightArm.localRotation = Quaternion.Euler(armsForward - swing + cover + 40f * talk + wave, 0f, 6f + 20f * talk);
    }

    // Farben aus der Position: gleiche Stelle = gleiche Person
    private void ApplyColors()
    {
        Vector3 p = transform.position;
        int hash = Mathf.Abs(Mathf.RoundToInt(p.x * 7.3f) * 73856093 ^ Mathf.RoundToInt(p.z * 5.9f) * 19349663 ^ Mathf.RoundToInt(p.y * 3.1f) * 83492791);
        seed = (hash % 1000) / 1000f;
        if (fixedOutfit)
        {
            SetColor(shirtRenderers, outfitShirt);
            SetColor(skinRenderers, outfitSkin);
            SetColor(hairRenderers, outfitHair);
            return;
        }
        SetColor(shirtRenderers, Shirts[hash % Shirts.Length]);
        SetColor(skinRenderers, Skins[(hash / 7) % Skins.Length]);
        SetColor(hairRenderers, Hairs[(hash / 13) % Hairs.Length]);
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
