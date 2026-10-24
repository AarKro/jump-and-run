using System.Collections;
using UnityEngine;

// Tod und Wiederauftauchen des Spielers. GameManager ruft das beim Respawn und beim Levelstart auf:
// Tod = einfrieren, aufs Gesicht fallen, der Karton fliegt im Bogen nach vorn und klappt auf, die Pizza dreht sich heraus
// und landet mit dem Käse nach unten ("Splat"). Er bleibt kurz liegen, dann schließt sich die Iris über ihm.
// Danach läuft er aus einer Tür (SpawnDoor) heraus, oder ploppt ohne Tür am Respawn-Punkt auf.
// Während beides läuft, ist der Rigidbody kinematisch: Die Physik hält sich raus, der Code bewegt ihn.
[RequireComponent(typeof(PlayerController))]
public class PlayerLife : MonoBehaviour
{
    [Header("Tod")]
    [Tooltip("So lange kippt er aufs Gesicht (mit kurzem Nachfedern)")]
    [SerializeField] private float fallDuration = 0.45f;
    [Tooltip("So lange bleibt er liegen, bevor sich die Iris schließt")]
    [SerializeField] private float lieTime = 0.8f;
    [SerializeField] private float irisCloseDuration = 0.5f;
    [SerializeField] private float irisOpenDuration = 0.5f;
    [Tooltip("Flug des Kartons: so weit nach vorn, so hoch, so lange")]
    [SerializeField] private float cartonThrowDistance = 2.2f;
    [SerializeField] private float cartonThrowHeight = 1.2f;
    [SerializeField] private float cartonFlightTime = 0.7f;
    [Tooltip("Die Pizza fliegt aus dem offenen Karton noch ein Stück weiter")]
    [SerializeField] private float pizzaExtraDistance = 0.9f;
    [SerializeField] private ParticleSystem splat;
    [SerializeField] private int splatParticles = 24;
    [Tooltip("Ohne Iris im Level (Fallback): Rauchwolke und verschwinden wie früher")]
    [SerializeField] private ParticleSystem poof;
    [SerializeField] private int poofParticles = 24;

    [Header("Aus der Tür laufen")]
    [SerializeField] private float walkOutDuration = 0.6f;
    [SerializeField] private float walkBobHeight = 0.08f;

    [Header("Sturz vom Skateboard (Markt, Level 4)")]
    [Tooltip("So weit wird er zurückgeworfen")]
    [SerializeField] private float crashKnockBack = 1.5f;
    [SerializeField] private float crashKnockDuration = 0.35f;
    [Tooltip("So weit rutscht das Board zurück (weiter als er fliegt, er läuft ihm hinterher)")]
    [SerializeField] private float crashBoardSlide = 3.5f;
    [SerializeField] private float crashBoardDuration = 0.6f;
    [Tooltip("So lange sitzt er benommen da (Sterne über dem Kopf), bevor er zum Board läuft")]
    [SerializeField] private float crashDazedTime = 1f;
    [SerializeField] private float crashRunDuration = 0.35f;
    [Tooltip("Danach sind Leute und Karren so lange durchlässig")]
    [SerializeField] private float crashGraceTime = 1f;
    [SerializeField] private LayerMask crashGroundLayer = 1 << 3;

