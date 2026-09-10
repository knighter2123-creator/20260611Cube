using UnityEngine;

/// <summary>
/// 골드 즉시 지급.
///
/// [수치 감각]
/// StageManager 의 statMultiplier 가 1.5 라 배율이 1.5^n 으로 폭발합니다.
/// 20스테이지면 이미 약 3,300배. baseAmount 2000 → 660만 골드입니다.
/// 잡몹 하나가 rewardGold × statMult 를 주니,
/// "잡몹 몇 마리치인가"를 기준으로 잡으면 감이 잡힙니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/골드 지급", fileName = "_Gold")]
public class AugmentGold : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddGold(amount);
}
