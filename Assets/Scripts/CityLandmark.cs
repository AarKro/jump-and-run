using System.Collections.Generic;
using UnityEngine;

// Festes Gebäude der Stadt mit Spiel-Funktion: Pizzeria (Start-Tür), Café (Checkpoint), Kiosk (Mikrowelle).
// Die Stadt bleibt über alle Level geladen, deshalb ist die Funktion erst mal aus: Jedes Level schaltet über LevelCity nur
// ein, was es braucht, und setzt dabei alles auf Anfang (Café zu, Mikrowelle bereit).
// Verweise zwischen zwei Szenen kann Unity nicht speichern, deshalb finden Level die Gebäude zur Laufzeit über ihren Namen.
public class CityLandmark : MonoBehaviour
{
    // Static: Mit ausgeschaltetem Domain Reload überlebt das Dictionary zwischen Play-Sessions, deshalb in OnDestroy austragen
    private static readonly Dictionary<string, CityLandmark> Registry = new Dictionary<string, CityLandmark>();

    [Tooltip("Name, unter dem Level das Gebäude finden (LevelCity)")]
    [SerializeField] private string id;

    public string Id => id;
    public SpawnDoor Door { get; private set; }

    public static IEnumerable<CityLandmark> All => Registry.Values;

    public static CityLandmark Find(string landmarkId)
    {
        if (string.IsNullOrEmpty(landmarkId) || !Registry.TryGetValue(landmarkId, out CityLandmark landmark))
        {
            return null;
        }
        return landmark != null ? landmark : null;
    }

    private void Awake()
    {
        Door = GetComponentInChildren<SpawnDoor>(true);
        Registry[id] = this;
    }

    private void OnDestroy()
    {
        if (Registry.TryGetValue(id, out CityLandmark registered) && registered == this)
        {
            Registry.Remove(id);
        }
    }

    // used: Das aktuelle Level nutzt das Gebäude als Checkpoint bzw. Mikrowelle
    public void SetUsed(bool used)
    {
        // Trigger-Meldungen (OnTriggerEnter) bekommen auch abgeschaltete Skripte. Deshalb den Trigger-Collider abschalten
        foreach (Collider trigger in GetComponentsInChildren<Collider>(true))
        {
            if (trigger.isTrigger)
            {
                trigger.enabled = used;
            }
        }
        foreach (Checkpoint checkpoint in GetComponentsInChildren<Checkpoint>(true))
        {
            checkpoint.ResetCheckpoint();
        }
        foreach (CoffeeShopEffects effects in GetComponentsInChildren<CoffeeShopEffects>(true))
        {
            effects.ResetLook(closed: used);
        }
        foreach (MicrowaveStation microwave in GetComponentsInChildren<MicrowaveStation>(true))
        {
            microwave.ResetForNewLevel();
        }
        if (Door != null)
        {
            Door.SnapClosed();
        }
    }
}
