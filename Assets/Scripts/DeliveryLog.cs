using System.Collections.Generic;

// Merkt sich über die Levelwechsel hinweg, wie viel Trinkgeld es pro Level gab, für den Endbildschirm nach Level 5.
// Static statt Komponente: Jeder Levelwechsel entlädt die Level-Szene samt GameManager, ein statisches Feld bleibt
// dagegen erhalten, solange das Spiel läuft (ohne DontDestroyOnLoad-Objekt)
public static class DeliveryLog
{
    public struct Entry
    {
        public string address;
        public int coins;
    }

    // Levelnummer (1, 2, ...) → Lieferung. Wird ein Level nochmal geliefert, zählt die letzte Lieferung
    private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();

    // Mit ausgeschaltetem Domain Reload bliebe der Inhalt vom letzten Play stehen
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear();
    }

    public static void Record(int levelNumber, string address, int coins)
    {
        entries[levelNumber] = new Entry { address = address, coins = coins };
    }

    public static bool TryGet(int levelNumber, out Entry entry)
    {
        return entries.TryGetValue(levelNumber, out entry);
    }

    // Neues Spiel ab Level 1: Trinkgeld wieder bei 0
    public static void Clear()
    {
        entries.Clear();
    }
}
