using System.Collections;
using System.Collections.Generic;
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
    public int  killCountToSpawnBoss = 20;
    public int  maxTotalSpawn        = 20;

    private int  maxEnemyCount      = 20;
    private int  totalEnemiesSpawned = 0;
    private int  totalEnemiesKilled  = 0;
    private bool bossSpawned         = false;

    // ✅ 스테이지별 스탯 배율
    private float statMultiplier = 1f;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private Coroutine respawnCoroutine;

    void Awake() { Instance = this; }

    void Start() { respawnCoroutine = StartCoroutine(RespawnLoop()); }

    // ── 스테이지 리셋 (StageManager에서 호출) ──────

    public void ResetStage(float newStatMult)
    {
        statMultiplier      = newStatMult;
        totalEnemiesSpawned = 0;
        totalEnemiesKilled  = 0;
        bossSpawned         = false;
        activeEnemies.Clear();

        // 기존 코루틴 재시작
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

        int beforeCount = activeEnemies.Count;
        activeEnemies.RemoveAll(item => item == null);
        totalEnemiesKilled += beforeCount - activeEnemies.Count;

        if (totalEnemiesKilled >= killCountToSpawnBoss)
        {
            SpawnBoss();
            return;
        }

        if (totalEnemiesSpawned >= maxTotalSpawn) return;
        if (activeEnemies.Count >= maxEnemyCount) return;

        Vector3 spawnPos = spawnWaypoints[0].position;
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // ✅ 스탯 배율 적용
        Enemy enemyScript = newEnemy.GetComponent<Enemy>();
        if (enemyScript != null)
            enemyScript.ApplyStatMultiplier(statMultiplier);

        FindObjectOfType<HpBar>()?.RegisterEnemy(newEnemy);
        totalEnemiesSpawned++;
        activeEnemies.Add(newEnemy);

        TargetMove moveScript = newEnemy.GetComponent<TargetMove>();
        if (moveScript != null)
            moveScript.SetupPath(spawnWaypoints);
    }

    public void SpawnBoss()
    {
        if (bossSpawned || bossPrefab == null) return;
        bossSpawned = true;

        Vector3 spawnPos = spawnWaypoints[0].position;
        GameObject boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);

        // ✅ 보스 스탯 배율 적용
        Enemy bossScript = boss.GetComponent<Enemy>();
        if (bossScript != null)
            bossScript.ApplyStatMultiplier(statMultiplier);

        FindObjectOfType<HpBar>()?.RegisterEnemy(boss);

        TargetMove moveScript = boss.GetComponent<TargetMove>();
        if (moveScript != null)
            moveScript.SetupPath(spawnWaypoints);

        Debug.Log("보스 소환!");
    }

    private IEnumerator RespawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnEnemy();
        }
    }
}