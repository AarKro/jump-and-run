using UnityEngine;

// Level 5 nach dem Finale: Die Echse liegt benommen auf der Oregano Street, das Maul offen die Salami Street hinunter. In
// festem Takt hickst sie Feuerbälle, die die Straße entlang nach Westen rollen, über die Kreuzung mit der Basil Street (dort
// quert die Bahn) bis an die Häuser gegenüber, wo sie verpuffen. Wie bei den Autos: deterministische Uhr, man wartet eine
// Lücke ab oder springt drüber; getroffen = Respawn. Vor jedem Hicks geht das Maul weiter auf, ein Glühen wächst darin und
// der Kopf zittert (die Warnung sieht man von der Kreuzung aus, 50 m weiter).
// Der Unterkiefer liegt dabei flach auf der Straße (Level5Builder legt den Kopf so hin), aufgemacht wird das Maul, indem
// sich der Oberkopf um das Kiefer-Scharnier hebt, wie bei einem Tier, das mit dem Kinn am Boden liegt (2026-10-29: vorher
// klappte der Kiefer nach unten, dafür musste der Kopf 3 m über der Straße schweben). Dazu kreisen Sterne über dem Kopf und aus den
// Nüstern raucht es. Alles erst, wenn sie liegt (KaijuBattle.LizardDown), ein Respawn setzt es mit dem Kampf zurück.
//
// Getroffen wird von Hand geprüft (Abstand in FixedUpdate) wie bei FallingDebris: Die Bälle werden per Uhr gesetzt, nicht
// von der Physik bewegt.
// Läuft nach FireBreath (das setzt den Kiefer in LateUpdate, hier wird er überschrieben)
[DefaultExecutionOrder(100)]
public class KaijuHiccups : MonoBehaviour
{
    [Header("Echse")]
    [SerializeField] private KaijuBattle battle;
    [Tooltip("Startpunkt der Feuerbälle (Maulspitze)")]
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform head;
    [SerializeField] private Transform jaw;
    [Tooltip("So weit steht das Maul offen (Grad), beim Hicks zusätzlich. Der Kopf ist so hingelegt, dass bei jawOpen der " +
             "Unterkiefer flach auf der Straße liegt")]
    [SerializeField] private float jawOpen = 22f;
    [SerializeField] private float jawHiccup = 22f;

    [Header("Takt")]
    [Tooltip("Ein Feuerball alle so viele Sekunden")]
    [SerializeField] private float interval = 2.4f;
    [Tooltip("So lange vor jedem Ball glüht das Maul auf (Warnung)")]
    [SerializeField] private float warnTime = 0.8f;
    [SerializeField] private float speed = 14f;

    [Header("Weg")]
    [Tooltip("Mitte der Salami Street (Welt-Z), auf der die Bälle rollen")]
    [SerializeField] private float streetZ = 265f;
    [Tooltip("Hier verpuffen sie (Welt-X, an den Häusern westlich der Basil Street)")]
    [SerializeField] private float endX = -187f;
    [SerializeField] private float ground = 0.3f;
    [SerializeField] private float radius = 1.2f;
    [Tooltip("Auf dieser Strecke fallen sie aus dem Maul auf die Straße")]
    [SerializeField] private float dropDistance = 9f;

    [Header("Teile")]
    [Tooltip("Feuerbälle (Pool), werden reihum benutzt")]
    [SerializeField] private Transform[] balls;
    [Tooltip("Glühen im Maul, wächst vor jedem Hicks")]
    [SerializeField] private Transform glow;
    [SerializeField] private ParticleSystem puff;
    [SerializeField] private ParticleSystem fizzle;
    [Tooltip("Nur sichtbar, solange sie liegt: kreisende Sterne, Rauch aus den Nüstern")]
    [SerializeField] private GameObject dazedDecor;

    private float clock;
    private bool down;
    private int lastFizzled = -1;
    private Quaternion jawRest, headDown;
    private Vector3 hinge;
    private Vector3 glowScale;
    private Collider playerCollider;

