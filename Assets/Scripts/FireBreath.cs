using System.Collections.Generic;
using UnityEngine;

// Feueratem der Riesenechse (Level 5). KaijuBattle steuert, wann geladen und wann gespien wird.
// Aufladen (SetCharge 0 … 1): Der Unterkiefer klappt auf, von allen Seiten werden Funken ins Maul gesaugt, dort wächst ein
//   Feuerball, ein flackerndes Licht beleuchtet das Gesicht, aus den Nüstern qualmt es. Gegen Ende schneller und heller,
//   kurz vor dem Speien ein Blitz.
// Speien: Ein Flammenstrom aus Partikeln (heller Kern, Flammen, Rauch) schießt aus dem Maul und schlägt hinter dem Eiswagen
//   ein (Feuerball). Von dort rollt eine Feuerwalze als Ring nach außen über den Boden, teilt sich am Wagen (in seinem
//   Schatten brennt nichts) und rast links und rechts am Spieler vorbei; wo sie durch ist, brennt der Boden weiter.
// - Trifft den Spieler, sobald die Walze bei ihm ist, solange er im Feuerbereich der Bahn ist und nicht hinter einer Deckung
//   steht: Respawn.
// - Hinter einer Deckung (Stück der Bahn direkt hinter einem Hindernis, vom Feuer abgewandt, Füße nicht höher als dessen
//   Oberkante) ist er sicher, und weil links und rechts das Feuer lodert, wird die Pizza dabei langsam wieder warm.
// - Beim ersten Einschlag fangen die Bäume des Parks im Feuerbereich Feuer (nur Optik, sie brennen danach weiter).
// Treffer entlang der Bahn in u gerechnet (Lane.Project), nicht mit Physik-Strahlen: so ist es genau und gut einstellbar.
public class FireBreath : MonoBehaviour
{
    [System.Serializable]
    public struct Cover
    {
        [Tooltip("Sicherer Bereich hinter dem Hindernis (u von/bis)")]
        public float shelterFrom;
        public float shelterTo;
        [Tooltip("Oberkante des Hindernisses (Welt-Höhe): Wer mit den Füßen darüber ist, wird getroffen")]
        public float top;
    }

    [Header("Strom")]
    [Tooltip("Punkt vorne im Maul (Kind des Kopfes): Hier beginnt der Flammenstrom")]
    [SerializeField] private Transform mouth;
    [Tooltip("Hier schlägt der Strom auf (Welt)")]
    [SerializeField] private Vector3 target;
    [Tooltip("Partikel des Stroms (Kern, Flammen, Rauch). Werden jeden Frame vom Maul aufs Ziel gerichtet, ihre Lebensdauer " +
             "so gesetzt, dass sie gerade am Ziel ankommen (lokal Z = Flugrichtung)")]
    [SerializeField] private ParticleSystem[] stream;
    [Tooltip("Feuerball und Rauchpilz beim Einschlag (nur Emit)")]
    [SerializeField] private ParticleSystem[] impactBursts;
    [SerializeField] private Light impactLight;

    [Header("Feuerwalze")]
    [Tooltip("Flammen der Walze (nur Emit, Welt-Simulation)")]
    [SerializeField] private ParticleSystem waveFlames;
    [Tooltip("So schnell läuft die Walze vom Einschlag nach außen (m/s)")]
    [SerializeField] private float waveSpeed = 34f;
    [Tooltip("Bis zu diesem Abstand vom Einschlag rollt sie")]
    [SerializeField] private float waveRange = 58f;
    [Tooltip("Abstand der Flammen auf dem Ring (m)")]
    [SerializeField] private float waveSpacing = 3f;
    [Tooltip("Mitte des Hindernisses, hinter dem die Walze nicht weiterkommt (Welt)")]
    [SerializeField] private Vector3 shadowCaster;
    [Tooltip("Halbe Breite des Schattens auf Höhe des Hindernisses; dahinter wird er im gleichen Winkel breiter")]
    [SerializeField] private float shadowHalfWidth = 2.2f;
    [Tooltip("Flammen am Boden (Bahn und Park): starten, wenn die Walze bei ihnen ankommt, brennen bis zum Ende des Atems")]
    [SerializeField] private ParticleSystem[] groundFires;

