using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [Tooltip("Blickpunkt so hoch über dem Player. Höher = flacherer Blick (mehr Hintergrund), 2.86 ≈ 15° nach unten, 1 ≈ 27°")]
    [SerializeField] private float lookHeight = 2.86f;

    [Header("Nachziehen (Sekunden bis ~63 % des Weges)")]
    [SerializeField] private float horizontalSmoothTime = 0.08f;
    [SerializeField] private float verticalSmoothTime = 0.2f;
    [Tooltip("Wie schnell die Kamera in Kurven mitdreht (größer = direkter)")]
    [SerializeField] private float rotationSharpness = 8f;

    private Vector3 velocity;

    // Für die Kamerafahrt zum Levelstart (IntroFlyover): Sie landet genau in dieser Spielkamera-Pose
    public Vector3 Offset => offset;
    public float LookHeight => lookHeight;

    // Abstand und Blickhöhe an der Stelle u, mit allen Kamera-Zonen eingerechnet (z.B. weiter weg vor dem Leaning Tower)
    private void ViewAt(float u, out Vector3 viewOffset, out float viewLookHeight)
    {
        viewOffset = offset;
        viewLookHeight = lookHeight;
        foreach (CameraZone zone in CameraZone.Active)
        {
            float weight = zone.Weight(u);
            if (weight > 0f)
            {
                viewOffset = Vector3.Lerp(viewOffset, zone.Offset, weight);
                viewLookHeight = Mathf.Lerp(viewLookHeight, zone.LookHeight, weight);
            }
        }
    }

    private void Start()
    {
        // Beim Start direkt an die Zielposition springen, statt vom Szenen-Ursprung heranzufahren
        SnapToTarget();
    }

    // Sofort in die Spielkamera-Pose (ohne Nachziehen), z.B. nach der Kamerafahrt
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }
        float u = Lane.Project(target.position);
        ViewAt(u, out Vector3 viewOffset, out float viewLookHeight);
        Quaternion frame = Lane.Frame(u);
        transform.position = Anchor(u) + frame * viewOffset;
        transform.rotation = frame * FixedRotation(viewOffset, viewLookHeight);
        velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Offset im Lane-Rahmen: Auf der Geraden ist das genau der alte Welt-Offset, in einer Kurve dreht er mit,
        // so bleibt die Seitenansicht erhalten und die Kamera schwenkt außen um die Ecke
        float u = Lane.Project(target.position);
        ViewAt(u, out Vector3 viewOffset, out float viewLookHeight);
        Quaternion frame = Lane.Frame(u);
        Vector3 desiredPosition = Anchor(u) + frame * viewOffset;
        Vector3 position = transform.position;

        // Horizontal eng folgen, vertikal weicher, damit Sprünge das Bild nicht durchschütteln
        position.x = Mathf.SmoothDamp(position.x, desiredPosition.x, ref velocity.x, horizontalSmoothTime);
        position.z = Mathf.SmoothDamp(position.z, desiredPosition.z, ref velocity.z, horizontalSmoothTime);
        position.y = Mathf.SmoothDamp(position.y, desiredPosition.y, ref velocity.y, verticalSmoothTime);
        transform.position = position;

        // Fester Blickwinkel (im Lane-Rahmen) statt LookAt auf den Player: verhindert Wackeln der Rotation.
        // Exponentielles Nachziehen: Anteil 1 - e^(-k*dt) ist unabhängig von der Framerate gleich schnell
        Quaternion desiredRotation = frame * FixedRotation(viewOffset, viewLookHeight);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
    }

    // Punkt auf der Laufbahn an der Stelle des Players, in seiner Höhe. Im Spiel steht er sowieso auf der Bahn,
    // aber beim Herauslaufen aus einer Tür (oder an der Mikrowelle) ist er ein Stück dahinter: Die Kamera bleibt
    // dann ruhig auf der Bahn, statt ihm ins Haus hinterher zu rücken
    private Vector3 Anchor(float u)
    {
        Vector3 anchor = Lane.Point(u);
        anchor.y = target.position.y;
        return anchor;
    }

    private static Quaternion FixedRotation(Vector3 viewOffset, float viewLookHeight)
    {
        // Blickrichtung von der Wunschposition auf den Punkt über dem Player
        return Quaternion.LookRotation(Vector3.up * viewLookHeight - viewOffset);
    }
}
