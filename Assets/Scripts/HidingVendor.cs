using UnityEngine;

// Eisverkäufer hinter seinem umgekippten Wagen im Park (Level 5). Kauert dort die ganze Zeit (CrowdFigure.Duck halb).
// Lädt die Echse ihren Feueratem auf (KaijuBattle ruft Shout), springt er halb hoch, winkt den Spieler mit großen Armbewegungen
// zu sich und ruft "OVER HERE!". Kommt das Feuer (TakeCover), duckt er sich ganz weg, Arme über dem Kopf. Danach (Relax)
// kauert er wieder. Die Arme werden in LateUpdate gesetzt, also nach der Animation von CrowdFigure (die läuft in Update).
public class HidingVendor : MonoBehaviour
{
    [SerializeField] private CrowdFigure figure;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftArm;
    [SerializeField] private GameObject bubble;
    [Tooltip("Kauern im Ruhezustand (0 = steht, 1 = ganz geduckt)")]
    [SerializeField] private float idleCrouch = 0.6f;

    private enum State { Hiding, Shouting, Covering }

    private State state = State.Hiding;
    private float stateTime;

    private void Start()
    {
        Relax();
    }

    public void Shout()
    {
        SetState(State.Shouting);
        bubble.SetActive(true);
    }

    public void TakeCover()
    {
        SetState(State.Covering);
        bubble.SetActive(false);
    }

    public void Relax()
    {
        SetState(State.Hiding);
        bubble.SetActive(false);
    }

    private void SetState(State next)
    {
        state = next;
        stateTime = 0f;
    }

    private void Update()
    {
        stateTime += Time.deltaTime;
        switch (state)
        {
            case State.Shouting:
                // Kommt halb aus der Deckung, um gesehen zu werden
                figure.Duck = Mathf.Lerp(idleCrouch, 0.15f, Mathf.Clamp01(stateTime / 0.3f));
                break;
            case State.Covering:
                figure.Duck = 1f;
                break;
            default:
                figure.Duck = idleCrouch;
                break;
        }
    }

    private void LateUpdate()
    {
        if (state != State.Shouting)
        {
            return;
        }
        // Winkt mit dem rechten Arm über dem Kopf heran (Drehung um X: positiv = nach vorn/oben), der linke zeigt auf den Boden
        // neben sich ("hierher!")
        float wave = Mathf.Sin(stateTime * 11f);
        rightArm.localRotation = Quaternion.Euler(150f + wave * 30f, 0f, 25f + wave * 15f);
        leftArm.localRotation = Quaternion.Euler(50f, 0f, -35f);
    }
}
