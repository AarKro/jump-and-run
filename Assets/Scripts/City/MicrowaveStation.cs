using System.Collections;
using TMPro;
using UnityEngine;

// Mikrowellen-Station im 24/7-Kiosk: Läuft der Spieler in den Trigger, geht er zur Theke, schiebt den Pizzakarton
// in die Mikrowelle, "DING!", nimmt ihn wieder heraus und läuft weiter, mit wieder ofenfrischer Pizza (100%).
// Einmal pro Level (Level 4): Danach "USED", auch nach einem Respawn. Sonst könnte man im Hafen sterben, am Kiosk-Checkpoint
// wieder auftauchen und gleich wieder aufwärmen. Erst ein Neustart (kalte Pizza) macht sie wieder bereit.
public class MicrowaveStation : MonoBehaviour
{
    [Header("Wege (Spieler-Mitte)")]
    [Tooltip("Vor der Theke, hinter der Laufbahn")]
    [SerializeField] private Transform standPoint;
    [Tooltip("Zurück auf der Laufbahn (Z = 0)")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float walkDuration = 0.45f;
    [Tooltip("An: nach einem Respawn bleibt sie benutzt (nur ein Neustart setzt sie zurück). Aus: einmal pro Leben")]
    [SerializeField] private bool oncePerLevel = true;

    [Header("Mikrowelle")]
    [Tooltip("Tür-Drehpunkt am linken Rand")]
    [SerializeField] private Transform door;
    [SerializeField] private float doorOpenAngle = 100f;
    [SerializeField] private float doorSwingDuration = 0.15f;
    [Tooltip("Hier liegt der Karton beim Aufwärmen. Dreht sich wie ein Drehteller")]
    [SerializeField] private Transform cartonSpot;
    [SerializeField] private float cartonMoveDuration = 0.25f;
    [SerializeField] private float cookDuration = 1.4f;
    [SerializeField] private float turntableSpeed = 180f;
    [SerializeField] private Renderer doorGlass;
    [SerializeField] private Material glassOff;
    [Tooltip("Leuchtendes Glas, solange sie läuft")]
    [SerializeField] private Material glassOn;
    [SerializeField] private TextMeshPro display;
    [SerializeField] private GameObject dingBubble;

    private static readonly Color ReadyColor = new Color(0.4f, 1f, 0.45f);
    private static readonly Color BusyColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color UsedColor = new Color(1f, 0.35f, 0.3f);

    private bool used;
    private bool running;

    private void Awake()
    {
        GameManager.LevelReset += ResetStation;
        ShowReady();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetStation;
    }

    private void ResetStation()
    {
        if (running || (oncePerLevel && used))
        {
            return;
        }
        ShowReady();
    }

    // Für ein neues Level (die Stadt mit dem Kiosk bleibt geladen). Wurde das alte Level mitten im Aufwärmen entladen
    // (z.B. Pizza kalt), liegt noch der Karton des alten Spielers auf dem Drehteller: der kommt weg
    public void ResetForNewLevel()
    {
        StopAllCoroutines();
        running = false;
        for (int i = cartonSpot.childCount - 1; i >= 0; i--)
        {
            Destroy(cartonSpot.GetChild(i).gameObject);
        }
        cartonSpot.localRotation = Quaternion.identity;
        door.localRotation = Quaternion.identity;
        SetGlass(glassOff);
        ShowReady();
    }

    private void ShowReady()
    {
        used = false;
        SetDisplay("READY", ReadyColor);
        if (dingBubble != null)
        {
            dingBubble.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used || running || !other.CompareTag("Player"))
        {
            return;
        }

        GameManager game = GameManager.Instance;
        if (game != null && (game.LevelFinished || game.IsRespawning || game.InCutscene))
        {
            return;
        }

        PlayerLife life = other.GetComponent<PlayerLife>();
        PizzaCarton carton = other.GetComponentInChildren<PizzaCarton>();
        if (life == null || carton == null)
        {
            return;
        }
        // Die Szene läuft auf dem Spieler, nicht auf dem Kiosk: Der Kiosk gehört der Stadt und überlebt einen Neustart
        // (Pizza kalt mitten im Aufwärmen), der Spieler nicht. Eine Coroutine stirbt mit ihrem MonoBehaviour, so läuft sie
        // nicht mit dem zerstörten Spieler weiter (MissingReferenceException). Den Rest räumt ResetForNewLevel auf
        life.StartCoroutine(Reheat(life, carton));
    }

    private IEnumerator Reheat(PlayerLife life, PizzaCarton carton)
    {
        used = true;
        running = true;
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            game.InCutscene = true;
        }

        life.BeginScripted();
        yield return life.WalkTo(standPoint.position, walkDuration);

        // Karton übernehmen: PizzaCarton aus, sonst setzt es den Karton in LateUpdate wieder neben den Spieler
        carton.enabled = false;
        Transform cartonTransform = carton.transform;
        Transform holder = cartonTransform.parent;
        Vector3 heldPosition = cartonTransform.localPosition;
        Quaternion heldRotation = cartonTransform.localRotation;

        yield return SwingDoor(0f, doorOpenAngle);
        yield return MoveCarton(cartonTransform, cartonSpot, Vector3.zero, Quaternion.identity);
        yield return SwingDoor(doorOpenAngle, 0f);

        // Läuft: Glas leuchtet, Countdown im Display, der Karton dreht sich auf dem Drehteller
        SetGlass(glassOn);
        for (float t = 0f; t < cookDuration; t += Time.deltaTime)
        {
            cartonSpot.Rotate(0f, turntableSpeed * Time.deltaTime, 0f);
            SetDisplay($"0:0{Mathf.CeilToInt(cookDuration - t)}", BusyColor);
            yield return null;
        }
        cartonSpot.localRotation = Quaternion.identity;
        SetGlass(glassOff);

        // DING! Die Pizza ist wieder heiß
        SetDisplay("DING", BusyColor);
        if (game != null)
        {
            game.ReheatPizza();
        }
        StartCoroutine(PopDingBubble());

        yield return SwingDoor(0f, doorOpenAngle);
        yield return MoveCarton(cartonTransform, holder, heldPosition, heldRotation);
        carton.enabled = true;
        StartCoroutine(SwingDoor(doorOpenAngle, 0f));

        yield return life.WalkTo(exitPoint.position, walkDuration);
        life.EndScripted();
        if (game != null)
        {
            game.InCutscene = false;
        }
        SetDisplay("USED", UsedColor);
        running = false;
    }

