using System.Collections;
using TMPro;
using UnityEngine;

// "Checkpoint unlocked!" über einem Checkpoint-Gebäude (Café, Gelateria, Bauwagen): ploppt auf, steigt langsam hoch und
// blendet aus, dazu eine Ladung Funken. Der Text liegt in der Welt (nicht im HUD), dadurch sieht man direkt,
// WELCHES Gebäude jetzt der Checkpoint ist
public static class CheckpointPopup
{
    public static IEnumerator Play(TextMeshPro text, Vector3 startLocalPosition, ParticleSystem sparkles, int sparkleCount,
        float rise, float holdTime, float fadeTime)
    {
        if (text == null)
        {
            yield break;
        }
        Transform t = text.transform;
        text.gameObject.SetActive(true);
        t.localPosition = startLocalPosition;
        text.alpha = 1f;
        if (sparkles != null)
        {
            sparkles.transform.position = t.position;
            sparkles.Emit(sparkleCount);
        }

        const float popTime = 0.15f;
        float total = popTime + holdTime + fadeTime;
        for (float time = 0f; time < total; time += Time.deltaTime)
        {
            // Aufploppen: von 1.4-fach auf normale Größe
            t.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, time / popTime);
            // Ease-out: schnell los, oben langsam
            float k = time / total;
            t.localPosition = startLocalPosition + Vector3.up * (rise * (1f - (1f - k) * (1f - k)));
            text.alpha = 1f - Mathf.Clamp01((time - popTime - holdTime) / fadeTime);
            yield return null;
        }
        text.gameObject.SetActive(false);
        t.localScale = Vector3.one;
    }
}
