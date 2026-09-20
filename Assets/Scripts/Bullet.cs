using UnityEngine;

/// <summary>
/// ★ 이번 수정: 여러 발을 부채꼴로 동시에 쏘는 LaunchVolley() 추가
///
/// 기존 Launch()는 그대로 남아 있고, 내부적으로 LaunchVolley(..., count:1)를 부릅니다.
/// 즉 이 파일 밖에서 Bullet.Launch를 쓰던 코드(동료 타워 등)는 한 줄도 안 고쳐도 됩니다.
///
/// ─── 이게 왜 중요한가? (학습 포인트) ────────────────────────────────
/// 기능을 추가할 때 기존 함수의 '시그니처(이름+매개변수)'를 바꾸면,
/// 그 함수를 부르던 모든 곳이 컴파일 에러가 납니다. 급하면 전부 고치게 되고,
/// 그 과정에서 관계없는 코드에 실수를 심습니다.
///
/// 대신 새 함수를 만들고 기존 함수를 그 위의 얇은 껍데기로 남기면,
///   · 기존 호출부는 전혀 영향 없음
///   · 새 기능이 필요한 곳만 새 함수를 씀
///   · 나중에 전부 옮겨간 게 확인되면 옛 함수를 지우면 됨
/// 이걸 "하위 호환을 유지하는 확장"이라고 하고, 남의 코드를 고칠 때의 기본기입니다.
/// ──────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet 설정")]
    [SerializeField] private float speed    = 15f;
    [SerializeField] private float lifeTime = 3f;

    private float       damage;
    private bool        isCritical;
    private Vector2     direction;
    private float       timer;
    private Rigidbody2D rb;
    private bool        isReturned;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
    }

    void OnEnable()
    {
        timer      = 0f;
        isReturned = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnDisable()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // ══════════════════════════════════════════════════════════════
    //  발사
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 기존 호출부 호환용 — 1발만 발사합니다.
    /// (동료 타워 등 각성과 무관한 곳은 계속 이걸 쓰면 됩니다)
    /// </summary>
    public static void Launch(Enemy target, Transform firePoint, PlayerStat stat)
        => LaunchVolley(target, firePoint, stat, 1, 0f);

    /// <summary>
    /// 타겟 방향을 중심으로 count발을 부채꼴로 동시에 발사합니다.
    /// </summary>
    /// <param name="count">동시 발사체 수 (1 이상)</param>
    /// <param name="spreadAngle">발사체 사이의 각도 간격(도). count가 1이면 무시됩니다.</param>
    public static void LaunchVolley(Enemy target, Transform firePoint, PlayerStat stat,
                                    int count, float spreadAngle)
    {
        // 방어 가드
        if (target == null || target.isDead) return;
        if (stat == null) return;
        if (firePoint == null) return;
        if (ObjectPoolManager.Instance == null) return;

        count = Mathf.Max(1, count);

        // ★ 증강 ─────────────────────────────────────────────────────────
        // 공격력은 세 겹의 곱으로 결정됩니다.
        //     baseDamage    강화로 올리는 순수 기본값 (세이브에 저장되는 유일한 값)
        //   × 증강 배율      AugmentManager — FinalDamage 프로퍼티가 대신 곱해줍니다
        //   × 버프 배율      PlayerBuffManager.DamageMultiplier (각성 영구 버프가 여기 쌓입니다)
        //
        // 각성의 '발사체/연사'는 이 곱셈 레이어에 끼어들지 않습니다.
        // 데미지를 키우는 게 아니라 '발사 횟수'를 늘리는 별개의 축이라서,
        // 기존 세 레이어를 전혀 건드리지 않고 위에 얹을 수 있습니다.
        // ────────────────────────────────────────────────────────────────
        float buffMult     = PlayerBuffManager.Instance?.DamageMultiplier ?? 1f;
        float buffedDamage = stat.FinalDamage * buffMult;

        Vector2 spawnPos = (Vector2)firePoint.position;
        Vector2 toTarget = (Vector2)target.transform.position - spawnPos;

        // 플레이어와 적이 정확히 겹치면 normalized가 (0,0)이 되어 총알이 제자리에 섭니다.
        Vector2 baseDir   = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
        float   baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + FanOffset(i, spreadAngle);
            SpawnOne(spawnPos, angle, buffedDamage, stat);
        }
    }

    /// <summary>
    /// i번째 발사체의 각도 오프셋을 계산합니다.
    ///
    /// ═══ 왜 '가운데 정조준'을 반드시 남기는가? (중요) ═══════════════════
    ///
    /// 처음엔 좌우 대칭으로 균등 분배하는 게 자연스러워 보입니다.
    ///     2발 → [-s/2, +s/2]
    ///     3발 → [-s,  0,  +s]
    ///
    /// 그런데 짝수 발일 때 **가운데가 비어버립니다.** 각도 12도, 사거리 10 기준으로
    /// 각 발이 중심에서 약 1.05 유닛씩 벗어나는데, 적 콜라이더가 그보다 작으면
    /// **두 발 다 빗나갑니다.** "발사체 +1 보상을 받았는데 보스 DPS가 오히려 0이 되는"
    /// 최악의 상황이 나옵니다.
    ///
    /// 그래서 0번은 항상 정조준(0도)으로 두고, 나머지를 좌우로 번갈아 붙입니다.
    ///     1발 → [0]
    ///     2발 → [0, +s]
    ///     3발 → [0, +s, -s]
    ///     4발 → [0, +s, -s, +2s]
    ///
    /// 짝수 발일 때 좌우가 살짝 비대칭해 보이지만, 그 대가로
    /// **단일 타겟 DPS가 발사체 수에 정확히 비례**합니다. 밸런스를 잡을 때
    /// "예측 가능한 숫자"가 "보기 좋은 모양"보다 훨씬 중요합니다.
    ///
    /// ★ 일반화하면: 보상은 어떤 상황에서도 최소한 손해가 되면 안 됩니다.
    ///   플레이어가 "이거 받고 나서 더 약해진 것 같은데?"라고 느끼는 순간
    ///   성장 시스템 전체의 신뢰가 무너집니다.
    /// ══════════════════════════════════════════════════════════════════
    /// </summary>
    private static float FanOffset(int index, float spreadAngle)
    {
        if (index == 0) return 0f;          // 0번은 언제나 정조준

        int   pair = (index + 1) / 2;       // 1,1,2,2,3,3...
        float sign = (index % 2 == 1) ? 1f : -1f;

        return spreadAngle * pair * sign;
    }

    /// <summary>총알 1발을 풀에서 꺼내 지정한 각도로 쏩니다.</summary>
    private static void SpawnOne(Vector2 spawnPos, float angleDeg, float buffedDamage, PlayerStat stat)
    {
        // ★ 크리티컬은 '발사체마다' 따로 굴립니다.
        //   한 번 굴려서 전부 크리 / 전부 논크리로 하면 데미지가 크게 출렁입니다.
        //   발마다 굴리면 표본이 늘어 기대값에 수렴해서 체감이 훨씬 안정적이에요.
        //   (각성 후에 크리 확률 스탯의 가치가 오히려 올라가는 효과도 있습니다)
        //
        // ★ stat.Critical 이 아니라 stat.FinalCritical 을 씁니다. (이번에 고친 부분)
        //
        //   PlayerStat 에는 두 값이 있습니다.
        //     Critical       — 강화로 올린 순수 확률
        //     FinalCritical  — Min(Critical + 증강 보너스, 100) ← 스탯창이 보여주는 값
        //
        //   지금은 치명타 확률 증강 카드가 없어서 두 값이 완전히 같습니다.
        //   하지만 PlayerStat 주석대로 나중에 그 카드를 추가하면,
        //   스탯창에는 오른 확률이 뜨는데 실제 발사는 옛 값으로 굴립니다.
        //
        //   "화면에 보이는 수치와 실제 동작이 다른 것은 가장 나쁜 종류의 버그"라고
        //   PlayerStat 에 직접 적어두셨죠. 지금 바꿔두면 그 버그가 태어나지 않습니다.
        //   (오늘 기준으로는 동작이 100% 동일하므로 위험이 없는 변경입니다)
        bool  isCritical  = Random.Range(0f, 100f) < stat.FinalCritical;
        float finalDamage = isCritical
            ? buffedDamage * stat.FinalCriticalMultiplier
            : buffedDamage;

        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();

        // ★ 파괴된 인스턴스 방어.
        //   유니티에서 Destroy된 오브젝트는 C#의 진짜 null이 아니지만
        //   == null 비교는 true가 나옵니다. 이 검사가 없으면 아래 TryGetComponent에서
        //   MissingReferenceException이 터집니다.
        //   (근본 해결은 ObjectPoolManager.GetBulletInactive 쪽 — 별도 파일 참고)
        if (bulletObj == null) return;

        if (!bulletObj.TryGetComponent(out Bullet bullet))
        {
            Debug.LogError("[Bullet] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

        // 각도 → 방향 벡터.
        // (부채꼴이라 타겟 좌표를 다시 쓰면 안 되고, 반드시 각도로 계산해야 합니다)
        float   rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        bulletObj.transform.SetPositionAndRotation(
            (Vector3)spawnPos, Quaternion.Euler(0f, 0f, angleDeg));

        bulletObj.SetActive(true);
        bullet.Init(dir, finalDamage, isCritical);
    }

    // ══════════════════════════════════════════════════════════════
    //  인스턴스 동작
    // ══════════════════════════════════════════════════════════════

    public void Init(Vector2 dir, float dmg, bool crit = false)
    {
        direction         = dir.normalized;
        damage            = dmg;
        isCritical        = crit;
        timer             = 0f;
        isReturned        = false;
        rb.linearVelocity = direction * speed;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            ReturnToPool();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out Enemy target)) return;
        if (target.isDead) return;

        target.TakeDamage(damage, isCritical);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturned) return;
        isReturned = true;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnBullet(gameObject);
        else
            gameObject.SetActive(false);
    }
}