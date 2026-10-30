using UnityEngine;

// Brennende Stelle auf dem Gehweg (Level 5, unterer Mozzarella Blvd) mit einem Feuerwehrmann, der sie im festen Takt löscht.
// Ein Takt: brennt (Berührung = Respawn) → wird gelöscht (der Strahl liegt auf der Stelle: wer drinsteht, wird nass, die
// Pizza kühlt ab, kein Respawn) → sicher (nass, dampft) → flackert als Warnung (harmlos) → brennt wieder.
// Zwischen den Löschphasen spritzt der Feuerwehrmann ins brennende Haus hinter der Bahn: Senkt er den Strahl, kommt das
// Löschen (so sieht man es kommen).
//
// Wie bei den Frisbees und Autos ist der Takt deterministisch: Aus der Uhr seit dem Levelstart wird die Phase direkt
// ausgerechnet, nach jedem Respawn startet die Uhr neu. Ohne Feuerwehrmann und mit alwaysBurning brennt die Stelle immer
// (das breite Feuer unter der Drehleiter).
// Der Trigger-Collider über der Stelle sitzt auf derselben Wurzel (lokal X = Länge).
// Aufgebaut von Tools > Jump and Run > City > Build Fire Prefabs.
public class FirePatch : MonoBehaviour
{
    private enum Phase { Burning, Spraying, Safe, Flicker }

    [Header("Takt")]
    [Tooltip("Brennt immer (kein Feuerwehrmann, kein Takt)")]
    [SerializeField] private bool alwaysBurning;
    [SerializeField] private float burnTime = 3f;
    [Tooltip("So lange liegt der Strahl auf der Stelle (wer drinsteht, wird nass)")]
    [SerializeField] private float sprayTime = 1.5f;
    [Tooltip("So lange ist die Stelle danach gefahrlos")]
    [SerializeField] private float safeTime = 2f;
    [Tooltip("Kleine Flammen als Warnung, noch harmlos")]
    [SerializeField] private float flickerTime = 0.5f;
    [Tooltip("Verschiebt den Takt (Sekunden), damit mehrere Stellen nicht gleichzeitig gelöscht werden")]
    [SerializeField] private float startOffset;

    [Header("Nass werden")]
    [Tooltip("So viel Wärme kostet es, vom Strahl getroffen zu werden (Anteil des ganzen Balkens, einmal pro Löschen)")]
    [SerializeField] private float soakHeat = 1f / 12f;
    [SerializeField] private int soakSteamPuff = 14;

    [Header("Wasserstrahl")]
    [Tooltip("Austrittsgeschwindigkeit (m/s). Die Tropfen fliegen ballistisch, die Neigung der Düse wird passend berechnet")]
    [SerializeField] private float jetSpeed = 13f;
    [SerializeField] private float jetGravity = 1f;
    [Tooltip("So lange vor dem Löschen senkt er den Strahl vom Haus auf die Stelle (gehört noch zur Brennphase)")]
    [SerializeField] private float aimDownTime = 0.45f;
    [SerializeField] private float aimUpTime = 0.25f;
    [Tooltip("Ziel im Haus (lokal), wenn er nicht gerade die Stelle löscht")]
    [SerializeField] private Vector3 houseTarget = new Vector3(1.5f, 5f, 3f);
    [Tooltip("Beim Löschen fährt der Strahl entlang der Stelle hin und her (halbe Länge)")]
    [SerializeField] private float sweepHalf = 2.8f;
    [Tooltip("In dieser Höhe (lokal) trifft der Strahl beim Löschen: Boden 0.5, bei der hohen Feuersäule in die Flammen")]
    [SerializeField] private float sprayHeight = 0.5f;

    [Header("Teile")]
    [SerializeField] private ParticleSystem fire;
    [Tooltip("Heller Kern der Flammen (optional)")]
    [SerializeField] private ParticleSystem fireCore;
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private ParticleSystem steam;
    [SerializeField] private Light glow;
    [Tooltip("Dunkler, halbdurchsichtiger Fleck: so lange die Stelle nass ist")]
    [SerializeField] private Renderer wet;
    [SerializeField] private Transform firefighter;
    [SerializeField] private Transform armLeft;
    [SerializeField] private Transform armRight;
    [Tooltip("Düse in der Hand: Partikel des Strahls fliegen entlang ihrer lokalen Z-Achse")]
    [SerializeField] private Transform nozzle;
    [SerializeField] private ParticleSystem jet;
    [SerializeField] private ParticleSystem splash;

    [Header("Farben")]
    [SerializeField] private Color coat = new Color(1f, 0.8f, 0.12f);
    [SerializeField] private Color skin = new Color(0.9f, 0.7f, 0.55f);
    [SerializeField] private Renderer[] coatRenderers;
    [SerializeField] private Renderer[] skinRenderers;

