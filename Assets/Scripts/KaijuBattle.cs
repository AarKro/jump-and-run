using System.Collections;
using UnityEngine;

// Steuert die beiden Riesenmonster in Level 5 durch den ganzen Ablauf:
// 1. Wüten: Die Echse wiegt sich. Betritt der Spieler den Park (Bereich breathFromU … breathToU), speit sie EINMAL Feuer,
//    als geskriptetes Ereignis (der Spieler steuert weiter): Die Kamera-Zone blendet ein (weiter weg, nach oben, die Echse im
//    Bild), sie lädt auf (lehnt sich zurück, Kopf hoch, Maul glüht), der Eisverkäufer hinter seinem Wagen ruft und winkt
//    → Feueratem (lehnt sich vor, FireBreath trifft alles außer hinter dem Wagen) → erholen, die Kamera-Zone blendet aus.
//    Stirbt der Spieler dabei, kommt es nach dem Respawn beim nächsten Betreten wieder.
// 2. Ankunft (Spieler überquert arrivalU): kurze Zwischensequenz. Der Roboter stürzt mit Düsenflammen und Rauchschweif
//    aus dem Himmel, schlägt mit Staubwolke auf (Bildschirm wackelt) und richtet sich auf. Die Echse brüllt und dreht sich
//    zu ihm um. Danach ringen beide (KaijuFight), kein Feuer mehr.
// 3. Finale (Spieler überquert finaleU): Zwischensequenz. Der Roboter verpasst der Echse einen Aufwärtshaken, sie kippt
//    zur Seite auf die Ruinen, der Roboter reckt die Arme.
// Während der Zwischensequenzen ist der Spieler eingefroren, die Kamera schaut aufs Geschehen, und die verlorene Zeit wird
// der Wärme gutgeschrieben (kostet also nichts). Stirbt der Spieler vorher (Respawn), fängt alles wieder bei 1 an.
// Bewegt wird relativ zur gebauten Pose (wie KaijuFight).
public class KaijuBattle : MonoBehaviour, IIntroExpose
{
    [Header("Echse")]
    [SerializeField] private Transform lizardRoot;
    [SerializeField] private Transform lizardUpper;
    [SerializeField] private Transform lizardHead;
    [SerializeField] private FireBreath breath;
    [Tooltip("Arm, mit dem sie im Intro zuschlägt (der zum Hochhaus hin)")]
    [SerializeField] private Transform lizardSmashArm;
    [Tooltip("Hochhaus, das sie im Intro zerschlägt")]
    [SerializeField] private TowerCollapse introTower;

    [Header("Roboter")]
    [SerializeField] private Transform robotRoot;
    [SerializeField] private Transform robotUpper;
    [SerializeField] private Transform robotArmLeft;
    [SerializeField] private Transform robotArmRight;
    [SerializeField] private KaijuFight fight;
    [SerializeField] private ParticleSystem[] thrusters;
    [SerializeField] private ParticleSystem landingDust;
    [SerializeField] private ParticleSystem fallDust;

    [Header("Feueratem im Park")]
    [Tooltip("Ruft und winkt, während sie auflädt")]
    [SerializeField] private HidingVendor vendor;
    [Tooltip("Kamera-Zone, die während des Ereignisses einblendet (Stärke 0 bis 1), damit man die Echse sieht")]
    [SerializeField] private CameraZone breathCamera;
    [SerializeField] private float cameraFade = 1.2f;

    [Header("Ablauf (u auf der Bahn)")]
    [Tooltip("Betritt der Spieler diesen Bereich, kommt der Feueratem (u von/bis)")]
    [SerializeField] private float breathFromU;
    [SerializeField] private float breathToU;
    [Tooltip("Hier kommt der Roboter (Spieler läuft nach -u, also sobald u kleiner ist)")]
    [SerializeField] private float arrivalU;
    [Tooltip("Hier fällt die Echse")]
    [SerializeField] private float finaleU;

    [Header("Feueratem-Ablauf")]
    [Tooltip("So lange lädt sie auf: Zeit, um hinter den Wagen zu laufen")]
    [SerializeField] private float chargeTime = 4f;
    [SerializeField] private float breathTime = 3f;
    [SerializeField] private float recoverTime = 1f;
    [SerializeField] private float chargeLean = 14f;
    [SerializeField] private float breathLean = 10f;

    [Header("Roboter-Ankunft")]
    [SerializeField] private float dropHeight = 220f;
    [SerializeField] private float fallTime = 2.2f;

