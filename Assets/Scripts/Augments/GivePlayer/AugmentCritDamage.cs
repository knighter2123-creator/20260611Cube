using UnityEngine;

/// <summary>
/// 치명타 대미지 증가. (영구 · 합연산)
///
/// [왜 이건 합연산인가]
/// 치명타 대미지는 "기본 150% + 증가분" 형태로 다룹니다.
/// 배수 자체에 %를 곱하면 체감이 이상해지므로(1.5 × 1.2 = 1.8),
/// 배수에 직접 더하는(1.5 + 0.25 = 1.75) 합연산이 직관적입니다.
///
/// ※ 이 게임의 기본 치명타 확률(PlayerStat.Critical)이 3% 라서,
///   확률 강화를 어느 정도 한 뒤에야 값어치가 생깁니다.
///   그래서 등급은 Rare 이상으로 두는 걸 권합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/치명타 대미지", fileName = "_Critdmg")]
public class AugmentCritDamage : AugmentCard
{
    [Header("효과")]
    [Tooltip("0.25 = 치명타 대미지 배수에 +0.25 (예: 1.5배 → 1.75배)")]
    [SerializeField] private float amount = 0.25f;

    public override bool IsPermanent => true;

    protected override string GetValueText() => $"{amount * 100f:0.#}%p";

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        manager.AddPermanentStack(this);
    }

    public override void ContributePermanent(AugmentManager manager)
    {
        manager.AddCritDamage(amount);
    }
}
