using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// partial: 이 파일 = 보스 스폰·클리어·복귀, .Timer = 제한시간(2분)·시간초과 실패
public partial class EvolveStageManager : MonoBehaviour
{
    public static EvolveStageManager Instance;

    [Header("UI (선택)")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("보스 스폰")]
    // FormerlySerializedAs — 필드 이름을 bossPrefab 에서 바꿔도 기존 인스펙터 연결 유지 (모든 씬이 재저장된 뒤엔 삭제 가능)
    [FormerlySerializedAs("bossPrefab")]
    [Tooltip("티어 데이터(EvolveStageData)에 bossPrefab이 비어 있을 때 사용할 예비 프리팹")]
    [SerializeField] private EvolveBoss fallbackBossPrefab;

    [Tooltip("보스가 따라갈 이동 경로 (일반 스테이지의 spawnWaypoints와 동일 역할). " +
             "EvolveScene 안에 waypoint 오브젝트들을 두고 연결하세요.")]
    [SerializeField] private Transform[] spawnWaypoints;

    [Tooltip("waypoint가 없을 때만 사용하는 고정 스폰 위치 (정지형 보스용)")]
    [SerializeField] private Transform   bossSpawnPoint;

    [Header("단독 테스트용 — 입장 경로 없이 씬 직접 실행 시 데이터")]
    [SerializeField] private EvolveStageData fallbackData;

    public event Action OnStageClear;
    public event Action OnStageFail;

    public EvolveStageData ActiveData { get; private set; }

    private bool stageOver = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 입장 경로(EvolveStageEntry)에서 넣어준 데이터, 없으면 fallback
        ActiveData = EvolveStageContext.SelectedData != null
            ? EvolveStageContext.SelectedData
            : fallbackData;

        if (stageText != null && ActiveData != null)
            stageText.text = ActiveData.displayName;

        StartTimer();   // ← Timer 파셜 (2분 카운트다운 시작)
        SpawnBoss();
    }

    /// <summary>보스 프리팹 결정(티어 데이터 → fallback) — 규칙이 늘 수 있는 부분이라 함수로 분리.</summary>
    private EvolveBoss ResolveBossPrefab()
    {
        if (ActiveData != null && ActiveData.bossPrefab != null)
            return ActiveData.bossPrefab;

        return fallbackBossPrefab;
    }

    private void SpawnBoss()
    {
        EvolveBoss prefab = ResolveBossPrefab();

        if (prefab == null)
        {
            Debug.LogError("[EvolveStageManager] 보스 프리팹이 없습니다. " +
                           "EvolveStageData의 bossPrefab 또는 인스펙터의 fallbackBossPrefab을 채워주세요.");
            return;
        }

        bool hasPath = spawnWaypoints != null && spawnWaypoints.Length > 0;

        // 경로가 있으면 경로 시작점에서 스폰 (일반 스테이지와 동일), 없으면 고정 위치
        Vector3 spawnPos = hasPath
            ? spawnWaypoints[0].position
            : (bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero);

        EvolveBoss boss = Instantiate(prefab, spawnPos, Quaternion.identity);
        HapticManager.PlayHeavy();   // 보스 등장 진동

        // 스탯 초기화 필수 (안 부르면 원본 체력 그대로), 일반 스테이지 배율은 적용 안 함 — 난이도는 티어 데이터만
        boss.InitForEvolveStage();
        
        // ── 일반 스테이지 보스와 동일하게 이동 경로 전달 ──
        TargetMove move = boss.GetComponent<TargetMove>();
        if (move != null && hasPath)
            move.SetupPath(spawnWaypoints);
        else if (move != null && !hasPath)
            Debug.LogWarning("[EvolveStageManager] spawnWaypoints가 비어 보스가 이동하지 않습니다.");

        // ── HP바 등록 ──
        FindFirstObjectByType<HpBar>()?.RegisterEnemy(boss.gameObject);
    }

    /// <summary>EvolveBoss.Die()에서 호출 → 클리어(보상은 보스 쪽에서 이미 지급).</summary>
    public void ReportBossKill()
    {
        if (stageOver) return;
        stageOver = true;

        // 가이드 퀘스트 '각성 1회' 보고 — 씬 전환(ReturnToStage) 전에
        if (GuideQuestManager.Instance != null)
            GuideQuestManager.Instance.ReportEvolveClear();
        else
            Debug.LogWarning("[EvolveStageManager] GuideQuestManager를 찾을 수 없어 각성 퀘스트를 보고하지 못했습니다.");

        // 각성 보상 갱신 — 세이브 기록을 다시 읽어 계산하는 멱등 호출, 순서가 어긋나도 sceneLoaded 에서 자가 복구
        if (AwakeningManager.Instance != null)
            AwakeningManager.Instance.ReportEvolveCleared(ActiveData != null ? ActiveData.id : null);
        else
            Debug.LogWarning("[EvolveStageManager] AwakeningManager를 찾을 수 없어 각성 효과를 갱신하지 못했습니다. " +
                             "LoginScene의 Managers 루트에 추가했는지 확인하세요.");

        OnStageClear?.Invoke();
        GameLog.Info("[EvolveStageManager] 진화 스테이지 클리어 → 원래 스테이지로 복귀");

        ReturnToStage();
    }

    /// <summary>클리어/실패 공통 — 잔여 적 정리 후 원래 스테이지로 복귀.</summary>
    private void ReturnToStage()
    {
        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Enemy e = enemy.GetComponent<Enemy>();
            e?.RemoveHpBar();
            Destroy(enemy);
        }

        // 복귀 위치는 EvolveStageContext에 저장돼 있고, StageManager가 복원함
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.ReturnFromEvolve();
    }
}