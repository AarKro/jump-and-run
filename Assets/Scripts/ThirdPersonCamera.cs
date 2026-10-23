using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [SerializeField] private float lookHeight = 1f;

    [Header("Nachziehen (Sekunden bis ~63 % des Weges)")]
    [SerializeField] private float horizontalSmoothTime = 0.08f;
    [SerializeField] private float verticalSmoothTime = 0.2f;
    [Tooltip("Wie schnell die Kamera in Kurven mitdreht (größer = direkter)")]
    [SerializeField] private float rotationSharpness = 8f;

    private Vector3 velocity;

    private void Start()
    {
        // Beim Start direkt an die Zielposition springen, statt vom Szenen-Ursprung heranzufahren
        if (target != null)
        {
            Quaternion frame = Lane.Frame(Lane.Project(target.position));
            transform.position = target.position + frame * offset;
            transform.rotation = frame * FixedRotation();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Offset im Lane-Rahmen: Auf der Geraden ist das genau der alte Welt-Offset, in einer Kurve dreht er mit,
        // so bleibt die Seitenansicht erhalten und die Kamera schwenkt außen um die Ecke
        Quaternion frame = Lane.Frame(Lane.Project(target.position));
        Vector3 desiredPosition = target.position + frame * offset;
        Vector3 position = transform.position;

        // Horizontal eng folgen, vertikal weicher, damit Sprünge das Bild nicht durchschütteln
        position.x = Mathf.SmoothDamp(position.x, desiredPosition.x, ref velocity.x, horizontalSmoothTime);
        position.z = Mathf.SmoothDamp(position.z, desiredPosition.z, ref velocity.z, horizontalSmoothTime);
        position.y = Mathf.SmoothDamp(position.y, desiredPosition.y, ref velocity.y, verticalSmoothTime);
        transform.position = position;

        // Fester Blickwinkel (im Lane-Rahmen) statt LookAt auf den Player: verhindert Wackeln der Rotation.
        // Exponentielles Nachziehen: Anteil 1 - e^(-k*dt) ist unabhängig von der Framerate gleich schnell
        Quaternion desiredRotation = frame * FixedRotation();
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
    }

    private Quaternion FixedRotation()
    {
        // Blickrichtung von der Wunschposition auf den Punkt über dem Player
        return Quaternion.LookRotation(Vector3.up * lookHeight - offset);
    }
}
