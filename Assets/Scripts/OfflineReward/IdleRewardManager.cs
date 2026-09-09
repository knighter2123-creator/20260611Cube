using System;
using UnityEngine;

/// <summary>
/// 방치형 오프라인 보상.
/// "마지막 정산 시각(lastIdleClaimTime)"과 현재 시각의 차이만큼
/// (현재 스테이지 분당 수급률 × idleRatio)로 골드/경험치를 지급한다.
///
/// ★ lastIdleClaimTime은 딱 두 시점에만 갱신된다:
///    1) 게임 최초 시작 시 1회 (0 → 지금) : EnsureInitialized()
///    2) 보상을 실제로 수령할 때          : Claim() 내부
///   그 외 상점 왕복·씬 전환·앱 재시작에서는 절대 건드리지 않는다(경과 누적).
///
/// ★ 이번 수정: MaxAccrualSeconds 프로퍼티 하나만 추가했습니다. 나머지는 원본 그대로입니다.
/// </summary>
public class IdleRewardManager : MonoBehaviour
{
    public static IdleRewardManager Instance;

    [Header("방치 보상 설정")]
    [SerializeField] private float idleRatio       = 0.8f;   // 실시간 대비 80%
    [SerializeField] private int   maxAccrualHours = 24;     // 최대 누적 24시간

    [Header("전투 기준값 (Enemy와 동일하게 맞출 것)")]
    [SerializeField] private int   rewardGoldPerKill = 10;   // Enemy.rewardGold와 동일
    [SerializeField] private int   rewardExpPerKill  = 5;    // Enemy.rewardExp와 동일
    [SerializeField] private float killsPerMinute    = 25f;  // 분당 처치 수 가정(튜닝)

    // StageManager와 동일해야 하는 상수 (폴백 계산용)
    private const float STAGE_STAT_MULTIPLIER = 1.5f;
    private const int   MAX_STAGE_PER_WORLD   = 10;

    // ──────────────────────────────────────────────
    //  ★ 신규 — 누적 상한을 외부에 공개
    // ──────────────────────────────────────────────
    /// <summary>
    /// 최대 누적 시간(초). UI가 "가득 찼는가"를 판정할 때 사용합니다.
    ///
    /// ─── 왜 값을 복사해 주는 대신 프로퍼티로 여는가? (학습 포인트) ──────────
    /// UI 쪽에 "최대 24시간"을 따로 적어두는 방법도 있습니다. 하지만 그러면
    /// 나중에 인스펙터에서 maxAccrualHours를 12로 바꿨을 때 UI는 여전히 24를
    /// 기준으로 판단합니다. 12시간에 이미 꽉 찼는데 "최대"가 안 뜨는 거죠.
    /// 게다가 이런 버그는 에러도 안 나고 로그도 안 남아서 찾기가 아주 어렵습니다.
    ///
    /// 값을 가진 쪽이 창구를 열어두고, 필요한 쪽이 그때그때 물어보게 하면
    /// 어긋날 방법 자체가 없어집니다. 필드는 private으로 잠가둔 채
    /// 읽기 전용 프로퍼티(get만 있는 => 형태)만 여는 게 정석이에요.
    ///
    /// GetElapsedSeconds()도 같은 값을 쓰므로, 계산식을 여기 한 줄로 모아
    /// 두 곳이 같은 정의를 공유하게 했습니다.
    /// ──────────────────────────────────────────────────────────────────
    /// </summary>
    public double MaxAccrualSeconds => maxAccrualHours * 3600.0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ──────────────────────────────────────────────
    //  최초 1회 시각 세팅 (0일 때만)
    //  상점 왕복마다 불려도 안전 — 이미 값이 있으면 아무것도 안 함.
    // ──────────────────────────────────────────────
    public void EnsureInitialized()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return;

        if (data.lastIdleClaimTime == 0)
        {
            data.lastIdleClaimTime = DateTime.UtcNow.ToBinary();
            SaveManager.Instance?.Save();
            Debug.Log("[Idle] 최초 정산 시각 세팅");
        }
    }

    // ──────────────────────────────────────────────
    //  경과 시간(초). 최대 누적 시간으로 클램프.
    // ──────────────────────────────────────────────
    public double GetElapsedSeconds()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 0;

        // 아직 최초 세팅 전이면 경과 0 (EnsureInitialized가 곧 세팅함)
        if (data.lastIdleClaimTime == 0) return 0;

        DateTime last    = DateTime.FromBinary(data.lastIdleClaimTime);
        double   seconds = (DateTime.UtcNow - last).TotalSeconds;
        if (seconds < 0) seconds = 0;   // 시계 조작/역행 방어

        return Math.Min(seconds, MaxAccrualSeconds);   // ★ 프로퍼티로 통일
    }

    // ──────────────────────────────────────────────
    //  분당 기준 수급량 (현재 스테이지 배율 반영)
    // ──────────────────────────────────────────────
    private void GetBaseRatePerMinute(out int goldPerMin, out int expPerMin)
    {
        float mult = GetCurrentStatMult();
        goldPerMin = Mathf.RoundToInt(rewardGoldPerKill * mult * killsPerMinute);
        expPerMin  = Mathf.RoundToInt(rewardExpPerKill  * mult * killsPerMinute);
    }

    // 현재 스테이지 배율 — StageManager가 살아있으면 그 값, 아니면 세이브로 재계산
    private float GetCurrentStatMult()
    {
        if (StageManager.Instance != null)
            return StageManager.Instance.CurrentStatMult;

        var data = SaveManager.Instance?.Current;
        if (data == null) return 1f;

        int progress = (data.currentWorld - 1) * MAX_STAGE_PER_WORLD + (data.currentStage - 1);
        return Mathf.Pow(STAGE_STAT_MULTIPLIER, progress);
    }

    // ──────────────────────────────────────────────
    //  미리보기 (지급 안 함, UI 표시용)
    // ──────────────────────────────────────────────
    public (int gold, int exp, double seconds) Preview()
    {
        double seconds = GetElapsedSeconds();
        GetBaseRatePerMinute(out int goldPerMin, out int expPerMin);

        double minutes = seconds / 60.0;
        int gold = Mathf.RoundToInt((float)(minutes * goldPerMin * idleRatio));
        int exp  = Mathf.RoundToInt((float)(minutes * expPerMin  * idleRatio));
        return (gold, exp, seconds);
    }

    // ──────────────────────────────────────────────
    //  보상 정산(지급) + 시각 리셋
    //  bonusMultiplier=2f면 2배 수령.
    // ──────────────────────────────────────────────
    public (int gold, int exp) Claim(float bonusMultiplier = 1f)
    {
        var (gold, exp, _) = Preview();
        gold = Mathf.RoundToInt(gold * bonusMultiplier);
        exp  = Mathf.RoundToInt(exp  * bonusMultiplier);

        if (gold > 0) CurrencyManager.Instance?.AddGold(gold);
        if (exp  > 0) LevelUpManager.Instance?.AddExp(exp);

        ResetClaimTime();               // ★ 수령 시에만 시각 리셋
        SaveManager.Instance?.Save();   // 지급 + 시각 리셋을 함께 저장

        Debug.Log($"[Idle] 보상 지급 (×{bonusMultiplier}) — 골드 +{gold}, exp +{exp}");
        return (gold, exp);
    }

    // 정산 시각을 현재로 리셋 (Claim 내부에서만 호출)
    public void ResetClaimTime()
    {
        if (SaveManager.Instance?.Current == null) return;
        SaveManager.Instance.Current.lastIdleClaimTime = DateTime.UtcNow.ToBinary();
    }
}