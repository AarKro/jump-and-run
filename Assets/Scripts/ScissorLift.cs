using UnityEngine;

// Gelbe Scherenbühne von der Baustelle: Die Plattform (Wurzel, MovingPlatform) fährt hoch und runter, der Sockel bleibt
// auf dem Boden stehen und die Scherenarme spannen sich dazwischen auf. Nur Optik, Sockel und Arme haben keine Collider.
//
// Läuft in LateUpdate: Da hat die Rigidbody-Interpolation die Plattform für diesen Frame schon hingesetzt. In FixedUpdate
// hingen die Arme bis zu einem Physik-Schritt hinter der gezeichneten Plattform her und würden zittern.
public class ScissorLift : MonoBehaviour
{
    [Tooltip("Welt-Höhe des Bodens unter dem Sockel (Gehweg 0.5, Sohle einer Baugrube -3.9). Setzt der Level-Generator")]
    [SerializeField] private float groundY = 0.5f;
    [SerializeField] private Transform chassis;
    [Tooltip("Eine Stufe pro Scheren-X, von unten nach oben. Ihre Kinder sind die Arme")]
    [SerializeField] private Transform[] stages;
    [Tooltip("Arme, die nach rechts oben zeigen (Drehung +Winkel um lokal Z)")]
    [SerializeField] private Transform[] risingArms;
    [Tooltip("Arme, die nach rechts unten zeigen (Drehung -Winkel)")]
    [SerializeField] private Transform[] fallingArms;
    [SerializeField] private float armLength = 2.4f;
    [SerializeField] private float chassisHeight = 0.4f;
    [Tooltip("Unterkante der Plattform, lokal über der Wurzel")]
    [SerializeField] private float deckBottom = 0.2f;

    private void LateUpdate()
    {
        Arrange();
    }

    // Sockel auf den Boden, Arme zwischen Sockel und Plattform. Auch vom Level-Generator aufgerufen, damit es im Editor stimmt
    public void Arrange()
    {
        if (chassis == null || stages == null || stages.Length == 0)
        {
            return;
        }
        Vector3 root = transform.position;
        float baseTop = groundY + chassisHeight;
        chassis.position = new Vector3(root.x, groundY + chassisHeight / 2f, root.z);

        // Jedes X ist gleich hoch. Zwei gekreuzte Arme der Länge L überbrücken die Höhe h: sin(Winkel) = h / L.
        // Je höher die Bühne, desto steiler die Arme und desto schmaler das X, wie bei der echten Schere
        float height = Mathf.Max(root.y + deckBottom - baseTop, 0.05f);
        float stageHeight = height / stages.Length;
        float angle = Mathf.Asin(Mathf.Clamp(stageHeight / armLength, 0f, 0.99f)) * Mathf.Rad2Deg;
        for (int i = 0; i < stages.Length; i++)
        {
            stages[i].position = new Vector3(root.x, baseTop + stageHeight * (i + 0.5f), root.z);
        }
        foreach (Transform arm in risingArms)
        {
            arm.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        foreach (Transform arm in fallingArms)
        {
            arm.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
