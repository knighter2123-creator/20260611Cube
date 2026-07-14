using System;
using Manager.currency;
using UnityEngine;

/// <summary>
/// 가이드 퀘스트 진행 관리자. UI 참조를 절대 들지 않는다.
/// ManagerRoot 하위(LoginScene)에 배치 — DontDestroyOnLoad는 ManagerRoot가 담당.
/// </summary>
public partial class GuideQuestManager : MonoBehaviour
{
    public static GuideQuestManager Instance { get; private set; }

    [SerializeField] private GuideQuestTable table;

    private int currentStep = 0;   // 0-based
    private long progress = 0;     // 현재 퀘스트 진행도
    private int highestChapter = 0;
    private int highestStage = 0;  // 클리어한 최고 스테이지 (스테이지 퀘스트 판정용)

    private bool isLoading = false;   // ApplyFrom 중 저장 억제 (다른 매니저 데이터 보호)
    private bool isClaiming = false;  // autoClaim 재귀 방지

    // ── 이벤트 ────────────────────────────────────
    /// <summary>퀘스트가 교체될 때 (초기화 / 다음 단계 진입)</summary>
    public event Action<GuideQuest> OnQuestChanged;
    /// <summary>진행도가 변할 때 (현재, 목표)</summary>
    public event Action<long, long> OnProgressChanged;
    // ── 이벤트 (기존 OnRewardClaimed 교체) ────────
    /// <summary>보상 수령 완료 시 (재화 종류, 수량)</summary>
    public event Action<CurrencyType, int> OnRewardClaimed;
    /// <summary>스테이지 씬의 강화 UI가 즉시 열려야 할 때</summary>
    public event Action<LevelUpManager.StatType> OnFocusStatUpgrade;

    public GuideQuest Current { get; private set; }
    public long Progress => progress;
    public bool IsComplete => Current != null && progress >= Current.requiredCount;

