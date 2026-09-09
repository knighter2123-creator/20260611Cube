using UnityEngine;

// ─────────────────────────────────────────────────────────────
//  영구 스탯 증강 카드
//  한 번 고르면 계속 남고, 저장/로드 대상입니다.
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 공격력 증가. (곱연산)
///
/// [곱연산 vs 합연산]
/// 이 카드는 "+15%" 를 배율 1.15 로 곱합니다. 3번 쌓이면 1.15³ ≈ 1.52배.
/// 합연산(1 + 0.15×3 = 1.45)보다 후반에 더 가파르게 오릅니다.
/// 방치형은 숫자가 커지는 쾌감이 중요하니 곱연산이 잘 어울립니다.
/// 밸런스가 터진다 싶으면 아래 addative 를 켜서 합연산으로 바꿀 수 있게 해뒀습니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/공격력", fileName = "Augment_Attack")]
public class AugmentAttack : AugmentCard
{
    [Header("효과")]
    [Tooltip("0.15 = 공격력 15% 증가")]
    [SerializeField] private float amount = 0.15f;

    [Tooltip("체크하면 합연산(+15%씩 더하기), 해제하면 곱연산(×1.15씩 곱하기)")]
    [SerializeField] private bool additive = false;

    public override bool IsPermanent => true;

    protected override string GetValueText() => $"{amount * 100f:0.#}%";

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // 영구 카드는 "스택을 1 올린다" 가 전부입니다.
        // 실제 배율 계산은 매니저가 ContributePermanent 를 스택 수만큼 불러서 처리합니다.
        manager.AddPermanentStack(this);
    }

    public override void ContributePermanent(AugmentManager manager)
    {
        if (additive) manager.AddAttackAdditive(amount);
        else          manager.MultiplyAttack(1f + amount);
    }
}


/// <summary>
/// 치명타 대미지 증가. (합연산)
///
/// [왜 이건 합연산인가]
/// 치명타 대미지는 보통 "기본 150% + 증가분" 형태로 다룹니다.
/// 배수 자체에 %를 곱하면 체감이 이상해지므로(1.5 × 1.2 = 1.8) ,
/// 배수에 직접 더하는(1.5 + 0.2 = 1.7) 합연산이 직관적입니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/치명타 대미지", fileName = "Augment_CritDamage")]
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
