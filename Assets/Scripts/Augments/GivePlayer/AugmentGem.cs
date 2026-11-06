using UnityEngine;

/// <summary>보석 즉시 지급 — ⚠ scaleWithStage 는 끌 것(후반에 보석이 쏟아져 과금 설계가 무너짐), Legendary·baseAmount 30 정도 권장.</summary>
[CreateAssetMenu(menuName = "Game/Augment/젬 지급", fileName = "_Gem")]
public class AugmentGem : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddGem(amount);
}