    private float clock;
    private int soakedCycle = -1;
    private float fireRate, coreRate, smokeRate, glowIntensity;
    private ParticleSystem.MinMaxCurve fireSize, coreSize;
    private float wetAlpha;
    private MaterialPropertyBlock wetBlock;
    private PizzaCarton carton;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private float Period => burnTime + sprayTime + safeTime + flickerTime;
    private float CycleTime => Mathf.Repeat(clock + startOffset, Period);
    private int Cycle => Mathf.FloorToInt((clock + startOffset) / Period);

    private void Awake()
    {
        GameManager.LevelReset += ResetClock;
        fireRate = fire.emission.rateOverTime.constant;
        fireSize = fire.main.startSize;
        if (fireCore != null)
        {
            coreRate = fireCore.emission.rateOverTime.constant;
            coreSize = fireCore.main.startSize;
        }
        smokeRate = smoke != null ? smoke.emission.rateOverTime.constant : 0f;
        glowIntensity = glow != null ? glow.intensity : 0f;
        if (wet != null)
        {
            wetAlpha = wet.sharedMaterial.GetColor(BaseColorId).a;
        }
        ApplyColors();
        ResetClock();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetClock;
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    private void ResetClock()
    {
        clock = 0f;
        soakedCycle = -1;
    }

    // Takt in FixedUpdate wie die Frisbee: Der Trigger soll mit derselben Uhr urteilen, mit der die Physik rechnet
    private void FixedUpdate()
    {
        clock += Time.fixedDeltaTime;
    }

    private Phase CurrentPhase(out float k)
    {
        k = 0f;
        if (alwaysBurning)
        {
            return Phase.Burning;
        }
        float t = CycleTime;
        if (t < burnTime)
        {
            k = t / burnTime;
            return Phase.Burning;
        }
        t -= burnTime;
        if (t < sprayTime)
        {
            k = t / sprayTime;
            return Phase.Spraying;
        }
        t -= sprayTime;
        if (t < safeTime)
        {
            k = t / safeTime;
            return Phase.Safe;
        }
        k = (t - safeTime) / flickerTime;
        return Phase.Flicker;
    }

    // OnTriggerStay statt Enter: Wer noch auf der Stelle steht, wenn sie wieder aufflammt, verbrennt auch
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        GameManager game = GameManager.Instance;
        if (game == null || game.LevelFinished || game.IsRespawning || game.InCutscene)
        {
            return;
        }
        Phase phase = CurrentPhase(out _);
        if (phase == Phase.Burning)
        {
            game.RespawnPlayer();
        }
        else if (phase == Phase.Spraying && soakedCycle != Cycle)
        {
            // Einmal pro Löschen: Sonst würde der Strahl in jedem Physik-Schritt neu abziehen
            soakedCycle = Cycle;
            game.AddHeat(-soakHeat);
            if (carton == null)
            {
                carton = other.GetComponentInChildren<PizzaCarton>();
            }
            if (carton != null)
            {
                carton.PuffSteam(soakSteamPuff);
            }
            if (splash != null)
            {
                var burst = new ParticleSystem.EmitParams { position = other.bounds.center + Vector3.up * 0.6f, applyShapeToPosition = true };
                splash.Emit(burst, 30);
            }
        }
    }

