using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float maxHealth  = 100f;
    [SerializeField] private   int   rewardGold = 10;
    [SerializeField] private   int   rewardExp  = 5;

    [Header("이동 속도")]
    [SerializeField] private float moveSpeed = 3f;      // ✅ 기본 이동 속도

    protected float currentHealth;
    public    bool  isDead { get; set; }

    private GameObject hpBarObject;
    private EnemyHpBar hpBarController;

    private float originalMoveSpeed;                    // ✅ 원래 속도 저장용
    private Coroutine slowCoroutine;                    // ✅ 둔화 코루틴 중복 방지

    // ──────────────────────────────────────────────
    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject     = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    void Awake() { InitStats(); }

    protected virtual void InitStats()
    {
        currentHealth    = maxHealth;
        isDead           = false;
        originalMoveSpeed = moveSpeed;                  // ✅ 원래 속도 저장
    }

    public virtual void ApplyStatMultiplier(float mult)
    {
        maxHealth     *= mult;
        currentHealth  = maxHealth;
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    // ──────────────────────────────────────────────
    //  데미지
    // ──────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        hpBarController?.UpdateHp(currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    // ──────────────────────────────────────────────
    //  둔화
    // ──────────────────────────────────────────────

    /// <summary>SkillSlow에서 호출. 이미 둔화 중이면 갱신됩니다.</summary>
    // ──────────────────────────────────────────────
//  둔화
// ──────────────────────────────────────────────
    public void ApplySlow(float slowRate, float duration)
    {
        if (isDead) return;

        TargetMove moveScript = GetComponent<TargetMove>();
        if (moveScript == null) return;

        // 기존 둔화 코루틴 취소 후 갱신
        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowCoroutine = StartCoroutine(SlowCoroutine(moveScript, slowRate, duration));
    }

    private IEnumerator SlowCoroutine(TargetMove moveScript, float slowRate, float duration)
    {
        // ✅ 둔화 적용 직전 현재 속도를 저장
        float originalSpeed = moveScript.GetSpeed();
        float slowedSpeed   = originalSpeed * (1f - slowRate);

        moveScript.SetSpeed(slowedSpeed);
        Debug.Log($"[Enemy] 둔화 적용 — {originalSpeed} → {slowedSpeed} ({duration}초)");

        yield return new WaitForSeconds(duration);

        // ✅ 저장해둔 원래 속도로 복원
        moveScript.SetSpeed(originalSpeed);
        slowCoroutine = null;
        Debug.Log($"[Enemy] 둔화 해제 — 속도 복원: {originalSpeed}");
    }

    // ──────────────────────────────────────────────
    //  사망
    // ──────────────────────────────────────────────
    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);   // ✅ 사망 시 코루틴 정리

        RemoveHpBar();
        CurrencyManager.Instance?.AddGold(rewardGold);
        LevelUpManager.Instance?.AddExp(rewardExp);
        StageManager.Instance?.ReportEnemyKill();

        Destroy(gameObject);
    }

    protected void RemoveHpBar()
    {
        if (hpBarObject != null)
            Destroy(hpBarObject);
    }
}