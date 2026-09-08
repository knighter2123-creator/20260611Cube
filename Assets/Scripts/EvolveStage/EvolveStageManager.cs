using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;   // ★ FormerlySerializedAs 사용을 위해 추가

// EvolveStageManager는 partial로 분리되어 있습니다.
//   EvolveStageManager.cs        — 보스 스폰 / 클리어 / 복귀
//   EvolveStageManager.Timer.cs  — 제한시간(2분) / 시간초과 실패
//
// ★ 이번 수정 요약
//   1) 보스 프리팹을 티어(EvolveStageData)에서 우선적으로 읽도록 변경
//      → 티어마다 다른 보스 겉모습을 쓸 수 있습니다.
//   2) 기존 bossPrefab 필드는 fallbackBossPrefab으로 이름 변경 (역할이 "예비"로 바뀌었으므로)
//   3) 스폰 로직을 ResolveBossPrefab()으로 분리해 읽기 쉽게 정리
public partial class EvolveStageManager : MonoBehaviour
{
    public static EvolveStageManager Instance;

    [Header("UI (선택)")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("보스 스폰")]
    // ─── [FormerlySerializedAs] 는 무엇인가? (학습 포인트) ──────────────────
    // 유니티는 인스펙터에 넣은 값을 "필드 이름"을 열쇠로 삼아 씬 파일에 저장합니다.
    // 그래서 필드 이름을 bossPrefab → fallbackBossPrefab 으로 바꾸면,
    // 유니티는 "fallbackBossPrefab이라는 새 필드가 생겼네" 라고 판단하고
    // 기존에 연결해 둔 프리팹을 잃어버립니다(None이 됨).
    //
    // 이 속성을 붙여두면 "예전 이름으로 저장된 값도 이 필드로 읽어와라"라는 뜻이 되어
    // 연결이 그대로 유지됩니다. 이름을 바꿀 때 습관적으로 붙여주면 사고를 막을 수 있어요.
    // (충분히 시간이 지나 모든 씬이 새 이름으로 저장된 뒤에는 지워도 됩니다)
    // ────────────────────────────────────────────────────────────────────
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

    /// <summary>
    /// 이번 티어에서 쓸 보스 프리팹을 결정합니다.
    /// 우선순위: 티어 데이터의 bossPrefab → 인스펙터의 fallbackBossPrefab
    ///
    /// ─── 왜 함수로 뺐는가? (학습 포인트) ────────────────────────────
    /// SpawnBoss() 안에 삼항연산자로 한 줄 넣어도 동작은 같습니다.
    /// 하지만 "어느 프리팹이 선택되는가"는 나중에 규칙이 늘어날 가능성이 큰 부분이에요.
    /// (예: 첫 클리어 이후엔 다른 스킨, 이벤트 기간엔 특별 보스 …)
    /// 변할 것 같은 판단 로직을 이름 있는 함수로 떼어두면,
    /// 나중에 그 함수 안만 고치면 되고 SpawnBoss는 건드릴 일이 없습니다.
    /// ────────────────────────────────────────────────────────────
    /// </summary>
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

        // ★ 스탯 초기화 — 반드시 호출해야 합니다.
        //
        //   기존 코드는 EvolveBoss.InitStats()가 알아서 배율을 적용한다고 가정했지만,
        //   InitStats()는 실제로 아무도 호출하지 않는 죽은 코드였습니다.
        //   즉 지금까지 진화 보스는 프리팹 원본 체력 그대로 나오고 있었어요
        //   (bossHpMultiplier 15배가 한 번도 안 먹었습니다).
        //
        //   일반 스테이지 배율(statMult)은 여기서도 적용하지 않습니다 —
        //   진화 스테이지는 티어 데이터만으로 난이도가 결정되는 별개의 축이니까요.
        boss.InitForEvolveStage();

        string tierId = ActiveData != null ? ActiveData.id : "(티어 데이터 없음)";
        Debug.Log($"[EvolveStageManager] 보스 스폰 — {prefab.name} / 티어: {tierId}");

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

        // ★ 가이드 퀘스트 '각성 1회' 보고.
        //   ReturnToStage()가 씬을 전환하므로 반드시 그 전에 호출해야 합니다.
        //   (이 호출이 없어서 버프는 적용되는데 퀘스트만 안 끝나던 문제)
        if (GuideQuestManager.Instance != null)
            GuideQuestManager.Instance.ReportEvolveClear();
        else
            Debug.LogWarning("[EvolveStageManager] GuideQuestManager를 찾을 수 없어 각성 퀘스트를 보고하지 못했습니다.");

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
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.ReturnFromEvolve();
    }
}