    [Header("Kamera in den Zwischensequenzen")]
    [Tooltip("Kamera-Position relativ zur Mitte zwischen den Monstern: bei der Ankunft von Westen (über den Ruinen), beide " +
             "nebeneinander im Bild; im Finale von Osten, die Echse fällt von der Kamera weg")]
    [SerializeField] private Vector3 arrivalCameraOffset = new Vector3(-85f, 35f, -10f);
    [SerializeField] private Vector3 finaleCameraOffset = new Vector3(85f, 35f, -10f);

    private enum Stage { Rampage, Arrival, Fight, Finale, Done }
    private enum Phase { Charge, Breath, Recover }

    private Stage stage = Stage.Rampage;
    private Phase phase = Phase.Charge;
    private float phaseTime;
    private bool breathActive;
    private bool breathDone;
    private float lean;
    private float headPitch;

    private bool exposing;
    private Coroutine exposeRoutine;
    private Quaternion smashArmRest;
    private Quaternion lizardRootRest, upperRest, headRest, robotRootRest, robotUpperRest, robotArmLeftRest, robotArmRightRest;
    private Vector3 robotLanding;
    private Transform player;
    private PlayerController controller;
    private Coroutine sequence;

    private void Awake()
    {
        lizardRootRest = lizardRoot.localRotation;
        upperRest = lizardUpper.localRotation;
        headRest = lizardHead.localRotation;
        robotRootRest = robotRoot.localRotation;
        robotUpperRest = robotUpper.localRotation;
        robotArmLeftRest = robotArmLeft.localRotation;
        robotArmRightRest = robotArmRight.localRotation;
        robotLanding = robotRoot.localPosition;
        if (lizardSmashArm != null)
        {
            smashArmRest = lizardSmashArm.localRotation;
        }
        GameManager.LevelReset += ResetBattle;
    }

    private void OnDestroy()
    {
        GameManager.LevelReset -= ResetBattle;
    }

    private void Start()
    {
        controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
        {
            player = controller.transform;
        }
        ResetBattle();
    }

    // Alles auf Anfang: Roboter weg, Echse schaut nach Süden und wütet
    private void ResetBattle()
    {
        if (sequence != null)
        {
            StopCoroutine(sequence);
            sequence = null;
        }
        stage = Stage.Rampage;
        breathActive = false;
        breathDone = false;
        phase = Phase.Charge;
        phaseTime = 0f;
        if (vendor != null)
        {
            vendor.Relax();
        }
        if (breathCamera != null)
        {
            breathCamera.Strength = 0f;
        }
        fight.enabled = false;
        lizardRoot.localRotation = lizardRootRest;
        lizardUpper.localRotation = upperRest;
        lizardHead.localRotation = headRest;
        robotRoot.gameObject.SetActive(false);
        robotRoot.localPosition = robotLanding;
        robotRoot.localRotation = robotRootRest;
        robotUpper.localRotation = robotUpperRest;
        robotArmLeft.localRotation = robotArmLeftRest;
        robotArmRight.localRotation = robotArmRightRest;
        if (breath != null)
        {
            breath.StopBreath();
            breath.SetCharge(0f);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }
        GameManager game = GameManager.Instance;
        bool canStart = game != null && !game.IsRespawning && !game.InCutscene && !game.LevelFinished;
        float u = Lane.Project(player.position);
        switch (stage)
        {
            case Stage.Rampage:
                if (exposing)
                {
                    return;
                }
                if (canStart && u <= arrivalU)
                {
                    sequence = StartCoroutine(Arrival());
                    return;
                }
                Rampage(u);
                break;
            case Stage.Fight:
                if (canStart && u <= finaleU)
                {
                    sequence = StartCoroutine(Finale());
                }
                break;
        }
    }

    // ---------- Wüten mit Feueratem ----------

