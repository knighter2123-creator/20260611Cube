using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 적의 공통 기반 클래스.
///
/// ★ 이번 수정: Die() 안의 "보상 지급"과 "처치 보고"를 가상 메서드로 분리
///
/// ─── 왜 이렇게 바꾸나? (핵심 학습 포인트: 템플릿 메서드 패턴) ─────────────
///
/// 지금까지 BossMonster와 EvolveBoss는 Die()를 통째로 재정의(override)했습니다.
/// 그런데 Die()가 하는 일은 사실 7가지나 됩니다:
///
///   ① isDead 플래그 세우기        ② Active 목록에서 빼기
///   ③ 콜라이더 끄기               ④ 회색 이펙트 정리
///   ⑤ 디버프 정리 / HP바 제거     ⑥ 보상 지급 + 처치 보고
///   ⑦ 사망 연출 후 풀에 반환
///
/// 이 중 자식 클래스마다 다른 건 ⑥ 하나뿐인데, Die()를 통째로 덮어쓰면
/// 나머지 6개를 전부 다시 써야 합니다. 그리고 실제로 빠뜨렸어요 —
/// BossMonster.Die()에는 ③④⑦이 없어서 보스가 풀로 안 돌아가고 Destroy됩니다.
///
/// 해결책: "뼈대(Die)는 부모가 고정해두고, 달라지는 부분만 구멍(virtual)을 뚫어준다."
/// 이걸 템플릿 메서드 패턴이라고 부릅니다. 자식은 구멍만 채우면 되고,
/// 나중에 Die()에 ⑧번 단계를 추가해도 모든 자식이 자동으로 혜택을 봅니다.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public partial class Enemy : MonoBehaviour, ITakeDamage
{
    // ── 활성 적 목록 (Player.FindTarget / StageManager.NextStage용) ──
    public static readonly List<Enemy> Active = new List<Enemy>(64);

    [Header("Enemy 스탯")]
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float defence   = 5f;

    // ★ private → protected 로 변경: 자식(BossMonster 등)이 보상 계산에 쓸 수 있도록
    [SerializeField] protected int rewardGold = 10;
    [SerializeField] protected int rewardExp  = 5;

    [Header("사망 연출")]
    [Tooltip("사망 시 녹색으로 물들며 사라지는 연출을 사용합니다")]
    [SerializeField] private bool useDeathEffect = true;

    protected float statMult = 1f;   // 스폰 시 받은 누적 배율

    // ★ 풀 재사용 대비 원본 기준값 (프리팹 인스펙터 값을 Awake에서 1회 보관)
    protected float baseMaxHealth;
    protected float baseDefence;

    public bool isDead { get; set; }

    private GameObject   hpBarObject;
    private EnemyHpBar   hpBarController;
    private Collider2D[] cachedColliders;

    private EnemyDeathEffect deathEffect;
    private bool returnedToPool;      // 이중 반환 방지

    void Awake()
    {
        baseMaxHealth   = maxHealth;
        baseDefence     = defence;
        cachedColliders = GetComponentsInChildren<Collider2D>(true);

        CacheDebuffRefs();   // SpriteRenderer / TargetMove / 원본 색 캐싱

        if (useDeathEffect)
            deathEffect = EnemyDeathEffect.GetOrAdd(gameObject);
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject     = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    /// <summary>풀에서 꺼낸 직후 호출. 이 함수는 override하지 마세요.</summary>
    public void OnSpawnFromPool(float mult)   // ← virtual 제거
    {
        isDead          = false;
        returnedToPool  = false;
        hpBarObject     = null;
        hpBarController = null;

        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = true;

        deathEffect?.ResetState();
        ResetDebuffs();
        ApplyStatMultiplier(mult);

        OnResetForSpawn();   // ★ 자식이 채우는 구멍
    }

    /// <summary>훅 — 자식이 추가한 상태를 초기화. 기본은 아무것도 안 함.</summary>
    protected virtual void OnResetForSpawn() { }

    /// <summary>
    /// ★ 원본 기준값에서 매번 새로 계산합니다.
    ///   maxHealth *= mult 로 두면 풀 재사용 시 체력이 무한히 불어납니다.
    ///   (자식 클래스에서 override할 때도 이 원칙을 반드시 지키세요 —
    ///    "누적(*=)이 아니라 대입(=)")
    /// </summary>
    public virtual void ApplyStatMultiplier(float mult)
    {
        statMult      = mult;
        maxHealth     = baseMaxHealth * mult;
        defence       = baseDefence;      // 디버프로 변형됐을 수 있으므로 복구
        currentHealth = maxHealth;
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    // 일반 데미지 (스킬용 — 크리티컬 없음)
    public virtual void TakeDamage(float damage)
    {
        TakeDamage(damage, isCritical: false);
    }

    public virtual void TakeDamage(float damage, bool isCritical)
    {
        if (isDead) return;

        float reducedDefence = defence / _armorBreakMultiplier;
        float finalDamage    = Mathf.Max(damage - reducedDefence, 0f);

        currentHealth -= finalDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        hpBarController?.UpdateHp(currentHealth, maxHealth);

        DamageTextPool.Instance?.ShowDamage(
            transform.position,
            Mathf.RoundToInt(finalDamage),
            isCritical
        );

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// 사망 처리의 "뼈대".
    ///
    /// ★ 이제 자식 클래스는 이 함수를 override하지 마세요.
    ///   대신 아래 두 개의 훅(GrantRewards / ReportKill)만 override하면 됩니다.
    ///   그래야 콜라이더 정리·풀 반환 같은 필수 단계를 빠뜨리지 않습니다.
    ///
    /// (virtual을 그대로 둔 이유는 기존 코드 호환 때문입니다. 하지만
    ///  꼭 필요한 경우가 아니면 override하지 않는 것을 권합니다.)
    /// </summary>
    protected void Die()
    {
        if (isDead) return;
        isDead = true;

        // 사망 연출 동안 아직 살아있는 것처럼 취급되지 않도록 즉시 목록에서 제외.
        Active.Remove(this);

        // 늦게 들어오는 총알 충돌로 중복 처리되는 것 방지
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = false;

        // 반드시 사망 연출보다 먼저. 회색 머티리얼이 '원본'으로 캐싱되는 걸 막습니다.
        GrayscaleEffect.Clear(gameObject);

        StopAllDebuffs();
        RemoveHpBar();

        // ★ 여기가 자식마다 달라지는 부분 — 훅으로 분리
        GrantRewards();
        ReportKill();

        // 연출이 끝난 뒤 풀로 반환
        if (useDeathEffect && deathEffect != null && gameObject.activeInHierarchy)
            deathEffect.Play(ReturnToPool);
        else
            ReturnToPool();
    }

    /// <summary>
    /// 훅 ①: 처치 보상 지급. 잡몹은 골드 + 경험치.
    /// 보스는 이걸 override해서 보석·추가 배율을 주면 됩니다.
    /// </summary>
    protected virtual void GrantRewards()
    {
        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(rewardGold * statMult));
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(rewardExp * statMult));
    }

    /// <summary>
    /// 훅 ②: 처치 사실을 각 매니저에 보고.
    /// 잡몹은 StageManager.ReportEnemyKill(킬 카운트 증가),
    /// 보스는 ReportBossKill(스테이지 클리어)로 갈라집니다.
    ///
    /// ※ StageManager.ReportEnemyKill 내부에서 GuideQuest를 호출하므로
    ///   여기서 GuideQuestManager를 직접 부르면 2중 카운트됩니다.
    /// </summary>
    protected virtual void ReportKill()
    {
        StageManager.Instance?.ReportEnemyKill();
        MissionManager.Instance?.ReportEnemyKill();
    }

    /// <summary>
    /// Destroy 대신 풀로 반환.
    ///
    /// ※ 풀에서 나온 오브젝트가 아니면(직접 Instantiate된 진화 보스 등)
    ///   ObjectPoolManager.Return이 알아서 Destroy해 줍니다.
    ///   그래서 모든 적이 이 함수 하나만 쓰면 됩니다.
    /// </summary>
    protected void ReturnToPool()
    {
        if (returnedToPool) return;   // 연출 콜백 + 강제 비활성화가 겹쳐도 한 번만
        returnedToPool = true;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Return(gameObject);
        else
            gameObject.SetActive(false);
    }

    public void RemoveHpBar()
    {
        if (hpBarObject != null)
            Destroy(hpBarObject);

        hpBarObject     = null;
        hpBarController = null;
    }
}