using System.Collections;
using TMPro;
using UnityEngine;

// Zeigt am Café, ob der Checkpoint schon erreicht ist:
// vorher dunkel (Fenster aus, Schild matt, "CLOSED" an der Tür, schwebende Kaffeetasse als Hinweis, Tafel "CLOSED"),
// danach hell (Fenster an, Schild leuchtet, "OPEN"-Neon flackert an, Tafel klappt auf "OPEN", Tasse ploppt weg).
[RequireComponent(typeof(Checkpoint))]
public class CoffeeShopEffects : MonoBehaviour
{
    [Header("Fenster und Schild")]
    [SerializeField] private Renderer[] shopWindows;
    [SerializeField] private Material windowsClosed;
    [SerializeField] private Material windowsOpen;
    [SerializeField] private TextMeshPro signText;
    [SerializeField] private Color signClosedColor = new Color(0.5f, 0.55f, 0.5f);
    [SerializeField] private Color signOpenColor = new Color(1f, 0.98f, 0.9f);

    [Header("Schilder")]
    [SerializeField] private GameObject closedDoorSign;
    [SerializeField] private GameObject neonOff;
    [SerializeField] private GameObject neonOn;
    [Tooltip("Gehweg-Tafel mit CLOSED vorne und OPEN hinten, dreht sich beim Aktivieren um 180°")]
    [SerializeField] private Transform chalkboard;
    [SerializeField] private float flipDuration = 0.45f;

    [Header("Hinweis-Tasse")]
    [SerializeField] private Transform cupHint;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private ParticleSystem sparkles;
    [SerializeField] private int sparkleCount = 30;

    private Checkpoint checkpoint;
    private Vector3 cupStartPosition;
    private Vector3 cupStartScale;
    private bool open;

    private void Awake()
    {
        checkpoint = GetComponent<Checkpoint>();
        checkpoint.Activated += OnActivated;
        if (cupHint != null)
        {
            cupStartPosition = cupHint.localPosition;
            cupStartScale = cupHint.localScale;
        }
        ShowClosed();
    }

    private void OnDestroy()
    {
        checkpoint.Activated -= OnActivated;
    }

    private void Update()
    {
        if (open || cupHint == null)
        {
            return;
        }
        // Schweben und drehen, damit die Tasse auffällt
        cupHint.localPosition = cupStartPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        cupHint.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    // Für ein neues Level (die Stadt bleibt geladen): Ist das Café dort der Checkpoint, startet es geschlossen mit Tasse.
    // Sonst ist es einfach ein offenes Café, ohne Flackern und Effekte
    public void ResetLook(bool closed)
    {
        StopAllCoroutines();
        open = !closed;
        if (cupHint != null)
        {
            cupHint.localPosition = cupStartPosition;
            cupHint.localScale = cupStartScale;
            cupHint.gameObject.SetActive(closed);
        }
        if (chalkboard != null)
        {
            chalkboard.localRotation = Quaternion.Euler(0f, closed ? 0f : 180f, 0f);
        }
        if (closed)
        {
            ShowClosed();
            return;
        }
        SetWindows(windowsOpen);
        if (signText != null)
        {
            signText.color = signOpenColor;
        }
        SetActive(closedDoorSign, false);
        SetActive(neonOff, false);
        SetActive(neonOn, true);
    }

    private void ShowClosed()
    {
        SetWindows(windowsClosed);
        if (signText != null)
        {
            signText.color = signClosedColor;
        }
        SetActive(closedDoorSign, true);
        SetActive(neonOff, true);
        SetActive(neonOn, false);
    }

    private void OnActivated()
    {
        open = true;
        SetWindows(windowsOpen);
        if (signText != null)
        {
            signText.color = signOpenColor;
        }
        SetActive(closedDoorSign, false);
        StartCoroutine(FlickerNeon());
        StartCoroutine(FlipChalkboard());
        StartCoroutine(PopCup());
    }

    // Neonröhren springen nicht sofort an, sondern flackern kurz
    private IEnumerator FlickerNeon()
    {
        float[] pattern = { 0.06f, 0.08f, 0.05f, 0.15f, 0.06f };
        bool on = false;
        foreach (float wait in pattern)
        {
            on = !on;
            SetActive(neonOn, on);
            SetActive(neonOff, !on);
            yield return new WaitForSeconds(wait);
        }
        SetActive(neonOn, true);
        SetActive(neonOff, false);
    }

    private IEnumerator FlipChalkboard()
    {
        if (chalkboard == null)
        {
            yield break;
        }
        for (float t = 0f; t < flipDuration; t += Time.deltaTime)
        {
            float k = t / flipDuration;
            // Etwas über das Ziel hinaus und zurück (wie ein Schild, das nachwackelt)
            float overshoot = Mathf.Sin(k * Mathf.PI) * 0.15f;
            chalkboard.localRotation = Quaternion.Euler(0f, 180f * Mathf.SmoothStep(0f, 1f, k) * (1f + overshoot), 0f);
            yield return null;
        }
        chalkboard.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private IEnumerator PopCup()
    {
        if (cupHint == null)
        {
            yield break;
        }
        if (sparkles != null)
        {
            sparkles.transform.position = cupHint.position;
            sparkles.Emit(sparkleCount);
        }
        const float growTime = 0.12f;
        const float shrinkTime = 0.18f;
        for (float t = 0f; t < growTime; t += Time.deltaTime)
        {
            cupHint.localScale = cupStartScale * Mathf.Lerp(1f, 1.6f, t / growTime);
            yield return null;
        }
        for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
        {
            cupHint.localScale = cupStartScale * Mathf.Lerp(1.6f, 0f, t / shrinkTime);
            yield return null;
        }
        cupHint.gameObject.SetActive(false);
    }

    private void SetWindows(Material material)
    {
        if (material == null || shopWindows == null)
        {
            return;
        }
        foreach (Renderer window in shopWindows)
        {
            window.sharedMaterial = material;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
