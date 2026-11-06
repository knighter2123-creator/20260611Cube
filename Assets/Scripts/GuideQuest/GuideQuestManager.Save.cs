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
    /// <summary>세이브를 한 번이라도 반영했는가. CaptureTo 가드용.</summary>
    private bool loaded;

    public void CaptureTo(SaveData data)
    {
        if (data == null) return;

        // 로드 전(0단계)엔 저장 안 함 — 외부 Save() 가 1단계로 덮어쓰는 것 방지 (isLoading 은 내부 RequestSave 만 막아 둘 다 필요)
        if (!loaded) return;

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

                // 신규 유저도 loaded 를 세움 (안 그러면 첫 플레이 진행이 영영 저장 안 됨)
                loaded = true;
                return;
            }

            currentStep    = Mathf.Max(0, data.guideQuest.currentStep);
            progress       = Math.Max(0, data.guideQuest.progress);
            highestChapter = data.guideQuest.highestChapter;
            highestStage   = data.guideQuest.highestStage;

            RebuildCurrent(notify: true);
            ReSyncCurrent();   // 이미 충족된 조건 반영

            loaded = true;
        }
        finally
        {
            isLoading = false;   // 예외가 나도 반드시 해제
        }

        GameLog.Info($"[GuideQuest] 복원 완료 — {currentStep + 1}단계 / 진행 {progress}");
    }
}