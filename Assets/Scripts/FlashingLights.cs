using UnityEngine;

// Blaulicht/Rotlicht eines Einsatzfahrzeugs: zwei Gruppen von Leuchten blinken abwechselnd. Die Leuchten haben ein
// Unlit-Material (leuchten auch im Schatten), aus sind sie einfach unsichtbar, darunter liegt ein dunkles Gehäuse.
// Der Phasen-Versatz kommt aus der Position, damit nicht alle Fahrzeuge im Gleichtakt blinken.
public class FlashingLights : MonoBehaviour
{
    [SerializeField] private Renderer[] groupA;
    [SerializeField] private Renderer[] groupB;
    [Tooltip("So lange leuchtet eine Gruppe (Sekunden)")]
    [SerializeField] private float interval = 0.22f;

    private float offset;

    private void Awake()
    {
        offset = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.61f, 2f * interval);
    }

    private void Update()
    {
        bool a = Mathf.Repeat(Time.time + offset, 2f * interval) < interval;
        SetVisible(groupA, a);
        SetVisible(groupB, !a);
    }

    private static void SetVisible(Renderer[] renderers, bool visible)
    {
        if (renderers == null)
        {
            return;
        }
        foreach (Renderer target in renderers)
        {
            if (target != null)
            {
                target.enabled = visible;
            }
        }
    }
}
