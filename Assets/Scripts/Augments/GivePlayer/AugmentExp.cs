using UnityEngine;

/// <summary>경험치 즉시 지급 — LevelUpManager 를 거쳐 레벨업 연출도 재생, 적 처치와 같은 경험치 전용 배율 사용.</summary>
[CreateAssetMenu(menuName = "Game/Augment/경험치 지급", fileName = "_Exp")]
public class AugmentExp : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddExp(amount);

    // 적 처치 경험치와 같은 곡선을 따라가도록 체력 배율(1.3^n) 대신 경험치 전용 배율을 씁니다.
    protected override double StageMultiplier() => StageFormula.CurrentExpMult();
}
