using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [SerializeField] private float lookHeight = 1f;

    [Header("Nachziehen (Sekunden bis ~63 % des Weges)")]
    [SerializeField] private float horizontalSmoothTime = 0.08f;
    [SerializeField] private float verticalSmoothTime = 0.2f;

    private Vector3 velocity;

    private void Start()
    {
        // Beim Start direkt an die Zielposition springen, statt vom Szenen-Ursprung heranzufahren
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.rotation = FixedRotation();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Offset in Weltkoordinaten: Die Kamera dreht sich nicht mit dem Player mit
        Vector3 desiredPosition = target.position + offset;
        Vector3 position = transform.position;

        // Horizontal eng folgen, vertikal weicher, damit Sprünge das Bild nicht durchschütteln
        position.x = Mathf.SmoothDamp(position.x, desiredPosition.x, ref velocity.x, horizontalSmoothTime);
        position.z = Mathf.SmoothDamp(position.z, desiredPosition.z, ref velocity.z, horizontalSmoothTime);
        position.y = Mathf.SmoothDamp(position.y, desiredPosition.y, ref velocity.y, verticalSmoothTime);
        transform.position = position;

        // Fester Blickwinkel statt LookAt auf den Player: verhindert Wackeln der Rotation
        transform.rotation = FixedRotation();
    }

    private Quaternion FixedRotation()
    {
        // Blickrichtung von der Wunschposition auf den Punkt über dem Player
        return Quaternion.LookRotation(Vector3.up * lookHeight - offset);
    }
}
