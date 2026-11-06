using System;
using UnityEngine;

/// <summary>오프라인 보상 — (지금 − 마지막 정산 시각) × 스테이지 분당 수급률 × idleRatio 로 골드/경험치 지급, 정산 시각은 최초 1회와 수령 때만 갱신(씬 전환·재시작엔 누적).</summary>
public class IdleRewardManager : MonoBehaviour
{
    public static IdleRewardManager Instance { get; private set; }

    [Header("방치 보상 설정")]
    [SerializeField] private float idleRatio       = 0.8f;   // 실시간 대비 80%
    [SerializeField] private int   maxAccrualHours = 24;     // 최대 누적 24시간

    [Header("전투 기준값 (Enemy 프리팹과 맞출 것)")]
    [SerializeField] private int   rewardGoldPerKill = 10;   // Enemy.rewardGold 기본값
    [SerializeField] private int   rewardExpPerKill  = 5;    // Enemy.rewardExp 기본값
    [SerializeField] private float killsPerMinute    = 25f;  // 분당 처치 수 가정(튜닝)

    // 배율은 StageFormula 사용 (상수를 복사해 두면 실제 값과 어긋남)

    /// <summary>최대 누적 시간(초). 상태 판정에 쓴다.</summary>
    public double MaxAccrualSeconds => maxAccrualHours * 3600.0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 최초 1회 시각 세팅 (0일 때만, 여러 번 불려도 안전) ──
    public void EnsureInitialized()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return;

        if (data.lastIdleClaimTime == 0)
        {
            data.lastIdleClaimTime = DateTime.UtcNow.ToBinary();
            SaveManager.Instance?.Save();
            GameLog.Info("[Idle] 최초 정산 시각 세팅");
        }
    }

    // ── 경과 시간(초). 최대 누적 시간으로 클램프. ──
    public double GetElapsedSeconds()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 0;

        // 아직 최초 세팅 전이면 경과 0 (EnsureInitialized가 곧 세팅함)
        if (data.lastIdleClaimTime == 0) return 0;

        DateTime last    = DateTime.FromBinary(data.lastIdleClaimTime);
        double   seconds = (DateTime.UtcNow - last).TotalSeconds;
        if (seconds < 0) seconds = 0;   // 시계 조작/역행 방어

        return Math.Min(seconds, MaxAccrualSeconds);
    }

    // ── 분당 기준 수급량 (현재 스테이지 배율 반영, 튜플 반환) ──
    private (double gold, double exp) GetBaseRatePerMinute()
    {
        float  mult    = GetCurrentStatMult();
        double expMult = GetCurrentExpMult();   // 경험치는 체력 배율이 아니라 경험치 전용 배율 (Enemy 와 동일)
        return (rewardGoldPerKill * mult    * killsPerMinute,
                rewardExpPerKill  * expMult * killsPerMinute);
    }

    // 현재 스테이지의 경험치 배율 — 스탯 배율과 같은 이유로 세이브 기준.
    private double GetCurrentExpMult()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 1.0;

        return StageFormula.ExpMult(data.currentWorld, data.currentStage);
    }

    // 스테이지 배율은 세이브의 월드/스테이지로 계산 — 씬 구성·실행 순서와 무관하게 같은 값
    private float GetCurrentStatMult()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 1f;

        return StageFormula.StatMult(data.currentWorld, data.currentStage);
    }

    // ── 미리보기 (지급 안 함). 상태 판정까지 여기서 끝낸다. ──
    public IdleRewardPreview Preview()
    {
        double seconds = GetElapsedSeconds();
        var (goldPerMin, expPerMin) = GetBaseRatePerMinute();

        double minutes = seconds / 60.0;
        double gold = minutes * goldPerMin * idleRatio;
        double exp  = minutes * expPerMin  * idleRatio;

        var state = IdleRewardStatus.Evaluate(gold, exp, seconds, MaxAccrualSeconds);
        return new IdleRewardPreview(gold, exp, seconds, state);
    }

    /// <summary>double → int 재화(넘으면 int.MaxValue) — 팝업도 같은 함수를 써서 보이는 금액 = 받는 금액.</summary>
    public static int ToIntClamped(double value)
    {
        double rounded = Math.Round(value);
        double capped  = Math.Min(rounded, int.MaxValue);
        return (int)capped;
    }

    // ── 보상 정산 + 시각 리셋 (bonusMultiplier=2 면 2배) ──
    public (int gold, int exp) Claim(float bonusMultiplier = 1f)
    {
        var preview = Preview();

        // 수령 가능 검사는 지급하는 쪽에서 (UI 만 검사하면 직접 호출 시 규칙 우회)
        if (!preview.CanClaim) return (0, 0);

        int gold = ToIntClamped(preview.Gold * bonusMultiplier);
        int exp  = ToIntClamped(preview.Exp  * bonusMultiplier);

        if (gold > 0) CurrencyManager.Instance?.AddGold(gold);
        if (exp  > 0) LevelUpManager.Instance?.AddExp(exp);

        ResetClaimTime();               // 수령 시에만 시각 리셋
        SaveManager.Instance?.Save();   // 지급 + 시각 리셋을 함께 저장

        GameLog.Info($"[Idle] 보상 지급 (×{bonusMultiplier}) — 골드 +{gold}, exp +{exp}");
        return (gold, exp);
    }

    // 정산 시각을 현재로 리셋 (Claim 내부에서만 호출)
    private void ResetClaimTime()
    {
        if (SaveManager.Instance?.Current == null) return;
        SaveManager.Instance.Current.lastIdleClaimTime = DateTime.UtcNow.ToBinary();
    }
}
