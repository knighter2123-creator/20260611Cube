using System;
using UnityEngine;

/// <summary>
/// 방치형 오프라인 보상. "마지막 정산 시각"과 현재 시각의 차이만큼
/// (현재 스테이지 분당 수급률 × 80%)로 골드/경험치를 지급한다.
/// 상점 왕복·앱 재시작·수동 버튼 모두 같은 Claim 경로를 쓴다.
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
    [SerializeField] private float killsPerMinute    = 25f;  // 분당 처치 수 가정 (튜닝)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 경과 시간(초) 계산. 최대 누적 시간으로 클램프 ──
    public double GetElapsedSeconds()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 0;

        // 첫 실행(0)이면 지금을 기준으로 → 경과 0
        if (data.lastIdleClaimTime == 0) return 0;

        DateTime last = DateTime.FromBinary(data.lastIdleClaimTime);
        double seconds = (DateTime.UtcNow - last).TotalSeconds;
        if (seconds < 0) seconds = 0;                          // 시계 조작/역행 방어

        double maxSeconds = maxAccrualHours * 3600.0;
        return Math.Min(seconds, maxSeconds);                  // 24시간 클램프
    }

   
// ── 분당 기준 수급량 (현재 스테이지 배율 반영) ──
    private void GetBaseRatePerMinute(out int goldPerMin, out int expPerMin)
    {
        float mult = GetCurrentStatMult();   // 현재 스테이지의 currentStatMult

        goldPerMin = Mathf.RoundToInt(rewardGoldPerKill * mult * killsPerMinute);
        expPerMin  = Mathf.RoundToInt(rewardExpPerKill  * mult * killsPerMinute);
    }
    // 현재 스테이지를 하나의 진행도 숫자로 (월드·스테이지 → 선형 인덱스)
    private int GetCurrentStageIndex()
    {
        var data = SaveManager.Instance?.Current;
        if (data == null) return 1;
        // 예: 월드당 10스테이지 가정 → (world-1)*10 + stage
        return Mathf.Max(1, (data.currentWorld - 1) * 10 + data.currentStage);
    }
    // 현재 스테이지 배율 — StageManager가 살아있으면 그 값, 아니면 세이브 스테이지로 재계산
    private float GetCurrentStatMult()
    {
        if (StageManager.Instance != null)
            return StageManager.Instance.CurrentStatMult;

        // StageManager가 아직 없을 때(앱 켜자마자 등) — 세이브 진행도로 직접 계산
        var data = SaveManager.Instance?.Current;
        if (data == null) return 1f;

        const float statMultiplier   = 1.5f;   // StageManager.statMultiplier와 동일
        const int   maxStagePerWorld = 10;
        int progress = (data.currentWorld - 1) * maxStagePerWorld + (data.currentStage - 1);
        return Mathf.Pow(statMultiplier, progress);
    }

    // ── 현재 쌓인 보상 미리보기 (UI 표시용, 지급 안 함) ──
    public (int gold, int exp, double seconds) Preview()
    {
        double seconds = GetElapsedSeconds();
        GetBaseRatePerMinute(out int goldPerMin, out int expPerMin);

        double minutes = seconds / 60.0;
        int gold = Mathf.RoundToInt((float)(minutes * goldPerMin * idleRatio));
        int exp  = Mathf.RoundToInt((float)(minutes * expPerMin  * idleRatio));
        return (gold, exp, seconds);
    }

    // ── 보상 정산(지급) + 시각 리셋 ──
    public (int gold, int exp) Claim(float bonusMultiplier = 1f)
    {
        var (gold, exp, _) = Preview();
        gold = Mathf.RoundToInt(gold * bonusMultiplier);
        exp  = Mathf.RoundToInt(exp  * bonusMultiplier);

        if (gold > 0) CurrencyManager.Instance?.AddGold(gold);
        if (exp  > 0) LevelUpManager.Instance?.AddExp(exp);

        ResetClaimTime();
        SaveManager.Instance?.Save();

        Debug.Log($"[Idle] 보상 지급 (×{bonusMultiplier}) — 골드 +{gold}, exp +{exp}");
        return (gold, exp);
    }

    // ── 정산 시각을 현재로 리셋 ──
    public void ResetClaimTime()
    {
        if (SaveManager.Instance?.Current == null) return;
        SaveManager.Instance.Current.lastIdleClaimTime = DateTime.UtcNow.ToBinary();
    }
}