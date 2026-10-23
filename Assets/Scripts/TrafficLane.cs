using System.Collections.Generic;
using UnityEngine;

// Eine Fahrspur der Querstraße: Autos kommen in festem Takt aus dem Hintergrund (+Z) und fahren
// Richtung Kamera (-Z) durch die Laufbahn des Spielers (Z = 0). Die Warnung ist, dass man sie kommen sieht.
//
// Der Takt ist deterministisch: Aus der Spur-Uhr wird für jedes Auto direkt ausgerechnet, wo es gerade ist.
// Auto Nummer n startet zur Zeit n * interval am hinteren Ende. Beim Respawn springt die Uhr auf 0,
// dadurch sieht man nach jedem Respawn genau dieselbe Verkehrssituation.
public class TrafficLane : MonoBehaviour
{
    [Header("Takt")]
    [SerializeField] private float speed = 12f;
    [Tooltip("Sekunden zwischen zwei Autos")]
    [SerializeField] private float interval = 2.5f;
    [Tooltip("Verschiebt den Takt dieser Spur (z.B. halbes Intervall für die zweite Spur)")]
    [SerializeField] private float startOffset;

    [Header("Strecke (lokales Z)")]
    [Tooltip("Hier tauchen die Autos auf, weit im Hintergrund")]
    [SerializeField] private float spawnZ = 40f;
    [Tooltip("Hier verschwinden sie wieder, hinter der Kamera (die steht bei etwa Z = -8)")]
    [SerializeField] private float despawnZ = -15f;
    [Tooltip("Fahrbahnhöhe (lokales Y) über lokalem Z. Damit rollen die Autos über den erhöhten Zebrastreifen. " +
             "Waagerecht bei 0 = flache Straße")]
    [SerializeField] private AnimationCurve heightOverZ = AnimationCurve.Constant(-100f, 100f, 0f);

    [Header("Autos")]
    [Tooltip("Ausgeschaltete Vorlage als Kind der Spur. Daraus wird beim Start der Pool erzeugt")]
    [SerializeField] private Car carTemplate;

    private readonly List<Car> pool = new List<Car>();
    // Welche Autonummer (n) jedes Pool-Auto gerade fährt, -1 = frei
    private int[] poolSlot;
    private float laneTime;

    private float TravelTime => (spawnZ - despawnZ) / speed;

    private void Awake()
    {
        GameManager.LevelReset += ResetLane;

        // Object Pool: Einmal genug Autos für die längste Situation anlegen und dann immer wiederverwenden.
        // Kein Instantiate/Destroy im laufenden Spiel, also auch keine Ruckler durch den Garbage Collector
        int poolSize = Mathf.FloorToInt(TravelTime / interval) + 2;
        poolSlot = new int[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            Car car = Instantiate(carTemplate, transform);
            car.name = "Car " + (i + 1);
            car.gameObject.SetActive(false);
            pool.Add(car);
            poolSlot[i] = -1;
        }
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetLane;
    }

    private void ResetLane()
    {
        laneTime = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(false);
            poolSlot[i] = -1;
        }
    }

    // FixedUpdate, weil die Autos Physik-Collider haben: Bewegt werden sie über den kinematischen Rigidbody im Physik-Takt
    private void FixedUpdate()
    {
        laneTime += Time.fixedDeltaTime;
        float t = laneTime - startOffset;

        // Welche Autonummern sind gerade unterwegs? Gestartet (n * interval <= t) und noch nicht durch (t - n * interval <= TravelTime)
        int newest = t >= 0f ? Mathf.FloorToInt(t / interval) : -1;
        int oldest = Mathf.Max(0, Mathf.CeilToInt((t - TravelTime) / interval));

        for (int i = 0; i < pool.Count; i++)
        {
            // Pool-Platz i fährt immer die Autonummern i, i + poolSize, i + 2 * poolSize, ...
            int n = FindCarNumber(i, oldest, newest);
            Car car = pool[i];

            if (n < 0)
            {
                if (poolSlot[i] != -1)
                {
                    car.gameObject.SetActive(false);
                    poolSlot[i] = -1;
                }
                continue;
            }

            float z = spawnZ - speed * (t - n * interval);
            // Die Kurve ist hier eine Funktion Z → Höhe: Evaluate liest den Wert an der Stelle z ab
            Vector3 position = transform.TransformPoint(new Vector3(0f, heightOverZ.Evaluate(z), z));

            if (poolSlot[i] != n)
            {
                // Neu losgefahren: hinten hinsetzen (teleportieren), dann erst einschalten
                poolSlot[i] = n;
                car.transform.SetPositionAndRotation(position, transform.rotation);
                car.Repaint(n);
                car.gameObject.SetActive(true);
                car.Body.position = position;
            }
            else
            {
                // MovePosition: der kinematische Rigidbody fährt dorthin, mit Interpolation ruckelfrei zwischen den Physik-Schritten
                car.Body.MovePosition(position);
            }
        }
    }

    private int FindCarNumber(int poolIndex, int oldest, int newest)
    {
        for (int n = oldest; n <= newest; n++)
        {
            if (n % pool.Count == poolIndex)
            {
                return n;
            }
        }
        return -1;
    }
}
