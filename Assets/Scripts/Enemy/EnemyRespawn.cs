using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스폰 담당. 스테이지가 바뀔 때마다 StageManager가 ResetStage()를 불러
/// "이번 스테이지는 어떤 프리팹을, 어떤 배율로, 얼마 간격으로 뽑을지"를 알려줍니다.
///
/// ★ 이번 수정은 RespawnLoop() 한 군데뿐입니다. "★ 증강" 을 검색하세요.
///   증강 '적 생성 주기 감소' 배율을 스폰 대기시간에 반영합니다.
///
/// ─── 기존 수정 요약 (그대로 유지) ───
///   1) enemyPrefab / bossPrefab 단일 필드 → worldSets(WorldEnemySet 배열)로 교체
///   2) ResetStage가 world / stage 번호를 받아 프리팹과 속도 배율을 결정
///   3) Start()에서 스폰 루프를 자동 시작하지 않음 (프리팹이 정해지기 전이므로)
///   4) Spawn() 안의 중복 호출 제거
/// </summary>
public class EnemyRespawn : MonoBehaviour
{
    public static EnemyRespawn Instance;

    [Header("이 프리팹이 따라갈 이동 경로")]
    public Transform[] spawnWaypoints;

    [Header("월드별 적 세트")]
    [Tooltip("배열 순서가 곧 월드 번호입니다. index 0 = 월드 1, index 1 = 월드 2 ...")]
    [SerializeField] private WorldEnemySet[] worldSets;

    [Tooltip("적 생성 주기 (초). 오버라이드의 respawnDelayMultiplier가 여기에 곱해집니다.")]
    [SerializeField] private float respawnDelay = 3f;

    [Tooltip("증강 등으로 주기가 줄어들 때의 하한선(초). 너무 작으면 화면이 적으로 뒤덮입니다.")]
    [SerializeField] private float minRespawnDelay = 0.15f;

    [Header("Boss Settings")]
    [Tooltip("보스 등장 전까지 뽑을 잡몹 최대 수")]
    public int maxTotalSpawn = 20;

    [Header("풀 예열 개수")]
    [SerializeField] private int prewarmCount = 12;

    private int   totalEnemiesSpawned = 0;
    private bool  bossSpawned         = false;
    private float statMultiplier      = 1f;

    // ── 현재 스테이지에 적용 중인 값 (ApplyStageSet에서 채워짐) ──
    //
    // 이렇게 "지금 무엇이 적용 중인지"를 필드로 들고 있으면,
    // SpawnEnemy()나 SpawnBoss()는 매번 표를 다시 뒤질 필요 없이
    // 이 값만 읽으면 됩니다. 계산은 스테이지 전환 때 1번만.
    private GameObject currentEnemyPrefab;
    private GameObject currentBossPrefab;
    private float      currentSpeedMult = 1f;
    private float      currentDelay;

    private HpBar     hpBarRoot;          // 매 스폰마다 씬을 뒤지지 않도록 캐싱
    private Coroutine respawnCoroutine;

    void Awake()
    {
        // 중복 인스턴스 가드 (없으면 스폰 코루틴이 두 벌 돌 수 있음)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        currentDelay = respawnDelay;   // ResetStage 전에 참조돼도 0이 되지 않도록
    }

    void Start()
    {
        hpBarRoot = FindFirstObjectByType<HpBar>();

        // ★ 여기서 스폰 루프를 시작하지 않습니다.
        //   프리팹은 "몇 월드 몇 스테이지인가"를 알아야 정할 수 있고,
        //   그 정보는 StageManager만 가지고 있기 때문입니다.
        //   StageManager가 ResetStage()를 부르는 순간 루프가 시작됩니다.
        //   → "누가 주도권을 갖는가"를 한 곳으로 몰아주는 게 버그를 줄입니다.
    }

    // ── 스테이지 리셋 (StageManager에서 호출) ──────

