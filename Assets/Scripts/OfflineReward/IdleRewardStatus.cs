using UnityEngine;

/// <summary>
/// 오프라인 보상의 현재 상태.
/// </summary>
public enum IdleRewardState
{
    /// <summary>아직 모인 게 없음 (최소 시간 미달) → 안내 텍스트를 숨긴다</summary>
    NotEnough,

    /// <summary>수령 가능</summary>
    Claimable,

    /// <summary>최대 누적 시간까지 꽉 참 — 더 쌓이지 않으므로 빨리 받으라고 알려야 한다</summary>
    Full,
}

/// <summary>
/// 오프라인 보상 상태를 판정하는 공용 로직.
///
/// ─── 왜 static 클래스로 따로 뺐는가? (학습 포인트) ─────────────────────
/// 이 판정은 두 곳에서 필요합니다.
///   ① 메인 화면의 상시 안내 텍스트 (IdleRewardStatusText)
///   ② 팝업의 수령 버튼 활성/비활성 (IdleRewardPopup)
///
/// 같은 판정을 두 군데에 각각 써 두면, 나중에 "최소 시간을 60초에서 5분으로"
/// 같은 변경이 왔을 때 한쪽만 고치는 사고가 반드시 납니다.
/// 그러면 메인 화면엔 "수령 가능"이 떠 있는데 팝업 버튼은 비활성인,
/// 재현하기도 설명하기도 어려운 버그가 되죠.
///
/// 판단 기준이 여러 곳에서 쓰이면 한 곳에 모아둔다 —
/// 이걸 "단일 진실 공급원(Single Source of Truth)"이라고 부릅니다.
///
/// MonoBehaviour가 아니므로 씬에 붙이지 않습니다. 그냥 함수 모음이에요.
/// (static 클래스는 인스턴스를 만들 수 없고, 클래스 이름으로 바로 호출합니다)
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public static class IdleRewardStatus
{
    /// <summary>이 시간 미만이면 수령 불가 (IdleRewardPopup의 기존 60초 기준과 동일)</summary>
    public const float MinClaimSeconds = 60f;

    /// <summary>
    /// "꽉 참"으로 볼 여유(초). 누적 시간이 상한에 이만큼 근접하면 Full로 봅니다.
    ///
    /// 왜 딱 == 로 비교하지 않는가:
    /// 실수(double/float) 계산에는 아주 작은 오차가 생깁니다. 86400초가 되어야 하는데
    /// 86399.9997이 나오는 식이죠. == 로 비교하면 영원히 참이 안 될 수 있습니다.
    /// 실수를 비교할 때는 항상 "이 정도면 같다"는 허용 오차를 둡니다.
    /// 여기서는 화면 표시용이라 넉넉하게 5초로 잡았습니다.
    /// </summary>
    public const double FullEpsilonSeconds = 5.0;

    /// <summary>
    /// 값만으로 상태를 판정하는 순수 함수.
    ///
    /// 순수 함수 = 외부 상태를 읽지도 바꾸지도 않고, 입력이 같으면 항상 같은 결과.
    /// IdleRewardManager 없이도 이 함수만 따로 검증할 수 있다는 게 장점입니다.
    ///
    /// 매개변수를 double로 받는 이유: Preview()가 (int, int, double)을 돌려주는데
    /// int는 double로 자동 변환됩니다. 나중에 매니저 쪽 자료형이 long이나 float으로
    /// 바뀌어도 이 파일은 고칠 일이 없습니다.
    /// </summary>
    /// <param name="maxSeconds">최대 누적 시간(초). 0 이하면 Full 판정을 건너뜁니다.</param>
    public static IdleRewardState Evaluate(double gold, double exp,
                                           double elapsedSeconds, double maxSeconds)
    {
        // 상한에 도달 → Full (수령 가능하면서 "더 못 쌓임"을 함께 알리는 상태)
        if (maxSeconds > 0 && elapsedSeconds >= maxSeconds - FullEpsilonSeconds)
            return IdleRewardState.Full;

        // 최소 시간을 넘겼고 실제로 줄 보상이 있으면 → Claimable
        if (elapsedSeconds >= MinClaimSeconds && (gold > 0 || exp > 0))
            return IdleRewardState.Claimable;

        return IdleRewardState.NotEnough;
    }

    /// <summary>
    /// Preview() 결과로 상태를 판정합니다. 상한은 매니저에게 직접 물어봅니다.
    ///
    /// ★ 상한값(maxAccrualHours)을 여기 복사해 두지 않는 이유:
    ///   인스펙터에서 24시간을 12시간으로 바꿔도 UI가 자동으로 따라옵니다.
    ///   값을 가진 쪽(IdleRewardManager)이 유일한 기준점이 되도록 두는 겁니다.
    /// </summary>
    public static IdleRewardState Evaluate(double gold, double exp, double elapsedSeconds)
    {
        double maxSeconds = IdleRewardManager.Instance != null
            ? IdleRewardManager.Instance.MaxAccrualSeconds
            : 0d;   // 매니저가 아직 없으면 Full 판정을 건너뜀

        return Evaluate(gold, exp, elapsedSeconds, maxSeconds);
    }

    /// <summary>
    /// 지금 이 순간의 상태를 IdleRewardManager에게 물어 판정합니다.
    /// 매니저가 아직 없으면(씬 로드 순서) 안전하게 NotEnough를 돌려줍니다.
    /// </summary>
    public static IdleRewardState EvaluateNow()
    {
        if (IdleRewardManager.Instance == null) return IdleRewardState.NotEnough;

        var (gold, exp, sec) = IdleRewardManager.Instance.Preview();
        return Evaluate(gold, exp, sec, IdleRewardManager.Instance.MaxAccrualSeconds);
    }

    /// <summary>수령 버튼을 누를 수 있는 상태인가? (Claimable 과 Full 둘 다 수령 가능)</summary>
    public static bool CanClaim(IdleRewardState state)
        => state == IdleRewardState.Claimable || state == IdleRewardState.Full;
}
