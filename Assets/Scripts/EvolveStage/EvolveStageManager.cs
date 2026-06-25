using System;
using TMPro;
using UnityEngine;

// EvolveStageManager는 partial로 분리되어 있습니다.
//   EvolveStageManager.cs        — 보스 스폰 / 클리어 / 복귀
//   EvolveStageManager.Timer.cs  — 제한시간(2분) / 시간초과 실패
public partial class EvolveStageManager : MonoBehaviour
{
    public static EvolveStageManager Instance;

    [Header("UI (선택)")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("보스 스폰")]
    [SerializeField] private EvolveBoss bossPrefab;
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

    private void SpawnBoss()
    {
        if (bossPrefab == null)
        {
            Debug.LogError("[EvolveStageManager] bossPrefab이 비어 있습니다.");
            return;
        }

        bool hasPath = spawnWaypoints != null && spawnWaypoints.Length > 0;

        // 경로가 있으면 경로 시작점에서 스폰 (일반 스테이지와 동일), 없으면 고정 위치
        Vector3 spawnPos = hasPath
            ? spawnWaypoints[0].position
            : (bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero);

        EvolveBoss boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        // 스탯 배율은 EvolveBoss.InitStats가 EvolveStageData로 직접 적용 →
        // 여기서 ApplyStatMultiplier는 호출하지 않음 (일반 스테이지 배율 미적용)

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

        OnStageClear?.Invoke();
        Debug.Log("[EvolveStageManager] 진화 스테이지 클리어 → 원래 스테이지로 복귀");

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
        SceneLoader.Instance?.ReturnFromEvolve();
    }
}