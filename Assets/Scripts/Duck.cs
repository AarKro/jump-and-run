using System.Collections;
using UnityEngine;

// Entenmutter (Level 3, Pesto Park): ergänzt GroundPatrol (schneller, kurzsichtiger als der Hund, flattert über Lücken)
// um Watscheln, "QUACK!" beim Entdecken, gestreckten Hals und Flügelschlagen beim Verfolgen. Draufgesprungen flattert
// sie in den Himmel davon (Federn), ihre Küken stieben auseinander.
// Sie schreibt ihre Spur mit (Bahn-Position, Flatter-Höhe, Blickrichtung, gelaufene Strecke): Die Küken laufen dieselbe
// Spur in festem Abstand nach (nach Strecke, nicht nach Zeit: Bleibt sie stehen, bleiben die Küken in ihrer Reihe stehen,
// statt in sie hineinzulaufen). Dreht sie um, läuft sie ihnen auf der eigenen Spur entgegen, die Reihe klappt zu einem
// Knäuel zusammen (Lücke zum Drüberspringen).
// Aufgebaut von Tools > Jump and Run > City > Build Park Prefabs.
[RequireComponent(typeof(GroundPatrol))]
public class Duck : MonoBehaviour, IStompHandler
{
    public struct TrailSample
    {
        public float u;
        public float hop;
        public float facing;
        public float distance;   // gelaufene Strecke bis hierher
    }

    [Header("Körperteile")]
    [Tooltip("Die ganze Enten-Optik. Die Patrouille spiegelt und dreht sie, hier hüpft sie beim Quaken")]
    [SerializeField] private Transform body;
    [Tooltip("Kind von Body: kippt beim Watscheln (eigener Drehpunkt, sonst überschriebe er das Umdrehen der Patrouille)")]
    [SerializeField] private Transform waddle;
    [SerializeField] private Transform neck;
    [SerializeField] private Transform wingLeft;
    [SerializeField] private Transform wingRight;
    [SerializeField] private Transform[] legs;
    [Tooltip("Sprechblase \"QUACK!\", hängt außerhalb von Body (wird nicht gespiegelt)")]
    [SerializeField] private GameObject quackBubble;
    [SerializeField] private ParticleSystem feathers;
    [SerializeField] private Duckling[] ducklings;

    [Header("Watscheln")]
    [SerializeField] private float waddleAngle = 9f;
    [SerializeField] private float waddleSpeed = 14f;
    [SerializeField] private float legSwingAngle = 35f;
    [Tooltip("Hals beim Verfolgen nach vorne gestreckt (Grad)")]
    [SerializeField] private float neckStretch = 55f;
    [SerializeField] private float wingFlapAngle = 50f;
    [SerializeField] private float wingFlapSpeed = 22f;

    [Header("Quaken")]
    [SerializeField] private float quackDuration = 0.4f;
    [SerializeField] private float quackHopHeight = 0.2f;

    [Header("Davonfliegen")]
    [SerializeField] private float flyAwayTime = 1.6f;
    [SerializeField] private float flyAwayHeight = 9f;
    [Tooltip("So weit fliegt sie nach hinten (lokal +Z, vom Weg weg zum Teich)")]
    [SerializeField] private float flyAwayBack = 7f;
    [SerializeField] private int featherCount = 25;

    private const int TrailLength = 128;
    // Neuer Spur-Punkt nach so viel Strecke (oder wenn sie sich umdreht)
    private const float TrailStep = 0.05f;
    private readonly TrailSample[] trail = new TrailSample[TrailLength];
    private int newest;
    // Selbst ausgerechnet statt patrol.StartU: Ob GroundPatrol.Awake vor diesem Awake läuft, ist nicht festgelegt
    private float startU;

    private GroundPatrol patrol;
    private bool wasChasing;
    private bool stomped;
    private float quackTimer;
    private Vector3 bodyStartPosition;
    private Quaternion bodyStartRotation;
    private Vector3 lastPosition;

    private void Awake()
    {
        patrol = GetComponent<GroundPatrol>();
        bodyStartPosition = body.localPosition;
        bodyStartRotation = body.localRotation;
        lastPosition = transform.position;
        startU = Lane.Project(transform.position);
        quackBubble.SetActive(false);
        GameManager.LevelReset += ResetDuck;
        ResetTrail();
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetDuck;
    }

    // Spur so vorbelegen, als wäre sie schon eine Weile mit Patrouillen-Tempo aus der Gegenrichtung gekommen:
    // Dann stehen die Küken vom ersten Frame an in einer Reihe hinter ihr. Hängt nur von Startwerten ab, darf also
    // mehrfach laufen (die Küken rufen es beim Respawn selbst auf, falls ihr LevelReset vor dem der Mutter kommt)
    public void ResetTrail()
    {
        float facing = patrol.StartFacing;
        for (int i = 0; i < TrailLength; i++)
        {
            // i = 0 ist der älteste Eintrag
            int age = TrailLength - 1 - i;
            trail[i] = new TrailSample { u = startU - facing * TrailStep * age, hop = 0f, facing = facing, distance = TrailStep * i };
        }
        newest = TrailLength - 1;
    }

