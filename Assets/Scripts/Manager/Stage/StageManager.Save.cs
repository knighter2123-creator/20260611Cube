using UnityEngine;

// StageManager의 세이브 연동 partial.
// 스테이지 진행도(world/stage)를 SaveData와 주고받는다.
public partial class StageManager
{
    /// <summary>현재 진행도를 SaveData에 기록.</summary>
    public void CaptureTo(SaveData d)
    {
        d.currentWorld = currentWorld;
        d.currentStage = currentStage;
    }

    /// <summary>
    /// SaveData의 진행도를 필드에 반영하고 누적 난이도 배율을 재계산.
    /// 적 스폰은 호출자가 NextStage()로 처리 (Start 참고).
    /// </summary>
    public void ApplyFrom(SaveData d)
    {
        currentWorld    = d.currentWorld;
        currentStage    = d.currentStage;
        currentStatMult = Mathf.Pow(statMultiplier,
            (currentWorld - 1) * maxStagePerWorld + (currentStage - 1));
    }
}