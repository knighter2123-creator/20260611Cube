using System.Collections;
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

    protected float statMult = 1f;   // 스폰 시 받은 누적 배율

    // ★ 풀 재사용 대비 원본 기준값 (프리팹 인스펙터 값을 Awake에서 1회 보관)
    protected float baseMaxHealth;
    protected float baseDefence;

    public bool isDead { get; set; }

    private GameObject   hpBarObject;
    private EnemyHpBar   hpBarController;
    private Collider2D[] cachedColliders;

    void Awake()
    {
        baseMaxHealth   = maxHealth;
        baseDefence     = defence;
        cachedColliders = GetComponentsInChildren<Collider2D>(true);

        CacheDebuffRefs();   // ★ 추가 — SpriteRenderer / TargetMove / 원본 색 캐싱
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
        hpBarObject     = null;
        hpBarController = null;

        // 콜라이더 복구 (Die에서 껐던 것)
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = true;

        ResetDebuffs();          // ★ 디버프 partial에 구현 필요 (아래 주의사항 참고)
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

        // 늦게 들어오는 총알 충돌로 중복 처리되는 것 방지
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = false;

        StopAllDebuffs();
        RemoveHpBar();
        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(rewardGold * statMult));
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(rewardExp * statMult));

        // ★ StageManager.ReportEnemyKill 내부에서 GuideQuest를 호출하므로
        //   여기서 GuideQuestManager를 직접 부르면 2중 카운트됨
        StageManager.Instance?.ReportEnemyKill();
        MissionManager.Instance?.ReportEnemyKill();

        ReturnToPool();
    }

    /// <summary>Destroy 대신 풀로 반환</summary>
    protected void ReturnToPool()
    {
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