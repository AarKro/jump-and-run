using UnityEngine;

// Brandung am Strand der Promenade: Schaumlinien laufen den Abhang hinauf und wieder zurück, zeitversetzt.
// Das Objekt liegt genau in der Schräge des Strandes (lokal Z = hangaufwärts), die Linien sind seine Kinder und
// werden nur entlang lokal Z verschoben. Oben werden sie schmaler, als würde die Welle auslaufen.
// Aufgebaut von CityBuilder.Beach (Tools > Jump and Run > City > Rebuild City Scene).
public class BeachSurf : MonoBehaviour
{
    [SerializeField] private Transform[] foamLines;
    [Tooltip("So weit (Meter entlang des Hangs) läuft eine Welle über ihre Startposition hinaus hinauf")]
    [SerializeField] private float reach = 1.4f;
    [Tooltip("Sekunden für einmal hinauf und zurück")]
    [SerializeField] private float period = 4.5f;
    [Tooltip("So schmal wird eine Linie ganz oben (Anteil der Ausgangsbreite)")]
    [SerializeField] private float thinAtTop = 0.35f;

    private float[] startZ;
    private Vector3[] startScale;

    private void Awake()
    {
        startZ = new float[foamLines.Length];
        startScale = new Vector3[foamLines.Length];
        for (int i = 0; i < foamLines.Length; i++)
        {
            startZ[i] = foamLines[i].localPosition.z;
            startScale[i] = foamLines[i].localScale;
        }
    }

    private void Update()
    {
        for (int i = 0; i < foamLines.Length; i++)
        {
            // 0 = unten, 1 = ganz oben. Die Kosinus-Kurve ist an beiden Enden langsam: Die Welle bleibt oben kurz stehen
            float phase = Time.time / period + (float)i / foamLines.Length;
            float up = 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);

            Vector3 position = foamLines[i].localPosition;
            position.z = startZ[i] + reach * up;
            foamLines[i].localPosition = position;

            Vector3 scale = startScale[i];
            scale.z *= Mathf.Lerp(1f, thinAtTop, up);
            foamLines[i].localScale = scale;
        }
    }
}
