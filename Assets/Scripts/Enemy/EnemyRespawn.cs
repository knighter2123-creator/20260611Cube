using System.Collections;
using UnityEngine;

/// <summary>적 스폰 — StageManager 가 ResetStage 로 프리팹·배율을 정해주면 보스 전까지 주기마다 enemiesPerSpawn 마리를 원 위 등각으로 스폰 (주기는 증강 실시간 반영, 보스는 SpawnBoss).</summary>
public class EnemyRespawn : MonoBehaviour
{
    public static EnemyRespawn Instance { get; private set; }

    [Header("이 프리팹이 따라갈 이동 경로")]
    [SerializeField] private Transform[] spawnWaypoints;

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

    // ── 현재 스테이지 값 (ApplyStageSet 에서 1번 계산, 스폰 땐 읽기만) ──
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

        // 루프는 여기서 시작 안 함 — 월드·스테이지 정보가 StageManager 에 있어 ResetStage() 때 시작
    }

    // ── 스테이지 리셋 (StageManager에서 호출) ──────

    /// <summary>스테이지 시작 시 호출 — 스탯 배율과 월드-스테이지를 받음.</summary>
    public void ResetStage(float newStatMult, int world, int stage)
    {
        statMultiplier = newStatMult;
        bossSpawned    = false;

        ApplyStageSet(world, stage);

        // 이전 루프 정리 (안 하면 루프가 겹쳐 적이 배로 쏟아짐)
        if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnLoop());

        GameLog.Info($"[EnemyRespawn] {world}-{stage} 시작 — 프리팹: {currentEnemyPrefab?.name} " +
                  $"/ 스탯 x{statMultiplier:F3} / 속도 x{currentSpeedMult:F2} / 주기 {currentDelay:F2}s " +
                  $"/ 동시 생성 {SpawnCount}마리");
    }

    /// <summary>월드/스테이지 번호로 이번 스테이지의 프리팹·배율을 확정.</summary>
    private void ApplyStageSet(int world, int stage)
    {
        if (worldSets == null || worldSets.Length == 0)
        {
            Debug.LogError("[EnemyRespawn] worldSets가 비어 있습니다. 인스펙터에서 WorldEnemySet 에셋을 넣어주세요.");
            return;
        }

        // 준비된 월드 수를 넘으면 마지막 세트 사용 (끝없는 스테이지의 IndexOutOfRange 방지)
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

        // 매 스테이지 예열(부족분만, 여러 프레임에 나눠) — 보스도 1마리 미리 만들어 등장 순간 멈칫 방지
        ObjectPoolManager pool = ObjectPoolManager.Instance;
        if (pool != null)
        {
            pool.PrewarmOverFrames(currentEnemyPrefab, prewarmCount);
            pool.PrewarmOverFrames(currentBossPrefab, 1);
        }
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
        HapticManager.PlayHeavy();   // 보스 등장 진동 (일일 던전·진화 보스도 각 매니저의 SpawnBoss 에서 같게)
        GameLog.Info("[EnemyRespawn] 보스 소환!");
    }

    /// <summary>동시 스폰 count 마리 중 index 번째 오프셋(1마리면 0) — 랜덤은 겹칠 수 있어 원 위 등각 배치로 겹침 없음 보장.</summary>
    private Vector2 GetSpawnOffset(int index, int count)
    {
        if (count <= 1 || spawnJitterRadius <= 0f) return Vector2.zero;

        float angle = (360f / count) * index * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnJitterRadius;
    }

    /// <summary>풀에서 꺼내 초기화→활성화하는 공통 경로 — offset 은 스폰 위치와 경로 전체를 평행 이동(원본 좌표 불변).</summary>
    // ⚠ 순서 고정: GetInactive → OnSpawnFromPool(배율·오프셋 리셋) → SetSpawnSpeedMultiplier/SetPathOffset → SetupPath → SetActive → RegisterEnemy(HP바)
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

    /// <summary>스폰 루프 — 증강이 도중에 주기를 바꾸므로 매 주기 delay 재계산, WaitForSeconds 는 값이 바뀔 때만 새로 생성(할당 절약).</summary>
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
