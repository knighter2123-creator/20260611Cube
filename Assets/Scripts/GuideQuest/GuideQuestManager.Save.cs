using System;
using UnityEngine;

[Serializable]
public class GuideQuestSaveData
{
    public int currentStep;
    public long progress;
    public int highestChapter;
    public int highestStage;
}

public partial class GuideQuestManager
{
    public void CaptureTo(SaveData data)
    {
        if (data == null) return;
        if (data.guideQuest == null) data.guideQuest = new GuideQuestSaveData();

        data.guideQuest.currentStep    = currentStep;
        data.guideQuest.progress       = progress;
        data.guideQuest.highestChapter = highestChapter;
        data.guideQuest.highestStage   = highestStage;
    }

    public void ApplyFrom(SaveData data)
    {
        isLoading = true;   // 이 안에서 발생하는 모든 저장을 막는다
        try
        {
            if (data == null || data.guideQuest == null)
            {
                // 신규 유저 — 1단계부터
                currentStep = 0;
                progress = 0;
                RebuildCurrent(notify: true);
                return;
            }

            currentStep    = Mathf.Max(0, data.guideQuest.currentStep);
            progress       = Math.Max(0, data.guideQuest.progress);
            highestChapter = data.guideQuest.highestChapter;
            highestStage   = data.guideQuest.highestStage;

            RebuildCurrent(notify: true);
            ReSyncCurrent();   // 이미 충족된 조건 반영
        }
        finally
        {
            isLoading = false;   // 예외가 나도 반드시 해제
        }

        Debug.Log($"[GuideQuest] 복원 완료 — {currentStep + 1}단계 / 진행 {progress}");
    }
}