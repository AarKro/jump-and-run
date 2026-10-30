using UnityEngine;

// Drehleiter eines Feuerwehrautos (Level 5): Der Rettungskorb ist eine MovingPlatform, die über das breite Feuer hin und her
// fährt. Die Leiter folgt ihm: Der Drehkranz dreht sich um die Hochachse zum Korb, die Leiter neigt sich zu ihm und fährt
// aus bzw. ein (Teleskop: das hintere Stück fest, das vordere wird gestreckt).
// LateUpdate: nach der Interpolation des Rigidbodies am Korb, sonst hinge die Leiter einen Physik-Schritt hinterher (wie
// das Kranseil in Level 4).
public class LadderBoom : MonoBehaviour
{
    [Tooltip("Drehkranz (dreht sich um die Hochachse)")]
    [SerializeField] private Transform turntable;
    [Tooltip("Leiter-Drehpunkt auf dem Drehkranz (neigt sich, lokal +Z = Leiter)")]
    [SerializeField] private Transform boom;
    [Tooltip("Vorderes Teleskop-Stück: Quader mit Länge 1 entlang Z, wird bis zum Korb gestreckt")]
    [SerializeField] private Transform flySection;
    [Tooltip("Ab hier (lokal Z am Drehpunkt) beginnt das vordere Stück")]
    [SerializeField] private float flyStart = 3.5f;
    [Tooltip("Rettungskorb (setzt der Level-Generator, er ist ein eigenes Prefab)")]
    [SerializeField] private Transform basket;
    [Tooltip("Hier am Korb (lokal) hängt die Leiter")]
    [SerializeField] private Vector3 basketMount = new Vector3(0f, 0.2f, -1f);

    private void LateUpdate()
    {
        if (basket == null)
        {
            return;
        }
        Vector3 tip = basket.TransformPoint(basketMount);
        Vector3 toTip = tip - boom.position;
        Vector3 flat = new Vector3(toTip.x, 0f, toTip.z);
        if (flat.sqrMagnitude < 0.01f)
        {
            return;
        }
        turntable.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        boom.rotation = Quaternion.LookRotation(toTip.normalized, Vector3.up);

        // Das vordere Stück reicht von flyStart bis zur Spitze (nie kürzer als ein halber Meter)
        float length = Mathf.Max(flyStart + 0.5f, toTip.magnitude);
        float flyLength = length - flyStart;
        flySection.localPosition = new Vector3(flySection.localPosition.x, flySection.localPosition.y, flyStart + flyLength / 2f);
        Vector3 scale = flySection.localScale;
        scale.z = flyLength;
        flySection.localScale = scale;
    }
}
