using UnityEngine;

// StageManager의 UI 갱신 전용 partial
public partial class StageManager
{
    private void UpdateKillUI()
    {
        if (killCountText != null)
            killCountText.text = $"처치  {killCount} / {killGoal}";
    }

    // 마지막으로 표시한 '초'. 같은 초면 문자열을 다시 만들지 않습니다.
    private int lastTimerSecond = int.MinValue;

    // ★ [프레임 드랍 수정] Update 에서 매 프레임 불리는 함수입니다.
    //   예전에는 화면 숫자가 그대로여도 매 프레임 $"..." 로 새 문자열을 만들었습니다(초당 60개 → GC).
    //   표시가 바뀌는 건 1초에 한 번뿐이라, 초가 바뀔 때만 갱신합니다.
    //   (빨간색 전환 30초도 정수 초 경계라 같은 시점에 함께 바뀝니다)
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