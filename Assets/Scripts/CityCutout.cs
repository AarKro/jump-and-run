using UnityEngine;

// Unsichtbare Box in einer Level-Szene: Solange das Level geladen ist, verschwindet die Stadt in diesem Bereich
// (LevelCity wendet das an). Boden der Stadt (CityGround) wird ausgeschnitten, alles andere, dessen Mitte in der Box
// liegt, ausgeblendet. So kann ein Level eigene Baugruben, ein eigenes Kundenhaus oder eine Absperrung hineinsetzen.
// Größe = Scale des Objekts (wie bei einem Würfel). Es zählt nur der Grundriss (X/Z), keine Drehung.
// treesOnly: nimmt nur Bäume und Büsche weg (ganz, mit Stamm und Krone) und lässt Boden, Wege und alles andere stehen.
// Für Stellen, an denen nur die Kamera freie Sicht braucht (Level 5 im Park).
public class CityCutout : MonoBehaviour
{
    [SerializeField] private bool treesOnly;

    public bool TreesOnly => treesOnly;

    public Rect Area
    {
        get
        {
            Vector3 center = transform.position;
            Vector3 size = transform.lossyScale;
            return new Rect(center.x - size.x / 2f, center.z - size.z / 2f, size.x, size.z);
        }
    }

    public bool Contains(Vector3 point)
    {
        return Area.Contains(new Vector2(point.x, point.z));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.15f);
        Gizmos.DrawCube(transform.position, transform.lossyScale);
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
