using UnityEngine;

/// <summary>치명타 대미지 증가(영구·합연산 1.5 + 0.25 — 배수에 %를 곱하면 체감이 이상해서) — 기본 치명 확률 3% 라 Rare 이상 등급 권장.</summary>
[CreateAssetMenu(menuName = "Game/Augment/치명타 대미지", fileName = "_Critdmg")]
public class AugmentCritDamage : AugmentPermanent
{
    [Header("효과")]
    [Tooltip("0.25 = 치명타 대미지 배수에 +0.25 (예: 1.5배 → 1.75배)")]
    [SerializeField] private float amount = 0.25f;

    protected override string GetValueText() => $"{amount * 100f:0.#}%p";

    public override void ContributePermanent(AugmentManager manager)
    {
        manager.AddCritDamage(amount);
    }
}
