using UnityEngine;

// Auf jemandem landen (z.B. auf dem Kopf eines Frisbee-Spielers): kurz eine Sprechblase "HEY!". Nur eine Reaktion,
// passiert ist sonst nichts. Sitzt auf dem Objekt mit dem Collider; OnCollisionEnter kommt auch ohne eigenen Rigidbody an,
// weil der Spieler einen hat
public class HeyBubble : MonoBehaviour
{
    [Tooltip("Sprechblase, im Prefab ausgeschaltet. Hängt nicht an der gedrehten Figur, damit der Text zur Kamera schaut")]
    [SerializeField] private GameObject bubble;
    [SerializeField] private float showTime = 1f;
    [Tooltip("Als 'von oben' gilt: Füße des Spielers höchstens so viel unter der Oberkante")]
    [SerializeField] private float fromAboveTolerance = 0.25f;

    private float hideAt;

    private void Awake()
    {
        bubble.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        Collider own = GetComponent<Collider>();
        if (collision.collider.bounds.min.y < own.bounds.max.y - fromAboveTolerance)
        {
            return;
        }
        bubble.SetActive(true);
        hideAt = Time.time + showTime;
    }

    private void Update()
    {
        if (bubble.activeSelf && Time.time >= hideAt)
        {
            bubble.SetActive(false);
        }
    }
}
