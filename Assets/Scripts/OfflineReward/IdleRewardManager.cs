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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

        double maxSeconds = maxAccrualHours * 3600.0;
        return Math.Min(seconds, maxSeconds);
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