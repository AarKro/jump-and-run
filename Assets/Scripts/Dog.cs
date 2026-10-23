using System.Collections;
using UnityEngine;

// Hund: ergänzt EnemyPlatformPatrol um Bellen vor dem Losrennen und reagiert auf Draufspringen
// (benommen mit Sternchen, dann rennt er in den Hintergrund davon). Die Bewegung selbst macht weiterhin die Patrouille.
// Aufgebaut von Tools > Jump and Run > Build Dog Prefab.
[RequireComponent(typeof(EnemyPlatformPatrol))]
public class Dog : MonoBehaviour, IStompHandler
{
    [Header("Farben")]
    [SerializeField] private Color furColor = new Color(0.55f, 0.35f, 0.18f);
    [SerializeField] private Color earColor = new Color(0.3f, 0.18f, 0.08f);
    [SerializeField] private Renderer[] furRenderers;
    [SerializeField] private Renderer[] earRenderers;

    [Header("Körperteile")]
    [Tooltip("Die ganze Hunde-Optik. Wird von der Patrouille gespiegelt, hier gehüpft und gedreht")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform tail;
    [SerializeField] private Transform[] legs;
    [Tooltip("Sprechblase \"WOOF!\". Hängt bewusst außerhalb von Body, damit der Text nie gespiegelt wird")]
    [SerializeField] private GameObject woofBubble;
    [SerializeField] private Transform dizzyStars;

    [Header("Bellen")]
    [Tooltip("So lange bleibt der Hund stehen und bellt, bevor er losrennt (die Warnung für den Spieler)")]
    [SerializeField] private float barkDuration = 0.5f;
    [SerializeField] private float barkHopHeight = 0.25f;

    [Header("Laufen")]
    [SerializeField] private float legSwingAngle = 30f;
    [SerializeField] private float legSwingSpeed = 14f;
    [SerializeField] private float tailWagAngle = 25f;
    [SerializeField] private float tailWagSpeed = 12f;

    [Header("Benommen und weglaufen")]
    [SerializeField] private float dizzyDuration = 1f;
    [SerializeField] private float starSpinSpeed = 360f;
    [SerializeField] private float runAwaySpeed = 6f;
    [Tooltip("So weit läuft der Hund in den Hintergrund (+Z), bevor er verschwindet")]
    [SerializeField] private float runAwayDistance = 12f;

    private EnemyPlatformPatrol patrol;
    private bool wasChasing;
    private bool stomped;
    private Vector3 bodyStartPosition;
    private Quaternion bodyStartRotation;
    private float lastX;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        patrol = GetComponent<EnemyPlatformPatrol>();
        bodyStartPosition = body.localPosition;
        bodyStartRotation = body.localRotation;
        lastX = transform.position.x;
        GameManager.LevelReset += ResetDog;

        woofBubble.SetActive(false);
        dizzyStars.gameObject.SetActive(false);
        ApplyColors();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetDog;
    }

    private void OnValidate()
    {
        ApplyColors();
    }

    // Wie beim Kunden: Farbe pro Renderer über einen MaterialPropertyBlock, alle Hunde teilen sich Dog.mat
    private void ApplyColors()
    {
        SetColor(furRenderers, furColor);
        SetColor(earRenderers, earColor);
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

    private void Update()
    {
        if (stomped)
        {
            return;
        }

        // Spieler ist gerade auf die Plattform gekommen: erst bellen, dann darf die Patrouille losrennen
        bool chasing = patrol.IsChasing;
        if (chasing && !wasChasing)
        {
            StopAllCoroutines();
            StartCoroutine(Bark());
        }
        wasChasing = chasing;

        // Geschwindigkeit aus der Positionsänderung, weil der kinematische Rigidbody keine Geschwindigkeit meldet
        float speed = Time.deltaTime > 0f ? Mathf.Abs(transform.position.x - lastX) / Time.deltaTime : 0f;
        lastX = transform.position.x;
        AnimateRun(speed > 0.1f);
    }

    private IEnumerator Bark()
    {
        patrol.HoldPosition = true;
        woofBubble.SetActive(true);

        for (float t = 0f; t < barkDuration; t += Time.deltaTime)
        {
            float k = t / barkDuration;
            // Ein kleiner Hüpfer: Parabel, 0 am Anfang und Ende
            body.localPosition = bodyStartPosition + Vector3.up * (4f * barkHopHeight * k * (1f - k));
            yield return null;
        }

        body.localPosition = bodyStartPosition;
        woofBubble.SetActive(false);
        patrol.HoldPosition = false;
    }

    private void AnimateRun(bool moving)
    {
        float t = Time.time;
        // Diagonal gegenüberliegende Beine schwingen gleich (wie beim Traben), daher abwechselnd +/-
        for (int i = 0; i < legs.Length; i++)
        {
            float sign = (i % 2 == 0) ? 1f : -1f;
            float angle = moving ? Mathf.Sin(t * legSwingSpeed) * legSwingAngle * sign : 0f;
            legs[i].localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Der Schwanz wedelt immer, beim Laufen schneller
        float wagSpeed = moving ? tailWagSpeed * 1.5f : tailWagSpeed;
        tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * wagSpeed) * tailWagAngle);
    }

    // Von EnemyDamage aufgerufen. Collider und Patrouille sind da schon aus, nur dieses Skript läuft weiter
    public void OnStomped()
    {
        stomped = true;
        StopAllCoroutines();
        woofBubble.SetActive(false);
        body.localPosition = bodyStartPosition;
        StartCoroutine(DizzyThenRunAway());
    }

    private IEnumerator DizzyThenRunAway()
    {
        dizzyStars.gameObject.SetActive(true);
        for (float t = 0f; t < dizzyDuration; t += Time.deltaTime)
        {
            dizzyStars.Rotate(0f, starSpinSpeed * Time.deltaTime, 0f);
            // Benommen hin und her schwanken
            body.localRotation = bodyStartRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 15f) * 10f);
            AnimateRun(false);
            yield return null;
        }
        dizzyStars.gameObject.SetActive(false);

        // Zum Hintergrund drehen: Die Optik schaut nach +X, -90° um Y dreht +X nach +Z.
        // Spiegelung aufheben, sonst würde ein nach links schauender Hund rückwärts laufen
        Vector3 scale = body.localScale;
        scale.x = Mathf.Abs(scale.x);
        body.localScale = scale;
        body.localRotation = bodyStartRotation * Quaternion.Euler(0f, -90f, 0f);

        for (float run = 0f; run < runAwayDistance; run += runAwaySpeed * Time.deltaTime)
        {
            transform.position += Vector3.forward * (runAwaySpeed * Time.deltaTime);
            AnimateRun(true);
            yield return null;
        }

        // Nur ausblenden, damit er beim Respawn des Spielers zurückkommt
        gameObject.SetActive(false);
    }

    // Position, Aktivierung und Collider setzt EnemyDamage zurück, die Spiegelung die Patrouille. Hier nur die Optik
    private void ResetDog()
    {
        StopAllCoroutines();
        stomped = false;
        wasChasing = false;
        body.localPosition = bodyStartPosition;
        body.localRotation = bodyStartRotation;
        woofBubble.SetActive(false);
        dizzyStars.gameObject.SetActive(false);
        lastX = transform.position.x;
    }
}
