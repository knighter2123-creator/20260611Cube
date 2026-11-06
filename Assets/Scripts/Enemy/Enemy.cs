using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>모든 적의 기반 클래스 — Die()의 7단계 뼈대는 고정하고 자식은 훅(OnResetForSpawn·ApplyStatMultiplier·GrantRewards·ReportKill)만 채우는 템플릿 메서드 패턴.</summary>
public partial class Enemy : MonoBehaviour, ITakeDamage
{
    // ── 활성 적 목록 (Player.FindTarget / StageManager.NextStage용) ──
    public static readonly List<Enemy> Active = new List<Enemy>(64);

    [Header("Enemy 스탯")]
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float defence   = 5f;

    [SerializeField] protected int rewardGold = 10;
    [SerializeField] protected int rewardExp  = 5;

    [Header("사망 연출")]
    [Tooltip("사망 애니메이션(EnemyDeathAnimator)을 재생한 뒤 사라집니다. 끄면 즉시 사라집니다")]
    [SerializeField] private bool useDeathEffect = true;

    protected float statMult = 1f;   // 스폰 시 받은 누적 배율 (체력·골드용)
    protected double expMult = 1.0;  // 스폰 시 스테이지 기준 경험치 배율 (체력 배율과 분리)

    // 풀 재사용 대비 원본 기준값 (프리팹 인스펙터 값을 Awake에서 1회 보관)
    protected float baseMaxHealth;
    protected float baseDefence;

    public bool isDead { get; set; }

    private GameObject   hpBarObject;
    private EnemyHpBar   hpBarController;
    private Collider2D[] cachedColliders;

    private EnemyDeathAnimator deathAnimator;
    private bool returnedToPool;      // 이중 반환 방지

    // Profiler 마커 — "Enemy.Die" 검색, 멈춘 프레임에서 작다면 렌더링 쪽 문제
    private static readonly ProfilerMarker DieMarker     = new ProfilerMarker("Enemy.Die");
    private static readonly ProfilerMarker CleanupMarker = new ProfilerMarker("Enemy.Die.Cleanup");
    private static readonly ProfilerMarker RewardMarker  = new ProfilerMarker("Enemy.Die.Rewards");
    private static readonly ProfilerMarker ReportMarker  = new ProfilerMarker("Enemy.Die.ReportKill");
    private static readonly ProfilerMarker EffectMarker  = new ProfilerMarker("Enemy.Die.Effect");

    void Awake()
    {
        baseMaxHealth   = maxHealth;
        baseDefence     = defence;
        cachedColliders = GetComponentsInChildren<Collider2D>(true);

        CacheDebuffRefs();   // SpriteRenderer / TargetMove / 원본 색 캐싱

        if (useDeathEffect)
            deathAnimator = EnemyDeathAnimator.GetOrAdd(gameObject);
    }

