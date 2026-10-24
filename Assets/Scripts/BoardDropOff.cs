using System.Collections;
using UnityEngine;

// Kiosk (Level 4): Am Checkpoint stellt der Spieler sein Skateboard ab. Er rollt bis vor die Tür, springt ab, das Board
// fliegt in einem kleinen Bogen an die Wand und lehnt dort (leaningBoard, Kulisse am Kiosk). Ab hier geht es zu Fuß weiter,
// auch nach einem Respawn aus der Kiosk-Tür (GameManager.SetRespawnSkating(false)).
// Das Board des Spielers fliegt genau in die Lage des angelehnten Boards (gleicher Aufbau), dann wird getauscht: Das
// Spieler-Board kommt zurück unter seine Füße (unsichtbar, solange er nicht skatet), das am Kiosk wird sichtbar.
// Der Kiosk gehört der Stadt (bleibt über Level geladen), darum setzt CityLandmark ihn pro Level zurück (ResetForNewLevel).
[RequireComponent(typeof(Checkpoint))]
[RequireComponent(typeof(SpawnDoor))]
public class BoardDropOff : MonoBehaviour
{
    [Tooltip("Angelehntes Board an der Kiosk-Wand, im Prefab aus")]
    [SerializeField] private Transform leaningBoard;
    [Tooltip("Vom Trigger bis vor die Tür rollen")]
    [SerializeField] private float rollDuration = 0.3f;
    [Tooltip("Absprung: Das Board fliegt so lange an die Wand, er hüpft einmal")]
    [SerializeField] private float hopDuration = 0.45f;
    [SerializeField] private float hopHeight = 0.3f;

    private Checkpoint checkpoint;
    private SpawnDoor door;

    private void Awake()
    {
        checkpoint = GetComponent<Checkpoint>();
        door = GetComponent<SpawnDoor>();
        checkpoint.Activated += OnActivated;
        leaningBoard.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        checkpoint.Activated -= OnActivated;
    }

    // Neues Level (Stadt bleibt geladen): kein Board an der Wand
    public void ResetForNewLevel()
    {
        StopAllCoroutines();
        leaningBoard.gameObject.SetActive(false);
    }

    private void OnActivated()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null || !player.TryGetComponent(out PlayerLife life) || !player.TryGetComponent(out PlayerController controller) ||
            !player.TryGetComponent(out DeliveryGuy guy))
        {
            return;
        }
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            game.SetRespawnSkating(false);
        }
        if (controller.IsSkating)
        {
            StartCoroutine(DropBoard(life, controller, guy, player.GetComponent<Rigidbody>()));
        }
    }

    private IEnumerator DropBoard(PlayerLife life, PlayerController controller, DeliveryGuy guy, Rigidbody body)
    {
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            game.InCutscene = true;
        }
        life.BeginScripted();
        yield return life.WalkTo(door.OutsidePosition, rollDuration);

        Transform board = guy.DetachBoard();
        controller.SetSkating(false);
        if (board != null)
        {
            Vector3 boardFrom = board.position;
            Quaternion rotationFrom = board.rotation;
            Vector3 standing = body.position;
            for (float t = 0f; t < hopDuration; t += Time.fixedDeltaTime)
            {
                float k = t / hopDuration;
                // Board im Bogen an die Wand, dreht sich dabei hochkant
                Vector3 position = Vector3.Lerp(boardFrom, leaningBoard.position, Mathf.SmoothStep(0f, 1f, k));
                position.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
                board.SetPositionAndRotation(position, Quaternion.Slerp(rotationFrom, leaningBoard.rotation, Mathf.SmoothStep(0f, 1f, k)));
                // Er hüpft vom Board auf den Boden
                body.MovePosition(standing + Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight));
                yield return new WaitForFixedUpdate();
            }
            guy.AttachBoard();
        }
        leaningBoard.gameObject.SetActive(true);

        life.EndScripted();
        if (game != null)
        {
            game.InCutscene = false;
        }
    }
}
