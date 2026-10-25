using System.Collections.Generic;
using UnityEngine;

// Feueratem der Riesenechse (Level 5): ein Strahl aus dem Maul, der sich zum Boden hin kegelförmig aufweitet und auf einem
// Stück der Laufbahn und im Park daneben aufschlägt. KaijuBattle steuert, wann geladen (Maul glüht) und wann gespien wird.
// - Trifft den Spieler, solange er im Feuerbereich der Bahn ist und nicht hinter einer Deckung steht: Respawn.
// - Hinter einer Deckung (Stück der Bahn direkt hinter einem Hindernis, vom Strahl abgewandt, Füße nicht höher als dessen
//   Oberkante) ist er sicher, und weil links und rechts das Feuer lodert, wird die Pizza dabei langsam wieder warm.
// - Beim ersten Atem fangen die Bäume des Parks im Feuerbereich Feuer (nur Optik, sie brennen danach weiter).
// Alles entlang der Bahn in u gerechnet (Lane.Project), nicht mit Physik-Strahlen: so ist es genau und gut einstellbar.
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

    [Header("Strahl")]
    [Tooltip("Punkt im Maul (Kind des Kopfes)")]
    [SerializeField] private Transform mouth;
    [Tooltip("Hier schlägt der Strahl auf (Welt)")]
    [SerializeField] private Vector3 target;
    [Tooltip("Drehpunkt des Strahls: wird jeden Frame auf das Ziel gedreht und auf die Länge gestreckt (lokal Z = Länge 1)")]
    [SerializeField] private Transform beam;
    [SerializeField] private ParticleSystem beamParticles;
    [Tooltip("Flammen am Boden (Bahn und Park), brennen nur während des Atems")]
    [SerializeField] private ParticleSystem[] groundFires;
    [SerializeField] private Light impactLight;
    [Tooltip("Glut im Maul, wächst beim Aufladen")]
    [SerializeField] private Transform mouthGlow;

    [Header("Treffer")]
    [Tooltip("Feuerbereich auf der Bahn (u von/bis)")]
    [SerializeField] private float hitFromU;
    [SerializeField] private float hitToU;
    [SerializeField] private Cover[] covers;
    [Tooltip("So viel Wärme pro Sekunde (Anteil des Balkens) hinter einer Deckung, während das Feuer brennt")]
    [SerializeField] private float warmPerSecond = 0.03f;
    [Tooltip("Erst nach dieser Zeit im Atem trifft es (der Strahl muss erst ankommen)")]
    [SerializeField] private float hitDelay = 0.35f;

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
    private PlayerController playerController;
    private Collider playerCollider;
    private PizzaCarton carton;
    private float breathTime;
    private bool treesLit;
    private float charge;
    private readonly List<GameObject> treeFires = new List<GameObject>();

    private void Awake()
    {
        SetBeamVisible(false);
        SetCharge(0f);
    }

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
            playerCollider = playerController.GetComponent<Collider>();
            carton = playerController.GetComponentInChildren<PizzaCarton>(true);
        }
    }

    // 0 = Maul zu, 1 = voll aufgeladen (Glut groß und hell)
    public void SetCharge(float k)
    {
        charge = Mathf.Clamp01(k);
        if (mouthGlow != null)
        {
            mouthGlow.gameObject.SetActive(charge > 0.01f);
            mouthGlow.localScale = Vector3.one * Mathf.Lerp(0.5f, 5f, charge);
        }
    }

    public void StartBreath()
    {
        Breathing = true;
        breathTime = 0f;
        SetBeamVisible(true);
        if (!treesLit)
        {
            treesLit = true;
            IgniteTrees();
        }
    }

    public void StopBreath()
    {
        Breathing = false;
        Sheltered = false;
        SetBeamVisible(false);
    }

    private void SetBeamVisible(bool on)
    {
        if (beam != null)
        {
            beam.gameObject.SetActive(on);
        }
        if (beamParticles != null)
        {
            if (on) beamParticles.Play(); else beamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (groundFires != null)
        {
            foreach (ParticleSystem fire in groundFires)
            {
                if (on) fire.Play(); else fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        if (impactLight != null)
        {
            impactLight.enabled = on;
        }
    }

    private void LateUpdate()
    {
        if (!Breathing || beam == null || mouth == null)
        {
            return;
        }
        breathTime += Time.deltaTime;
        // Strahl vom Maul zum Ziel, flackert in der Dicke
        Vector3 from = mouth.position;
        Vector3 delta = target - from;
        beam.SetPositionAndRotation(from, Quaternion.LookRotation(delta));
        if (beamParticles != null)
        {
            beamParticles.transform.SetPositionAndRotation(from, beam.rotation);
        }
        float flicker = 1f + Mathf.Sin(Time.time * 37f) * 0.06f + Mathf.Sin(Time.time * 23f) * 0.04f;
        // Am Anfang schießt er erst heraus (Länge wächst in 0.3 s)
        float grow = Mathf.Clamp01(breathTime / hitDelay);
        beam.localScale = new Vector3(flicker, flicker, delta.magnitude * grow);
        if (impactLight != null)
        {
            impactLight.intensity = 40f * grow * flicker;
        }
    }

    private void Update()
    {
        Sheltered = false;
        if (!Breathing || breathTime < hitDelay || player == null)
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
        float feet = playerCollider != null ? playerCollider.bounds.min.y : player.position.y - 0.5f;
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
            main.startDelay = Random.Range(0f, 1.5f);
            fire.Play();
            treeFires.Add(fire.gameObject);
        }
    }
}
