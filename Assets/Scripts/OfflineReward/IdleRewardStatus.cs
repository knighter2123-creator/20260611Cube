/// <summary>오프라인 보상의 현재 상태.</summary>
public enum IdleRewardState
{
    /// <summary>아직 모인 게 없음 (최소 시간 미달이거나 보상 0)</summary>
    NotEnough,

    /// <summary>수령 가능</summary>
    Claimable,

    /// <summary>최대 누적 시간까지 꽉 참 — 수령 가능하면서 더 쌓이지 않음</summary>
    Full,
}

/// <summary>Preview() 결과(금액·경과·상태) — 판정까지 매니저가 끝내 받는 쪽마다 따로 판정해 어긋나지 않게.</summary>
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

/// <summary>상태 판정 규칙(순수 함수만, 매니저 의존 없음) — '지금 상태'는 Preview() 가 이걸 불러 계산.</summary>
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
        // '받을 게 있는가'를 먼저, 그다음 Full (반대면 보상 0 인데 Full 이 됨)
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
