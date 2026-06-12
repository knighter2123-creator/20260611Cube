using System.Collections;
using System.Collections.Generic; // List 사용을 위해 추가
using UnityEngine;

/// <summary>
/// 적 스폰 전담 매니저.
/// 적이 처치되지 않아도 3초마다 생성되며, 필드에 최대 20마리까지만 존재하도록 제한합니다.
/// </summary>
public class EnemyRespawn : MonoBehaviour
{
    public static EnemyRespawn Instance;

    [Header("이 프리랩이 따라갈 이동 경로")]
    public Transform[] spawnWaypoints; 
    
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab; // 추후 인라인 보스 방식 확장용

    [Tooltip("적 생성 주기 (초)")]
    [SerializeField] private float respawnDelay = 3f;

    [Tooltip("필드에 존재할 수 있는 최대 적 마리 수")]
    [SerializeField] private int maxEnemyCount = 20;

    // 현재 필드에 살아있는 적들을 관리할 리스트 (Null 체크로 카운트)
    private List<GameObject> activeEnemies = new List<GameObject>();

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Update 대신, Start에서 코루틴을 단 한 번만 시작하여 반복 루프를 돌립니다.
        StartCoroutine(RespawnLoop());
    }

    // ──────────────────────────────────────────────
    //  스폰 API
    // ──────────────────────────────────────────────

    /// <summary>
    /// 일반 몬스터를 조건(최대 수량 미달) 확인 후 소환합니다.
    /// </summary>
    void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnWaypoints.Length == 0) return;

        // 1. 리스트에서 이미 파괴된(죽은) 적들을 제거하여 현재 마리 수 최신화
        activeEnemies.RemoveAll(item => item == null);

        // 2. 현재 살아있는 적이 20마리 이상이면 스폰을 건너뜀
        if (activeEnemies.Count >= maxEnemyCount)
        {
            Debug.Log($"현재 적 수({activeEnemies.Count}마리)가 최대치에 도상하여 스폰을 대기합니다.");
            return;
        }

        // 3. 현재 스포너 위치에 프리랩 실시간 생성
        GameObject newEnemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);

        // 4. 생성된 적을 추적 리스트에 추가
        activeEnemies.Add(newEnemy);

        // 5. 생성된 객체에서 이동 스크립트를 컴포넌트로 가져옴
        TargetMove moveScript = newEnemy.GetComponent<TargetMove>();

        // 6. 스포너가 들고 있던 경로 배열을 생성된 객체에게 주입
        if (moveScript != null)
        {
            moveScript.SetupPath(spawnWaypoints);
        }
    }
    
    /// <summary>보스 소환 — StageCount에서 킬 목표 달성 시 호출</summary>
    public void SpawnBoss()
    {
        if (bossPrefab == null || spawnWaypoints.Length == 0)
        {
            Debug.LogWarning("[EnemyRespawn] bossPrefab 또는 spawnWaypoints가 없습니다.");
            return;
        }

        // 보스 소환 전 일반 몬스터 스폰 중단
        StopAllCoroutines();

        GameObject boss = Instantiate(bossPrefab, transform.position, Quaternion.identity);

        TargetMove moveScript = boss.GetComponent<TargetMove>();
        if (moveScript != null)
            moveScript.SetupPath(spawnWaypoints);

        Debug.Log("[EnemyRespawn] 보스 소환!");
    }

    // ──────────────────────────────────────────────
    //  내부 유틸 (코루틴 루프)
    // ──────────────────────────────────────────────

    /// <summary>
    /// 무한 루프를 돌며 respawnDelay(3초)마다 SpawnEnemy를 호출합니다.
    /// </summary>
    private IEnumerator RespawnLoop()
    {
        // 게임이 시작되고 첫 스폰 전 약간의 대기시간을 주거나, 즉시 스폰하고 싶다면 순서를 바꾸면 됩니다.
        while (true)
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnEnemy();
        }
    }
}