    private void Rampage(float u)
    {
        float dt = Time.deltaTime;
        bool inRange = u >= Mathf.Min(breathFromU, breathToU) && u <= Mathf.Max(breathFromU, breathToU);
        if (inRange && !breathActive && !breathDone)
        {
            breathActive = true;
            Next(Phase.Charge);
            if (vendor != null)
            {
                vendor.Shout();
            }
        }

        // Kamera-Zone weich ein- und ausblenden
        if (breathCamera != null)
        {
            breathCamera.Strength = Mathf.MoveTowards(breathCamera.Strength, breathActive ? 1f : 0f, dt / cameraFade);
        }

        float targetLean = 0f, targetHead = 0f;
        if (breathActive)
        {
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Charge:
                    float k = Mathf.Clamp01(phaseTime / chargeTime);
                    targetLean = -chargeLean * k;
                    // Kopf nur leicht hoch: Das Maul mit dem Feuerball soll von unten (Spielkamera) sichtbar bleiben
                    targetHead = -10f * k;
                    breath.SetCharge(k);
                    if (phaseTime >= chargeTime)
                    {
                        breath.SetCharge(0.6f);
                        breath.StartBreath();
                        Next(Phase.Breath);
                        if (vendor != null)
                        {
                            vendor.TakeCover();
                        }
                    }
                    break;
                case Phase.Breath:
                    targetLean = breathLean;
                    targetHead = 22f;
                    if (phaseTime >= breathTime)
                    {
                        breath.StopBreath();
                        breath.SetCharge(0f);
                        Next(Phase.Recover);
                    }
                    break;
                case Phase.Recover:
                    if (phaseTime >= recoverTime)
                    {
                        // Nur einmal (bis zum nächsten Respawn)
                        breathActive = false;
                        breathDone = true;
                        if (vendor != null)
                        {
                            vendor.Relax();
                        }
                    }
                    break;
            }
        }

