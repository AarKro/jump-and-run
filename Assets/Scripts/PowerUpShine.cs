using System.Collections.Generic;
using UnityEngine;

// Lässt den Player leuchten, solange ein Power-Up aktiv ist.
// Legt dazu ein zweites Material mit dem PowerUpShine-Shader über das normale Material
// (bei mehr Materialien als Submeshes zeichnet Unity das Mesh einfach nochmal mit dem Extra-Material).
public class PowerUpShine : MonoBehaviour
{
    [SerializeField] private Shader shineShader;
    [SerializeField] private Renderer targetRenderer;

    [Header("Farben (HDR: Werte über 1 leuchten mit Bloom stärker)")]
    [ColorUsage(false, true)]
    [SerializeField] private Color speedColor = new Color(0.3f, 1.2f, 2f);
    [ColorUsage(false, true)]
    [SerializeField] private Color jumpColor = new Color(2f, 1.5f, 0.3f);

    [Header("Timing")]
    [SerializeField] private float fadeTime = 0.2f;
    [Tooltip("So viele Sekunden vor Ablauf fängt der Glanz an zu blinken")]
    [SerializeField] private float warningTime = 1f;
    [SerializeField] private float warningBlinkSpeed = 12f;

    private static readonly int ShineColorId = Shader.PropertyToID("_ShineColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private readonly Dictionary<PowerUp.PowerUpType, float> endTimes = new Dictionary<PowerUp.PowerUpType, float>();
    private Material shineMaterial;
    private Material[] baseMaterials;
    private Material[] shinyMaterials;
    private bool overlayAttached;
    private float intensity;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
        if (shineShader == null || targetRenderer == null)
        {
            Debug.LogWarning($"{name}: PowerUpShine braucht Shader und Renderer.", this);
            enabled = false;
            return;
        }

        shineMaterial = new Material(shineShader);
        baseMaterials = targetRenderer.sharedMaterials;
        shinyMaterials = new Material[baseMaterials.Length + 1];
        baseMaterials.CopyTo(shinyMaterials, 0);
        shinyMaterials[baseMaterials.Length] = shineMaterial;
    }

    private void OnDestroy()
    {
        if (shineMaterial != null)
        {
            Destroy(shineMaterial);
        }
    }

    // Sofort aus, z. B. beim Respawn
    public void Clear()
    {
        endTimes.Clear();
        intensity = 0f;
        if (overlayAttached)
        {
            targetRenderer.sharedMaterials = baseMaterials;
            overlayAttached = false;
        }
    }

    public void Play(PowerUp.PowerUpType type, float duration)
    {
        endTimes[type] = Time.time + duration;
    }

    private void Update()
    {
        // Aktive Power-Ups einsammeln: Farben mischen, das zuletzt ablaufende bestimmt das Blinken
        Color color = Color.black;
        int activeCount = 0;
        float latestEnd = 0f;
        foreach (KeyValuePair<PowerUp.PowerUpType, float> entry in endTimes)
        {
            if (entry.Value <= Time.time)
            {
                continue;
            }
            color += ColorFor(entry.Key);
            activeCount++;
            latestEnd = Mathf.Max(latestEnd, entry.Value);
        }

        float targetIntensity = 0f;
        if (activeCount > 0)
        {
            color /= activeCount;
            shineMaterial.SetColor(ShineColorId, color);

            targetIntensity = 1f;
            float remaining = latestEnd - Time.time;
            if (remaining < warningTime)
            {
                // Kurz vor Ablauf blinken, damit man weiß, dass der Boost gleich endet
                targetIntensity = Mathf.Sin(Time.time * warningBlinkSpeed) > 0f ? 1f : 0.2f;
            }
        }

        intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.deltaTime / Mathf.Max(fadeTime, 0.01f));
        shineMaterial.SetFloat(IntensityId, intensity);

        // Overlay nur anhängen, solange man es sieht: spart den zweiten Draw-Call
        bool shouldAttach = intensity > 0f;
        if (shouldAttach != overlayAttached)
        {
            targetRenderer.sharedMaterials = shouldAttach ? shinyMaterials : baseMaterials;
            overlayAttached = shouldAttach;
        }
    }

    private Color ColorFor(PowerUp.PowerUpType type)
    {
        switch (type)
        {
            case PowerUp.PowerUpType.Speed:
                return speedColor;
            case PowerUp.PowerUpType.Jump:
                return jumpColor;
            default:
                return Color.white;
        }
    }
}
