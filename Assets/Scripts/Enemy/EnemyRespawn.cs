using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스폰 담당. 스테이지가 바뀔 때마다 StageManager가 ResetStage()를 불러
/// "이번 스테이지는 어떤 프리팹을, 어떤 배율로, 얼마 간격으로 뽑을지"를 알려줍니다.
///
/// ★ 이번 수정 (일반 적 생성 주기 개편, 2/3 항목)
///   1) "보스 등장 전까지 뽑을 잡몹 최대 수"(maxTotalSpawn) 제한을 없앴습니다.
///      → 이제 잡몹은 보스가 나오기 전까지(bossSpawned == false인 동안) 계속 스폰됩니다.
///   2) 한 번의 생성 주기(기존 3초)마다 한 마리가 아니라 여러 마리를 동시에 뽑을 수 있도록
///      enemiesPerSpawn 필드를 추가했습니다. "다수 생성 + 일반 적 체력 상승" 방향에 맞춰
///      기본값은 우선 1(기존과 동일)로 두었으니, 체력 밸런스를 정하신 뒤 인스펙터에서 올려주세요.
///
///   ★ 3번째 요청("스테이지 카운트 도달 시 잡몹 즉시 전체 삭제 후 보스 등장")은 이 파일만으로는
///     안전하게 구현할 수 없어서 별도로 StageManager.cs에서 처리했습니다.
///
///   ★ 추가 반영 — 스폰 위치 흩뿌리기 (spawnJitterRadius)
///     enemiesPerSpawn > 1이면 여러 마리가 spawnWaypoints[0]의 정확히 같은 좌표에서
///     태어나 완전히 겹쳐 보이는 문제가 있었습니다. TargetMove에 pathOffset 레이어를
///     추가하고(원본 좌표는 안 건드리고 읽을 때만 더함), 한 번에 태어나는 마릿수만큼
///     원 위에 등각으로 배치해 겹침을 없앴습니다. enemiesPerSpawn = 1이면 offset이
///     항상 Vector2.zero라 기존과 100% 동일합니다.
///
/// ─── 기존 수정 요약 (그대로 유지) ───
///   1) enemyPrefab / bossPrefab 단일 필드 → worldSets(WorldEnemySet 배열)로 교체
///   2) ResetStage가 world / stage 번호를 받아 프리팹과 속도 배율을 결정
///   3) Start()에서 스폰 루프를 자동 시작하지 않음 (프리팹이 정해지기 전이므로)
///   4) Spawn() 안의 중복 호출 제거
///   5) 증강 '적 생성 주기 감소' 배율을 스폰 대기시간에 반영 (RespawnLoop, "★ 증강" 검색)
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

    [Header("다수 생성 (신규)")]
    [Tooltip("한 번의 생성 주기(위 respawnDelay 간격)마다 동시에 몇 마리를 뽑을지. " +
             "1이면 기존과 완전히 동일하게 동작합니다. 값을 올릴 때는 일반 적 체력도 " +
             "같이 조정하는 걸 고려하세요 (이 파일에는 체력 관련 필드가 없습니다).")]
    [SerializeField] private int enemiesPerSpawn = 1;

    [Tooltip("enemiesPerSpawn이 2 이상일 때, 한 번에 태어나는 적들을 원형으로 살짝 " +
             "흩뿌려 배치할 반지름(월드 단위). 0이면 흩뿌리지 않고 예전처럼 완전히 겹쳐서 " +
             "태어납니다. enemiesPerSpawn이 1이면 이 값과 무관하게 항상 0으로 취급합니다 " +
             "(기존 동작과 100% 동일하게 유지하기 위함).")]
    [SerializeField] private float spawnJitterRadius = 0.3f;

    // ★ 삭제됨: maxTotalSpawn ("보스 등장 전까지 뽑을 잡몹 최대 수").
    //   요청대로 일반 적 생성 수를 더 이상 제한하지 않습니다.
    //   보스 등장 시점은 여전히 bossSpawned 플래그(= 외부에서 SpawnBoss() 호출)로만 결정됩니다.

    [Header("풀 예열 개수")]
    [SerializeField] private int prewarmCount = 12;

    // ★ 더 이상 스폰을 막는 데 쓰이지 않지만, 디버그 로그/향후 통계용으로 계속 셉니다.
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
                  $"/ 스탯 x{statMultiplier:F3} / 속도 x{currentSpeedMult:F2} / 주기 {currentDelay:F2}s " +
                  $"/ 동시 생성 {Mathf.Max(1, enemiesPerSpawn)}마리");
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
        //
        // ★ 참고: 한 번에 여러 마리(enemiesPerSpawn)를 동시에 꺼내 쓰게 됐으니,
        //   풀이 너무 자주 바닥나 새로 Instantiate가 일어난다면 prewarmCount를
        //   enemiesPerSpawn에 맞춰 넉넉히 올려주는 게 좋습니다.
        ObjectPoolManager.Instance?.Prewarm(currentEnemyPrefab, prewarmCount);
    }

    // ── 스폰 ───────────────────────────────────────

    void SpawnEnemy()
    {
        if (currentEnemyPrefab == null || spawnWaypoints.Length == 0) return;
        if (bossSpawned) return;

        // ★ 다수 생성: 한 번의 주기에 여러 마리를 동시에 뽑습니다.
        //   enemiesPerSpawn = 1이면 기존과 완전히 동일합니다(반복문이 한 번만 돕니다).
        int count = Mathf.Max(1, enemiesPerSpawn);
        for (int i = 0; i < count; i++)
        {
            // ★ 스폰 위치 흩뿌리기 — count가 1이면 항상 Vector2.zero(오프셋 없음)라서
            //   기존 동작과 완전히 동일합니다. 2 이상일 때만 원 위에 균등하게 배치합니다.
            //
            // ─── 왜 랜덤이 아니라 균등 분배(원 위 등각)인가? (학습 포인트) ──────────
            // Random.insideUnitCircle으로 각자 뽑으면 운 나쁘게 두 마리가 비슷한
            // 방향으로 겹쳐 나올 수 있습니다(특히 2~3마리일 때 체감이 큼).
            // 360도를 마릿수만큼 등분해서 배치하면 "몇 마리가 나오든 서로 겹치지
            // 않는다"가 항상 보장됩니다 — 매 스폰 주기마다 배치가 똑같이 반복되긴
            // 하지만, 반지름이 작아서 눈에 띄는 패턴으로 느껴지진 않습니다.
            // ────────────────────────────────────────────────────────────────────
            Vector2 offset = Vector2.zero;
            if (count > 1 && spawnJitterRadius > 0f)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnJitterRadius;
            }

            // ★ 스폰에 성공했을 때만 카운트를 올립니다.
            if (Spawn(currentEnemyPrefab, offset) != null)
                totalEnemiesSpawned++;
        }
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
    ///
    /// ★ 신규: 여러 마리를 동시에 스폰할 때(enemiesPerSpawn > 1) 서로 겹치지 않도록
    ///   offset 파라미터로 스폰 위치와 이동 경로 전체를 살짝 밀어줍니다.
    ///   TargetMove.SetPathOffset()이 웨이포인트 전체에 똑같이 더해서 계산하므로
    ///   (원본 좌표 자체는 건드리지 않음), 경로 중간에 갑자기 튀거나 다시 겹치는 일이
    ///   없습니다. offset이 Vector2.zero면(기존 호출부, 보스, enemiesPerSpawn=1일 때)
    ///   전부 예전과 100% 동일하게 동작합니다.
    /// ──────────────────────────────────────────────────────────────────
    /// </summary>
    private GameObject Spawn(GameObject prefab, Vector2 offset = default)
    {
        if (ObjectPoolManager.Instance == null) return null;

        Vector3 spawnPos = spawnWaypoints[0].position + new Vector3(offset.x, offset.y, 0f);
        GameObject obj = ObjectPoolManager.Instance.GetInactive(
            prefab, spawnPos, Quaternion.identity);
        if (obj == null) return null;

        // ② 스탯·디버프 초기화 (SetActive 이전이어야 함)
        //    ResetDebuffs() 안에서 TargetMove.ResetForSpawn()이 불려 pathOffset도
        //    0으로 리셋되므로, 아래 SetPathOffset()은 반드시 이 줄보다 뒤에 와야 합니다
        //    (SetSpawnSpeedMultiplier와 완전히 같은 이유 — TargetMove.cs 주석 참고).
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