using UnityEngine;

/// <summary>
/// 스테이지 난이도 공식. StageManager와 IdleRewardManager가 함께 쓴다.
///
/// 이전: 같은 공식(Mathf.Pow(배율, 진행도))이 StageManager에 4번, IdleRewardManager에 1번
///       복사돼 있었고, 배율도 StageManager 인스펙터(1.3)와 IdleRewardManager 상수(1.5)가 달랐다.
///       → StageManager 없이 계산된 오프라인 보상이 실제보다 훨씬 크게 나왔다.
/// 지금: 값과 공식을 여기 한 곳에만 둔다. static이라 StageManager가 씬에 없어도 쓸 수 있다.
/// </summary>
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

    /// <summary>
    /// 경험치 보상 성장 지수. 경험치 배율 = (1 + 진행도) ^ ExpGrowthPower.
    /// 이전: 경험치에도 적 체력 배율(1.3^n)을 곱해서, 후반에 마리당 경험치가 폭발하고
    ///       9월드 무렵엔 int 범위를 넘어 음수가 될 수 있었다. 이제 다항식으로 따로 키운다.
    /// </summary>
    public const double ExpGrowthPower = 1.5;

    /// <summary>해당 스테이지의 경험치 보상 배율 (1-1 = 1.0). 적 체력 배율과는 별개.</summary>
    public static double ExpMult(int world, int stage)
        => System.Math.Pow(1 + Progress(world, stage), ExpGrowthPower);

    /// <summary>
    /// 지금 진행 중인 스테이지의 경험치 배율. StageManager 가 없으면(다른 씬 등) 세이브 기준, 그것도 없으면 1.
    /// </summary>
    public static double CurrentExpMult()
    {
        var sm = StageManager.Instance;
        if (sm != null) return ExpMult(sm.CurrentWorld, sm.CurrentStage);

        var data = SaveManager.Instance?.Current;
        return data != null ? ExpMult(data.currentWorld, data.currentStage) : 1.0;
    }
}
