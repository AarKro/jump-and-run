using System.Collections;
using UnityEngine;

// Chili-Schote: Einsammeln wärmt die Pizza wieder auf (ein Stück des Wärmebalkens).
// Schwebt und schaukelt, damit sie auffällt. Beim Einsammeln ploppt sie auf, sprüht Glut und der Karton dampft kurz.
public class HeatPickup : MonoBehaviour
{
    [Header("Wärme")]
    [Tooltip("Anteil des ganzen Wärmebalkens. 1/6 = eine halbe Münz-Stufe (die Stufen liegen bei 1/3 und 2/3)")]
    [SerializeField] private float heatFraction = 1f / 6f;
    [Tooltip("Taucht nach dem Tod wieder auf. Aus lassen, sonst kann man durch Sterben Wärme farmen")]
    [SerializeField] private bool respawnOnLevelReset;

    [Header("Animation")]
    [Tooltip("Das Modell (Kind), das schwebt und schaukelt. Die Wurzel mit dem Trigger bleibt stehen")]
    [SerializeField] private Transform model;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2.5f;
    [Tooltip("Schaukeln um die Hochachse in Grad. Kein volles Drehen: Von vorne gesehen wäre die Schote nur ein roter Klumpen")]
    [SerializeField] private float swayAngle = 30f;
    [SerializeField] private float swaySpeed = 2f;

    [Header("Effekte")]
    [Tooltip("Dauer-Flämmchen an der Schote, stoppt beim Einsammeln")]
    [SerializeField] private ParticleSystem flames;
    [SerializeField] private ParticleSystem burst;
    [SerializeField] private int burstCount = 25;
    [SerializeField] private int steamPuffCount = 15;

    private Collider trigger;
    private Vector3 modelStartPosition;
    private Vector3 modelStartScale;
    private Quaternion modelStartRotation;
    private bool collected;

    private void Awake()
    {
        trigger = GetComponent<Collider>();
        modelStartPosition = model.localPosition;
        modelStartScale = model.localScale;
        modelStartRotation = model.localRotation;
        GameManager.LevelReset += Respawn;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= Respawn;
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }
        model.localPosition = modelStartPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        // Erst schaukeln (um die Hochachse), dann die Schräglage aus dem Prefab: Reihenfolge der Multiplikation zählt
        model.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * swaySpeed) * swayAngle, 0f) * modelStartRotation;
    }

    private void Respawn()
    {
        if (!respawnOnLevelReset)
        {
            return;
        }
        StopAllCoroutines();
        collected = false;
        trigger.enabled = true;
        model.localPosition = modelStartPosition;
        model.localScale = modelStartScale;
        gameObject.SetActive(true);
        if (flames != null)
        {
            flames.Play();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player"))
        {
            return;
        }
        collected = true;
        trigger.enabled = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddHeat(heatFraction);
        }
        PizzaCarton carton = other.GetComponentInChildren<PizzaCarton>();
        if (carton != null)
        {
            carton.PuffSteam(steamPuffCount);
        }
        StartCoroutine(Collect());
    }

    // Aufploppen und verschwinden. Erst ausschalten, wenn die Glut verflogen ist: Ein ausgeschaltetes Objekt
    // nimmt seine Kind-Partikelsysteme mit, die Funken wären sonst sofort weg.
    // Nur ausblenden statt zerstören, damit LevelReset die Chili zurückholen kann
    private IEnumerator Collect()
    {
        if (burst != null)
        {
            burst.Emit(burstCount);
        }
        if (flames != null)
        {
            flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        const float growTime = 0.1f;
        const float shrinkTime = 0.15f;
        for (float t = 0f; t < growTime; t += Time.deltaTime)
        {
            model.localScale = modelStartScale * Mathf.Lerp(1f, 1.5f, t / growTime);
            yield return null;
        }
        for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
        {
            model.localScale = modelStartScale * Mathf.Lerp(1.5f, 0f, t / shrinkTime);
            yield return null;
        }
        model.localScale = Vector3.zero;
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
    }
}
