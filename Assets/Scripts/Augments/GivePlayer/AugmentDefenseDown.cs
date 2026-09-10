using UnityEngine;

/// <summary>
/// 적 방어력 감소 = 우리 대미지가 더 잘 들어감.
///
/// Enemy.TakeDamage() 에서
///   defence × 스킬디버프 × 증강배율
/// 세 겹의 곱으로 계산됩니다. 스킬 디버프가 끝나도 이 효과는 그대로 남습니다.
///
/// [체감이 잘 안 날 때]
/// 이 게임은 뺄셈 방어(대미지 - 방어력)라, 대미지가 방어력보다 훨씬 크면
/// 방어력을 깎아도 티가 잘 안 납니다. 후반 고체력 구간에서 값어치가 커집니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/적 방어력 감소", fileName = "_DefDown")]
public class AugmentDefenseDown : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.EnemyDefense;
}
