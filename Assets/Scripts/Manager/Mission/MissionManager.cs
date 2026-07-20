using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MissionManager : MonoBehaviour
{
    // ── 조회용 인덱스 (allMissions 선형 탐색 제거) ──
    private readonly Dictionary<string, MissionData> missionById
        = new Dictionary<string, MissionData>();
    private readonly Dictionary<MissionType, List<MissionData>> missionsByType
        = new Dictionary<MissionType, List<MissionData>>();
    private readonly Dictionary<MissionConditionType, List<MissionData>> missionsByCondition
        = new Dictionary<MissionConditionType, List<MissionData>>();
    
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
        EnsureInitialized();
        StartCoroutine(ResetCheckLoop());
    }
    
    private IEnumerator ResetCheckLoop()
    {
        var wait = new WaitForSecondsRealtime(ResetCheckInterval);
        while (true)
        {
            yield return wait;

            long beforeD = lastDailyResetTicks;
            long beforeW = lastWeeklyResetTicks;
            CheckResets();
            if (beforeD != lastDailyResetTicks || beforeW != lastWeeklyResetTicks)
                OnMissionUpdated?.Invoke();
        }
    }


    // ApplyFrom 끝, Start, 또는 최초 리포트 시 호출되어도 딱 한 번만 초기화
    public void EnsureInitialized()
    {
        if (initialized) return;

        BuildIndex();           // ★ 추가 — 반드시 가장 먼저
        BuildMissingProgress();
        CheckResets();

        initialized = true;
    }
    
    // 인스펙터 리스트를 딱 한 번만 순회해서 3개 인덱스를 구축
    private void BuildIndex()
    {
        missionById.Clear();
        missionsByType.Clear();
        missionsByCondition.Clear();

        foreach (var m in allMissions)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) continue;

            missionById[m.id] = m;

            if (!missionsByType.TryGetValue(m.missionType, out var byType))
                missionsByType[m.missionType] = byType = new List<MissionData>();
            byType.Add(m);

            if (!missionsByCondition.TryGetValue(m.conditionType, out var byCond))
                missionsByCondition[m.conditionType] = byCond = new List<MissionData>();
            byCond.Add(m);
        }
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

    

    // ---------------- 진행도 리포트 ----------------
    public void ReportProgress(MissionConditionType conditionType, int amount = 1)
    {
        if (!initialized) EnsureInitialized();
        if (amount <= 0) return;
        if (!missionsByCondition.TryGetValue(conditionType, out var list)) return;

        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (!progressMap.TryGetValue(m.id, out var p)) continue;
            if (p.claimed) continue;
            if (p.currentCount >= m.requiredCount) continue;

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
        if (!missionsByType.TryGetValue(type, out var list)) return 0;

        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            // GetState(id) 대신 데이터를 직접 넘겨 재탐색 제거
            if (GetStateOf(list[i]) == MissionState.Claimed) count++;
        }
        return count;
    }

    // 해당 타입의 개별 미션 총 개수
    public int CountTotal(MissionType type)
        => missionsByType.TryGetValue(type, out var list) ? list.Count : 0;
    
    // ---------------- 조회 (UI 용) ----------------
    public MissionData GetMissionData(string missionId)
    {
        if (string.IsNullOrEmpty(missionId)) return null;
        missionById.TryGetValue(missionId, out var m);
        return m;
    }

    public MissionProgress GetProgress(string missionId)
    {
        progressMap.TryGetValue(missionId, out var p);
        return p;
    }

    // 반환값을 수정하지 마세요 (내부 캐시를 그대로 반환)
    public IReadOnlyList<MissionData> GetMissions(MissionType type)
    {
        return missionsByType.TryGetValue(type, out var list)
            ? list
            : System.Array.Empty<MissionData>();
    }
    public MissionState GetState(string missionId)
        => GetStateOf(GetMissionData(missionId));
    
    private MissionState GetStateOf(MissionData m)
    {
        if (m == null) return MissionState.InProgress;
        if (!progressMap.TryGetValue(m.id, out var p) || p == null)
            return MissionState.InProgress;

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