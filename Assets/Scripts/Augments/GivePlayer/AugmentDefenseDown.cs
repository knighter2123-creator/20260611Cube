using UnityEngine;

/// <summary>적 방어력 감소(임시) — Enemy.TakeDamage 에서 defence × 스킬 디버프 × 증강 배율로 곱해짐 (뺄셈 방어라 대미지가 훨씬 크면 체감이 작음).</summary>
[CreateAssetMenu(menuName = "Game/Augment/적 방어력 감소", fileName = "_DefDown")]
public class AugmentDefenseDown : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.EnemyDefense;
}
