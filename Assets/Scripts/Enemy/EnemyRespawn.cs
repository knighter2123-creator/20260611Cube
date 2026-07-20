using System.Collections;
using UnityEngine;

public class EnemyRespawn : MonoBehaviour
{
    public static EnemyRespawn Instance;

    [Header("이 프리팹이 따라갈 이동 경로")]
    public Transform[] spawnWaypoints;

    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("적 생성 주기 (초)")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Boss Settings")]
    public GameObject bossPrefab;
    public int maxTotalSpawn = 20;

    [Header("풀 예열 개수")]
    [SerializeField] private int prewarmCount = 12;

    private int   totalEnemiesSpawned = 0;
    private bool  bossSpawned         = false;
    private float statMultiplier      = 1f;

    private HpBar     hpBarRoot;          // ★ 매 스폰마다 씬 스캔하던 것 캐싱
    private Coroutine respawnCoroutine;

    void Awake()
    {
        // ★ 중복 인스턴스 가드 (없으면 스폰 코루틴이 두 벌 돌 수 있음)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        hpBarRoot = FindFirstObjectByType<HpBar>();
        ObjectPoolManager.Instance?.Prewarm(enemyPrefab, prewarmCount);

        // StageManager.Start가 먼저 ResetStage를 호출했을 수 있으므로 중복 방지
        if (respawnCoroutine == null)
            respawnCoroutine = StartCoroutine(RespawnLoop());
    }

    // ── 스테이지 리셋 (StageManager에서 호출) ──────

    public void ResetStage(float newStatMult)
    {
        statMultiplier      = newStatMult;
        totalEnemiesSpawned = 0;
        bossSpawned         = false;

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnLoop());

        Debug.Log($"[EnemyRespawn] 스테이지 리셋 — 스탯 배율: {statMultiplier:F4}");
    }

    // ── 스폰 ───────────────────────────────────────

    void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnWaypoints.Length == 0) return;
        if (bossSpawned) return;
        if (totalEnemiesSpawned >= maxTotalSpawn) return;

        GameObject obj = Spawn(enemyPrefab);
        if (obj == null) return;

        totalEnemiesSpawned++;
    }

    public void SpawnBoss()
    {
        if (bossSpawned || bossPrefab == null) return;
        bossSpawned = true;

        Spawn(bossPrefab);
        Debug.Log("[EnemyRespawn] 보스 소환!");
    }

    /// <summary>풀에서 꺼내 초기화 → 활성화까지 담당하는 공통 경로</summary>
    private GameObject Spawn(GameObject prefab)
    {
        if (ObjectPoolManager.Instance == null) return null;

        Vector3 spawnPos = spawnWaypoints[0].position;
        GameObject obj = ObjectPoolManager.Instance.GetInactive(
            prefab, spawnPos, Quaternion.identity);
        if (obj == null) return null;

        // ★ 반드시 SetActive(true) 이전에 초기화 (OnEnable에서 Active 등록되므로)
        if (obj.TryGetComponent(out Enemy enemy))
            enemy.OnSpawnFromPool(statMultiplier);

        if (obj.TryGetComponent(out TargetMove move))
            move.SetupPath(spawnWaypoints);

        enemy.OnSpawnFromPool(statMultiplier);   // ResetDebuffs → ResetForSpawn (isInitialized = false)
        move.SetupPath(spawnWaypoints);          // ★ 반드시 이 다음 (isInitialized = true)
        obj.SetActive(true);

        hpBarRoot?.RegisterEnemy(obj);   // HpBar는 활성화 이후 등록
        return obj;
    }

    private IEnumerator RespawnLoop()
    {
        var wait = new WaitForSeconds(respawnDelay);   // 매 루프 할당 제거
        while (true)
        {
            yield return wait;
            SpawnEnemy();
        }
    }
}