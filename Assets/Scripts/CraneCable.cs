using UnityEngine;

// Seil eines Hafenkrans (Level 4): Der Container hängt an einem Haken, das Seil läuft senkrecht hinauf zur Laufkatze unter
// dem Kranträger. Die Laufkatze bleibt immer auf Trägerhöhe (topY) genau über dem Haken: Fährt der Container zur Seite
// (Fähre), fährt sie mit; fährt er hoch und runter (Aufzug), wird nur das Seil länger oder kürzer.
// LateUpdate: nach der Interpolation des Rigidbodies, sonst hinge das Seil einen Physik-Schritt hinterher (wie die
// Scherenarme der Hebebühne in Level 3).
public class CraneCable : MonoBehaviour
{
    [Tooltip("Haken über dem Container (bewegt sich mit ihm)")]
    [SerializeField] private Transform hook;
    [Tooltip("Seil: ein Quader mit Höhe 1, wird zwischen Haken und Laufkatze gestreckt")]
    [SerializeField] private Transform cable;
    [SerializeField] private Transform trolley;
    [Tooltip("Welt-Höhe der Laufkatze (Unterkante des Kranträgers). Setzt der Level-Generator")]
    [SerializeField] private float topY = 14f;

    private void LateUpdate()
    {
        Vector3 bottom = hook.position;
        Vector3 top = new Vector3(bottom.x, topY, bottom.z);
        trolley.position = top;
        cable.position = (bottom + top) / 2f;
        Vector3 scale = cable.localScale;
        // Das Seil hängt am Container, dessen Skalierung 1 ist: lokale Höhe = Weltlänge
        scale.y = Mathf.Max(0.01f, top.y - bottom.y);
        cable.localScale = scale;
    }
}
