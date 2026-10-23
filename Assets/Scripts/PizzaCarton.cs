using UnityEngine;

// Der Pizzakarton, den der Spieler trägt. Sitzt als Kind am Player und wechselt die Seite,
// je nachdem wohin der Spieler schaut. Später kommt hier auch der Dampf der Wärmeanzeige dazu.
public class PizzaCarton : MonoBehaviour
{
    private PlayerController player;
    // Abstand zur Körpermitte in X, aus der Position im Prefab gelesen (Vorzeichen egal)
    private float carryOffsetX;

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        carryOffsetX = Mathf.Abs(transform.localPosition.x);
    }

    // LateUpdate: läuft nach allen Updates, also nachdem der Spieler seine Blickrichtung gesetzt hat
    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        // localPosition ist relativ zum Spieler, darum wandert der Karton automatisch mit.
        // Nur X wird gespiegelt, Höhe und Tiefe bleiben wie im Prefab eingestellt.
        Vector3 local = transform.localPosition;
        local.x = carryOffsetX * player.FacingDirection;
        transform.localPosition = local;
    }
}
