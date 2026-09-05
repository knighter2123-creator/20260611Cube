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

    // ★ '각성 1회' 단계에 아직 도달하지 않았을 때 들어온 각성 클리어를 적립해둔다.
    //   Claim이 수동이라, 레벨업 보상을 받기 전에 각성 스테이지를 클리어하면
    //   보고가 그냥 버려지고 그 티어의 각성 퀘스트를 영영 못 깨는 사고가 납니다.
    private int pendingEvolveClears = 0;
    private const int MaxPendingEvolveClears = 8;

    private bool levelUpBound = false;

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

        TryBindLevelUp();
    }

    private void Update()
    {
        // ★ 원래는 Start에서 한 번만 구독을 시도해서, LevelUpManager가 아직 없으면
        //   그 세션 내내 레벨업 퀘스트가 진행되지 않았습니다. 붙을 때까지 재시도합니다.
        if (!levelUpBound) TryBindLevelUp();
    }

    private void TryBindLevelUp()
    {
        if (levelUpBound) return;
        if (LevelUpManager.Instance == null) return;

        LevelUpManager.Instance.OnLevelUp += ReportLevelUp;

        // ★ 세이브 로드/씬 전환으로 레벨이 '복원'될 때도 퀘스트 진행도는 따라가야 합니다.
        //   (연출은 OnStatRestored 를 구독하지 않으므로 레벨업 이펙트는 재생되지 않습니다)
        LevelUpManager.Instance.OnStatRestored += ReportLevelUp;

        levelUpBound = true;

        ReSyncCurrent();   // 이미 목표 레벨을 넘어섰다면 즉시 반영
    }

    private void OnDestroy()
    {
        if (levelUpBound && LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.OnLevelUp      -= ReportLevelUp;
            LevelUpManager.Instance.OnStatRestored -= ReportLevelUp;
        }

        if (Instance == this) Instance = null;
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

    /// <summary>
    /// ★ 신규 — 각성(진화) 스테이지 클리어 시 호출 (EvolveStageManager.ReportBossKill).
    /// 아직 '각성 1회' 단계가 아니면 버리지 않고 적립해뒀다가 그 단계 진입 시 반영합니다.
    /// </summary>
    public void ReportEvolveClear(int count = 1)
    {
        if (count <= 0) return;

        if (Current != null && Current.type == GuideQuestType.EvolveClear)
        {
            AddProgress(count);
            return;
        }

        pendingEvolveClears = Mathf.Min(pendingEvolveClears + count, MaxPendingEvolveClears);
        Debug.Log($"[GuideQuest] 각성 클리어를 적립했습니다 (대기 {pendingEvolveClears}회). " +
                  $"현재 퀘스트: {(Current != null ? Current.type.ToString() : "없음")}");
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
            Debug.Log($"[GuideQuest] {Current.Title} 조건 충족: {Current.Description} (수령 대기)");
            RequestSave();   // ★ 자동 지급 제거 — 완료 상태만 저장, 지급은 버튼(Claim)에서
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

    /// <summary>
    /// 이미 달성한 상태(목표 스테이지를 이미 클리어함 / 레벨이 이미 높음 / 각성을 이미 클리어함)를 즉시 반영.
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

            // ★ 단계 진입 전에 미리 클리어해둔 각성이 있으면 여기서 반영
            case GuideQuestType.EvolveClear:
                if (pendingEvolveClears > 0)
                {
                    int use = pendingEvolveClears;
                    pendingEvolveClears = 0;
                    Debug.Log($"[GuideQuest] 적립해둔 각성 클리어 {use}회를 반영합니다.");
                    AddProgress(use);
                }
                break;
        }
    }

    /// <summary>로드 중이 아닐 때만 저장 (미초기화 매니저가 빈 값으로 덮어쓰는 것 방지)</summary>
    private void RequestSave()
    {
        if (isLoading) return;   // ★ isClaiming 조건 제거
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