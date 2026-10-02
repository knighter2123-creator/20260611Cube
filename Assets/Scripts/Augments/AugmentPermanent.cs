/// <summary>
/// 한 번 고르면 계속 남는 영구 카드들의 공통 부모. (공격력 / 치명타 대미지)
///
/// abstract 이므로 이 클래스로는 에셋을 만들 수 없습니다.
/// 자식인 AugmentAttack / AugmentCritDamage 로 만드세요.
///
/// 즉시형(AugmentReward) · 임시형(AugmentTempBuff) 과 같은 자리의 부모입니다.
///   Apply               → 스택을 1 올린다 (복구 중이어도 동일 — 저장된 스택을 다시 쌓는 과정)
///   ContributePermanent → 자식이 "스택 1개가 배율에 얼마를 더하는지" 만 구현
/// </summary>
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
