using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스폰 전담 매니저.
///
/// [핵심 설계 원칙]
///  "첫 번째 SpawnEnemy()는 반드시 이벤트 구독 완료 이후에 호출된다."
///
///  Start()에서 이벤트 구독을 완료한 뒤
///  StageManager.RequestFirstSpawn()을 호출하여
///  첫 번째 몬스터 소환을 요청합니다.
///
///  이렇게 하면 Enemy가 소환되는 시점에 OnEnemyDied 구독이
///  반드시 완료된 상태이므로, Enemy 사망 시 HandleEnemyDied()가
///  정상적으로 호출됩니다.
/// </summary>
public class EnemyRespawn : MonoBehaviour
{
    public static EnemyRespawn Instance;

    [Header("이 프리랩이 따라갈 이동 경로")]
    public Transform[] spawnWaypoints; 
    
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab; // 추후 인라인 보스 방식 확장용

    [Tooltip("일반 몬스터 처치 후 다음 소환까지 대기 시간 (초)")]
    [SerializeField] private float respawnDelay = 1f;

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnEnemy();
    }

    void Update()
    {
        RespawnCoroutine();
    }
    
    // ──────────────────────────────────────────────
    //  스폰 API
    // ──────────────────────────────────────────────

    /// <summary>
    /// 일반 몬스터를 즉시 소환합니다.
    /// 호출 전에 반드시 이벤트 구독이 완료된 상태여야 합니다.
    /// </summary>
    void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnWaypoints.Length == 0) return;

        // 1. 현재 스포너 위치에 프리랩 실시간 생성
        GameObject newEnemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);

        // 2. 생성된 객체에서 이동 스크립트를 컴포넌트로 가져옴
        TargetMove moveScript = newEnemy.GetComponent<TargetMove>();

        // 3. 스포너가 들고 있던 경로 배열을 생성된 객체에게 주입
        if (moveScript != null)
        {
            moveScript.SetupPath(spawnWaypoints);
        }
    }

    // ──────────────────────────────────────────────
    //  내부 유틸
    // ──────────────────────────────────────────────

    /// <summary>
    /// respawnDelay 후 일반 몬스터를 소환합니다.
    /// 대기 중 보스 페이즈로 전환됐으면 소환을 취소합니다.
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnEnemy();
    }
}