using System.Collections;
using UnityEngine;

// Entenküken: läuft die Spur der Mutter (Duck) in festem Abstand nach, dadurch watschelt die Familie in einer Reihe,
// auch durch Kurven und im Flatterbogen über einen Bach. Berührung = Respawn (EnemyDamage), Draufspringen = das Küken
// rennt piepsend zur Kamera davon (nur dieses). Wird die Mutter getroffen, stieben alle Küken auseinander (Scatter).
// Kinematischer Rigidbody wie beim Hund: bewegt in FixedUpdate, gerendert interpoliert.
[RequireComponent(typeof(Rigidbody))]
public class Duckling : MonoBehaviour, IStompHandler
{
    [SerializeField] private Duck mother;
    [Tooltip("So viele Meter hinter der Mutter auf ihrer Spur (pro Küken mehr, dann laufen sie in einer Reihe)")]
    [SerializeField] private float spacing = 0.7f;
    [Tooltip("Die Optik: wird je nach Laufrichtung gespiegelt")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform[] legs;
    [SerializeField] private float waddleAngle = 12f;
    [SerializeField] private float waddleSpeed = 22f;

    [Header("Davonlaufen")]
    [SerializeField] private float runAwaySpeed = 5f;
    [Tooltip("So weit zur Kamera (lokal -Z), dann ist es unten aus dem Bild")]
    [SerializeField] private float runAwayDistance = 10f;
    [Tooltip("Beim Auseinanderstieben läuft jedes Küken etwas schräg (Grad), damit sie nicht in einer Reihe flüchten")]
    [SerializeField] private float scatterSpread = 35f;

    private Rigidbody rb;
    private Collider ownCollider;
    private float height;
    private Vector3 bodyScale;
    private Quaternion bodyStartRotation;
    private Vector3 lastPosition;
    private bool fleeing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        height = transform.position.y;
        bodyScale = body.localScale;
        bodyStartRotation = body.localRotation;
        GameManager.LevelReset += ResetDuckling;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetDuckling;
    }

    private void Start()
    {
        SnapToTrail();
    }

    // Sofort an die richtige Stelle der Spur (Start und Respawn), ohne dass die Interpolation hinübergleitet
    private void SnapToTrail()
    {
        Duck.TrailSample sample = mother.Sample(spacing);
        Vector3 position = Lane.Point(sample.u) + Vector3.up * (height + sample.hop);
        transform.SetPositionAndRotation(position, Lane.Frame(sample.u));
        rb.position = position;
        rb.rotation = transform.rotation;
        SetFacing(sample.facing);
        lastPosition = position;
    }

    private void FixedUpdate()
    {
        if (fleeing)
        {
            return;
        }
        Duck.TrailSample sample = mother.Sample(spacing);
        rb.MovePosition(Lane.Point(sample.u) + Vector3.up * (height + sample.hop));
        rb.MoveRotation(Lane.Frame(sample.u));
        SetFacing(sample.facing);
    }

    private void SetFacing(float facing)
    {
        Vector3 scale = bodyScale;
        scale.x = Mathf.Abs(bodyScale.x) * (facing >= 0f ? 1f : -1f);
        body.localScale = scale;
    }

    private void Update()
    {
        if (fleeing)
        {
            return;
        }
        float speed = Time.deltaTime > 0f ? Vector3.Distance(transform.position, lastPosition) / Time.deltaTime : 0f;
        lastPosition = transform.position;
        Waddle(speed > 0.1f);
    }

    private void Waddle(bool moving)
    {
        float t = Time.time + spacing;
        body.localRotation = bodyStartRotation * Quaternion.Euler(moving ? Mathf.Sin(t * waddleSpeed) * waddleAngle : 0f, 0f, 0f);
        for (int i = 0; i < legs.Length; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            legs[i].localRotation = Quaternion.Euler(0f, 0f, moving ? Mathf.Sin(t * waddleSpeed) * 40f * sign : 0f);
        }
    }

    // Draufgesprungen (von EnemyDamage, Collider ist schon aus): nur dieses Küken rennt weg
    public void OnStomped()
    {
        StartCoroutine(RunAway(0f));
    }

    // Die Mutter wurde getroffen: harmlos werden und davonlaufen, jedes Küken in eine etwas andere Richtung
    public void Scatter()
    {
        if (fleeing || !gameObject.activeInHierarchy)
        {
            return;
        }
        ownCollider.enabled = false;
        float spread = (transform.GetSiblingIndex() % 3 - 1) * scatterSpread;
        StartCoroutine(RunAway(spread));
    }

    // Zur Kamera (lokal -Z) und unten aus dem Bild, dann ausblenden (kommt beim Respawn zurück)
    private IEnumerator RunAway(float spread)
    {
        fleeing = true;
        rb.interpolation = RigidbodyInterpolation.None;
        Vector3 direction = Quaternion.Euler(0f, spread, 0f) * -transform.forward;
        // Optik in Laufrichtung drehen: Sie schaut nach lokal +X, +90° um Y dreht +X nach -Z
        body.localScale = new Vector3(Mathf.Abs(bodyScale.x), bodyScale.y, bodyScale.z);
        body.localRotation = bodyStartRotation * Quaternion.Euler(0f, 90f - spread, 0f);
        Vector3 start = transform.position;
        for (float run = 0f; run < runAwayDistance; run += runAwaySpeed * Time.deltaTime)
        {
            // Kleine Hüpfer beim Rennen
            transform.position = start + direction * run + Vector3.up * (Mathf.Abs(Mathf.Sin(run * 4f)) * 0.12f);
            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 30f) * 45f * (i % 2 == 0 ? 1f : -1f));
            }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    // Nach einem Respawn wieder da und hinter der Mutter in der Reihe. In welcher Reihenfolge Unity die LevelReset-Empfänger
    // aufruft, ist nicht festgelegt: darum die Spur der Mutter hier selbst zurücksetzen, bevor das Küken sich einreiht
    private void ResetDuckling()
    {
        mother.ResetTrail();
        StopAllCoroutines();
        fleeing = false;
        ownCollider.enabled = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        body.localRotation = bodyStartRotation;
        gameObject.SetActive(true);
        SnapToTrail();
    }
}
