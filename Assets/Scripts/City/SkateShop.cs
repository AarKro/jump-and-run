using System.Collections;
using UnityEngine;

// Board-Shop (Level 4): Erreicht der Spieler den Checkpoint, läuft er automatisch in den Laden (wie beim Respawn, nur
// andersherum), drinnen bekommt er das Skateboard, und er rollt darauf wieder aus der Tür. Ab hier respawnt er auch
// auf dem Board (GameManager.SetRespawnSkating), bis ein späterer Checkpoint (Kiosk) es wieder abnimmt.
// Das Licht, das OPEN-Neon und "Checkpoint unlocked!" macht CoffeeShopEffects, beides hängt am selben Checkpoint.Activated.
[RequireComponent(typeof(Checkpoint))]
[RequireComponent(typeof(SpawnDoor))]
public class SkateShop : MonoBehaviour
{
    [Tooltip("Vom Trigger bis vor die Tür")]
    [SerializeField] private float walkToDoorDuration = 0.25f;
    [Tooltip("Durch die Tür in den Laden")]
    [SerializeField] private float walkInDuration = 0.4f;
    [Tooltip("So lange ist er drinnen (Board aussuchen), bevor er herausrollt")]
    [SerializeField] private float insidePause = 0.6f;

    private Checkpoint checkpoint;
    private SpawnDoor door;

    private void Awake()
    {
        checkpoint = GetComponent<Checkpoint>();
        door = GetComponent<SpawnDoor>();
        checkpoint.Activated += OnActivated;
    }

    private void OnDestroy()
    {
        checkpoint.Activated -= OnActivated;
    }

    private void OnActivated()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null || !player.TryGetComponent(out PlayerLife life) || !player.TryGetComponent(out PlayerController controller))
        {
            return;
        }
        // Auf dem Spieler starten, nicht auf dem Laden (Stadt): Wird das Level mittendrin neu geladen (Pizza kalt), endet die
        // Coroutine mit dem Spieler, statt mit zerstörten Verweisen weiterzulaufen
        life.StartCoroutine(PickUpBoard(life, controller));
    }

    private IEnumerator PickUpBoard(PlayerLife life, PlayerController controller)
    {
        GameManager game = GameManager.Instance;
        // Solange er im Laden ist, zählen Treffer nicht (ein Auto oder Hund könnte sonst einen Respawn mittendrin auslösen)
        if (game != null)
        {
            game.InCutscene = true;
        }

        life.BeginScripted();
        yield return life.WalkTo(door.OutsidePosition, walkToDoorDuration);
        yield return door.Open();
        yield return life.WalkTo(door.InsidePosition, walkInDuration);
        yield return door.Close();
        yield return new WaitForSeconds(insidePause);

        controller.SetSkating(true);
        if (game != null)
        {
            game.SetRespawnSkating(true);
        }
        // Gleicher Weg hinaus wie beim Respawn, gibt am Ende die Steuerung zurück
        yield return life.PlayExit(door);

        if (game != null)
        {
            game.InCutscene = false;
        }
    }
}
