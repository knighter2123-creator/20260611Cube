using UnityEngine;

/// <summary>공격력 증가(영구) — 기본 곱연산(1.15³ ≈ 1.52배)으로 후반에 가파르게, 밸런스가 터지면 additive 로 합연산 전환.</summary>
[CreateAssetMenu(menuName = "Game/Augment/공격력", fileName = "_Atk")]
public class AugmentAttack : AugmentPermanent
{
    [Header("효과")]
    [Tooltip("0.15 = 공격력 15% 증가")]
    [SerializeField] private float amount = 0.15f;

    [Tooltip("체크하면 합연산(+15%씩 더하기), 해제하면 곱연산(×1.15씩 곱하기)")]
    [SerializeField] private bool additive = false;

    protected override string GetValueText() => $"{amount * 100f:0.#}%";

    public override void ContributePermanent(AugmentManager manager)
    {
        if (additive) manager.AddAttackAdditive(amount);
        else          manager.MultiplyAttack(1f + amount);
    }
}