    void OnEnable()  => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject     = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
        RefreshHpBar();
    }

    /// <summary>풀에서 꺼낸 직후 호출. 자식은 이 함수 대신 OnResetForSpawn()을 override하세요.</summary>
    public void OnSpawnFromPool(float mult)
    {
        isDead          = false;
        returnedToPool  = false;
        hpBarObject     = null;
        hpBarController = null;

        SetCollidersEnabled(true);

        deathAnimator?.ResetState();
        ResetDebuffs();
        ApplyStatMultiplier(mult);
        expMult = StageFormula.CurrentExpMult();

        OnResetForSpawn();
    }

    /// <summary>훅 — 자식이 추가한 상태를 초기화. 기본은 아무것도 안 함.</summary>
    protected virtual void OnResetForSpawn() { }

    /// <summary>원본 기준값에서 매번 대입 계산 (*= 면 풀 재사용 때 체력이 무한히 불어남, override 도 동일 원칙).</summary>
    public virtual void ApplyStatMultiplier(float mult)
    {
        statMult      = mult;
        maxHealth     = baseMaxHealth * mult;
        defence       = baseDefence;      // 디버프로 변형됐을 수 있으므로 복구
        currentHealth = maxHealth;
        RefreshHpBar();
    }

    // 일반 데미지 (스킬용 — 크리티컬 없음)
    public virtual void TakeDamage(float damage)
    {
        TakeDamage(damage, isCritical: false);
    }

    public virtual void TakeDamage(float damage, bool isCritical)
    {
        if (isDead) return;

        // 방어력 감소 디버프 / 증강은 방어력에 "곱셈"으로 들어갑니다 (나눗셈이면 오히려 올라감).
        float effectiveDefence = defence * _armorBreakMultiplier * AugmentManager.EnemyDefense;
        float finalDamage      = Mathf.Max(damage - effectiveDefence, 0f);

        currentHealth = Mathf.Max(currentHealth - finalDamage, 0f);
        RefreshHpBar();

        // ?. 대신 != null — 파괴된 Instance 가 남을 수 있고 ?. 는 이를 못 걸러 MissingReferenceException
        DamageTextPool damageText = DamageTextPool.Instance;
        if (damageText != null)
        {
            int shown = Mathf.RoundToInt(finalDamage);
            if (_dotTick) damageText.ShowDoT(transform.position, shown);   // 독 틱 — Enemy.Debuffs.DotRoutine
            else          damageText.ShowDamage(transform.position, shown, isCritical);
        }

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>사망 처리 뼈대(virtual 아님) — 바꿀 부분은 훅으로만, 콜라이더 정리·풀 반환 같은 필수 단계 누락 방지.</summary>
    protected void Die()
    {
        if (isDead) return;
        isDead = true;

        using (DieMarker.Auto())
        {
            using (CleanupMarker.Auto())
            {
                // 사망 연출 동안 아직 살아있는 것처럼 취급되지 않도록 즉시 목록에서 제외.
                Active.Remove(this);

                // 늦게 들어오는 총알 충돌로 중복 처리되는 것 방지
                SetCollidersEnabled(false);

                // 반드시 사망 연출보다 먼저. 회색 머티리얼·디버프 색이 남은 채로 사망 프레임이 재생되는 걸 막습니다.
                GrayscaleEffect.Clear(gameObject);

                StopAllDebuffs();
                RemoveHpBar();
            }

            using (RewardMarker.Auto()) GrantRewards();
            using (ReportMarker.Auto()) ReportKill();

            // 연출이 끝난 뒤 풀로 반환
            using (EffectMarker.Auto())
            {
                if (useDeathEffect && deathAnimator != null && gameObject.activeInHierarchy)
                    deathAnimator.Play(ReturnToPool);
                else
                    ReturnToPool();
            }
        }
    }

    /// <summary>훅 ①: 처치 보상 — 잡몹은 골드+경험치, 보스는 override.</summary>
    protected virtual void GrantRewards()
    {
        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(rewardGold * statMult));
        LevelUpManager.Instance?.AddExp((long)Math.Round(rewardExp * expMult));
    }

    /// <summary>훅 ②: 처치 보고 — 잡몹은 ReportEnemyKill, 보스는 ReportBossKill.</summary>
    // ⚠ ReportEnemyKill 이 GuideQuest 를 부르므로 여기서 직접 부르면 2중 카운트
    protected virtual void ReportKill()
    {
        StageManager.Instance?.ReportEnemyKill();
        MissionManager.Instance?.ReportEnemyKill();
    }
    
    /// <summary>Destroy 대신 풀로 반환 (풀 출신이 아니면 Return 이 Destroy).</summary>
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
        {
            // HP 바는 HpBar 루트가 풀링합니다. 루트를 못 찾는 경우(구조 변경 등)에만 파괴.
            HpBar root = hpBarObject.GetComponentInParent<HpBar>(true);
            if (root != null) root.Release(hpBarObject);
            else              Destroy(hpBarObject);
        }

        hpBarObject     = null;
        hpBarController = null;
    }

    // HP 바는 적과 별개 오브젝트(HpBar 루트 아래)라 따로 파괴될 수 있어 유니티식 null 검사를 씁니다.
    private void RefreshHpBar()
    {
        if (hpBarController != null) hpBarController.UpdateHp(currentHealth, maxHealth);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < cachedColliders.Length; i++)
            cachedColliders[i].enabled = enabled;
    }
}
