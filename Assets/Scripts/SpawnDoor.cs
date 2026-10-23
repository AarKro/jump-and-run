using System.Collections;
using UnityEngine;

// Tür eines Gebäudes, aus der der Spieler beim Levelstart bzw. nach dem Respawn herausläuft
// (Pizzeria am Start, Café als Checkpoint). Die Tür dreht sich dabei auf und wieder zu.
public class SpawnDoor : MonoBehaviour
{
    [Tooltip("Startpunkt im Gebäude, hinter der Tür (Z > 0, außerhalb der Laufbahn)")]
    [SerializeField] private Transform insidePoint;
    [Tooltip("Hier steht der Spieler danach, auf der Laufbahn (Z = 0)")]
    [SerializeField] private Transform outsidePoint;
    [Tooltip("Türblatt mit Drehpunkt am Scharnier")]
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private float openAngle = -100f;
    [SerializeField] private float swingDuration = 0.2f;

    public Vector3 InsidePosition => insidePoint.position;
    public Vector3 OutsidePosition => outsidePoint.position;
    public Transform OutsidePoint => outsidePoint;

    public IEnumerator Open()
    {
        yield return Swing(0f, openAngle);
    }

    public IEnumerator Close()
    {
        yield return Swing(openAngle, 0f);
    }

    private IEnumerator Swing(float from, float to)
    {
        if (doorLeaf == null)
        {
            yield break;
        }

        for (float t = 0f; t < swingDuration; t += Time.deltaTime)
        {
            // SmoothStep: langsam anfangen und abbremsen statt gleichmäßig, wirkt wie eine echte Tür
            float angle = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / swingDuration));
            doorLeaf.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        doorLeaf.localRotation = Quaternion.Euler(0f, to, 0f);
    }

    private void OnDrawGizmos()
    {
        if (insidePoint == null || outsidePoint == null)
        {
            return;
        }
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(outsidePoint.position, 0.5f);
        Gizmos.DrawLine(insidePoint.position, outsidePoint.position);
    }
}
