using System.Collections.Generic;
using UnityEngine;

public partial class Enemy : MonoBehaviour, ITakeDamage
{
    // ── 활성 적 목록 (Player.FindTarget / StageManager.NextStage용) ──
    public static readonly List<Enemy> Active = new List<Enemy>(64);

    [Header("Enemy 스탯")]
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float defence   = 5f;
    [SerializeField] private int rewardGold = 10;
    [SerializeField] private int rewardExp  = 5;

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

        CacheDebuffRefs();   // ★ 추가 — SpriteRenderer / TargetMove / 원본 색 캐싱

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

    /// <summary>
    /// 풀에서 꺼낸 직후 호출. Awake를 대신해 재사용 상태를 완전히 초기화합니다.
    /// SetActive(true) 이전에 호출하세요.
    /// </summary>
    public virtual void OnSpawnFromPool(float mult)
    {
        isDead          = false;
        returnedToPool  = false;
        hpBarObject     = null;
        hpBarController = null;

        // 콜라이더 복구 (Die에서 껐던 것)
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = true;

        // ★ 사망 연출로 바뀐 머티리얼 / 투명도 복구
        deathEffect?.ResetState();

        ResetDebuffs();          // ★ 디버프 partial에 구현 필요
        ApplyStatMultiplier(mult);
    }

    protected virtual void InitStats()
    {
        currentHealth = maxHealth;
        isDead        = false;
    }

    /// <summary>
    /// ★ 곱셈 누적이 아니라 원본 기준값에서 매번 새로 계산합니다.
    ///   기존처럼 maxHealth *= mult 로 두면 재사용 시 체력이 무한히 불어납니다.
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

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // ★ 사망 연출 동안 아직 살아있는 것처럼 취급되지 않도록 즉시 목록에서 제외.
        //   (OnDisable에서도 Remove하지만, 없는 항목 제거는 안전합니다)
        Active.Remove(this);

        // 늦게 들어오는 총알 충돌로 중복 처리되는 것 방지
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = false;

        // ★ 반드시 사망 연출보다 먼저. 회색 머티리얼이 '원본'으로 캐싱되는 걸 막습니다.
        GrayscaleEffect.Clear(gameObject);

        StopAllDebuffs();
        RemoveHpBar();

        // 보상과 카운트는 연출을 기다리지 않고 즉시 처리 (반응성 유지)
        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(rewardGold * statMult));
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(rewardExp * statMult));

        // ★ StageManager.ReportEnemyKill 내부에서 GuideQuest를 호출하므로
        //   여기서 GuideQuestManager를 직접 부르면 2중 카운트됨
        StageManager.Instance?.ReportEnemyKill();
        MissionManager.Instance?.ReportEnemyKill();

        // 연출이 끝난 뒤 풀로 반환
        if (useDeathEffect && deathEffect != null && gameObject.activeInHierarchy)
            deathEffect.Play(ReturnToPool);
        else
            ReturnToPool();
    }

    /// <summary>Destroy 대신 풀로 반환</summary>
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