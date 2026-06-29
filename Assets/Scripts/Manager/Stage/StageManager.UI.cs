using UnityEngine;

// StageManager의 UI 갱신 전용 partial
public partial class StageManager
{
    private void UpdateKillUI()
    {
        if (killCountText != null)
            killCountText.text = $"처치  {killCount} / {killGoal}";
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(timeLeft / 60f);
        int s = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text  = $"{m}:{s:D2}";
        timerText.color = timeLeft <= 30f ? Color.red : Color.white;
    }

    private void UpdateStageUI()
    {
        if (stageText != null)
            stageText.text = $"{currentWorld}-{currentStage}";
    }
}