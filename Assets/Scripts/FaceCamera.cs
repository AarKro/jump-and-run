using UnityEngine;

// Sprechblase (WOOF, QUACK, HEY, WHOA, SKREE): steht immer parallel zum Bildschirm und auf der Kameraseite ihres Besitzers.
// Gebaut sind die Blasen für die normale Kamera vor der Bahn: lokal -Z des Besitzers ist die Kameraseite, die Quads sind
// nur von -Z aus sichtbar. Schaut die Kamera von woanders (CameraZone auf der anderen Seite der Bahn, Level 3 am Brunnen),
// sähe man sonst die Rückseite: Das weiße Feld verschwindet, übrig bleibt gespiegelte Schrift.
// Darum dreht diese Komponente die Blase und ihren gebauten Versatz zum Besitzer (das Elternobjekt) um die Hochachse in
// Blickrichtung der Kamera. In LateUpdate, damit sie nach Animationen der Besitzer zuletzt gesetzt wird.
public class FaceCamera : MonoBehaviour
{
    private Transform owner;
    // Versatz zum Besitzer, wie er gebaut ist (x = rechts im Bild, z < 0 = zur Kamera hin)
    private Vector3 builtOffset;

    private void Awake()
    {
        owner = transform.parent;
        builtOffset = transform.localPosition;
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        if (view == null || owner == null)
        {
            return;
        }
        // Nur die waagerechte Blickrichtung: Die Blase bleibt aufrecht, auch wenn die Kamera schräg nach unten schaut
        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Quaternion facing = Quaternion.LookRotation(forward.normalized, Vector3.up);
        transform.SetPositionAndRotation(owner.position + facing * builtOffset, facing);
    }
}
