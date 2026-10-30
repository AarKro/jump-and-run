using UnityEngine;

// Sitzt auf der Wurzel der Stadt-Szene. Sobald es eine Instanz gibt, ist die Stadt geladen (Awake gelaufen),
// darauf wartet LevelCity, bevor es Ausschnitte und Gebäude einrichtet.
public class CityRoot : MonoBehaviour
{
    public static CityRoot Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
