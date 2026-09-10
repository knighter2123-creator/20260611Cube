using UnityEngine;

/// <summary>
/// 젬(보석) 즉시 지급.
///
/// 고급 재화이므로 scaleWithStage 는 보통 꺼둡니다.
/// 스테이지 배율을 태우면 후반에 젬이 무한정 쏟아져 과금 설계가 무너집니다.
/// 등급을 Legendary 로 두고 baseAmount 를 작게(30 정도) 잡는 걸 권합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/젬 지급", fileName = "_Gem")]
public class AugmentGem : AugmentReward
{
    protected override void Grant(double amount) => AugmentBridge.AddGem(amount);
}
