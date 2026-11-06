using UnityEngine;

// StageManager의 UI 갱신 전용 partial
public partial class StageManager
{
    private void UpdateKillUI()
    {
        // 적을 잡을 때마다 불리므로 문자열을 만들지 않는 SetNumbers 를 씁니다 (TmpTextExtensions 참고)
        killCountText.SetNumbers("처치  {0} / {1}", killCount, killGoal);
    }

    // 마지막으로 표시한 '초'. 같은 초면 문자열을 다시 만들지 않습니다.
    private int lastTimerSecond = int.MinValue;

    // 매 프레임 호출되지만 초가 바뀔 때만 문자열 갱신 (매 프레임 만들면 GC — 빨간색 전환도 정수 초 경계)
    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int total = Mathf.FloorToInt(timeLeft);
        if (total == lastTimerSecond) return;
        lastTimerSecond = total;

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