    // Im Bogen von der aktuellen Position zum Ziel, dort umhängen. Ziel in lokalen Koordinaten des neuen Elternteils
    private IEnumerator MoveCarton(Transform carton, Transform newParent, Vector3 localTarget, Quaternion localRotation)
    {
        Vector3 from = carton.position;
        Quaternion fromRotation = carton.rotation;
        for (float t = 0f; t < cartonMoveDuration; t += Time.deltaTime)
        {
            float k = t / cartonMoveDuration;
            Vector3 to = newParent.TransformPoint(localTarget);
            Vector3 position = Vector3.Lerp(from, to, k);
            position.y += Mathf.Sin(k * Mathf.PI) * 0.25f;
            carton.position = position;
            carton.rotation = Quaternion.Slerp(fromRotation, newParent.rotation * localRotation, k);
            yield return null;
        }
        carton.SetParent(newParent, false);
        carton.localPosition = localTarget;
        carton.localRotation = localRotation;
    }

    private IEnumerator SwingDoor(float from, float to)
    {
        for (float t = 0f; t < doorSwingDuration; t += Time.deltaTime)
        {
            door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, t / doorSwingDuration), 0f);
            yield return null;
        }
        door.localRotation = Quaternion.Euler(0f, to, 0f);
    }

    private IEnumerator PopDingBubble()
    {
        if (dingBubble == null)
        {
            yield break;
        }
        dingBubble.SetActive(true);
        Transform bubble = dingBubble.transform;
        const float popTime = 0.15f;
        for (float t = 0f; t < popTime; t += Time.deltaTime)
        {
            bubble.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.2f, t / popTime);
            yield return null;
        }
        bubble.localScale = Vector3.one;
        yield return new WaitForSeconds(1.2f);
        dingBubble.SetActive(false);
    }

    private void SetGlass(Material material)
    {
        if (doorGlass != null && material != null)
        {
            doorGlass.sharedMaterial = material;
        }
    }

    private void SetDisplay(string text, Color color)
    {
        if (display != null)
        {
            display.text = text;
            display.color = color;
        }
    }
}
