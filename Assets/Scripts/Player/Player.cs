using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어 본체 — 가장 가까운 적을 쿨타임마다 공격, 공격 1회 = 연사 횟수 × 동시 발사체 수(각성 단계가 정함, 예: 2발 × 2연타 = 4발).</summary>
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private Transform firePoint;

    [Header("타겟 갱신")]
    [SerializeField] private float retargetInterval = 0.1f;   // 재탐색 주기(초)

    [Header("각성 - 발사 연출")]
    [Tooltip("연사(연속 공격)일 때 발과 발 사이 간격(초).\n" +
             "쿨타임보다 길어지지 않도록 코드가 자동으로 조여줍니다.")]
    [SerializeField] private float burstInterval = 0.08f;

    [Tooltip("동시 발사체가 2발 이상일 때 발 사이의 각도(도).\n" +
             "작게(5~8) 두면 거의 한 점에 모이고, 크게(15~25) 두면 넓게 퍼져 여러 적을 칩니다.")]
    [SerializeField] private float spreadAngle = 12f;

    /// <summary>연사 전체가 쿨타임의 이 비율 안에 끝나도록 간격을 조입니다. 나머지는 프레임 흔들림 여유분.</summary>
    private const float BURST_COOLDOWN_RATIO = 0.8f;

    private Enemy currentTarget;
    private float retargetTimer;
    private float attackTimer;

    // 연사 중인가(겹쳐 쏟아지지 않게) — burstRoutine != null 은 첫 yield 전에 끝난 코루틴도 남아 있어 플래그로 판단
    private Coroutine burstRoutine;
    private bool      isBursting;

    public PlayerStat stat => playerStat;

    private bool HasLiveTarget => currentTarget != null && !currentTarget.isDead;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
        if (!hasSave)
            playerStat.InitFull();

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    /// <summary>꺼질 때 코루틴이 조용히 멈춰 isBursting 이 true 로 굳어 영영 공격 못 하는 상태를 막음.</summary>
    void OnDisable() => StopBurst();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        retargetTimer += Time.deltaTime;

        // 타겟이 사라졌거나 주기가 됐을 때만 재탐색
        if (!HasLiveTarget || retargetTimer >= retargetInterval)
        {
            retargetTimer = 0f;
            FindTarget();
        }

        HandleAttack();
    }

    void FindTarget()
    {
        Vector2 myPos      = transform.position;
        float   closestSqr = stat.attackRange * stat.attackRange;  // 사거리 검사를 비교에 흡수
        Enemy   closest    = null;

        List<Enemy> list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)   // foreach 대신 for → 열거자 생성 없음
        {
            Enemy e = list[i];
            if (e == null || e.isDead) continue;

            float sqr = ((Vector2)e.transform.position - myPos).sqrMagnitude;
            if (sqr <= closestSqr)             // sqrt 생략
            {
                closestSqr = sqr;
                closest    = e;
            }
        }

        currentTarget = closest;
    }

    void HandleAttack()
    {
        // 표시용(PlayerStat.FinalAttackCooldown)과 같은 값을 써야 스탯창과 실제가 어긋나지 않습니다.
        float cooldown = stat.FinalAttackCooldown;

        if (attackTimer < cooldown)
        {
            attackTimer += Time.deltaTime;
            return;
        }

        // 연사 중이거나 타겟이 없으면 타이머를 유지한 채 대기 → 조건이 풀리는 즉시 다음 공격이 나갑니다.
        if (isBursting || !HasLiveTarget) return;

        attackTimer = 0f;
        PerformAttack(cooldown);
    }

    // ── 발사 ──

    /// <summary>각성 단계를 반영해 한 번의 '공격'을 수행합니다.</summary>
    private void PerformAttack(float cooldown)
    {
        // 매니저가 없어도(씬 단독 테스트 등) 기본값(1발 1연사)으로 동작합니다.
        AwakeningManager am = AwakeningManager.Instance;
        int volleys   = am != null ? am.VolleysPerAttack     : 1;
        int perVolley = am != null ? am.ProjectilesPerVolley : 1;

        // 연사가 없으면 코루틴을 만들 이유가 없습니다.
        if (volleys <= 1)
        {
            FireVolley(perVolley);
            return;
        }

        StopBurst();   // 혹시 남아 있는 이전 연사 정리
        burstRoutine = StartCoroutine(BurstRoutine(volleys, perVolley, cooldown));
    }

    /// <summary>연사 — 짧은 간격으로 volleys번 발사합니다.</summary>
    private IEnumerator BurstRoutine(int volleys, int perVolley, float cooldown)
    {
        isBursting = true;

        // 간격 자동 조임 — 연사가 쿨타임보다 길면 공격속도를 올릴수록 DPS 가 떨어지는 역전이 생김
        float interval = Mathf.Min(burstInterval, cooldown * BURST_COOLDOWN_RATIO / (volleys - 1));

        // WaitForSeconds(scaled) — 쿨타임과 같은 기준이라 배속엔 함께 빨라지고 카드창(timeScale 0)엔 함께 멈춤
        var wait = new WaitForSeconds(interval);

        for (int i = 0; i < volleys; i++)
        {
            // 첫 발에 적이 죽는 경우가 흔합니다. 남은 발을 허공에 버리지 않도록 재조준합니다.
            if (!HasLiveTarget)
            {
                FindTarget();
                if (currentTarget == null) break;   // 사거리 안에 적이 없으면 연사 중단
            }

            FireVolley(perVolley);

            if (i < volleys - 1)
                yield return wait;
        }

        isBursting   = false;
        burstRoutine = null;
    }

    private void FireVolley(int perVolley)
        => Bullet.LaunchVolley(currentTarget, firePoint, playerStat, perVolley, spreadAngle);

    /// <summary>연사 중단 — StopCoroutine 은 isBursting = false 줄에 못 가므로 여기서 직접 내림.</summary>
    private void StopBurst()
    {
        if (burstRoutine != null) StopCoroutine(burstRoutine);
        burstRoutine = null;
        isBursting   = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stat != null ? stat.attackRange : 10f);
    }
}
