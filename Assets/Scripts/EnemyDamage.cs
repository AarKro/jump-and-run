using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hat ein Gegner eine Komponente mit diesem Interface, entscheidet sie selbst, was beim Draufspringen passiert
// (statt plattgedrückt zu werden), z.B. der Hund wird benommen und läuft weg.
// Sie muss den Gegner am Ende selbst ausblenden (SetActive(false)). Zurückgesetzt wird über GameManager.LevelReset.
public interface IStompHandler
{
    void OnStomped();
}

public class EnemyDamage : MonoBehaviour
{
    [Header("Draufspringen")]
    [Tooltip("Draufgesprungen = Unterkante des Spielers liegt höchstens so viel unter der Oberkante des Gegners. " +
             "Etwas Spielraum, weil die Physik die Körper beim Aufprall leicht ineinanderschiebt")]
    [SerializeField] private float stompHeightTolerance = 0.2f;
    [SerializeField] private float squashDuration = 0.1f;
    [Tooltip("So lange bleibt der plattgedrückte Gegner sichtbar, bevor er verschwindet")]
    [SerializeField] private float squashedLingerTime = 0.3f;

    private bool squashed;
    private Vector3 startPosition;
    private Vector3 startScale;
    private RigidbodyInterpolation startInterpolation;
    // Collider ist in Unity kein Behaviour (hat aber auch .enabled), daher zwei Listen
    private readonly List<Behaviour> disabledOnSquash = new List<Behaviour>();
    private readonly List<Collider> collidersDisabledOnSquash = new List<Collider>();

    private void Awake()
    {
        startPosition = transform.position;
        startScale = transform.localScale;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            startInterpolation = body.interpolation;
        }
        GameManager.LevelReset += ResetEnemy;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetEnemy;
    }

    // Beim Respawn des Players: plattgedrückte Gegner wiederbeleben
    private void ResetEnemy()
    {
        if (!squashed)
        {
            return;
        }

        StopAllCoroutines();
        squashed = false;
        transform.localScale = startScale;
        transform.position = startPosition;

        foreach (Behaviour behaviour in disabledOnSquash)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }
        disabledOnSquash.Clear();
        foreach (Collider enemyCollider in collidersDisabledOnSquash)
        {
            if (enemyCollider != null)
            {
                enemyCollider.enabled = true;
            }
        }
        collidersDisabledOnSquash.Clear();

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = startPosition;
            body.interpolation = startInterpolation;
        }

        gameObject.SetActive(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other.gameObject);
    }

    private void HandleContact(GameObject other)
    {
        if (squashed || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && IsStomp(player))
        {
            IStompHandler handler = GetComponent<IStompHandler>();
            DisableEnemy(handler as Behaviour);
            if (handler != null)
            {
                handler.OnStomped();
            }
            else
            {
                StartCoroutine(SquashRoutine());
            }
            player.StompBounce();
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayer();
        }
    }

    // Von oben getroffen (Höhenvergleich der Collider-Kanten) und der Player ist nicht gerade im Aufsteigen.
    // Der Höhenvergleich funktioniert für jede Gegnerform; ein Winkel zur Mitte wäre bei langen Gegnern (Hund) ungenau
    private bool IsStomp(PlayerController player)
    {
        Collider playerCollider = player.GetComponent<Collider>();
        Collider enemyCollider = GetComponent<Collider>();
        if (playerCollider == null || enemyCollider == null)
        {
            return false;
        }

        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        bool notRising = playerBody == null || playerBody.linearVelocity.y <= 1f;
        bool fromAbove = playerCollider.bounds.min.y >= enemyCollider.bounds.max.y - stompHeightTolerance;
        return fromAbove && notRising;
    }

    // Sofort harmlos machen. "keepEnabled" (der Stomp-Handler) läuft weiter, damit er seine Reaktion abspielen kann
    private void DisableEnemy(Behaviour keepEnabled)
    {
        squashed = true;

        // Sofort harmlos machen: keine Kollision, keine Bewegung mehr
        // Gemerkt, damit ResetEnemy genau diese wieder einschalten kann
        disabledOnSquash.Clear();
        collidersDisabledOnSquash.Clear();
        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>())
        {
            if (enemyCollider.enabled)
            {
                enemyCollider.enabled = false;
                collidersDisabledOnSquash.Add(enemyCollider);
            }
        }
        // Alle anderen Skripte (Patrouille, Flugbewegung, ...) anhalten
        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour != this && behaviour != keepEnabled && behaviour.enabled)
            {
                behaviour.enabled = false;
                disabledOnSquash.Add(behaviour);
            }
        }

        // Sonst überschreibt die Interpolation des Rigidbody unsere Positionsänderungen beim Plattdrücken
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.interpolation = RigidbodyInterpolation.None;
        }
    }

    private IEnumerator SquashRoutine()
    {
        Vector3 startScale = transform.localScale;
        Vector3 squashedScale = new Vector3(startScale.x * 1.4f, startScale.y * 0.25f, startScale.z * 1.4f);

        // Unterkante bleibt auf der Plattform stehen, während der Gegner flach wird
        Vector3 startPosition = transform.position;
        float bottom = startPosition.y - 0.5f * startScale.y;

        for (float t = 0f; t < squashDuration; t += Time.deltaTime)
        {
            float k = t / squashDuration;
            transform.localScale = Vector3.Lerp(startScale, squashedScale, k);
            transform.position = new Vector3(startPosition.x, bottom + 0.5f * transform.localScale.y, startPosition.z);
            yield return null;
        }
        transform.localScale = squashedScale;
        transform.position = new Vector3(startPosition.x, bottom + 0.5f * squashedScale.y, startPosition.z);

        yield return new WaitForSeconds(squashedLingerTime);
        // Nur ausblenden statt zerstören, damit der Gegner beim Respawn zurückkommen kann
        gameObject.SetActive(false);
    }
}
