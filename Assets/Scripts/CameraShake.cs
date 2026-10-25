using UnityEngine;

// Bildschirm-Wackeln (Level 5: Roboter schlägt auf, Echse fällt um). Liegt auf der Main Camera.
// Das Wackeln wird nach allen anderen LateUpdates obendrauf gesetzt und im nächsten Update wieder abgezogen, bevor die
// Kamera-Steuerung (ThirdPersonCamera) rechnet: Sie sieht also immer die ruhige Position, ihr Glätten wird nicht gestört.
[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private float strength;
    private float duration;
    private float time;
    private Vector3 applied;

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

    // Stärke in Metern, klingt über die Dauer ab
    public static void Shake(float amount, float seconds)
    {
        if (Instance == null)
        {
            return;
        }
        Instance.strength = Mathf.Max(Instance.strength * (1f - Instance.time / Mathf.Max(Instance.duration, 0.01f)), amount);
        Instance.duration = seconds;
        Instance.time = 0f;
    }

    private void Update()
    {
        transform.position -= applied;
        applied = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (time >= duration)
        {
            return;
        }
        time += Time.deltaTime;
        float fade = 1f - Mathf.Clamp01(time / duration);
        float t = Time.time * 40f;
        applied = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * (2f * strength * fade);
        transform.position += applied;
    }
}
