using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class TimerDisplay : MonoBehaviour
{
    private Text timerText;

    private void Awake()
    {
        timerText = GetComponent<Text>();
    }

    private void Update()
    {
        if (GameManager.Instance == null)
        {
            timerText.text = "--:--";
            return;
        }

        float remaining = GameManager.Instance.RemainingTime;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
