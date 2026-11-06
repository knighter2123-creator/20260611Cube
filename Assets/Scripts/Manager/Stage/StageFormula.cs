using UnityEngine;

/// <summary>스테이지 난이도·경험치 공식의 유일한 위치(static 이라 StageManager 없이도 사용) — 복사본끼리 배율이 달라 오프라인 보상이 부풀었던 문제를 막으려고 한 곳에.</summary>
public static class StageFormula
{
    /// <summary>스테이지 하나당 적 스탯 배율. 기존 StageScene 인스펙터 값(1.3)을 그대로 옮겼다.</summary>
    public const float StatMultiplier = 1.3f;

    public const int MaxStagePerWorld = 10;

    /// <summary>1-1을 0으로 하는 누적 진행도.</summary>
    public static int Progress(int world, int stage)
        => (world - 1) * MaxStagePerWorld + (stage - 1);

    /// <summary>해당 스테이지의 누적 스탯 배율 (1-1 = 1.0).</summary>
    public static float StatMult(int world, int stage)
        => Mathf.Pow(StatMultiplier, Progress(world, stage));

    /// <summary>경험치 성장 지수 — 배율 = (1 + 진행도)^ExpGrowthPower 다항식 (체력 배율 1.3^n 을 쓰면 9월드쯤 int 를 넘어 음수가 됨).</summary>
    public const double ExpGrowthPower = 1.5;

    /// <summary>해당 스테이지의 경험치 보상 배율 (1-1 = 1.0). 적 체력 배율과는 별개.</summary>
    public static double ExpMult(int world, int stage)
        => System.Math.Pow(1 + Progress(world, stage), ExpGrowthPower);

    /// <summary>지금 스테이지의 경험치 배율 — StageManager 가 없으면 세이브 기준, 그것도 없으면 1.</summary>
    public static double CurrentExpMult()
    {
        var sm = StageManager.Instance;
        if (sm != null) return ExpMult(sm.CurrentWorld, sm.CurrentStage);

        var data = SaveManager.Instance?.Current;
        return data != null ? ExpMult(data.currentWorld, data.currentStage) : 1.0;
    }
}
