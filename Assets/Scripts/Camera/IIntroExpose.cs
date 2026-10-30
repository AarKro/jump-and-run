// Etwas, das im Vorspann eines Level-Intros vorgeführt wird (Level 5: die Echse zerschlägt ein Hochhaus), bevor die Kamera
// zum Kunden fliegt. IntroFlyover ruft PlayExpose zu Beginn des Vorspanns auf und EndExpose, wenn er vorbei ist (auch wenn
// er übersprungen wurde: dann muss sofort der Endzustand her)
public interface IIntroExpose
{
    void PlayExpose();
    void EndExpose();
}
