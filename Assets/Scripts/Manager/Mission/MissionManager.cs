using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Header("미션 정의 (ScriptableObject)")]
    [SerializeField] private List<MissionData> allMissions = new List<MissionData>();

    // 진행 상황: missionId -> progress (런타임 전용, Dictionary 는 세이브하지 않음)
    private readonly Dictionary<string, MissionProgress> progressMap = new Dictionary<string, MissionProgress>();

    // 마지막으로 초기화된 "기간 시작 시각"(로컬 벽시계 기준 Ticks)
    private long lastDailyResetTicks = 0;
    private long lastWeeklyResetTicks = 0;

    // 실행 순서 버그 방지용 초기화 플래그
    private bool initialized = false;

    // UI 갱신 신호
    public event Action OnMissionUpdated;

    public enum MissionState { InProgress, Claimable, Claimed }

    private const int ResetHour = 6;                 // am 6
    private const float ResetCheckInterval = 60f;    // 게임 켜둔 채 경계 넘길 때 대비
    private float resetCheckTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // 중복 → 파괴 (씬 재진입 시 새로 생긴 것 제거)
            return;
        }
        Instance = this;

        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
            ApplyFrom(SaveManager.Instance.Current);
        else
            EnsureInitialized();
    }

    private void Start()
    {
        // 세이브 로드(ApplyFrom)가 없었던 경우(신규 설치 등) 대비 폴백
        EnsureInitialized();
    }

    // ApplyFrom 끝, Start, 또는 최초 리포트 시 호출되어도 딱 한 번만 초기화
    public void EnsureInitialized()
    {
        if (initialized) return;

        BuildMissingProgress(); // 세이브에 없던 신규 미션 항목 생성
        CheckResets();          // 로드된 시각 기준으로 초기화 필요 여부 판정

        initialized = true;
    }

    private void BuildMissingProgress()
    {
        foreach (var m in allMissions)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) continue;
            if (!progressMap.ContainsKey(m.id))
                progressMap[m.id] = new MissionProgress(m.id);
        }
    }

    private void Update()
    {
        // Time.timeScale 영향을 받지 않도록 unscaled 사용
        resetCheckTimer += Time.unscaledDeltaTime;
        if (resetCheckTimer < ResetCheckInterval) return;

        resetCheckTimer = 0f;
        long beforeD = lastDailyResetTicks;
        long beforeW = lastWeeklyResetTicks;
        CheckResets();
        if (beforeD != lastDailyResetTicks || beforeW != lastWeeklyResetTicks)
            OnMissionUpdated?.Invoke();
    }

    // ---------------- 진행도 리포트 ----------------
    public void ReportProgress(MissionConditionType conditionType, int amount = 1)
    {
        Debug.Log($"[MissionManager] ReportProgress: {conditionType} +{amount}");
        if (!initialized) EnsureInitialized();
        if (amount <= 0) return;

        bool changed = false;

        foreach (var m in allMissions)
        {
            if (m == null || m.conditionType != conditionType) continue;
            if (!progressMap.TryGetValue(m.id, out var p)) continue;
            if (p.claimed) continue;                       // 이미 수령 → 누적 불필요
            if (p.currentCount >= m.requiredCount) continue; // 이미 달성 → 스킵

            // 일일/주간이 같은 이벤트로 각각 누적됨 (적 1마리 → 일일·주간 동시 +1)
            p.currentCount = Mathf.Min(p.currentCount + amount, m.requiredCount);
            changed = true;
        }

        if (changed) OnMissionUpdated?.Invoke();
    }

    public void ReportEnemyKill(int count = 1) => ReportProgress(MissionConditionType.EnemyKill, count);
    public void ReportBossKill(int count = 1)  => ReportProgress(MissionConditionType.BossKill, count);
    public void ReportGachaPull(int count = 1) => ReportProgress(MissionConditionType.GachaPull, count);

    // 해당 타입에서 "수령 완료(claimed)"된 개별 미션 수
    public int CountCompleted(MissionType type)
    {
        int count = 0;
        foreach (var m in allMissions)
        {
            if (m == null || m.missionType != type) continue;
            // "달성만으로 열리게" 하려면 아래를 GetState(m.id) != InProgress 로 교체
            if (GetState(m.id) == MissionState.Claimed) count++;
        }
        return count;
    }

    // 해당 타입의 개별 미션 총 개수
    public int CountTotal(MissionType type)
    {
        int count = 0;
        foreach (var m in allMissions)
            if (m != null && m.missionType == type) count++;
        return count;
    }
    
    // ---------------- 조회 (UI 용) ----------------
    public MissionData GetMissionData(string missionId)
    {
        foreach (var m in allMissions)
            if (m != null && m.id == missionId) return m;
        return null;
    }

    public MissionProgress GetProgress(string missionId)
    {
        progressMap.TryGetValue(missionId, out var p);
        return p;
    }

    public List<MissionData> GetMissions(MissionType type)
    {
        var list = new List<MissionData>();
        foreach (var m in allMissions)
            if (m != null && m.missionType == type) list.Add(m);
        return list;
    }

    public MissionState GetState(string missionId)
    {
        var m = GetMissionData(missionId);
        var p = GetProgress(missionId);
        if (m == null || p == null) return MissionState.InProgress;
        if (p.claimed) return MissionState.Claimed;
        if (p.currentCount >= m.requiredCount) return MissionState.Claimable;
        return MissionState.InProgress;
    }
    
    // ---------------- 앱 생명주기 ----------------
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveNow(); // 백그라운드 진입 시 저장 (모바일 필수)
        }
        else
        {
            CheckResets(); // 복귀 시 초기화 시점 재확인
            OnMissionUpdated?.Invoke();
        }
    }

    private void OnApplicationQuit() => SaveNow();
}