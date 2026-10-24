using System.Collections;
using TMPro;
using UnityEngine;

// Bauwagen als Checkpoint (Level 3, Baustelle am Mozzarella Blvd):
// vorher dunkel (Fenster aus, Rundumleuchte aus, Tor zu mit "CLOSED"-Schild),
// danach Fenster an, die gelbe Rundumleuchte dreht sich, das Tor schwingt auf und "Checkpoint unlocked!" steigt auf.
// Gehört zum Level (nicht zur Stadt), beim Neustart wird es mit der Level-Szene neu geladen: kein Zurücksetzen nötig
[RequireComponent(typeof(Checkpoint))]
public class SiteOfficeEffects : MonoBehaviour
{
    [Header("Fenster")]
    [SerializeField] private Renderer[] windows;
    [SerializeField] private Material windowsClosed;
    [SerializeField] private Material windowsOpen;

    [Header("Rundumleuchte")]
    [SerializeField] private Renderer beaconGlass;
    [SerializeField] private Material beaconOff;
    [SerializeField] private Material beaconOn;
    [Tooltip("Dreht sich, wenn die Leuchte an ist. Trägt den Lichtkegel (Spot Light)")]
    [SerializeField] private Transform beaconRotor;
    [SerializeField] private float beaconSpeed = 360f;

    [Header("Tor")]
    [Tooltip("Zwei Torflügel mit Drehpunkt am Scharnier: links öffnet mit -openAngle, rechts mit +openAngle (nach innen)")]
    [SerializeField] private Transform gateLeft;
    [SerializeField] private Transform gateRight;
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float gateDuration = 0.6f;
    [SerializeField] private GameObject closedSign;

    [Header("Meldung")]
    [SerializeField] private TextMeshPro unlockedText;
    [SerializeField] private float textRise = 1.2f;
    [SerializeField] private float textHoldTime = 1.2f;
    [SerializeField] private float textFadeTime = 0.6f;
    [SerializeField] private ParticleSystem sparkles;
    [SerializeField] private int sparkleCount = 30;

    private Checkpoint checkpoint;
    private Vector3 textStartPosition;
    private bool beaconRunning;

    private void Awake()
    {
        checkpoint = GetComponent<Checkpoint>();
        checkpoint.Activated += OnActivated;
        if (unlockedText != null)
        {
            textStartPosition = unlockedText.transform.localPosition;
        }
        SetLit(false);
    }

    private void OnDestroy()
    {
        checkpoint.Activated -= OnActivated;
    }

    private void Update()
    {
        if (beaconRunning && beaconRotor != null)
        {
            beaconRotor.Rotate(0f, beaconSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    private void OnActivated()
    {
        SetLit(true);
        if (closedSign != null)
        {
            closedSign.SetActive(false);
        }
        StartCoroutine(OpenGate());
        StartCoroutine(CheckpointPopup.Play(unlockedText, textStartPosition, sparkles, sparkleCount, textRise, textHoldTime, textFadeTime));
    }

    private void SetLit(bool lit)
    {
        if (windows != null)
        {
            foreach (Renderer window in windows)
            {
                window.sharedMaterial = lit ? windowsOpen : windowsClosed;
            }
        }
        if (beaconGlass != null)
        {
            beaconGlass.sharedMaterial = lit ? beaconOn : beaconOff;
        }
        if (beaconRotor != null)
        {
            beaconRotor.gameObject.SetActive(lit);
        }
        beaconRunning = lit;
    }

    // Beide Flügel schwingen nach innen auf, mit kleinem Nachfedern am Ende (wie ein schweres Tor, das anschlägt)
    private IEnumerator OpenGate()
    {
        for (float t = 0f; t < gateDuration; t += Time.deltaTime)
        {
            float k = t / gateDuration;
            float angle = openAngle * (Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.08f);
            SetGate(angle);
            yield return null;
        }
        SetGate(openAngle);
    }

    private void SetGate(float angle)
    {
        if (gateLeft != null)
        {
            gateLeft.localRotation = Quaternion.Euler(0f, -angle, 0f);
        }
        if (gateRight != null)
        {
            gateRight.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
