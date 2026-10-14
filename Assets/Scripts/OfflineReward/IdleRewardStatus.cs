/// <summary>
/// 오프라인 보상의 현재 상태.
/// </summary>
public enum IdleRewardState
{
    /// <summary>아직 모인 게 없음 (최소 시간 미달이거나 보상 0)</summary>
    NotEnough,

    /// <summary>수령 가능</summary>
    Claimable,

    /// <summary>최대 누적 시간까지 꽉 참 — 수령 가능하면서 더 쌓이지 않음</summary>
    Full,
}

/// <summary>
/// IdleRewardManager.Preview() 결과. 금액·경과 시간·상태를 한 번에 담는다.
///
/// 이전: (int, int, double) 튜플을 돌려줘서, 받는 쪽마다 상태를 따로 판정해야 했다
///       (팝업이 1초, IdleRewardStatus가 5초 여유로 각자 Full을 계산해 어긋난 원인).
/// 지금: 매니저가 판정까지 끝낸 결과를 넘겨주므로 받는 쪽은 읽기만 하면 된다.
/// </summary>
public readonly struct IdleRewardPreview
{
    public readonly double Gold;
    public readonly double Exp;
    public readonly double Seconds;
    public readonly IdleRewardState State;

    public IdleRewardPreview(double gold, double exp, double seconds, IdleRewardState state)
    {
        Gold    = gold;
        Exp     = exp;
        Seconds = seconds;
        State   = state;
    }

    public bool CanClaim => IdleRewardStatus.CanClaim(State);
}

/// <summary>
/// 오프라인 보상 상태 판정 규칙. 입력만으로 결과가 정해지는 순수 함수만 둔다.
///
/// 이전: Evaluate 오버로드와 EvaluateNow()가 IdleRewardManager.Instance를 직접 읽어서,
///       "순수 함수 모음"이 매니저에 의존했고 같은 일을 하는 진입점이 세 개였다.
/// 지금: "지금 상태"는 IdleRewardManager.Preview()가 이 함수를 불러 계산한다.
/// </summary>
public static class IdleRewardStatus
{
    /// <summary>이 시간 미만이면 수령 불가.</summary>
    public const float MinClaimSeconds = 60f;

    /// <summary>상한에 이만큼 근접하면 Full로 본다 (실수 오차 + 화면 표시 여유).</summary>
    public const double FullEpsilonSeconds = 5.0;

    /// <param name="maxSeconds">최대 누적 시간(초). 0 이하면 Full 판정을 건너뛴다.</param>
    public static IdleRewardState Evaluate(double gold, double exp,
                                           double elapsedSeconds, double maxSeconds)
    {
        // 이전: Full 검사를 먼저 해서, 상한만 차면 보상이 0이어도 Full(= 수령 가능)이 됐다.
        // 지금: "받을 게 있는가"를 먼저 보고, 그다음에 꽉 찼는지 본다.
        if (elapsedSeconds < MinClaimSeconds || (gold <= 0 && exp <= 0))
            return IdleRewardState.NotEnough;

        if (maxSeconds > 0 && elapsedSeconds >= maxSeconds - FullEpsilonSeconds)
            return IdleRewardState.Full;

        return IdleRewardState.Claimable;
    }

    /// <summary>수령 가능한 상태인가? (Claimable과 Full 둘 다 수령 가능)</summary>
    public static bool CanClaim(IdleRewardState state)
        => state == IdleRewardState.Claimable || state == IdleRewardState.Full;
}
