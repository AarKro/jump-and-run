using UnityEngine;

// Dreht ein Objekt gleichmäßig um eine lokale Achse (z.B. den Rotor eines Windrads).
// Time.deltaTime macht die Drehung unabhängig von der Framerate: Grad pro Sekunde statt Grad pro Frame
public class Spinner : MonoBehaviour
{
    [Tooltip("Drehachse im lokalen Raum")]
    [SerializeField] private Vector3 axis = Vector3.forward;
    [SerializeField] private float degreesPerSecond = 30f;

    private void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
