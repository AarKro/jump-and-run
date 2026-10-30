using UnityEngine;

// Spaziergänger als Kulisse (Markt, Level 4): läuft neben der Bahn (vor ihr am Wasser oder dahinter zwischen den Ständen)
// langsam hin und her, bleibt an den Enden kurz stehen. Ohne Physik und ohne Collider, also kein Hindernis.
// Die Wurzel wandert auf der Bahn (Lane.Point), die Figur hängt seitlich versetzt darunter (lokal Z, im Prefab gesetzt),
// so folgt sie auch Kurven. Animiert wird sie von ihrem CrowdFigure (aus der Positionsänderung).
public class Stroller : MonoBehaviour
{
    [Tooltip("Figur (mit CrowdFigure), dreht sich in Laufrichtung")]
    [SerializeField] private Transform figure;
    [Tooltip("So weit läuft sie in beide Richtungen vom Startpunkt aus (Meter entlang der Bahn)")]
    [SerializeField] private float range = 6f;
    [SerializeField] private float speed = 0.9f;
    [SerializeField] private float waitTime = 1.5f;
    [Tooltip("1 = startet in +u-Richtung, -1 = in -u-Richtung")]
    [SerializeField] private float startDirection = 1f;
    [SerializeField] private float turnSharpness = 6f;

    private float startU;
    private float u;
    private float direction;
    private float wait;
    private float height;
    private float yaw;

    private void Start()
    {
        height = transform.position.y;
        startU = Lane.Project(transform.position);
        u = startU;
        direction = startDirection >= 0f ? 1f : -1f;
        yaw = -90f * direction;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (wait > 0f)
        {
            wait -= dt;
        }
        else
        {
            u += direction * speed * dt;
            if (Mathf.Abs(u - startU) >= range)
            {
                u = startU + Mathf.Sign(u - startU) * range;
                direction = -direction;
                wait = waitTime;
            }
        }
        transform.SetPositionAndRotation(Lane.Point(u) + Vector3.up * height, Lane.Frame(u));

        // Figur schaut bei Drehung 0 zur Kamera (-Z), -90° = in +u-Richtung. Beim Warten schaut sie halb zur Bahn
        float target = wait > 0f ? -90f * -direction * 0.4f + 180f * (figure.localPosition.z < 0f ? 1f : 0f) : -90f * direction;
        yaw = Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-turnSharpness * dt));
        figure.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
