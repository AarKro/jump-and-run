using UnityEngine;

// Steuert die Riesenechse in Level 5. Phase "Wüten": Sie wiegt sich, brüllt ab und zu. Ist der Spieler im Bereich vor ihr
// (u von/bis), speit sie im Takt Feuer: kurz ruhig → aufladen (lehnt sich zurück, Kopf hoch, Maul glüht immer heller) →
// Feueratem (lehnt sich nach vorn, Kopf runter, FireBreath trifft) → erholen. Der Rhythmus läuft ab dem Betreten des
// Bereichs, bei einem Respawn fängt er von vorn an.
// Bewegt werden nur Oberkörper und Kopf, relativ zur gebauten Pose (wie KaijuFight). Kommt der Roboter, übernimmt KaijuFight.
public class KaijuBattle : MonoBehaviour
{
    [Header("Echse")]
    [SerializeField] private Transform lizardUpper;
    [SerializeField] private Transform lizardHead;
    [SerializeField] private FireBreath breath;

    [Header("Feueratem")]
    [Tooltip("Bereich der Bahn, in dem sie Feuer speit (u von/bis)")]
    [SerializeField] private float breathFromU;
    [SerializeField] private float breathToU;
    [SerializeField] private float calmTime = 1.5f;
    [SerializeField] private float chargeTime = 2f;
    [SerializeField] private float breathTime = 3f;
    [SerializeField] private float recoverTime = 1f;
    [Tooltip("Zurücklehnen beim Aufladen bzw. Vorlehnen beim Speien (Grad)")]
    [SerializeField] private float chargeLean = 14f;
    [SerializeField] private float breathLean = 10f;

    private enum Phase { Calm, Charge, Breath, Recover }

    private Quaternion upperRest;
    private Quaternion headRest;
    private Phase phase = Phase.Calm;
    private float phaseTime;
    private bool active;
    private float lean;
    private float headPitch;
    private Transform player;

    // Der Roboter ist da (Arbeitsschritt "Roboter"): kein Feueratem mehr
    public bool Busy { get; set; }

    private void Awake()
    {
        upperRest = lizardUpper.localRotation;
        headRest = lizardHead.localRotation;
        GameManager.LevelReset += ResetRhythm;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetRhythm;
    }

    private void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }

    private void ResetRhythm()
    {
        active = false;
        phase = Phase.Calm;
        phaseTime = 0f;
        if (breath != null)
        {
            breath.StopBreath();
            breath.SetCharge(0f);
        }
    }

    private void Update()
    {
        if (Busy)
        {
            return;
        }
        float dt = Time.deltaTime;
        bool inRange = player != null && InRange(Lane.Project(player.position));
        if (inRange && !active)
        {
            active = true;
            phase = Phase.Calm;
            phaseTime = 0f;
        }

        float targetLean = 0f, targetHead = 0f;
        if (active)
        {
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Calm:
                    if (phaseTime >= calmTime) Next(Phase.Charge);
                    break;
                case Phase.Charge:
                    float k = Mathf.Clamp01(phaseTime / chargeTime);
                    targetLean = -chargeLean * k;
                    targetHead = -18f * k;
                    breath.SetCharge(k);
                    if (phaseTime >= chargeTime)
                    {
                        breath.SetCharge(0.6f);
                        breath.StartBreath();
                        Next(Phase.Breath);
                    }
                    break;
                case Phase.Breath:
                    targetLean = breathLean;
                    targetHead = 22f;
                    if (phaseTime >= breathTime)
                    {
                        breath.StopBreath();
                        breath.SetCharge(0f);
                        Next(Phase.Recover);
                    }
                    break;
                case Phase.Recover:
                    if (phaseTime >= recoverTime)
                    {
                        // Ist der Spieler weitergelaufen (oder zurück), hört sie nach dieser Runde auf
                        active = inRange;
                        Next(Phase.Calm);
                    }
                    break;
            }
        }

        // Weich in die Pose, dazu ein langsames Wiegen (wirkt lebendig, auch wenn sie gerade nicht speit)
        float blend = 1f - Mathf.Exp(-6f * dt);
        lean = Mathf.Lerp(lean, targetLean, blend);
        headPitch = Mathf.Lerp(headPitch, targetHead, blend);
        float sway = Mathf.Sin(Time.time * 0.8f) * 3f;
        lizardUpper.localRotation = upperRest * Quaternion.Euler(lean, sway, Mathf.Sin(Time.time * 0.5f) * 2f);
        lizardHead.localRotation = headRest * Quaternion.Euler(headPitch, Mathf.Sin(Time.time * 0.6f) * 8f, 0f);
    }

    private void Next(Phase next)
    {
        phase = next;
        phaseTime = 0f;
    }

    private bool InRange(float u) => u >= Mathf.Min(breathFromU, breathToU) && u <= Mathf.Max(breathFromU, breathToU);
}
