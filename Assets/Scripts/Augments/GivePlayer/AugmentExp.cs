using UnityEngine;

/// <summary>
/// 경험치 즉시 지급.
///
/// LevelUpManager 를 거치므로, 이 카드로 레벨이 오르면
/// 기존 레벨업 연출(LevelUpEffect)도 그대로 재생됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/경험치 지급", fileName = "_Exp")]
public class AugmentExp : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddExp(amount);

    // 적 처치 경험치와 같은 곡선을 따라가도록 체력 배율(1.3^n) 대신 경험치 전용 배율을 씁니다.
    protected override double StageMultiplier() => StageFormula.CurrentExpMult();
}
