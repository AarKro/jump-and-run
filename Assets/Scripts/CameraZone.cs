using System.Collections.Generic;
using UnityEngine;

// Abschnitt der Laufbahn, in dem die Spielkamera anders steht, z.B. vor dem Leaning Tower of Pizza: weiter weg, höher und
// mit Blick nach oben, damit das ganze Bauwerk ins Bild passt. ThirdPersonCamera blendet Abstand (offset) und Blickhöhe
// (lookHeight) über die Gewichtung der Zone hinüber. Die Gewichtung hängt von der Position des Spielers ab, nicht von der Zeit:
// Am Rand der Zone steigt sie über "blend" Meter weich von 0 auf 1. So stimmt das Bild auch direkt nach einem Respawn.
public class CameraZone : MonoBehaviour
{
    // Alle aktiven Zonen. Static, damit die Kamera sie ohne Referenz findet. Ein- und Austragen in OnEnable/OnDisable,
    // so bleiben auch ohne Domain Reload keine alten Einträge stehen
    private static readonly List<CameraZone> ActiveZones = new List<CameraZone>();
    public static IReadOnlyList<CameraZone> Active => ActiveZones;

    [Header("Bereich (Lauf-Koordinate u)")]
    [SerializeField] private float fromU;
    [SerializeField] private float toU;
    [Tooltip("Über so viele Meter vor und hinter dem Bereich blendet die Kamera weich über")]
    [SerializeField] private float blend = 10f;

    [Header("Kamera in der Zone (wie bei ThirdPersonCamera, im Lane-Rahmen)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -35f);
    [SerializeField] private float lookHeight = 16.8f;
    [Tooltip("Steht die Kamera in der Zone auf der anderen Seite der Bahn (offset.z > 0), kreist sie beim Überblenden " +
             "um den Spieler herum statt durch ihn hindurch: +1 über die +u-Seite (hinter dem Spieler, wenn er Richtung -u " +
             "läuft), -1 über die -u-Seite")]
    [SerializeField] private float orbitSide = 1f;

    public Vector3 Offset => offset;
    public float LookHeight => lookHeight;
    public float OrbitSide => orbitSide >= 0f ? 1f : -1f;

    private void OnEnable()
    {
        ActiveZones.Add(this);
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
    }

    // 0 = außerhalb, 1 = mitten in der Zone, dazwischen weicher Übergang (SmoothStep: sanft rein und raus)
    public float Weight(float u)
    {
        float low = Mathf.Min(fromU, toU);
        float high = Mathf.Max(fromU, toU);
        float k;
        if (u < low)
        {
            k = 1f - (low - u) / blend;
        }
        else if (u > high)
        {
            k = 1f - (u - high) / blend;
        }
        else
        {
            k = 1f;
        }
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f);
        float low = Mathf.Min(fromU, toU);
        float high = Mathf.Max(fromU, toU);
        for (float u = low; u < high; u += 2f)
        {
            Gizmos.DrawLine(Lane.Point(u) + Vector3.up, Lane.Point(Mathf.Min(u + 2f, high)) + Vector3.up);
        }
        Gizmos.DrawWireSphere(Lane.Point(low) + Vector3.up, 0.6f);
        Gizmos.DrawWireSphere(Lane.Point(high) + Vector3.up, 0.6f);
    }
}