    // Optik jeden Frame: Flammen, Rauch, Dampf, nasser Fleck, Feuerwehrmann und Strahl
    private void Update()
    {
        Phase phase = CurrentPhase(out float k);
        float flames = 1f, size = 1f, steamAmount = 0f, wetness = 0f;
        switch (phase)
        {
            case Phase.Spraying:
                // Das Feuer geht in der ersten Hälfte des Löschens aus, der Dampf steigt
                flames = 1f - Mathf.Clamp01(k * 2f);
                steamAmount = Mathf.Clamp01(k * 2f);
                wetness = Mathf.Clamp01(k * 3f);
                break;
            case Phase.Safe:
                flames = 0f;
                steamAmount = 1f - k * 0.8f;
                wetness = 1f - k * 0.3f;
                break;
            case Phase.Flicker:
                // Ein paar kleine Flammen züngeln wieder (Warnung)
                flames = 0.35f;
                size = 0.45f;
                wetness = 0.7f * (1f - k);
                break;
        }

        SetFlames(fire, fireRate * flames, fireSize, size);
        if (fireCore != null)
        {
            SetFlames(fireCore, coreRate * flames, coreSize, size);
        }
        if (smoke != null)
        {
            ParticleSystem.EmissionModule smokeEmission = smoke.emission;
            smokeEmission.rateOverTime = smokeRate * flames;
        }
        if (steam != null)
        {
            ParticleSystem.EmissionModule steamEmission = steam.emission;
            steamEmission.rateOverTime = 30f * steamAmount;
        }
        if (glow != null)
        {
            // Etwas Flackern, damit das Licht lebt
            glow.intensity = glowIntensity * flames * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, transform.position.x));
        }
        if (wet != null)
        {
            // Einmal anlegen statt jedes Bild ein neuer Block (sonst Müll für den Garbage Collector). Erst hier: Nach einem
            // Neukompilieren mitten im Play Mode sind nicht gespeicherte Felder leer
            wetBlock ??= new MaterialPropertyBlock();
            wet.GetPropertyBlock(wetBlock);
            Color color = wet.sharedMaterial.GetColor(BaseColorId);
            color.a = wetAlpha * wetness;
            wetBlock.SetColor(BaseColorId, color);
            wet.SetPropertyBlock(wetBlock);
        }

        if (firefighter != null)
        {
            AimJet();
        }
    }

    // Größe zwischen zwei Werten (Zufall pro Teilchen): beide Grenzen skalieren
    private static void SetFlames(ParticleSystem system, float rate, ParticleSystem.MinMaxCurve baseSize, float scale)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        ParticleSystem.MainModule main = system.main;
        main.startSize = new ParticleSystem.MinMaxCurve(baseSize.constantMin * scale, baseSize.constantMax * scale);
    }

    // Wohin der Strahl gerade zielt (Welt): ins Haus, beim Löschen auf die Stelle (fährt sie ab), dazwischen schwenkt er
    private Vector3 JetTarget()
    {
        float t = CycleTime;
        // Etwas Bewegung im Haus, damit der Strahl nicht starr steht
        Vector3 house = houseTarget + new Vector3(Mathf.Sin(Time.time * 0.9f) * 0.8f, Mathf.Sin(Time.time * 1.3f) * 0.4f, 0f);
        Vector3 patchStart = new Vector3(-sweepHalf, sprayHeight, 0f);
        Vector3 local;
        if (t < burnTime - aimDownTime)
        {
            local = house;
        }
        else if (t < burnTime)
        {
            local = Vector3.Lerp(house, patchStart, Mathf.SmoothStep(0f, 1f, (t - (burnTime - aimDownTime)) / aimDownTime));
        }
        else if (t < burnTime + sprayTime)
        {
            // Einmal hin und zurück über die ganze Länge
            float k = (t - burnTime) / sprayTime;
            local = new Vector3(-sweepHalf * Mathf.Cos(k * 2f * Mathf.PI), sprayHeight, 0f);
        }
        else if (t < burnTime + sprayTime + aimUpTime)
        {
            local = Vector3.Lerp(patchStart, house, Mathf.SmoothStep(0f, 1f, (t - burnTime - sprayTime) / aimUpTime));
        }
        else
        {
            local = house;
        }
        return transform.TransformPoint(local);
    }

    private void AimJet()
    {
        Vector3 target = JetTarget();
        // Erst den Körper zum Ziel drehen (nur um die Hochachse), dann die Arme heben, dann die Düse genau ausrichten:
        // Die Düse hängt an der Hand, ihre Position hängt also von der Körper- und Armstellung ab
        Vector3 flat = target - firefighter.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f)
        {
            // Die Figur schaut bei yaw 0 nach -Z, darum die Blickrichtung umdrehen
            firefighter.rotation = Quaternion.LookRotation(-flat.normalized, Vector3.up);
        }

        Vector3 from = nozzle.position;
        Vector3 direction = LaunchDirection(from, target, jetSpeed, Physics.gravity.magnitude * jetGravity, out float flightTime);
        float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        // Arme: 90° = waagerecht nach vorne, mehr = schräg nach oben
        Quaternion arms = Quaternion.Euler(90f + elevation, 0f, 0f);
        // Leicht nach innen (um Z), damit beide Hände an der Düse vor der Brust liegen
        armLeft.localRotation = arms * Quaternion.Euler(0f, 0f, 12f);
        armRight.localRotation = arms * Quaternion.Euler(0f, 0f, -12f);
        nozzle.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Lebensdauer = Flugzeit: Die Tropfen verschwinden genau dort, wo sie aufschlagen (gilt für neue Partikel)
        ParticleSystem.MainModule main = jet.main;
        main.startSpeed = jetSpeed;
        main.gravityModifier = jetGravity;
        main.startLifetime = flightTime;
        if (splash != null)
        {
            splash.transform.position = target;
        }
    }

    // Wurfparabel: Unter welchem Winkel muss man mit Geschwindigkeit v werfen, um das Ziel zu treffen? Die flache der beiden
    // Lösungen. Reicht v nicht, nimmt man 45° (größte Weite)
    private static Vector3 LaunchDirection(Vector3 from, Vector3 to, float v, float g, out float flightTime)
    {
        Vector3 delta = to - from;
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float d = Mathf.Max(0.01f, flat.magnitude);
        float h = delta.y;
        float v2 = v * v;
        float root = v2 * v2 - g * (g * d * d + 2f * h * v2);
        float angle = root >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * d)) : Mathf.PI / 4f;
        flightTime = d / (v * Mathf.Cos(angle));
        return (flat / d) * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
    }

    private void ApplyColors()
    {
        SetColor(coatRenderers, coat);
        SetColor(skinRenderers, skin);
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
