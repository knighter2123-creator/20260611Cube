using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ★ 이번 수정: 각성 효과(동시 발사체 / 연사) 반영
///
/// 바뀐 곳은 세 군데뿐입니다.
///   1) 각성 연출용 인스펙터 값 2개 추가 (연사 간격 / 부채꼴 각도)
///   2) HandleAttack()에 연사 중복 방지 가드 한 줄
///   3) Bullet.Launch 직접 호출 → FireOnce() 경유로 변경
///
/// 타겟 탐색, 쿨타임 계산, 스탯 초기화 로직은 전혀 건드리지 않았습니다.
/// </summary>
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private Transform  firePoint;

    [Header("타겟 갱신")]
    [SerializeField] private float retargetInterval = 0.1f;   // 재탐색 주기(초)

    // ══════════════════════════════════════════════════════════════
    //  ★ 각성 발사 설정 (여기부터 추가)
    // ══════════════════════════════════════════════════════════════
    [Header("각성 - 발사 연출")]
    [Tooltip("연사(연속 공격)일 때 발과 발 사이 간격(초).\n" +
             "쿨타임보다 길어지지 않도록 코드가 자동으로 조여줍니다.")]
    [SerializeField] private float burstInterval = 0.08f;

    [Tooltip("동시 발사체가 2발 이상일 때 발 사이의 각도(도).\n" +
             "작게(5~8) 두면 거의 한 점에 모이고, 크게(15~25) 두면 넓게 퍼져 여러 적을 칩니다.")]
    [SerializeField] private float spreadAngle = 12f;
    // ══════════════════════════════════════════════════════════════

    private float retargetTimer;

    private Enemy currentTarget;
    private float attackTimer;
    private bool  isFirstLoad;

    // ★ 연사 상태. 연사가 끝나기 전에 다음 공격이 시작되면 총알이 겹쳐 쏟아집니다.
    private Coroutine burstRoutine;
    private bool      isBursting;

    public PlayerStat stat => playerStat;

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

        attackTimer = 0f;

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    /// <summary>
    /// ★ 추가 — 씬 전환이나 오브젝트 비활성화로 연사 코루틴이 중간에 끊겼을 때
    ///   isBursting이 true로 굳어 영영 공격을 못 하는 상태를 막습니다.
    ///
    ///   유니티는 오브젝트가 꺼지면 코루틴을 '조용히' 멈춥니다. 예외도, 로그도 없어요.
    ///   그래서 코루틴으로 플래그를 관리할 땐 OnDisable에서 되돌리는 게 습관이 돼야 합니다.
    ///   (원인 찾기가 정말 어려운 버그 유형입니다)
    /// </summary>
    void OnDisable()
    {
        if (burstRoutine != null) StopCoroutine(burstRoutine);
        burstRoutine = null;
        isBursting   = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        retargetTimer += Time.deltaTime;

        // 타겟이 사라졌거나 주기가 됐을 때만 재탐색
        if (currentTarget == null || currentTarget.isDead || retargetTimer >= retargetInterval)
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
        // ★ 리터럴 0.1f 대신 PlayerStat.MIN_ATTACK_COOLDOWN 을 씁니다.
        //   PlayerStat 에 "Player.HandleAttack 의 Mathf.Max 와 같은 값"이라고 적혀 있는데,
        //   같은 숫자가 두 파일에 흩어져 있으면 한쪽만 고쳐질 때를 막을 수 없습니다.
        //   스탯창의 FinalAttackCooldown 도 같은 상수를 쓰므로, 이제 표시와 실제가
        //   구조적으로 어긋날 수 없습니다.
        float cooldown = Mathf.Max(stat.attackCooldown, PlayerStat.MIN_ATTACK_COOLDOWN);

        if (attackTimer < cooldown)
        {
            attackTimer += Time.deltaTime;
            return;
        }

        // ★ 추가 — 연사가 아직 진행 중이면 새 공격을 시작하지 않습니다.
        //   attackTimer는 리셋하지 않으므로, 연사가 끝나는 즉시 다음 공격이 나갑니다.
        if (isBursting) return;

        // 쿨타임은 찼지만 타겟이 없음 → 타이머를 리셋하지 않고 유지(=발사 대기 상태)
        if (currentTarget == null || currentTarget.isDead) return;

        attackTimer = 0f;
        FireOnce(cooldown);
    }

    // ══════════════════════════════════════════════════════════════
    //  ★ 각성 발사 (추가)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 각성 단계를 반영해 한 번의 '공격'을 수행합니다.
    ///
    /// 공격 1회 = (연사 횟수) × (동시 발사체 수) 발
    ///   예) 3단계에서 2발 동시 + 2연타 → 한 번 공격에 4발
    /// </summary>
    private void FireOnce(float cooldown)
    {
        AwakeningManager am = AwakeningManager.Instance;

        // ★ 매니저가 없어도 게임이 멈추지 않게 기본값(1발 1연사)으로 떨어집니다.
        //   각성 시스템을 아직 씬에 안 붙였거나, EvolveScene 단독 테스트 중일 때를 위해서예요.
        int volleys   = am != null ? am.VolleysPerAttack     : 1;
        int perVolley = am != null ? am.ProjectilesPerVolley : 1;

        // 연사가 없으면 코루틴을 만들 이유가 없습니다.
        // (코루틴 생성도 공짜는 아니고, 무엇보다 디버깅할 때 콜스택이 단순해집니다)
        if (volleys <= 1)
        {
            Bullet.LaunchVolley(currentTarget, firePoint, playerStat, perVolley, spreadAngle);
            return;
        }

        // ★ 혹시 이전 연사가 남아 있으면 먼저 정리합니다.
        //   StopCoroutine은 코루틴을 '즉시 중단'시키므로, 그 안의
        //   isBursting = false 줄에 영영 도달하지 못합니다.
        //   그래서 멈추는 쪽에서 플래그를 직접 내려줘야 합니다.
        //   (코루틴으로 플래그를 관리할 때 가장 흔하게 빠뜨리는 부분입니다)
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
            isBursting   = false;
        }

        burstRoutine = StartCoroutine(BurstRoutine(volleys, perVolley, cooldown));
    }

    /// <summary>연사 — 짧은 간격으로 volleys번 발사합니다.</summary>
    private IEnumerator BurstRoutine(int volleys, int perVolley, float cooldown)
    {
        isBursting = true;

        // ─── 간격 자동 조임 (중요) ────────────────────────────────────
        // 공격속도 강화를 많이 하면 쿨타임이 0.1초까지 내려갑니다.
        // 그 상태에서 0.08초 × 2연타 = 0.16초면 연사가 쿨타임보다 길어져,
        // 공격속도를 올릴수록 오히려 DPS가 떨어지는 역전 현상이 생깁니다.
        //
        // 그래서 "연사 전체가 쿨타임의 80% 안에 끝난다"를 보장합니다.
        // 남는 20%는 프레임 흔들림에 대한 여유분이에요.
        //
        // ★ 이런 '상한을 코드가 보장하는' 처리를 해두면, 인스펙터에서 누가
        //   burstInterval을 0.5로 잘못 넣어도 게임이 망가지지 않습니다.
        //   인스펙터 값은 언젠가 잘못 들어간다고 가정하는 편이 안전합니다.
        // ────────────────────────────────────────────────────────────
        float maxTotal = cooldown * 0.8f;
        float interval = Mathf.Min(burstInterval, maxTotal / Mathf.Max(1, volleys - 1));

        for (int i = 0; i < volleys; i++)
        {
            // 첫 발에 적이 죽는 경우가 흔합니다. 남은 발을 허공에 버리지 않도록 재조준합니다.
            if (currentTarget == null || currentTarget.isDead)
            {
                FindTarget();
                if (currentTarget == null) break;   // 사거리 안에 적이 없으면 연사 중단
            }

            Bullet.LaunchVolley(currentTarget, firePoint, playerStat, perVolley, spreadAngle);

            if (i < volleys - 1)
                yield return new WaitForSeconds(interval);
            // ※ WaitForSeconds는 Time.timeScale의 영향을 받습니다.
            //   게임 배속(GameSpeedManager)을 올리면 연사도 같이 빨라지고,
            //   증강 카드창(timeScale = 0)에서는 함께 멈춥니다. 쿨타임 계산이
            //   Time.deltaTime을 쓰고 있으니 동일한 기준이라 일관적입니다.
        }

        isBursting   = false;
        burstRoutine = null;
    }

    // ══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stat != null ? stat.attackRange : 10f);
    }
}