    // ══════════════════════════════════════════════
    //  생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad 호출 금지 — ManagerRoot가 처리
    }

    private void Start()
    {
        // 세이브에서 복원 (없으면 1단계부터)
        if (SaveManager.Instance != null)
            ApplyFrom(SaveManager.Instance.Current);
        else
            RebuildCurrent(notify: true);

        // 레벨업 보고 구독
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnLevelUp += ReportLevelUp;
    }

    private void OnDestroy()
    {
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnLevelUp -= ReportLevelUp;
    }

    private void RebuildCurrent(bool notify)
    {
        if (table == null)
        {
            Debug.LogError("[GuideQuest] GuideQuestTable이 할당되지 않았습니다.");
            return;
        }

        Current = table.Build(currentStep);
        if (notify)
        {
            OnQuestChanged?.Invoke(Current);
            OnProgressChanged?.Invoke(progress, Current.requiredCount);
        }
    }

    // ══════════════════════════════════════════════
    //  진행 보고 API (외부 호출부)
    // ══════════════════════════════════════════════

    /// <summary>적 사망 시 호출 (Enemy.Die 등)</summary>
    public void ReportEnemyKill(int count = 1)
    {
        if (Current == null || Current.type != GuideQuestType.EnemyKill) return;
        AddProgress(count);
    }

    /// <summary>스테이지 클리어 시 호출 (StageManager)</summary>
    public void ReportStageClear(int chapter, int stage)
    {
        // 최고 기록 갱신 (이전 스테이지를 다시 깨도 진행이 후퇴하지 않도록)
        if (chapter > highestChapter || (chapter == highestChapter && stage > highestStage))
        {
            highestChapter = chapter;
            highestStage = stage;
        }

        if (Current == null || Current.type != GuideQuestType.StageClear) return;

        bool reached = chapter > Current.targetChapter ||
                       (chapter == Current.targetChapter && stage >= Current.targetStage);
        if (reached) SetProgress(Current.requiredCount);
    }

    /// <summary>강화가 실제로 성공했을 때 호출 (UpgradeUI에서 successCount 전달)</summary>
    public void ReportStatUpgrade(LevelUpManager.StatType type, int count = 1)
    {
        if (Current == null || Current.type != GuideQuestType.StatUpgrade) return;
        if (Current.statType != type) return;   // 지정된 스탯만 인정
        AddProgress(count);
    }

    /// <summary>동료 소환 확정 후 호출. 10연차면 count = 10</summary>
    public void ReportSummon(int count = 1)
    {
        if (Current == null || Current.type != GuideQuestType.SummonCompanion) return;
        AddProgress(count);
    }

    /// <summary>LevelUpManager.OnLevelUp 구독. 대입 방식이라 씬 전환 재발화에도 안전.</summary>
    public void ReportLevelUp(int newLevel)
    {
        if (Current == null || Current.type != GuideQuestType.LevelUp) return;
        SetProgress(newLevel);
    }

    // ══════════════════════════════════════════════
    //  진행도 / 보상
    // ══════════════════════════════════════════════

    private void AddProgress(long amount) => SetProgress(progress + amount);

    private void SetProgress(long value)
    {
        if (Current == null) return;

        long clamped = Math.Min(value, Current.requiredCount);
        if (clamped <= progress) return;   // 후퇴 금지

        progress = clamped;
        OnProgressChanged?.Invoke(progress, Current.requiredCount);

        if (IsComplete)
        {
            Debug.Log($"[GuideQuest] {Current.Title} 조건 충족: {Current.Description}");

            if (table != null && table.autoClaim && !isClaiming)
                ClaimAllComplete();
            else
                RequestSave();
        }
    }

    /// <summary>조건 충족 시 보상 지급 후 다음 단계로 진행 (제한 없음)</summary>
    public bool Claim()
    {
        if (!IsComplete) return false;

        CurrencyType type = Current.rewardType;
        int amount = Current.rewardAmount;

        // ★ AddCurrency가 Gold/Gem 분기를 처리
        CurrencyManager.Instance?.AddCurrency(type, amount);
        OnRewardClaimed?.Invoke(type, amount);

        Debug.Log($"[GuideQuest] {Current.Title} 보상 {Current.RewardTypeName} {amount} 지급");

        // 다음 단계로
        currentStep++;
        progress = 0;
        RebuildCurrent(notify: true);
        ReSyncCurrent();

        RequestSave();
        return true;
    }

    /// <summary>autoClaim 모드: 연쇄 충족을 재귀 없이 반복 처리</summary>
    private void ClaimAllComplete()
    {
        if (isClaiming) return;

        isClaiming = true;
        try
        {
            int safety = 0;
            while (IsComplete && safety++ < 200)
                Claim();
        }
        finally
        {
            isClaiming = false;
        }
        RequestSave();
    }

    /// <summary>
    /// 이미 달성한 상태(목표 스테이지를 이미 클리어함 / 레벨이 이미 높음)를 즉시 반영.
    /// 로드 직후, 단계 진행 직후 호출.
    /// </summary>
    public void ReSyncCurrent()
    {
        if (Current == null) return;

        switch (Current.type)
        {
            case GuideQuestType.StageClear:
                bool reached = highestChapter > Current.targetChapter ||
                               (highestChapter == Current.targetChapter && highestStage >= Current.targetStage);
                if (reached) SetProgress(Current.requiredCount);
                break;

            case GuideQuestType.LevelUp:
                int lv = LevelUpManager.Instance != null ? LevelUpManager.Instance.CurrentLevel : 0;
                if (lv > progress) SetProgress(lv);
                break;
        }
    }

    /// <summary>로드 중이 아닐 때만 저장 (미초기화 매니저가 빈 값으로 덮어쓰는 것 방지)</summary>
    private void RequestSave()
    {
        if (isLoading || isClaiming) return;
        SaveManager.Instance?.Save();
    }

    // ══════════════════════════════════════════════
    //  스탯 강화 UI 포커스 예약
    // ══════════════════════════════════════════════
    private bool hasPendingStatFocus = false;
    private LevelUpManager.StatType pendingStatType;

    /// <summary>강화 UI 열기 요청. 지금 열 수 있으면 즉시, 아니면 예약(씬 전환용).</summary>
    public void RequestStatFocus(LevelUpManager.StatType type, bool immediate)
    {
        if (immediate && OnFocusStatUpgrade != null)
        {
            OnFocusStatUpgrade.Invoke(type);
            return;
        }

        hasPendingStatFocus = true;
        pendingStatType = type;
    }

    /// <summary>UpgradeUI.Start에서 호출. 예약이 있으면 소비하고 true 반환.</summary>
    public bool TryConsumeStatFocus(out LevelUpManager.StatType type)
    {
        type = pendingStatType;
        if (!hasPendingStatFocus) return false;

        hasPendingStatFocus = false;
        return true;
    }
}