    [Header("Aufladen")]
    [SerializeField] private Transform jaw;
    [Tooltip("So weit klappt der Kiefer auf (Grad, um lokal X)")]
    [SerializeField] private float jawOpenAngle = 30f;
    [Tooltip("Feuerball im Maul, wächst beim Aufladen")]
    [SerializeField] private Transform mouthGlow;
    [Tooltip("Weicher Schein um den Feuerball (lokale Simulation, Größe wächst mit)")]
    [SerializeField] private ParticleSystem glowHalo;
    [Tooltip("Funken, die von allen Seiten ins Maul gesaugt werden (Kugel-Form mit negativer Geschwindigkeit)")]
    [SerializeField] private ParticleSystem chargeEmbers;
    [SerializeField] private ParticleSystem[] nostrilSmoke;
    [SerializeField] private Light mouthLight;
    [SerializeField] private float mouthLightIntensity = 80f;

    [Header("Treffer")]
    [Tooltip("Feuerbereich auf der Bahn (u von/bis)")]
    [SerializeField] private float hitFromU;
    [SerializeField] private float hitToU;
    [SerializeField] private Cover[] covers;
    [Tooltip("So viel Wärme pro Sekunde (Anteil des Balkens) hinter einer Deckung, während das Feuer brennt")]
    [SerializeField] private float warmPerSecond = 0.03f;

    [Header("Bäume")]
    [Tooltip("Bäume der Stadt in diesem Rechteck (Welt X/Z) fangen Feuer")]
    [SerializeField] private Rect burnArea;
    [Tooltip("Vorlage für das Feuer in einer Baumkrone (aus, wird pro Baum kopiert)")]
    [SerializeField] private ParticleSystem treeFireTemplate;
    [SerializeField] private int maxBurningTrees = 40;

    public bool Breathing { get; private set; }
    // Steht der Spieler gerade sicher hinter einer Deckung im Feuer (für eine Anzeige, z.B. Dampf vom Karton)
    public bool Sheltered { get; private set; }

    private Transform player;
    private Collider playerCollider;
    private PizzaCarton carton;
    private float breathTime;
    private bool treesLit;
    private bool impacted;
    private float charge;
    private float flash;
    private float waveEmitted;
    private float[] streamSpeeds;
    private bool[] groundFireStarted;
    private Quaternion jawRest;
    private readonly List<GameObject> treeFires = new List<GameObject>();

    private const float FlashTime = 0.25f;

    private void Awake()
    {
        if (jaw != null)
        {
            jawRest = jaw.localRotation;
        }
        StopBreath();
        SetCharge(0f);
    }

