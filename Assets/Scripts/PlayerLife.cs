using System.Collections;
using UnityEngine;

// Tod und Wiederauftauchen des Spielers. GameManager ruft das beim Respawn und beim Levelstart auf:
// Tod = einfrieren, plattdrücken, drehen, in einer Rauchwolke verschwinden.
// Danach läuft er aus einer Tür (SpawnDoor) heraus, oder ploppt ohne Tür am Respawn-Punkt auf.
// Während beides läuft, ist der Rigidbody kinematisch: Die Physik hält sich raus, der Code bewegt ihn.
[RequireComponent(typeof(PlayerController))]
public class PlayerLife : MonoBehaviour
{
    [Header("Tod")]
    [SerializeField] private float squashDuration = 0.35f;
    [SerializeField] private float vanishDuration = 0.15f;
    [Tooltip("Pause nach dem Verschwinden, bevor er wieder auftaucht")]
    [SerializeField] private float deadPause = 0.35f;
    [SerializeField] private float spinSpeed = 900f;
    [SerializeField] private ParticleSystem poof;
    [SerializeField] private int poofParticles = 24;

    [Header("Aus der Tür laufen")]
    [SerializeField] private float walkOutDuration = 0.6f;
    [SerializeField] private float walkBobHeight = 0.08f;

    private PlayerController controller;
    private Rigidbody body;
    private Vector3 normalScale;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
        normalScale = transform.localScale;
    }

    public IEnumerator PlayDeath()
    {
        controller.Freeze();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;

        Vector3 squashed = new Vector3(normalScale.x * 1.35f, normalScale.y * 0.55f, normalScale.z * 1.35f);
        for (float t = 0f; t < squashDuration; t += Time.deltaTime)
        {
            float k = t / squashDuration;
            transform.localScale = Vector3.Lerp(normalScale, squashed, Mathf.Sin(k * Mathf.PI * 0.5f));
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            body.rotation = transform.rotation;
            yield return null;
        }

        if (poof != null)
        {
            // Emit statt Play: einmalig eine Ladung Partikel ausstoßen, unabhängig von der Emissionsrate
            poof.transform.SetParent(null, true);
            poof.Emit(poofParticles);
        }

        Vector3 start = transform.localScale;
        for (float t = 0f; t < vanishDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(start, Vector3.zero, t / vanishDuration);
            yield return null;
        }
        transform.localScale = Vector3.zero;

        yield return new WaitForSeconds(deadPause);
    }

    // Läuft aus der Tür heraus auf die Laufbahn, danach hat der Spieler wieder die Kontrolle
    public IEnumerator PlayExit(SpawnDoor door)
    {
        controller.Freeze();
        body.isKinematic = true;
        ResetLook();
        Teleport(door.InsidePosition);

        yield return door.Open();

        Vector3 from = door.InsidePosition;
        Vector3 to = door.OutsidePosition;
        for (float t = 0f; t < walkOutDuration; t += Time.fixedDeltaTime)
        {
            float k = t / walkOutDuration;
            Vector3 position = Vector3.Lerp(from, to, k);
            // Kleines Auf und Ab, damit es nach Laufen aussieht statt nach Schweben
            position.y += Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * walkBobHeight;
            // MovePosition im Physik-Takt: mit Interpolation ruckelfrei, auch für kinematische Körper
            body.MovePosition(position);
            yield return new WaitForFixedUpdate();
        }

        Teleport(to);
        StartCoroutine(door.Close());
        Revive();
    }

    // Ohne Tür (z.B. Level 2): am Punkt kurz aufploppen
    public IEnumerator PlayPopIn(Vector3 position)
    {
        controller.Freeze();
        body.isKinematic = true;
        ResetLook();
        Teleport(position);

        const float popDuration = 0.25f;
        for (float t = 0f; t < popDuration; t += Time.deltaTime)
        {
            transform.localScale = normalScale * Mathf.Lerp(0f, 1f, t / popDuration);
            yield return null;
        }
        transform.localScale = normalScale;
        Revive();
    }

    private void ResetLook()
    {
        // Auch den Rigidbody zurückdrehen: Sonst schreibt die Physik beim nächsten MovePosition seine alte
        // (vom Tod verdrehte) Rotation zurück auf den Transform, und der Spieler bliebe schief
        body.rotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
        transform.localScale = normalScale;
        if (poof != null && poof.transform.parent != transform)
        {
            // Die Rauchwolke wurde zum Abspielen abgekoppelt (sonst würde sie mit dem Spieler schrumpfen), zurückholen
            poof.transform.SetParent(transform, false);
            poof.transform.localPosition = Vector3.zero;
        }
    }

    private void Teleport(Vector3 position)
    {
        body.position = position;
        transform.position = position;
    }

    private void Revive()
    {
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        controller.Unfreeze();
    }
}