    /// <summary>
    /// 스테이지 시작 시 호출. 스탯 배율과 현재 위치(월드-스테이지)를 받습니다.
    /// </summary>
    public void ResetStage(float newStatMult, int world, int stage)
    {
        statMultiplier      = newStatMult;
        totalEnemiesSpawned = 0;
        bossSpawned         = false;

        ApplyStageSet(world, stage);

        // 이전 스테이지의 코루틴이 남아 있으면 반드시 정리.
        // 안 그러면 스폰 루프가 두 개, 세 개로 늘어나 적이 배로 쏟아집니다.
        if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnLoop());

        Debug.Log($"[EnemyRespawn] {world}-{stage} 시작 — 프리팹: {currentEnemyPrefab?.name} " +
                  $"/ 스탯 x{statMultiplier:F3} / 속도 x{currentSpeedMult:F2} / 주기 {currentDelay:F2}s");
    }

    /// <summary>
    /// 월드/스테이지 번호로 이번 스테이지의 프리팹·배율을 확정합니다.
    /// </summary>
    private void ApplyStageSet(int world, int stage)
    {
        if (worldSets == null || worldSets.Length == 0)
        {
            Debug.LogError("[EnemyRespawn] worldSets가 비어 있습니다. 인스펙터에서 WorldEnemySet 에셋을 넣어주세요.");
            return;
        }

        // ─── Mathf.Clamp의 역할 (학습 포인트) ──────────────────────────
        // 월드 3개만 만들어 뒀는데 플레이어가 4월드에 도달하면?
        // worldSets[3] 은 배열 범위를 벗어나 게임이 터집니다(IndexOutOfRange).
        // Clamp는 값을 [최소, 최대] 안으로 강제로 밀어 넣어주는 함수라서,
        // 4월드 이상은 자동으로 "마지막 세트"를 계속 쓰게 됩니다.
        // 방치형처럼 스테이지가 끝없이 늘어나는 장르에서 아주 유용한 안전장치예요.
        // ──────────────────────────────────────────────────────────────
        int idx = Mathf.Clamp(world - 1, 0, worldSets.Length - 1);
        var set = worldSets[idx];

        if (set == null)
        {
            Debug.LogError($"[EnemyRespawn] worldSets[{idx}] 가 비어 있습니다(None).");
            return;
        }

        // 1단계: 기본값으로 세팅
        currentEnemyPrefab = set.normalPrefab;
        currentBossPrefab  = set.bossPrefab;
        currentSpeedMult   = 1f;
        currentDelay       = respawnDelay;

        // 2단계: 예외 규칙이 있으면 덮어쓰기 (예: 5스테이지 = 빠른 적)
        var ov = set.GetOverride(stage);
        if (ov != null)
        {
            if (ov.prefab != null) currentEnemyPrefab = ov.prefab;   // 비어 있으면 기본 프리팹 유지
            currentSpeedMult = ov.speedMultiplier;
            currentDelay     = respawnDelay * ov.respawnDelayMultiplier;
        }

        // 프리팹이 바뀌었을 수 있으므로 매 스테이지 예열.
        // (수정된 Prewarm은 부족분만 채우므로 여러 번 불러도 안전합니다)
        ObjectPoolManager.Instance?.Prewarm(currentEnemyPrefab, prewarmCount);
    }

    // ── 스폰 ───────────────────────────────────────

    void SpawnEnemy()
    {
        if (currentEnemyPrefab == null || spawnWaypoints.Length == 0) return;
        if (bossSpawned) return;
        if (totalEnemiesSpawned >= maxTotalSpawn) return;

        // ★ 스폰에 성공했을 때만 카운트를 올립니다.
        if (Spawn(currentEnemyPrefab) != null)
            totalEnemiesSpawned++;
    }

    public void SpawnBoss()
    {
        if (bossSpawned || currentBossPrefab == null) return;
        bossSpawned = true;

        Spawn(currentBossPrefab);
        Debug.Log("[EnemyRespawn] 보스 소환!");
    }

    /// <summary>
    /// 풀에서 꺼내 초기화 → 활성화까지 담당하는 공통 경로.
    ///
    /// ─── 호출 순서가 왜 중요한가 (가장 중요한 학습 포인트) ───────────────
    ///
    ///   ① GetInactive()      : 꺼져 있는 상태로 받아온다
    ///   ② OnSpawnFromPool()  : 체력·디버프 초기화
    ///                          → 내부에서 ResetDebuffs() → TargetMove.ResetForSpawn()
    ///                          → 이때 spawnSpeedMult가 1로 리셋됨!
    ///   ③ SetSpawnSpeedMultiplier() : 그래서 ②보다 뒤에 와야 한다
    ///   ④ SetupPath()        : isInitialized = true → 이제부터 움직일 수 있다
    ///   ⑤ SetActive(true)    : OnEnable에서 Enemy.Active 목록에 등록
    ///   ⑥ RegisterEnemy()    : HP바는 오브젝트가 켜진 뒤에 붙인다
    ///
    /// 순서를 바꾸면 "속도 배율이 안 먹는다", "적이 경로 중간에서 출발한다",
    /// "체력 0인 적이 한 프레임 보인다" 같은 재현하기 어려운 버그가 납니다.
    /// 이런 곳에는 주석으로 이유를 남겨두는 습관이 미래의 자신을 살립니다.
    /// ──────────────────────────────────────────────────────────────────
    /// </summary>
    private GameObject Spawn(GameObject prefab)
    {
        if (ObjectPoolManager.Instance == null) return null;

        Vector3 spawnPos = spawnWaypoints[0].position;
        GameObject obj = ObjectPoolManager.Instance.GetInactive(
            prefab, spawnPos, Quaternion.identity);
        if (obj == null) return null;

        // ② 스탯·디버프 초기화 (SetActive 이전이어야 함)
        if (obj.TryGetComponent(out Enemy enemy))
            enemy.OnSpawnFromPool(statMultiplier);

        // ③④ 속도 배율 → 경로 세팅 (반드시 이 순서)
        if (obj.TryGetComponent(out TargetMove move))
        {
            move.SetSpawnSpeedMultiplier(currentSpeedMult);
            move.SetupPath(spawnWaypoints);
        }

        // ⑤ 이제 켠다
        obj.SetActive(true);

        // ⑥ HpBar는 활성화 이후 등록
        hpBarRoot?.RegisterEnemy(obj);
        return obj;
    }

    private IEnumerator RespawnLoop()
    {
        // ★ 증강 ─────────────────────────────────────────────────────────
        //
        // 【원래 코드의 한계】
        //     var wait = new WaitForSeconds(currentDelay);   // 루프 밖에서 1회 생성
        //
        //   "값이 변하지 않는 대기 객체는 밖에서 한 번 만들어 재사용" — 이 원칙 자체는 옳습니다.
        //   스테이지가 바뀔 때마다 코루틴을 새로 시작하니까 그때는 문제가 없었어요.
        //   그런데 증강은 스테이지 '도중에' 주기를 바꿉니다.
        //   객체를 한 번만 만들면 그 변화가 영원히 반영되지 않습니다.
        //
        // 【해결】 매번 new 를 하는 게 아니라, "값이 바뀐 순간에만" 새로 만듭니다.
        //   증강이 켜지고 꺼질 때 딱 2번만 할당이 일어나므로
        //   GC 부담은 사실상 0이면서 실시간 반영도 됩니다.
        //   원래 주석의 교훈(불필요한 new 금지)을 지키면서 요구사항만 추가한 형태입니다.
        //
        //   ※ AugmentManager 가 없으면 SpawnDelay 는 항상 1을 돌려주므로
        //     이 코드는 증강 시스템 없이도 원래대로 동작합니다.
        // ────────────────────────────────────────────────────────────────

        float lastDelay = -1f;
        WaitForSeconds wait = null;

        while (true)
        {
            float delay = Mathf.Max(minRespawnDelay, currentDelay * AugmentManager.SpawnDelay);

            // float 비교에 == 대신 Approximately 를 쓰는 이유:
            // 부동소수점은 0.1 + 0.2 != 0.3 처럼 미세한 오차가 생겨서
            // == 로 비교하면 사실상 같은 값인데도 매번 다르다고 판정될 수 있습니다.
            if (!Mathf.Approximately(delay, lastDelay))
            {
                lastDelay = delay;
                wait = new WaitForSeconds(delay);
            }

            yield return wait;
            SpawnEnemy();
        }
    }
}