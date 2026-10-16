using System;
using UnityEngine;

/// <summary>
/// 방치형 오프라인 보상.
/// "마지막 정산 시각(lastIdleClaimTime)"과 현재 시각의 차이만큼
/// (현재 스테이지 분당 수급률 × idleRatio)로 골드/경험치를 지급한다.
///
/// lastIdleClaimTime은 두 시점에만 갱신된다:
///    1) 게임 최초 시작 시 1회 (0 → 지금) : EnsureInitialized()
///    2) 보상을 실제로 수령할 때          : Claim() 내부
///   그 외 상점 왕복·씬 전환·앱 재시작에서는 건드리지 않는다(경과 누적).
/// </summary>
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

    // 이전: STAGE_STAT_MULTIPLIER(1.5) / MAX_STAGE_PER_WORLD 상수를 StageManager에서 복사해 뒀다.
    //       실제 게임 값은 1.3이라 어긋나 있었다. 이제 StageFormula를 쓴다.

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
            GameLog.Info("[Idle] 최초 정산 시각 세팅");
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

        return Math.Min(seconds, MaxAccrualSeconds);
    }

    // ──────────────────────────────────────────────
    //  분당 기준 수급량 (현재 스테이지 배율 반영)
    //  이전: out 매개변수 두 개 → Preview()와 같은 튜플 반환으로 통일
    // ──────────────────────────────────────────────
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

    // 현재 스테이지 배율 — 세이브의 월드/스테이지로 계산한다.
    // 이전: StageManager가 있으면 그 값, 없으면 복사한 상수로 재계산.
    //   ① 복사한 상수(1.5)가 실제 값(1.3)과 달라 StageManager가 없을 때 보상이 과다 계산됐고,
    //   ② StageManager.Start()(ApplyFrom)보다 먼저 불리면 아직 초기값 1.0이라 과소 계산될 수 있었다.
    //   세이브 기준 하나로 통일하면 씬 구성·실행 순서와 상관없이 같은 값이 나온다.
    private float GetCurrentStatMult()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 1f;

        return StageFormula.StatMult(data.currentWorld, data.currentStage);
    }

    // ──────────────────────────────────────────────
    //  미리보기 (지급 안 함). 상태 판정까지 여기서 끝낸다.
    // ──────────────────────────────────────────────
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

    /// <summary>
    /// double 금액을 int 재화로 바꾼다. int 범위를 넘으면 int.MaxValue로 자른다.
    /// 팝업 표시도 같은 함수를 써서 "보이는 금액 = 받는 금액"이 되게 한다.
    /// </summary>
    public static int ToIntClamped(double value)
    {
        double rounded = Math.Round(value);
        double capped  = Math.Min(rounded, int.MaxValue);
        return (int)capped;
    }

    // ──────────────────────────────────────────────
    //  보상 정산(지급) + 시각 리셋
    //  bonusMultiplier=2f면 2배 수령.
    // ──────────────────────────────────────────────
    public (int gold, int exp) Claim(float bonusMultiplier = 1f)
    {
        var preview = Preview();

        // 이전: 수령 가능 여부를 팝업(UI)에서만 검사해서, 다른 코드가 Claim()을 직접 부르면
        //       60초 미만이어도 지급하고 정산 시각을 리셋했다. 규칙은 지급하는 쪽이 지킨다.
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