    private PlayerController controller;
    private Rigidbody body;
    private Vector3 normalScale;
    private DeliveryGuy guy;
    // Karton, Deckel-Scharnier und Pizza: werden beim Tod weggeworfen und beim Respawn zurückgeholt
    private PizzaCarton carton;
    private Transform cartonParent;
    private Vector3 cartonLocalPosition;
    private Quaternion cartonLocalRotation;
    private Transform lidHinge;
    private Transform pizza;
    private Vector3 pizzaLocalPosition;
    // Die Iris hat sich beim Tod geschlossen: beim Wiederauftauchen (Tür oder Aufploppen) wieder öffnen
    private bool irisClosed;
    private Coroutine throwRoutine;
    // Gerade vom Board gefallen (bis er wieder draufsteht)
    public bool IsCrashing { get; private set; }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
        guy = GetComponent<DeliveryGuy>();
        normalScale = transform.localScale;
        carton = GetComponentInChildren<PizzaCarton>(true);
        if (carton != null)
        {
            cartonParent = carton.transform.parent;
            cartonLocalPosition = carton.transform.localPosition;
            cartonLocalRotation = carton.transform.localRotation;
            lidHinge = carton.transform.Find("Lid Hinge");
            pizza = carton.transform.Find("Pizza");
            if (pizza != null)
            {
                pizzaLocalPosition = pizza.localPosition;
            }
        }
    }

    // ---------- Sturz vom Skateboard ----------

    // In jemanden hineingefahren: Er fliegt ein Stück zurück, das Board rutscht weiter zurück, er läuft automatisch hin
    // und springt wieder drauf (~1.5 s ohne Steuerung). Gibt false zurück, wenn gerade nichts gehen darf
    public bool BoardCrash()
    {
        GameManager game = GameManager.Instance;
        if (IsCrashing || !controller.IsSkating || body.isKinematic ||
            (game != null && (game.LevelFinished || game.IsRespawning || game.InCutscene)))
        {
            return false;
        }
        StartCoroutine(BoardCrashRoutine());
        return true;
    }

    private IEnumerator BoardCrashRoutine()
    {
        IsCrashing = true;
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            // Solange er vom Board fällt, zählt kein anderer Treffer (er ist kinematisch und kann nicht ausweichen)
            game.InCutscene = true;
        }

        float travel = controller.FacingDirection;
        controller.Freeze();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        controller.SetSkating(false);
        if (guy != null)
        {
            guy.Stumble(crashKnockDuration + 0.2f);
        }
        Transform board = guy != null ? guy.DetachBoard() : null;

        // Zurück = entgegen der Fahrtrichtung. Beide landen auf dem Boden darunter (er kann auch in der Luft angestoßen sein)
        float u = Lane.Project(body.position);
        Vector3 start = body.position;
        Vector3 knockTo = GroundCenter(u - travel * crashKnockBack, start.y);
        Vector3 boardFrom = board != null ? board.position : start;
        Quaternion boardRotation = board != null ? board.rotation : Quaternion.identity;
        Vector3 boardTo = GroundCenter(u - travel * crashBoardSlide, start.y) + Vector3.down * 0.5f;

        float duration = Mathf.Max(crashKnockDuration, crashBoardDuration);
        for (float t = 0f; t < duration; t += Time.fixedDeltaTime)
        {
            // Spieler: kleiner Bogen nach hinten
            float k = Mathf.Clamp01(t / crashKnockDuration);
            Vector3 position = Vector3.Lerp(start, knockTo, k);
            position.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
            body.MovePosition(position);
            // Board: rutscht ausrollend zurück und überschlägt sich einmal um seine Längsachse
            if (board != null)
            {
                float b = Mathf.Clamp01(t / crashBoardDuration);
                float eased = 1f - (1f - b) * (1f - b);
                Vector3 boardPosition = Vector3.Lerp(boardFrom, boardTo, eased);
                boardPosition.y += Mathf.Sin(b * Mathf.PI) * 0.4f;
                board.SetPositionAndRotation(boardPosition, Quaternion.AngleAxis(360f * eased, Lane.Tangent(u)) * boardRotation);
            }
            yield return new WaitForFixedUpdate();
        }

        // Benommen sitzen bleiben (die eigentliche Strafe), dann aufstehen
        if (guy != null)
        {
            guy.SetDazed(true);
            yield return new WaitForSeconds(crashDazedTime);
            guy.SetDazed(false);
            yield return new WaitForSeconds(0.2f);
        }

        // Zum Board laufen (schaut dabei dorthin), aufspringen, wieder in Fahrtrichtung drehen
        controller.Face(-travel);
        Vector3 boardStand = boardTo + Vector3.up * 0.5f;
        yield return WalkTo(boardStand + Lane.Tangent(u) * (travel * 0.4f), crashRunDuration);
        controller.Face(travel);
        yield return WalkTo(boardStand, 0.15f);
        if (guy != null)
        {
            guy.AttachBoard();
        }
        controller.SetSkating(true);
        Revive();

        if (game != null)
        {
            game.InCutscene = false;
        }
        IsCrashing = false;

        Collider own = GetComponent<Collider>();
        CrowdObstacle.SetPassable(own, true);
        yield return new WaitForSeconds(crashGraceTime);
        CrowdObstacle.SetPassable(own, false);
    }

    // Mitte des Spielers (Füße auf dem Boden) an der Stelle u: Boden per Strahl von oben suchen, sonst die Höhe behalten
    private Vector3 GroundCenter(float u, float fallbackY)
    {
        Vector3 point = Lane.Point(u);
        Vector3 origin = new Vector3(point.x, fallbackY + 1f, point.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, crashGroundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.5f;
        }
        return new Vector3(point.x, fallbackY, point.z);
    }

    public IEnumerator PlayDeath()
    {
        controller.Freeze();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        float facing = controller.FacingDirection;

        ScreenIris iris = FindAnyObjectByType<ScreenIris>();
        Camera view = Camera.main;
        if (iris == null || guy == null || view == null)
        {
            yield return PoofAway();
            yield break;
        }

        // Karton fliegt aus den Händen (parallel zum Hinfallen)
        throwRoutine = StartCoroutine(ThrowPizza(facing));

        // Aufs Gesicht kippen: beschleunigt (wie ein fallendes Brett), am Ende kurz nachfedern
        for (float t = 0f; t < fallDuration; t += Time.deltaTime)
        {
            float k = t / fallDuration;
            float fall = k < 0.8f ? Mathf.Pow(k / 0.8f, 2f) : 1f + Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI) * 0.06f;
            guy.FallAmount = Mathf.Max(0.001f, fall);
            yield return null;
        }
        guy.FallAmount = 1f;

        yield return new WaitForSeconds(lieTime);
        yield return iris.Close(view, transform.position + Vector3.up * 0.2f, irisCloseDuration);
        irisClosed = true;
    }

    // Karton im Bogen nach vorn (in Blickrichtung), überschlägt sich, der Deckel klappt auf. Kurz nach dem Abwurf dreht sich
    // die Pizza heraus und landet etwas weiter vorn mit dem Käse nach unten: Spritzer, flachgedrückt
    private IEnumerator ThrowPizza(float facing)
    {
        if (carton == null)
        {
            yield break;
        }
        carton.enabled = false;
        Transform box = carton.transform;
        ParticleSystem steam = box.GetComponentInChildren<ParticleSystem>();
        if (steam != null)
        {
            steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        box.SetParent(null, true);

        float u = Lane.Project(transform.position);
        Vector3 axis = Lane.Back(u) * facing;   // Überschlag nach vorn: Drehachse quer zur Laufrichtung
        Vector3 boxFrom = box.position;
        Quaternion boxRotation = box.rotation;
        Vector3 boxTo = GroundPoint(u + facing * cartonThrowDistance, transform.position.y) + Vector3.up * 0.04f;
        Vector3 pizzaTo = GroundPoint(u + facing * (cartonThrowDistance + pizzaExtraDistance), transform.position.y) + Vector3.up * 0.02f;

        bool pizzaOut = false;
        Vector3 pizzaFrom = Vector3.zero;
        Quaternion pizzaRotation = Quaternion.identity;
        float pizzaStart = cartonFlightTime * 0.3f;
        float pizzaFlight = cartonFlightTime * 0.85f;
        bool splatted = false;

        float total = Mathf.Max(cartonFlightTime, pizzaStart + pizzaFlight);
        for (float t = 0f; t <= total; t += Time.deltaTime)
        {
            // Karton: 1.5 Überschläge, landet umgedreht; Deckel bis zur Mitte des Flugs ganz auf
            float k = Mathf.Clamp01(t / cartonFlightTime);
            Vector3 position = Vector3.Lerp(boxFrom, boxTo, k) + Vector3.up * (4f * cartonThrowHeight * k * (1f - k));
            box.SetPositionAndRotation(position, Quaternion.AngleAxis(-540f * k, axis) * boxRotation);
            if (lidHinge != null)
            {
                lidHinge.localRotation = Quaternion.Euler(110f * Mathf.Clamp01(k * 2f), 0f, 0f);
            }

            // Pizza: verlässt den Karton im Flug, ein halber Überschlag plus Drehung um sich selbst, landet mit dem Käse unten
            if (!pizzaOut && t >= pizzaStart && pizza != null)
            {
                pizzaOut = true;
                pizza.gameObject.SetActive(true);
                pizza.SetParent(null, true);
                pizzaFrom = pizza.position;
                pizzaRotation = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up), Vector3.up);
            }
            if (pizzaOut)
            {
                float p = Mathf.Clamp01((t - pizzaStart) / pizzaFlight);
                Vector3 pizzaPosition = Vector3.Lerp(pizzaFrom, pizzaTo, p) + Vector3.up * (4f * (cartonThrowHeight + 0.4f) * p * (1f - p));
                pizza.SetPositionAndRotation(pizzaPosition, Quaternion.AngleAxis(-180f * p, axis) * Quaternion.AngleAxis(300f * p, Vector3.up) * pizzaRotation);
                if (p >= 1f && !splatted)
                {
                    splatted = true;
                    pizza.localScale = new Vector3(1.15f, 0.6f, 1.15f);
                    if (splat != null)
                    {
                        splat.transform.SetParent(null, true);
                        splat.transform.position = pizzaTo;
                        splat.Emit(splatParticles);
                    }
                }
            }
            yield return null;
        }
    }

    // Boden unter der Stelle u (Strahl von oben), sonst die Füße des Spielers
    private Vector3 GroundPoint(float u, float centerY)
    {
        Vector3 point = Lane.Point(u);
        Vector3 origin = new Vector3(point.x, centerY + 1.5f, point.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, crashGroundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return new Vector3(point.x, centerY - 0.5f, point.z);
    }

    // Karton, Deckel und Pizza zurück in die Hände, Pizza versteckt im Karton, Spieler steht wieder (beim Respawn)
    private void RestoreAfterDeath()
    {
        if (guy != null)
        {
            guy.ResetFall();
        }
        if (carton == null || carton.enabled)
        {
            return;
        }
        if (throwRoutine != null)
        {
            StopCoroutine(throwRoutine);
            throwRoutine = null;
        }
        Transform box = carton.transform;
        box.SetParent(cartonParent, false);
        box.localPosition = cartonLocalPosition;
        box.localRotation = cartonLocalRotation;
        if (lidHinge != null)
        {
            lidHinge.localRotation = Quaternion.identity;
        }
        if (pizza != null)
        {
            pizza.SetParent(box, false);
            pizza.localPosition = pizzaLocalPosition;
            pizza.localRotation = Quaternion.identity;
            pizza.localScale = Vector3.one;
            pizza.gameObject.SetActive(false);
        }
        if (splat != null && splat.transform.parent != transform)
        {
            splat.transform.SetParent(transform, false);
        }
        ParticleSystem steam = box.GetComponentInChildren<ParticleSystem>();
        if (steam != null)
        {
            steam.Play();
        }
        carton.enabled = true;
    }

    // Iris nach dem Tod wieder auf, um den Punkt, an dem er gleich auftaucht. Die Kamera springt vorher hin (sonst sähe man sie
    // quer durch die Stadt zur Tür schwenken)
    private void OpenIrisAt(Vector3 worldPoint)
    {
        if (!irisClosed)
        {
            return;
        }
        irisClosed = false;
        ScreenIris iris = FindAnyObjectByType<ScreenIris>();
        Camera view = Camera.main;
        if (view != null && view.TryGetComponent(out ThirdPersonCamera follow))
        {
            follow.SnapToTarget();
        }
        if (iris != null && view != null)
        {
            StartCoroutine(iris.Open(view, worldPoint, irisOpenDuration));
        }
    }

    // Alter Tod ohne Iris: plattdrücken, Rauchwolke, verschwinden
    private IEnumerator PoofAway()
    {
        Vector3 squashed = new Vector3(normalScale.x * 1.35f, normalScale.y * 0.55f, normalScale.z * 1.35f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(normalScale, squashed, Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f));
            yield return null;
        }
        if (poof != null)
        {
            poof.transform.SetParent(null, true);
            poof.Emit(poofParticles);
        }
        transform.localScale = Vector3.zero;
        yield return new WaitForSeconds(0.4f);
    }

    // Läuft aus der Tür heraus auf die Laufbahn, danach hat der Spieler wieder die Kontrolle
    public IEnumerator PlayExit(SpawnDoor door)
    {
        controller.Freeze();
        body.isKinematic = true;
        ResetLook(door.OutsidePosition);
        Teleport(door.InsidePosition);
        OpenIrisAt(door.OutsidePosition + Vector3.up * 0.8f);

        yield return door.Open();

        Vector3 from = door.InsidePosition;
        Vector3 to = door.OutsidePosition;
        // Auf dem Skateboard rollt er gleichmäßig heraus, ohne Schritte
        float bob = controller.IsSkating ? 0f : walkBobHeight;
        for (float t = 0f; t < walkOutDuration; t += Time.fixedDeltaTime)
        {
            float k = t / walkOutDuration;
            Vector3 position = Vector3.Lerp(from, to, k);
            // Kleines Auf und Ab, damit es nach Laufen aussieht statt nach Schweben
            position.y += Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * bob;
            // MovePosition im Physik-Takt: mit Interpolation ruckelfrei, auch für kinematische Körper
            body.MovePosition(position);
            yield return new WaitForFixedUpdate();
        }

        Teleport(to);
        StartCoroutine(door.Close());
        Revive();
    }

    // Wartet drinnen hinter der geschlossenen Tür (z.B. während der Kamerafahrt zum Levelstart). PlayExit holt ihn danach raus
    public void WaitInside(SpawnDoor door)
    {
        controller.Freeze();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        ResetLook(door.OutsidePosition);
        Teleport(door.InsidePosition);
    }

    // Ohne Tür (z.B. Level 2): am Punkt kurz aufploppen
    public IEnumerator PlayPopIn(Vector3 position)
    {
        controller.Freeze();
        body.isKinematic = true;
        ResetLook(position);
        Teleport(position);
        OpenIrisAt(position);

        const float popDuration = 0.25f;
        for (float t = 0f; t < popDuration; t += Time.deltaTime)
        {
            transform.localScale = normalScale * Mathf.Lerp(0f, 1f, t / popDuration);
            yield return null;
        }
        transform.localScale = normalScale;
        Revive();
    }

    // ---------- Für kurze Szenen (z.B. Mikrowelle): Spieler übernehmen, hinlaufen lassen, freigeben ----------

    public void BeginScripted()
    {
        controller.Freeze();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
    }

    public IEnumerator WalkTo(Vector3 target, float duration)
    {
        Vector3 from = body.position;
        for (float t = 0f; t < duration; t += Time.fixedDeltaTime)
        {
            float k = t / duration;
            Vector3 position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, k));
            position.y += Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * walkBobHeight;
            body.MovePosition(position);
            yield return new WaitForFixedUpdate();
        }
        Teleport(target);
    }

    public void EndScripted()
    {
        Revive();
    }

    // In den Lane-Rahmen an der Stelle drehen, an der er gleich steht (nach einer Kurve nicht mehr "geradeaus")
    private void ResetLook(Vector3 at)
    {
        // Auch den Rigidbody zurückdrehen: Sonst schreibt die Physik beim nächsten MovePosition seine alte
        // (vom Tod verdrehte) Rotation zurück auf den Transform, und der Spieler bliebe schief
        Quaternion frame = Lane.Frame(Lane.Project(at));
        body.rotation = frame;
        transform.rotation = frame;
        transform.localScale = normalScale;
        RestoreAfterDeath();
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