    // Wo war die Mutter, als sie "behind" Meter weniger gelaufen war? Zwischen zwei Spur-Punkten wird interpoliert
    public TrailSample Sample(float behind)
    {
        float target = trail[newest].distance - behind;
        for (int k = 0; k < TrailLength - 1; k++)
        {
            TrailSample newer = trail[(newest - k + TrailLength) % TrailLength];
            TrailSample older = trail[(newest - k - 1 + TrailLength) % TrailLength];
            if (older.distance <= target)
            {
                float span = newer.distance - older.distance;
                float t = span > 0f ? (target - older.distance) / span : 1f;
                return new TrailSample
                {
                    u = Mathf.Lerp(older.u, newer.u, t),
                    hop = Mathf.Lerp(older.hop, newer.hop, t),
                    facing = t >= 1f ? newer.facing : older.facing,
                    distance = target,
                };
            }
        }
        return trail[(newest + 1) % TrailLength];
    }

    private void FixedUpdate()
    {
        if (stomped)
        {
            return;
        }
        TrailSample last = trail[newest];
        float moved = Mathf.Abs(patrol.U - last.u) + Mathf.Abs(patrol.HopOffset - last.hop);
        if (moved < TrailStep && patrol.FacingDirection == last.facing)
        {
            return;
        }
        newest = (newest + 1) % TrailLength;
        trail[newest] = new TrailSample { u = patrol.U, hop = patrol.HopOffset, facing = patrol.FacingDirection, distance = last.distance + moved };
    }

    private void Update()
    {
        if (stomped)
        {
            return;
        }

        bool chasing = patrol.IsChasing;
        if (chasing && !wasChasing)
        {
            StopAllCoroutines();
            StartCoroutine(Quack());
        }
        wasChasing = chasing;

        float speed = Time.deltaTime > 0f ? Vector3.Distance(transform.position, lastPosition) / Time.deltaTime : 0f;
        lastPosition = transform.position;
        Animate(speed > 0.1f, chasing, patrol.IsFlapping);
    }

    private IEnumerator Quack()
    {
        patrol.HoldPosition = true;
        quackBubble.SetActive(true);
        for (float t = 0f; t < quackDuration; t += Time.deltaTime)
        {
            float k = t / quackDuration;
            body.localPosition = bodyStartPosition + Vector3.up * (4f * quackHopHeight * k * (1f - k));
            yield return null;
        }
        body.localPosition = bodyStartPosition;
        quackBubble.SetActive(false);
        patrol.HoldPosition = false;
    }

    // Watscheln = Körper kippt im Takt seitlich hin und her (um die Laufrichtung, lokal X), Beine abwechselnd.
    // Verfolgen: Hals nach vorne gestreckt und Flügel schlagen, im Flattern schlagen sie doppelt so schnell
    private void Animate(bool moving, bool chasing, bool flapping)
    {
        float t = Time.time;
        float roll = moving ? Mathf.Sin(t * waddleSpeed) * waddleAngle : 0f;
        waddle.localRotation = Quaternion.Euler(roll, 0f, 0f);

        for (int i = 0; i < legs.Length; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            legs[i].localRotation = Quaternion.Euler(0f, 0f, moving && !flapping ? Mathf.Sin(t * waddleSpeed) * legSwingAngle * sign : 0f);
        }

        neck.localRotation = Quaternion.Euler(0f, 0f, chasing ? -neckStretch : 0f);
        float flap = flapping ? wingFlapSpeed * 2f : wingFlapSpeed;
        float wing = chasing || flapping ? Mathf.Abs(Mathf.Sin(t * flap)) * wingFlapAngle : 0f;
        wingLeft.localRotation = Quaternion.Euler(-wing, 0f, 0f);
        wingRight.localRotation = Quaternion.Euler(wing, 0f, 0f);
    }

    // Von EnemyDamage aufgerufen. Collider und Patrouille sind schon aus, nur dieses Skript läuft weiter
    public void OnStomped()
    {
        stomped = true;
        StopAllCoroutines();
        quackBubble.SetActive(false);
        foreach (Duckling duckling in ducklings)
        {
            duckling.Scatter();
        }
        if (feathers != null)
        {
            feathers.transform.position = transform.position + Vector3.up * 0.3f;
            feathers.Emit(featherCount);
        }
        StartCoroutine(FlyAway());
    }

    // Steil hoch und nach hinten weg, Flügel schlagen wild. Dann nur ausblenden (kommt beim Respawn zurück)
    private IEnumerator FlyAway()
    {
        Vector3 start = transform.position;
        Vector3 back = transform.forward;
        for (float t = 0f; t < flyAwayTime; t += Time.deltaTime)
        {
            float k = t / flyAwayTime;
            transform.position = start + Vector3.up * (flyAwayHeight * k * k) + back * (flyAwayBack * k);
            Animate(false, true, true);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    // Position, Aktivierung und Collider setzt EnemyDamage zurück, Spiegelung und Bahn die Patrouille. Hier Optik und Spur
    private void ResetDuck()
    {
        StopAllCoroutines();
        stomped = false;
        wasChasing = false;
        body.localPosition = bodyStartPosition;
        body.localRotation = bodyStartRotation;
        waddle.localRotation = Quaternion.identity;
        quackBubble.SetActive(false);
        lastPosition = transform.position;
        ResetTrail();
    }
}
