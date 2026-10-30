using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Wird bei jedem Respawn ausgelöst: Gegner, Power-Ups und Plattformen setzen sich darauf hin zurück.
    // Static, damit sich Objekte im Awake anmelden können, egal ob der GameManager schon existiert
    public static event System.Action LevelReset;

    // Pizza abgeliefert, mit Trinkgeld in Münzen (1-3). Das HUD zeigt darauf das Ergebnis an
    public static event System.Action<int> PizzaDelivered;
    // Wärme ist auf 0: Pizza ist kalt, kurz darauf startet das Level neu
    public static event System.Action PizzaCold;

    // Ab dieser Restwärme (Anteil 0-1) gibt es 2 bzw. 3 Münzen Trinkgeld, darunter (aber über 0) 1 Münze
    public const float TwoCoinHeat = 1f / 3f;
    public const float ThreeCoinHeat = 2f / 3f;

    [Header("Player und Start")]
    [SerializeField] private GameObject player;
    [Tooltip("Tür, aus der der Spieler beim Levelstart läuft (Pizzeria). Setzt LevelCity zur Laufzeit (liegt in der Stadt-Szene)")]
    [SerializeField] private SpawnDoor startDoor;

    [Header("Wärme (Pizza wird kalt)")]
    [Tooltip("So viele Sekunden bleibt die Pizza warm. Bei 0 ist sie kalt und das Level startet neu.")]
    [SerializeField] private float levelTime = 120f;
    [Tooltip("So lange steht \"Pizza's cold!\" da, bevor das Level neu startet")]
    [SerializeField] private float coldRestartDelay = 1.5f;

    public float RemainingTime { get; private set; }
    // Restwärme als Anteil: 1 = frisch aus dem Ofen, 0 = kalt. Kann durch Wärme-Power-Ups über 1 gehen
    public float HeatPercent => levelTime > 0f ? RemainingTime / levelTime : 0f;
    // Geliefert oder kalt: Wärme läuft nicht mehr weiter, Respawns werden ignoriert
    public bool LevelFinished { get; private set; }

    // Gerade am Sterben/Wiederauftauchen: weitere Treffer in der Zeit zählen nicht
    public bool IsRespawning { get; private set; }
    // Eine kurze Szene mit dem Spieler läuft (z.B. Mikrowelle). Respawns werden solange ignoriert
    public bool InCutscene { get; set; }

    private PlayerController playerController;
    private PlayerLife playerLife;
    // Kennt die Lieferadresse des Levels (steht im Intro auf dem Lieferschein), für den Endbildschirm
    private IntroFlyover intro;

    // Tür des letzten Checkpoints (bzw. die Start-Tür): Beim Respawn läuft der Spieler aus ihr heraus
    private SpawnDoor currentDoor;
    // Kommt er nach einem Respawn auf dem Skateboard aus der Tür? Setzt der Checkpoint (Board-Shop an, Kiosk aus)
    private bool respawnSkating;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RemainingTime = levelTime;
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerLife = player.GetComponent<PlayerLife>();
        }
    }

    // Der Spieler ist draußen und spielt. Vorher (Stadt laden, Kamerafahrt, aus der Tür laufen) läuft die Wärme nicht
    private bool started;

    // Start als Coroutine: Unity wartet dann einfach mit dem Rest (Stadt laden, Kamerafahrt, Tür), Frame für Frame
    private IEnumerator Start()
    {
        // Nach "Pizza's cold!" ohne Kamerafahrt, damit der neue Versuch schnell losgeht. Immer abfragen, damit das Merkzeichen weg ist
        bool restarted = LevelFlow.ConsumeRestart();

        // Intro: sofort schwarz (Iris zu) und HUD aus, noch bevor das erste Bild gezeichnet wird
        intro = FindAnyObjectByType<IntroFlyover>();
        if (intro != null)
        {
            intro.Prepare();
        }

        if (LevelCity.Instance != null)
        {
            while (LevelCity.Instance != null && !LevelCity.Instance.Ready)
            {
                yield return null;
            }
        }
        currentDoor = startDoor;

        if (player != null && currentDoor != null && playerLife != null)
        {
            // Levelstart: Der Pizzabote wartet in der Pizzeria, die Kamera fliegt zum Kunden, Iris zu und an der Pizzeria
            // wieder auf, dann kommt er mit der frischen Pizza heraus
            IsRespawning = true;
            playerLife.WaitInside(currentDoor);
            LevelGoal goal = FindAnyObjectByType<LevelGoal>();
            if (intro != null)
            {
                yield return intro.Play(goal != null ? goal.transform : null, currentDoor, withFlight: !restarted);
            }
            yield return SpawnFromDoor();
        }
        else
        {
            // Jedes Level startet aus der Pizzeria der Stadt. Fehlt sie (oder der Spieler), lieber laut melden
            Debug.LogWarning("GameManager: Player, PlayerLife oder Start-Tür fehlt.");
            if (intro != null)
            {
                intro.Release();
            }
        }
        started = true;
    }

    private IEnumerator SpawnFromDoor()
    {
        IsRespawning = true;
        yield return playerLife.PlayExit(currentDoor);
        IsRespawning = false;
    }

    private void Update()
    {
        if (!started || LevelFinished)
        {
            return;
        }

        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);

        if (RemainingTime <= 0f)
        {
            HandleTimeUp();
        }
    }

    // Start-Tür aus einer anderen Szene (Pizzeria in der Stadt). Szenen-übergreifende Verweise kann man nicht im
    // Inspector speichern, deshalb setzt LevelCity sie zur Laufzeit, bevor das Level startet
    public void SetStartDoor(SpawnDoor door)
    {
        startDoor = door;
    }

    // Checkpoint mit Gebäude (Café): Beim Respawn läuft der Spieler aus dessen Tür
    public void SetCheckpoint(SpawnDoor door)
    {
        currentDoor = door;
    }

    // Ab dem letzten Checkpoint mit oder ohne Skateboard (Level 4: Board-Shop gibt es, am Kiosk wird es abgestellt)
    public void SetRespawnSkating(bool skating)
    {
        respawnSkating = skating;
    }

    public void RespawnPlayer()
    {
        // Nach dem Abliefern (oder bei kalter Pizza) soll z.B. ein Hund den Spieler nicht mehr zurücksetzen.
        // Während der Todesanimation zählen weitere Treffer (Auto, Hund, KillZone) nicht
        if (LevelFinished || IsRespawning || InCutscene)
        {
            return;
        }

        if (playerLife == null || currentDoor == null)
        {
            Debug.LogWarning("GameManager: PlayerLife oder Respawn-Tür fehlt.");
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    // Erst sterben, dann das Level zurücksetzen (Gegner, Power-Ups, Verkehr), dann wieder auftauchen.
    // Das Zurücksetzen passiert, während der Spieler unsichtbar ist, damit man es nicht sieht
    private IEnumerator RespawnRoutine()
    {
        IsRespawning = true;

        yield return playerLife.PlayDeath();

        LevelReset?.Invoke();
        if (playerController != null)
        {
            playerController.SetSkating(respawnSkating);
        }

        yield return playerLife.PlayExit(currentDoor);

        IsRespawning = false;
    }

    // Mikrowelle: wieder ofenfrisch (100%). Ein Puffer über 100% (Wärme-Bonus) bleibt erhalten
    public void ReheatPizza()
    {
        RemainingTime = Mathf.Max(RemainingTime, levelTime);
    }

    // Wärme-Bonus. Bewusst nicht auf levelTime begrenzt: Die Wärme darf als Puffer über 100% gehen
    public void AddTime(float seconds)
    {
        RemainingTime += seconds;
    }

    // Chili: Anteil des ganzen Balkens (1/6 = halbe Münz-Stufe). Auch hier darf es über 100% gehen
    public void AddHeat(float fraction)
    {
        AddTime(fraction * levelTime);
    }

    // Vom Ziel aufgerufen. Gibt false zurück, wenn das Level schon vorbei ist (z.B. Pizza gerade kalt geworden) oder der
    // Spieler gerade stirbt: Trifft ihn im selben Physik-Schritt ein Hund und er berührt das Ziel, liegt die Pizza schon auf
    // der Straße. Nach dem Respawn läuft er neu in den Trigger und liefert dann richtig
    public bool DeliverPizza()
    {
        if (LevelFinished || IsRespawning)
        {
            return false;
        }

        LevelFinished = true;
        FreezePlayer();
        int coins = CoinsForHeat(HeatPercent);
        DeliveryLog.Record(LevelFlow.CurrentLevelNumber(), intro != null ? intro.DeliveryAddress : null, coins);
        PizzaDelivered?.Invoke(coins);
        return true;
    }

    public static int CoinsForHeat(float heat)
    {
        if (heat >= ThreeCoinHeat)
        {
            return 3;
        }
        if (heat >= TwoCoinHeat)
        {
            return 2;
        }
        return heat > 0f ? 1 : 0;
    }

    public void RestartCurrentLevel()
    {
        // Mit Stadt: nur die Level-Szene neu laden, die Stadt bleibt
        LevelFlow.Restart();
    }

    private void FreezePlayer()
    {
        if (playerController != null)
        {
            playerController.Freeze();
        }
    }

    private void HandleTimeUp()
    {
        LevelFinished = true;
        FreezePlayer();
        PizzaCold?.Invoke();
        StartCoroutine(RestartAfterDelay());
    }

    private System.Collections.IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(coldRestartDelay);
        RestartCurrentLevel();
    }
}