        // Weich in die Pose (Lehnen: positiv = nach vorn, Kopf: positiv = Schnauze runter), dazu langsames Wiegen
        float blend = 1f - Mathf.Exp(-6f * dt);
        lean = Mathf.Lerp(lean, targetLean, blend);
        headPitch = Mathf.Lerp(headPitch, targetHead, blend);
        lizardUpper.localRotation = upperRest * Quaternion.Euler(lean, Mathf.Sin(Time.time * 0.8f) * 3f, Mathf.Sin(Time.time * 0.5f) * 2f);
        lizardHead.localRotation = headRest * Quaternion.Euler(headPitch, Mathf.Sin(Time.time * 0.6f) * 8f, 0f);
    }

    // ---------- Intro: Sie zerschlägt ein Hochhaus ----------

    // Vorspann des Level-Intros: Das Hochhaus steht noch, sie holt mit dem Arm aus (lehnt sich zurück, brüllt), schlägt in
    // die oberen Stockwerke, der Turm sackt in einer Staubwolke zusammen. Der Feueratem bleibt eine Überraschung fürs Level
    public void PlayExpose()
    {
        if (stage != Stage.Rampage || introTower == null || lizardSmashArm == null)
        {
            return;
        }
        if (exposeRoutine != null)
        {
            StopCoroutine(exposeRoutine);
        }
        exposeRoutine = StartCoroutine(Smash());
    }

    // Vorbei (oder übersprungen): Turm ist weg, Echse in Grundhaltung
    public void EndExpose()
    {
        if (exposeRoutine != null)
        {
            StopCoroutine(exposeRoutine);
            exposeRoutine = null;
        }
        exposing = false;
        if (introTower != null)
        {
            introTower.SetCollapsed();
        }
        if (lizardSmashArm != null)
        {
            lizardSmashArm.localRotation = smashArmRest;
        }
        lizardUpper.localRotation = upperRest;
        lizardHead.localRotation = headRest;
    }

    private IEnumerator Smash()
    {
        exposing = true;
        introTower.SetStanding();
        // Arm-Drehung um die lokale X-Achse: Ruhe = nach vorn (-80), hoch über den Kopf (-170), Schlag schräg nach unten (-40)
        Vector3 rest = smashArmRest.eulerAngles;
        Quaternion raised = Quaternion.Euler(-170f, rest.y, rest.z);
        Quaternion struck = Quaternion.Euler(-35f, rest.y, rest.z);
        const float calm = 0.6f, windUp = 1.2f, swing = 0.35f, recover = 1.6f;
        bool hit = false;
        for (float t = 0f; t < calm + windUp + swing + recover; t += Time.deltaTime)
        {
            float lean, head;
            Quaternion arm;
            if (t < calm)
            {
                // Wiegt sich noch, schaut auf den Turm
                lean = 0f;
                head = 0f;
                arm = smashArmRest;
            }
            else if (t < calm + windUp)
            {
                // Ausholen: Arm hoch, Oberkörper zurück, Kopf hoch (brüllt)
                float k = Mathf.SmoothStep(0f, 1f, (t - calm) / windUp);
                lean = -10f * k;
                head = -25f * k;
                arm = Quaternion.Slerp(smashArmRest, raised, k);
            }
            else if (t < calm + windUp + swing)
            {
                // Schlag: beschleunigt (k²) nach vorn unten, Oberkörper mit
                float k = (t - calm - windUp) / swing;
                k *= k;
                lean = Mathf.Lerp(-10f, 14f, k);
                head = Mathf.Lerp(-25f, 15f, k);
                arm = Quaternion.Slerp(raised, struck, k);
            }
            else
            {
                // Getroffen (einmal, im ersten Bild danach), dann langsam zurück in die Ruhe
                if (!hit)
                {
                    hit = true;
                    introTower.Smash(introTower.transform.position - lizardRoot.position);
                }
                float k = Mathf.SmoothStep(0f, 1f, (t - calm - windUp - swing) / recover);
                lean = Mathf.Lerp(14f, 0f, k);
                head = Mathf.Lerp(15f, 0f, k);
                arm = Quaternion.Slerp(struck, smashArmRest, k);
            }
            lizardSmashArm.localRotation = arm;
            lizardUpper.localRotation = upperRest * Quaternion.Euler(lean, 0f, 0f);
            lizardHead.localRotation = headRest * Quaternion.Euler(head, 0f, 0f);
            yield return null;
        }
        lizardSmashArm.localRotation = smashArmRest;
        exposing = false;
        exposeRoutine = null;
    }

    private void Next(Phase next)
    {
        phase = next;
        phaseTime = 0f;
    }

    // ---------- Zwischensequenzen ----------

    // Roboter stürzt herab, schlägt auf, richtet sich auf; Echse brüllt und dreht sich zu ihm um
    private IEnumerator Arrival()
    {
        stage = Stage.Arrival;
        breath.StopBreath();
        breath.SetCharge(0f);
        float started = Time.time;
        ThirdPersonCamera follow = BeginCutscene();
        Transform cam = Camera.main.transform;
        Vector3 landing = robotRoot.parent.TransformPoint(robotLanding);
        Vector3 camPos = (lizardRoot.position + landing) / 2f + arrivalCameraOffset;
        // Die Kamera springt an die neue Stelle, also auch gleich in die richtige Richtung (sonst dreht sie sich erst langsam her)
        cam.SetPositionAndRotation(camPos, Quaternion.LookRotation(landing + Vector3.up * 100f - camPos));

        robotRoot.gameObject.SetActive(true);
        robotUpper.localRotation = robotUpperRest * Quaternion.Euler(30f, 0f, 0f);
        foreach (ParticleSystem thruster in thrusters) thruster.Play();
        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            // Wird immer schneller (k²), am Ende ein bisschen schräg
            robotRoot.position = landing + Vector3.up * (dropHeight * (1f - k * k));
            robotRoot.localRotation = robotRootRest * Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * 8f);
            cam.position = camPos;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(robotRoot.position + Vector3.up * 40f - camPos), 1f - Mathf.Exp(-8f * Time.deltaTime));
            yield return null;
        }
        robotRoot.position = landing;
        robotRoot.localRotation = robotRootRest;
        foreach (ParticleSystem thruster in thrusters) thruster.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (landingDust != null) landingDust.Emit(160);
        CameraShake.Shake(1.4f, 1.6f);

        // Aufrichten aus der Hocke, die Echse brüllt (Kopf hoch) und dreht sich um (180° in 1.6 s)
        Quaternion lizardStart = lizardRoot.localRotation;
        Quaternion lizardEnd = lizardRootRest * Quaternion.Euler(0f, 180f, 0f);
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            float rise = Mathf.Clamp01(t / 1f);
            robotUpper.localRotation = robotUpperRest * Quaternion.Euler(30f * (1f - Mathf.SmoothStep(0f, 1f, rise)), 0f, 0f);
            float roar = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
            lizardHead.localRotation = headRest * Quaternion.Euler(-35f * roar, 0f, 0f);
            lizardUpper.localRotation = upperRest * Quaternion.Euler(-10f * roar, 0f, 0f);
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 2.2f, t));
            lizardRoot.localRotation = Quaternion.Slerp(lizardStart, lizardEnd, turn);
            Vector3 focus = Vector3.Lerp(landing, lizardRoot.position, 0.5f) + Vector3.up * 45f;
            cam.position = camPos;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(focus - camPos), 1f - Mathf.Exp(-4f * Time.deltaTime));
            yield return null;
        }
        robotUpper.localRotation = robotUpperRest;
        lizardRoot.localRotation = lizardEnd;
        fight.enabled = true;
        stage = Stage.Fight;
        EndCutscene(follow, Time.time - started);
        sequence = null;
    }

    // Aufwärtshaken, die Echse kippt zur Seite (Westen, auf die Ruinen), Staub, der Roboter reckt die Arme
    private IEnumerator Finale()
    {
        stage = Stage.Finale;
        fight.enabled = false;
        float started = Time.time;
        ThirdPersonCamera follow = BeginCutscene();
        Transform cam = Camera.main.transform;
        Vector3 camPos = (lizardRoot.position + robotRoot.position) / 2f + finaleCameraOffset;
        Vector3 between = Vector3.Lerp(robotRoot.position, lizardRoot.position, 0.5f) + Vector3.up * 40f;
        cam.SetPositionAndRotation(camPos, Quaternion.LookRotation(between - camPos));

        // Ausholen und Haken
        Quaternion armStart = robotArmRight.localRotation;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float wind = Mathf.Clamp01(t / 0.5f), punch = Mathf.Clamp01((t - 0.5f) / 0.2f);
            robotArmRight.localRotation = Quaternion.Slerp(armStart, robotArmRightRest * Quaternion.Euler(40f, 0f, 0f), wind);
            if (punch > 0f) robotArmRight.localRotation = robotArmRightRest * Quaternion.Euler(Mathf.Lerp(40f, -150f, punch), 0f, 0f);
            robotUpper.localRotation = robotUpperRest * Quaternion.Euler(-8f * punch, 0f, 0f);
            cam.position = camPos;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(between - camPos), 1f - Mathf.Exp(-8f * Time.deltaTime));
            yield return null;
        }
        CameraShake.Shake(0.6f, 0.5f);

        // Echse: Kopf fliegt zurück, dann kippt sie um ihre Füße zur Seite (lokal um Z, positiv = nach Westen), immer schneller
        Quaternion fallStart = lizardRoot.localRotation;
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 1.8f);
            lizardHead.localRotation = headRest * Quaternion.Euler(-40f * Mathf.Clamp01(t / 0.3f), 0f, 0f);
            lizardRoot.localRotation = fallStart * Quaternion.Euler(0f, 0f, 84f * k * k);
            cam.position = camPos;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(lizardRoot.position + lizardRoot.up * 25f - camPos), 1f - Mathf.Exp(-3f * Time.deltaTime));
            yield return null;
        }
        lizardRoot.localRotation = fallStart * Quaternion.Euler(0f, 0f, 84f);
        if (fallDust != null) fallDust.Emit(200);
        CameraShake.Shake(1.6f, 1.8f);

        // Siegerpose
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            robotArmLeft.localRotation = Quaternion.Slerp(robotArmLeftRest, robotArmLeftRest * Quaternion.Euler(-170f, 0f, 0f), k);
            robotArmRight.localRotation = Quaternion.Slerp(robotArmRight.localRotation, robotArmRightRest * Quaternion.Euler(-170f, 0f, 0f), k);
            robotUpper.localRotation = robotUpperRest;
            cam.position = camPos;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(robotRoot.position + Vector3.up * 55f - camPos), 1f - Mathf.Exp(-3f * Time.deltaTime));
            yield return null;
        }
        stage = Stage.Done;
        EndCutscene(follow, Time.time - started);
        sequence = null;
    }

    // Spieler einfrieren, Respawns sperren, Kamera übernehmen
    private ThirdPersonCamera BeginCutscene()
    {
        GameManager game = GameManager.Instance;
        if (game != null) game.InCutscene = true;
        controller.Freeze();
        ThirdPersonCamera follow = Camera.main.GetComponent<ThirdPersonCamera>();
        if (follow != null) follow.enabled = false;
        return follow;
    }

    // Kamera zurück zum Spieler (springt hin), Steuerung frei, die Dauer der Szene kostet keine Wärme
    private void EndCutscene(ThirdPersonCamera follow, float duration)
    {
        if (follow != null)
        {
            follow.enabled = true;
            follow.SnapToTarget();
        }
        controller.Unfreeze();
        GameManager game = GameManager.Instance;
        if (game != null)
        {
            game.InCutscene = false;
            game.AddTime(duration);
        }
    }
}