    private void Start()
    {
        var controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
            playerCollider = controller.GetComponent<Collider>();
            carton = controller.GetComponentInChildren<PizzaCarton>(true);
        }
    }

    // 0 = Maul zu, 1 = voll aufgeladen. Die Optik dazu setzt LateUpdate
    public void SetCharge(float k)
    {
        charge = Mathf.Clamp01(k);
    }

    public void StartBreath()
    {
        Breathing = true;
        breathTime = 0f;
        impacted = false;
        waveEmitted = 0f;
        flash = FlashTime;
        groundFireStarted = new bool[groundFires != null ? groundFires.Length : 0];
        foreach (ParticleSystem system in stream)
        {
            system.Play();
        }
    }

    public void StopBreath()
    {
        Breathing = false;
        Sheltered = false;
        foreach (ParticleSystem system in stream)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (groundFires != null)
        {
            foreach (ParticleSystem fire in groundFires)
            {
                fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        if (impactLight != null)
        {
            impactLight.enabled = false;
        }
    }

    // Abstand Maul → Ziel geteilt durch die Geschwindigkeit des Stroms: So lange braucht das Feuer bis zum Einschlag
    private float TravelTime => streamSpeeds != null && streamSpeeds.Length > 0
        ? Vector3.Distance(mouth.position, target) / streamSpeeds[0]
        : 0.4f;

    // Radius der Walze jetzt (0, solange der Strom noch unterwegs ist)
    private float WaveRadius => impacted ? Mathf.Min(waveRange, (breathTime - TravelTime) * waveSpeed) : 0f;

    private void LateUpdate()
    {
        UpdateChargeLook();
        if (!Breathing || mouth == null)
        {
            return;
        }
        breathTime += Time.deltaTime;
        AimStream();
        if (!impacted && breathTime >= TravelTime)
        {
            impacted = true;
            foreach (ParticleSystem burst in impactBursts)
            {
                burst.transform.position = target;
                burst.Emit(burst.main.maxParticles / 2);
            }
            if (impactLight != null)
            {
                impactLight.enabled = true;
            }
            if (!treesLit)
            {
                treesLit = true;
                IgniteTrees();
            }
        }
        if (!impacted)
        {
            return;
        }
        if (impactLight != null)
        {
            float flicker = 1f + Mathf.Sin(Time.time * 37f) * 0.15f + Mathf.Sin(Time.time * 23f) * 0.1f;
            impactLight.intensity = 60f * flicker;
        }
        EmitWave();
        StartReachedGroundFires();
    }

    // Kiefer, Feuerball, Schein, Funken, Licht und Qualm nach dem Ladestand. Beim Speien bleibt das Maul offen und glüht
    private void UpdateChargeLook()
    {
        float k = charge;
        // Quadratisch: Am Anfang passiert wenig, gegen Ende geht es schnell (die Spannung steigt)
        float rush = k * k;
        float tremble = Mathf.Sin(Time.time * 47f) * 0.5f + Mathf.Sin(Time.time * 31f) * 0.5f;
        flash = Mathf.Max(0f, flash - Time.deltaTime);
        float flashBoost = flash / FlashTime;

        if (jaw != null)
        {
            float open = Breathing ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));
            jaw.localRotation = jawRest * Quaternion.Euler(jawOpenAngle * open + tremble * 2f * rush, 0f, 0f);
        }
        if (mouthGlow != null)
        {
            mouthGlow.gameObject.SetActive(k > 0.01f || Breathing);
            float size = Mathf.Lerp(1f, 9f, rush) * (1f + tremble * 0.08f * k) * (1f + flashBoost * 0.8f);
            mouthGlow.localScale = Vector3.one * size;
        }
        if (glowHalo != null)
        {
            ParticleSystem.MainModule main = glowHalo.main;
            main.startSizeMultiplier = Mathf.Lerp(4f, 34f, rush) * (1f + flashBoost);
            SetRate(glowHalo, k > 0.01f || Breathing ? 25f : 0f);
        }
        if (chargeEmbers != null)
        {
            // Nur beim Laden: Funken fliegen ins Maul, gegen Ende immer mehr
            SetRate(chargeEmbers, Breathing ? 0f : 320f * rush + (k > 0.01f ? 15f : 0f));
        }
        if (nostrilSmoke != null)
        {
            foreach (ParticleSystem smoke in nostrilSmoke)
            {
                SetRate(smoke, Breathing ? 6f : 14f * rush);
            }
        }
        if (mouthLight != null)
        {
            mouthLight.enabled = k > 0.01f || Breathing;
            float flicker = 1f + tremble * (0.15f + 0.25f * k);
            mouthLight.intensity = mouthLightIntensity * Mathf.Max(rush, Breathing ? 0.6f : 0f) * flicker * (1f + flashBoost * 2f);
        }
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTimeMultiplier = rate;
        if (rate > 0f && !system.isPlaying)
        {
            system.Play();
        }
    }

    // Strom vom Maul zum Ziel. Die Teilchen fliegen in Welt-Koordinaten weiter (wiegt sich der Kopf, biegt sich der Strom
    // wie bei einem Gartenschlauch), ihre Lebensdauer endet am Ziel
    private void AimStream()
    {
        if (streamSpeeds == null || streamSpeeds.Length != stream.Length)
        {
            streamSpeeds = new float[stream.Length];
            for (int i = 0; i < stream.Length; i++)
            {
                streamSpeeds[i] = stream[i].main.startSpeed.constantMax;
            }
        }
        Vector3 from = mouth.position;
        Vector3 delta = target - from;
        Quaternion aim = Quaternion.LookRotation(delta);
        for (int i = 0; i < stream.Length; i++)
        {
            stream[i].transform.SetPositionAndRotation(from, aim);
            ParticleSystem.MainModule main = stream[i].main;
            main.startLifetime = delta.magnitude / streamSpeeds[i];
        }
    }

    // Der Ring wird in Schritten von waveSpacing ausgesendet: jedes Mal, wenn der Radius um so viel gewachsen ist, ein neuer
    // Ring Flammen, nach außen fliegend. Punkte im Schatten des Hindernisses (vom Einschlag aus gesehen dahinter, in einem
    // Keil, der sich mit dem Abstand weitet) bleiben frei: So teilt sich die Walze am Wagen
    private void EmitWave()
    {
        if (waveFlames == null)
        {
            return;
        }
        float radius = WaveRadius;
        while (waveEmitted + waveSpacing <= radius)
        {
            waveEmitted += waveSpacing;
            EmitRing(waveEmitted);
        }
    }

    private void EmitRing(float radius)
    {
        Vector3 toCaster = shadowCaster - target;
        toCaster.y = 0f;
        float casterDistance = toCaster.magnitude;
        Vector3 casterDirection = casterDistance > 0.01f ? toCaster / casterDistance : Vector3.forward;
        int count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / waveSpacing));
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 offset = direction * radius;
            float along = Vector3.Dot(offset, casterDirection);
            float side = Vector3.Cross(casterDirection, offset).magnitude;
            if (along > casterDistance - 1f && side < shadowHalfWidth * along / Mathf.Max(1f, casterDistance))
            {
                continue;
            }
            emit.position = new Vector3(target.x + offset.x, target.y, target.z + offset.z);
            emit.velocity = direction * waveSpeed * 0.25f + Vector3.up * Random.Range(3f, 7f);
            emit.startSize = Random.Range(3.5f, 7f);
            emit.startLifetime = Random.Range(0.6f, 1f);
            waveFlames.Emit(emit, 1);
        }
    }

    // Bodenfeuer starten, sobald die Walze ihre Mitte erreicht hat
    private void StartReachedGroundFires()
    {
        if (groundFires == null)
        {
            return;
        }
        float radius = WaveRadius;
        for (int i = 0; i < groundFires.Length; i++)
        {
            if (groundFireStarted[i])
            {
                continue;
            }
            Vector3 p = groundFires[i].transform.position;
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.x, target.z)) <= radius + 4f)
            {
                groundFireStarted[i] = true;
                groundFires[i].Play();
            }
        }
    }

    private void Update()
    {
        Sheltered = false;
        if (!Breathing || !impacted || player == null)
        {
            return;
        }
        GameManager game = GameManager.Instance;
        if (game == null || game.IsRespawning || game.LevelFinished)
        {
            return;
        }
        float u = Lane.Project(player.position);
        if (u < Mathf.Min(hitFromU, hitToU) || u > Mathf.Max(hitFromU, hitToU))
        {
            return;
        }
        // Erst getroffen, wenn die Walze beim Spieler angekommen ist
        Vector3 p = player.position;
        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.x, target.z)) > WaveRadius)
        {
            return;
        }
        float feet = playerCollider != null ? playerCollider.bounds.min.y : p.y - 0.5f;
        foreach (Cover cover in covers)
        {
            bool behind = u >= Mathf.Min(cover.shelterFrom, cover.shelterTo) && u <= Mathf.Max(cover.shelterFrom, cover.shelterTo);
            if (behind && feet <= cover.top)
            {
                Sheltered = true;
                game.AddHeat(warmPerSecond * Time.deltaTime);
                if (carton != null && Random.value < Time.deltaTime * 6f)
                {
                    carton.PuffSteam(2);
                }
                return;
            }
        }
        game.RespawnPlayer();
    }

    // Bäume (und Büsche) der Stadt im Brandbereich bekommen Feuer und Rauch in die Krone. Die Stadt bleibt geladen, darum
    // werden die Flammen nicht an die Bäume gehängt, sondern als eigene Objekte in die Level-Szene gesetzt (verschwinden mit ihr)
    private void IgniteTrees()
    {
        if (treeFireTemplate == null)
        {
            return;
        }
        foreach (Transform candidate in FindObjectsByType<Transform>())
        {
            if (treeFires.Count >= maxBurningTrees)
            {
                break;
            }
            if ((candidate.name != "Tree" && candidate.name != "Bush") || candidate.gameObject.scene == gameObject.scene)
            {
                continue;
            }
            Vector3 p = candidate.position;
            if (!burnArea.Contains(new Vector2(p.x, p.z)))
            {
                continue;
            }
            float height = candidate.name == "Tree" ? 3.8f * candidate.lossyScale.y : 0.8f;
            ParticleSystem fire = Instantiate(treeFireTemplate, p + Vector3.up * height, Quaternion.Euler(-90f, 0f, 0f), transform);
            fire.gameObject.SetActive(true);
            // Nicht alle gleichzeitig: Das Feuer greift von Baum zu Baum über
            ParticleSystem.MainModule main = fire.main;
            main.startDelay = Random.Range(0.5f, 2f);
            fire.Play();
            treeFires.Add(fire.gameObject);
        }
    }
}
