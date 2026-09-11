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

        // ★ 추가 — Awake ~ Start 사이에는 currentStep=0, progress=0 입니다.
        //   그 구간에 저장이 돌면 가이드 퀘스트가 1단계로 되돌아갑니다.
        //
        //   기존의 isLoading 플래그는 ApplyFrom 안에서 '자기가 부르는' RequestSave 만 막습니다.
        //   OnApplicationPause 처럼 밖에서 들어오는 SaveManager.Save() 는 막지 못합니다.
        //   두 플래그는 막는 대상이 다르므로 둘 다 필요합니다.
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

                // ★ 신규 유저의 "1단계 / 진행 0" 도 정상 상태이므로 저장을 허용해야 합니다.
                //   여기서 loaded 를 안 세우면 첫 플레이의 퀘스트 진행이 영영 저장되지 않습니다.
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

        Debug.Log($"[GuideQuest] 복원 완료 — {currentStep + 1}단계 / 진행 {progress}");
    }
}