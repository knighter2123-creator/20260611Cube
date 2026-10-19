using TMPro;
using UnityEngine;

// 진화 스테이지 제한시간 partial.
// 2분 안에 보스를 못 잡으면 실패 → 보상 없이 원래 스테이지로 복귀.
public partial class EvolveStageManager
{
    [Header("제한시간")]
    [SerializeField] private float timeLimit = 120f;       // 2분
    [SerializeField] private TextMeshProUGUI timerText;    // (선택) 남은 시간 표시

    private float timeLeft;

    private void StartTimer()
    {
        timeLeft = timeLimit;
        UpdateTimerUI();
    }

    void Update()
    {
        if (stageOver) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            UpdateTimerUI();
            StageFail();
            return;
        }

        UpdateTimerUI();
    }

    /// <summary>시간 초과 → 보상 없이 원래 스테이지로 복귀.</summary>
    private void StageFail()
    {
        if (stageOver) return;
        stageOver = true;

        OnStageFail?.Invoke();
        GameLog.Info("[EvolveStageManager] 시간 초과 실패 — 보상 없이 원래 스테이지로 복귀");

        ReturnToStage();
    }

    // 마지막으로 표시한 '초' — StageManager.UpdateTimerUI 와 같은 이유로 초가 바뀔 때만 갱신합니다.
    private int lastTimerSecond = int.MinValue;

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
}