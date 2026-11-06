using UnityEngine;

// StageManager 세이브 연동 — 진행도(world/stage)를 SaveData 와 주고받음
public partial class StageManager
{
    /// <summary>현재 진행도를 SaveData에 기록.</summary>
    public void CaptureTo(SaveData d)
    {
        d.currentWorld = currentWorld;
        d.currentStage = currentStage;
    }

    /// <summary>SaveData 진행도를 반영하고 난이도 배율 재계산 (스폰은 호출자가 NextStage 로).</summary>
    public void ApplyFrom(SaveData d)
    {
        SetPosition(d.currentWorld, d.currentStage);
    }
}