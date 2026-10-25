using UnityEngine;

// Person, die in Level 5 vor der Echse flieht (nur Kulisse, keine Kollision): rennt mit hochgerissenen Armen (CrowdFigure
// mit panic) auf einer Geraden von "from" nach "to" und fängt dann wieder bei "from" an. Damit niemand vor der Kamera
// verschwindet oder auftaucht, wird nur zurückgesetzt, wenn weder die Person noch der Startpunkt im Bild sind; sonst rennt
// sie einfach über "to" hinaus weiter (höchstens overrun Meter). Die Höhe folgt dem Boden (Strahl nach unten), so geht
// sie auch über die abgesenkten Querstraßen.
public class PanicRunner : MonoBehaviour
{
    [SerializeField] private Vector3 from;
    [SerializeField] private Vector3 to;
    [SerializeField] private float speed = 5f;
    [Tooltip("Startet so weit (m) entlang der Strecke, damit eine Gruppe nicht im Gleichschritt läuft")]
    [SerializeField] private float startOffset;
    [SerializeField] private float overrun = 30f;
    [SerializeField] private LayerMask groundLayers = 1 << 3;

    private float s;
    private Camera view;

    private void Start()
    {
        s = startOffset;
        Place();
    }

    private void Update()
    {
        float length = Vector3.Distance(from, to);
        s += speed * Time.deltaTime;
        if (s > length && (!Visible(transform.position) && !Visible(from) || s > length + overrun))
        {
            s = 0f;
        }
        Place();
    }

    private void Place()
    {
        Vector3 direction = (to - from).normalized;
        Vector3 p = from + direction * s;
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            p.y = hit.point.y;
        }
        // Die Figur schaut bei Drehung 0 nach -Z
        transform.SetPositionAndRotation(p, Quaternion.LookRotation(-direction));
    }

    private bool Visible(Vector3 point)
    {
        if (view == null)
        {
            view = Camera.main;
            if (view == null)
            {
                return false;
            }
        }
        Vector3 v = view.WorldToViewportPoint(point + Vector3.up);
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }
}
