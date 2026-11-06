/// <summary>영구 카드 부모(에셋은 자식 AugmentAttack/CritDamage 로) — Apply 는 스택 +1(복구 때도 동일), 자식은 스택 1개의 배율 기여만 구현.</summary>
public abstract class AugmentPermanent : AugmentCard
{
    public override bool IsPermanent => true;

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // 실제 배율 계산은 매니저가 ContributePermanent 를 스택 수만큼 불러서 처리합니다.
        manager.AddPermanentStack(this);
    }

    public abstract override void ContributePermanent(AugmentManager manager);
}
