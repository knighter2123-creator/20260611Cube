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
}
