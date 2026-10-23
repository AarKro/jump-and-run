using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Kreis-Blende über dem ganzen Bildschirm, wie im Zeichentrick ("Iris"): Schwarz überall außer in einem Kreis.
// Der Shader JumpAndRun/UI/Iris schneidet den Kreis aus, dieses Skript bewegt Mittelpunkt und Radius.
// Der Mittelpunkt folgt einem Punkt in der Welt (z.B. dem Kunden), auch wenn sich die Kamera dabei noch bewegt.
// Sitzt im HUD-Prefab (gebaut von Tools > Jump and Run > Pizza Heat > Create HUD Prefab).
[RequireComponent(typeof(Image))]
public class ScreenIris : MonoBehaviour
{
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    private Image image;
    private Material material;

    private void Awake()
    {
        image = GetComponent<Image>();
        // Eigene Kopie des Materials: Würden wir das Asset direkt ändern, bliebe das im Editor im Projekt gespeichert
        material = new Material(image.material);
        image.material = material;
        SetOpen();
    }

    private void OnDestroy()
    {
        Destroy(material);
    }

    // Ganz offen: Image aus, dann kostet die Blende auch nichts beim Zeichnen
    public void SetOpen()
    {
        image.enabled = false;
    }

    // Ganz zu: negativer Radius, damit auch der Mittelpunkt sicher schwarz ist
    public void SetClosed()
    {
        Apply(new Vector2(0.5f, 0.5f), -1f);
    }

    // Kreis schrumpft auf den Punkt zu, danach ist alles schwarz
    public IEnumerator Close(Camera view, Vector3 worldPoint, float duration)
    {
        yield return Animate(view, worldPoint, duration, closing: true);
    }

    // Kreis wächst vom Punkt aus, bis der ganze Bildschirm frei ist
    public IEnumerator Open(Camera view, Vector3 worldPoint, float duration)
    {
        yield return Animate(view, worldPoint, duration, closing: false);
    }

    private IEnumerator Animate(Camera view, Vector3 worldPoint, float duration, bool closing)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Jeden Frame neu: Bewegt sich die Kamera noch, bleibt der Kreis trotzdem auf dem Punkt
            Vector2 center = ViewportPoint(view, worldPoint);
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            float openness = closing ? 1f - k : k;
            Apply(center, CoverRadius(center) * openness);
            yield return null;
        }

        if (closing)
        {
            SetClosed();
        }
        else
        {
            SetOpen();
        }
    }

    private void Apply(Vector2 center, float radius)
    {
        image.enabled = true;
        Rect rect = image.rectTransform.rect;
        material.SetVector(CenterId, center);
        material.SetFloat(RadiusId, radius);
        material.SetFloat(AspectId, rect.height > 0f ? rect.width / rect.height : 16f / 9f);
    }

    // Welt → Bildschirm (0-1). Liegt der Punkt hinter der Kamera, einfach die Bildmitte nehmen
    private static Vector2 ViewportPoint(Camera view, Vector3 worldPoint)
    {
        if (view == null)
        {
            return new Vector2(0.5f, 0.5f);
        }
        Vector3 point = view.WorldToViewportPoint(worldPoint);
        return point.z > 0f ? new Vector2(point.x, point.y) : new Vector2(0.5f, 0.5f);
    }

    // So groß muss der Kreis sein, damit er von diesem Mittelpunkt aus auch die entfernteste Bildschirmecke erreicht
    private float CoverRadius(Vector2 center)
    {
        Rect rect = image.rectTransform.rect;
        float aspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
        float farthest = 0f;
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            Vector2 delta = corner - center;
            delta.x *= aspect;
            farthest = Mathf.Max(farthest, delta.magnitude);
        }
        return farthest + 0.02f;
    }
}
