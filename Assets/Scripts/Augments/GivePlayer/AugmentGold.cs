using UnityEngine;

/// <summary>골드 즉시 지급(× 스테이지 배율 1.5^n — 20스테이지 약 3,300배) — baseAmount 는 '잡몹 몇 마리치'로 잡으면 감이 옴.</summary>
[CreateAssetMenu(menuName = "Game/Augment/골드 지급", fileName = "_Gold")]
public class AugmentGold : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddGold(amount);
}