    private void Awake()
    {
        if (jaw != null)
        {
            jawRest = jaw.localRotation;
        }
        if (glow != null)
        {
            glowScale = glow.localScale;
        }
        GameManager.LevelReset += ResetHiccups;
        ResetHiccups();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetHiccups;
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            playerCollider = controller.GetComponent<Collider>();
        }
    }

    private void ResetHiccups()
    {
        clock = 0f;
        down = false;
        lastFizzled = -1;
        foreach (Transform ball in balls)
        {
            ball.gameObject.SetActive(false);
        }
        if (glow != null)
        {
            glow.gameObject.SetActive(false);
        }
        if (dazedDecor != null)
        {
            dazedDecor.SetActive(false);
        }
    }

    private void FixedUpdate()
    {
        if (!down)
        {
            if (battle == null || !battle.LizardDown)
            {
                return;
            }
            // Gerade umgefallen: Ab jetzt läuft die Uhr. Die Kopfhaltung merken, das Zucken kommt relativ dazu
            down = true;
            clock = 0f;
            if (head != null)
            {
                headDown = head.localRotation;
            }
            if (jaw != null)
            {
                // Scharnier in der Welt: bleibt beim Auf- und Zumachen, wo es ist (der Kopf dreht sich darum)
                hinge = jaw.position;
            }
            if (dazedDecor != null)
            {
                dazedDecor.SetActive(true);
            }
        }
        clock += Time.fixedDeltaTime;
        GameManager game = GameManager.Instance;
        if (playerCollider == null || game == null || game.IsRespawning || game.LevelFinished || game.InCutscene)
        {
            return;
        }
        Bounds player = playerCollider.bounds;
        for (int n = FirstLiveBall(clock); n <= LastLaunched(clock); n++)
        {
            if (n < 0 || !BallAt(clock - LaunchTime(n), out Vector3 center))
            {
                continue;
            }
            float margin = 0.35f;
            if (Mathf.Abs(player.center.x - center.x) < radius + margin && Mathf.Abs(player.center.z - center.z) < radius + margin
                && player.min.y < center.y + radius * 0.85f && player.max.y > center.y - radius)
            {
                game.RespawnPlayer();
                return;
            }
        }
    }

    // Ball n verlässt das Maul hier (nach der Warnung)
    private float LaunchTime(int n) => warnTime + n * interval;

    private int LastLaunched(float time) => Mathf.FloorToInt((time - warnTime) / interval);

    private float TravelTime => mouth != null ? (mouth.position.x - endX) / speed : 4f;

    private int FirstLiveBall(float time) => Mathf.CeilToInt((time - warnTime - TravelTime) / interval);

    // Wo ist ein Ball, der seit age Sekunden unterwegs ist? false, wenn noch nicht oder nicht mehr
    private bool BallAt(float age, out Vector3 center)
    {
        center = Vector3.zero;
        if (mouth == null || age < 0f)
        {
            return false;
        }
        Vector3 start = mouth.position;
        float travelled = age * speed;
        if (start.x - travelled < endX)
        {
            return false;
        }
        // Erst fällt er aus dem Maul auf die Straße (quadratisch, wie geworfen), dann rollt er
        float drop = Mathf.Clamp01(travelled / dropDistance);
        float rollY = ground + radius;
        center = new Vector3(start.x - travelled, Mathf.Lerp(start.y, rollY, drop * drop), Mathf.Lerp(start.z, streetZ, drop));
        return true;
    }

    private void Update()
    {
        if (!down)
        {
            return;
        }
        float time = clock + (Time.time - Time.fixedTime);
        int last = LastLaunched(time);
        // Pool: Ball n benutzt Platz n mod Länge. Sichtbar sind die, die gerade unterwegs sind. Nur umschalten, wenn sich
        // etwas ändert: SetActive(false) leert die Partikel, sonst fehlten die Flammen hinter dem Ball
        int first = Mathf.Max(0, FirstLiveBall(time));
        for (int i = 0; i < balls.Length; i++)
        {
            bool live = false;
            for (int n = first; n <= last; n++)
            {
                live |= n % balls.Length == i && BallAt(time - LaunchTime(n), out _);
            }
            if (balls[i].gameObject.activeSelf != live)
            {
                balls[i].gameObject.SetActive(live);
            }
        }
        for (int n = first; n <= last; n++)
        {
            Transform ball = balls[n % balls.Length];
            if (BallAt(time - LaunchTime(n), out Vector3 center))
            {
                ball.position = center;
                ball.Rotate(Vector3.forward, speed / radius * Mathf.Rad2Deg * Time.deltaTime, Space.World);
            }
            else if (n > lastFizzled && time - LaunchTime(n) > 0f)
            {
                // Gerade an den Häusern angekommen: verpuffen
                lastFizzled = n;
                if (fizzle != null)
                {
                    fizzle.transform.position = new Vector3(endX, ground + radius, streetZ);
                    fizzle.Emit(40);
                }
            }
        }

        // Warnung vor dem nächsten Ball: 0 → 1 über warnTime, beim Abschuss ein kurzer Stoß
        float untilNext = LaunchTime(last + 1) - time;
        float warn = 1f - Mathf.Clamp01(untilNext / warnTime);
        float sinceLaunch = last >= 0 ? time - LaunchTime(last) : 99f;
        float kick = Mathf.Clamp01(1f - sinceLaunch / 0.35f);
        if (sinceLaunch < Time.deltaTime * 1.5f && puff != null)
        {
            puff.Emit(25);
        }
        if (glow != null)
        {
            glow.gameObject.SetActive(warn > 0.02f || kick > 0f);
            glow.localScale = glowScale * Mathf.Max(warn * warn, kick);
        }
    }

    // Maul auf: Oberkopf um extra Grad heben (minus um X = Schnauze hoch), Kiefer im Kopf um ebenso viel weiter aufklappen,
    // so bleibt er in der Welt gleich liegen; dann den Kopf verschieben, bis das Scharnier wieder an seinem Platz ist.
    // In LateUpdate: FireBreath schließt den Kiefer in seinem LateUpdate wieder (Ausführungsreihenfolge 100 = danach)
    private void LateUpdate()
    {
        if (!down || jaw == null || head == null)
        {
            return;
        }
        float time = clock + (Time.time - Time.fixedTime);
        int last = LastLaunched(time);
        float warn = 1f - Mathf.Clamp01((LaunchTime(last + 1) - time) / warnTime);
        float kick = last >= 0 ? Mathf.Clamp01(1f - (time - LaunchTime(last)) / 0.35f) : 0f;
        float extra = jawHiccup * Mathf.Max(warn * warn, kick);
        float tremble = Mathf.Sin(Time.time * 41f) * warn * 1.5f;
        head.localRotation = headDown * Quaternion.Euler(-extra + tremble, 0f, 0f);
        jaw.localRotation = jawRest * Quaternion.Euler(jawOpen + extra - tremble, 0f, 0f);
        head.position += hinge - jaw.position;
    }
}
