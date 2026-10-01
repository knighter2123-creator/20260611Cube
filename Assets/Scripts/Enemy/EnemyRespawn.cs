using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스폰 담당. 스테이지가 바뀔 때마다 StageManager가 ResetStage()를 불러
/// "이번 스테이지는 어떤 프리팹을, 어떤 배율로, 얼마 간격으로 뽑을지"를 알려줍니다.
///
///   · 잡몹은 보스가 나오기 전까지(bossSpawned == false) 수 제한 없이 계속 스폰됩니다.
///   · 한 주기마다 enemiesPerSpawn 마리를 동시에 뽑고, 2마리 이상이면
///     spawnJitterRadius 반지름의 원 위에 등각으로 흩뿌려 겹침을 막습니다.
///   · 보스 등장 시점은 StageManager가 SpawnBoss()를 호출해 결정합니다.
///   · 스폰 주기에는 증강 '적 생성 주기 감소'(AugmentManager.SpawnDelay)가 실시간 반영됩니다.
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

    [Header("다수 생성")]
    [Tooltip("한 번의 생성 주기(위 respawnDelay 간격)마다 동시에 몇 마리를 뽑을지. " +
             "값을 올릴 때는 일반 적 체력도 같이 조정하는 걸 고려하세요.")]
    [SerializeField] private int enemiesPerSpawn = 1;

    [Tooltip("enemiesPerSpawn이 2 이상일 때, 한 번에 태어나는 적들을 원형으로 살짝 " +
             "흩뿌려 배치할 반지름(월드 단위). 0이면 흩뿌리지 않고 완전히 겹쳐서 태어납니다.")]
    [SerializeField] private float spawnJitterRadius = 0.3f;

    [Header("풀 예열 개수")]
    [Tooltip("한 주기에 여러 마리를 꺼내 쓰므로, 풀이 자주 바닥나면 enemiesPerSpawn에 맞춰 올려주세요.")]
    [SerializeField] private int prewarmCount = 12;

    private bool  bossSpawned    = false;
    private float statMultiplier = 1f;

    // ── 현재 스테이지에 적용 중인 값 (ApplyStageSet에서 채워짐) ──
    // 계산은 스테이지 전환 때 1번만 하고, 스폰할 때는 이 값만 읽습니다.
    private GameObject currentEnemyPrefab;
    private GameObject currentBossPrefab;
    private float      currentSpeedMult = 1f;
    private float      currentDelay;

    private HpBar     hpBarRoot;          // 매 스폰마다 씬을 뒤지지 않도록 캐싱
    private Coroutine respawnCoroutine;

    private int  SpawnCount => Mathf.Max(1, enemiesPerSpawn);
    private bool HasWaypoints => spawnWaypoints != null && spawnWaypoints.Length > 0;

    void Awake()
    {
        // 중복 인스턴스 가드 (없으면 스폰 코루틴이 두 벌 돌 수 있음)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        currentDelay = respawnDelay;   // ResetStage 전에 참조돼도 0이 되지 않도록
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        hpBarRoot = FindFirstObjectByType<HpBar>();

        // ★ 여기서 스폰 루프를 시작하지 않습니다.
        //   프리팹은 "몇 월드 몇 스테이지인가"를 알아야 정할 수 있고,
        //   그 정보는 StageManager만 가지고 있기 때문입니다.
        //   StageManager가 ResetStage()를 부르는 순간 루프가 시작됩니다.
    }

    // ── 스테이지 리셋 (StageManager에서 호출) ──────

    /// <summary>
    /// 스테이지 시작 시 호출. 스탯 배율과 현재 위치(월드-스테이지)를 받습니다.
    /// </summary>
    public void ResetStage(float newStatMult, int world, int stage)
    {
        statMultiplier = newStatMult;
        bossSpawned    = false;

        ApplyStageSet(world, stage);

        // 이전 스테이지의 코루틴이 남아 있으면 반드시 정리.
        // 안 그러면 스폰 루프가 두 개, 세 개로 늘어나 적이 배로 쏟아집니다.
        if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnLoop());

        Debug.Log($"[EnemyRespawn] {world}-{stage} 시작 — 프리팹: {currentEnemyPrefab?.name} " +
                  $"/ 스탯 x{statMultiplier:F3} / 속도 x{currentSpeedMult:F2} / 주기 {currentDelay:F2}s " +
                  $"/ 동시 생성 {SpawnCount}마리");
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

        // 준비된 월드 수를 넘어서면 Clamp로 "마지막 세트"를 계속 씁니다.
        // (방치형처럼 스테이지가 끝없이 늘어나는 장르의 IndexOutOfRange 안전장치)
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

        // 프리팹이 바뀌었을 수 있으므로 매 스테이지 예열 (부족분만 채우므로 여러 번 불러도 안전)
        ObjectPoolManager.Instance?.Prewarm(currentEnemyPrefab, prewarmCount);
    }

    // ── 스폰 ───────────────────────────────────────

    void SpawnEnemy()
    {
        if (bossSpawned || currentEnemyPrefab == null) return;

        int count = SpawnCount;
        for (int i = 0; i < count; i++)
            Spawn(currentEnemyPrefab, GetSpawnOffset(i, count));
    }

    public void SpawnBoss()
    {
        if (bossSpawned || currentBossPrefab == null) return;
        bossSpawned = true;

        // 보스는 항상 정확히 waypoint 위치에서 등장 (오프셋 없음)
        Spawn(currentBossPrefab, Vector2.zero);
        Debug.Log("[EnemyRespawn] 보스 소환!");
    }

    /// <summary>
    /// 동시에 태어나는 count마리 중 index번째의 위치 오프셋. count가 1이면 항상 0.
    ///
    /// ─── 왜 랜덤이 아니라 균등 분배(원 위 등각)인가? (학습 포인트) ──────────
    /// Random.insideUnitCircle으로 각자 뽑으면 운 나쁘게 두 마리가 비슷한
    /// 방향으로 겹쳐 나올 수 있습니다(특히 2~3마리일 때 체감이 큼).
    /// 360도를 마릿수만큼 등분해서 배치하면 "몇 마리가 나오든 서로 겹치지
    /// 않는다"가 항상 보장됩니다.
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
    private Vector2 GetSpawnOffset(int index, int count)
    {
        if (count <= 1 || spawnJitterRadius <= 0f) return Vector2.zero;

        float angle = (360f / count) * index * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnJitterRadius;
    }

    /// <summary>
    /// 풀에서 꺼내 초기화 → 활성화까지 담당하는 공통 경로.
    ///
    /// ─── 호출 순서가 왜 중요한가 (가장 중요한 학습 포인트) ───────────────
    ///
    ///   ① GetInactive()      : 꺼져 있는 상태로 받아온다
    ///   ② OnSpawnFromPool()  : 체력·디버프 초기화
    ///                          → 내부에서 ResetDebuffs() → TargetMove.ResetForSpawn()
    ///                          → 이때 spawnSpeedMult / pathOffset이 리셋됨!
    ///   ③ SetSpawnSpeedMultiplier() / SetPathOffset() : 그래서 ②보다 뒤에 와야 한다
    ///   ④ SetupPath()        : isInitialized = true → 이제부터 움직일 수 있다
    ///   ⑤ SetActive(true)    : OnEnable에서 Enemy.Active 목록에 등록
    ///   ⑥ RegisterEnemy()    : HP바는 오브젝트가 켜진 뒤에 붙인다
    ///
    /// 순서를 바꾸면 "속도 배율이 안 먹는다", "적이 경로 중간에서 출발한다",
    /// "체력 0인 적이 한 프레임 보인다" 같은 재현하기 어려운 버그가 납니다.
    ///
    /// offset은 스폰 위치와 이동 경로 전체를 똑같이 평행 이동시킵니다
    /// (TargetMove가 웨이포인트를 읽을 때 더할 뿐, 원본 좌표는 건드리지 않음).
    /// ──────────────────────────────────────────────────────────────────
    /// </summary>
    private GameObject Spawn(GameObject prefab, Vector2 offset)
    {
        if (ObjectPoolManager.Instance == null || !HasWaypoints) return null;

        Vector3 spawnPos = spawnWaypoints[0].position + (Vector3)offset;
        GameObject obj = ObjectPoolManager.Instance.GetInactive(prefab, spawnPos, Quaternion.identity);
        if (obj == null) return null;

        // ② 스탯·디버프 초기화 (SetActive 이전이어야 함)
        if (obj.TryGetComponent(out Enemy enemy))
            enemy.OnSpawnFromPool(statMultiplier);

        // ③④ 속도 배율 → 오프셋 → 경로 세팅 (반드시 ②보다 뒤)
        if (obj.TryGetComponent(out TargetMove move))
        {
            move.SetSpawnSpeedMultiplier(currentSpeedMult);
            move.SetPathOffset(offset);
            move.SetupPath(spawnWaypoints);
        }

        // ⑤ 이제 켠다
        obj.SetActive(true);

        // ⑥ HpBar는 활성화 이후 등록
        hpBarRoot?.RegisterEnemy(obj);
        return obj;
    }

    /// <summary>
    /// 스폰 루프. 증강은 스테이지 '도중에' 주기를 바꾸므로 매 주기 delay를 다시 계산하되,
    /// WaitForSeconds는 "값이 바뀐 순간에만" 새로 만들어 불필요한 할당을 피합니다.
    /// (AugmentManager가 없으면 SpawnDelay는 항상 1)
    /// </summary>
    private IEnumerator RespawnLoop()
    {
        float lastDelay = -1f;
        WaitForSeconds wait = null;

        while (true)
        {
            float delay = Mathf.Max(minRespawnDelay, currentDelay * AugmentManager.SpawnDelay);

            // float는 미세한 오차가 생기므로 == 대신 Approximately로 비교합니다.
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
