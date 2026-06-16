using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float maxHealth  = 100f;
    [SerializeField] private   int   rewardGold = 10;
    [SerializeField] private   int   rewardExp  = 5;

    // ❌ moveSpeed 제거 (TargetMove.speed로 관리)
    // ❌ originalMoveSpeed 제거 (SlowCoroutine에서 직접 저장)

    protected float currentHealth;
    public    bool  isDead { get; set; }

    private GameObject hpBarObject;
    private EnemyHpBar hpBarController;

    private Coroutine slowCoroutine;

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject     = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    void Awake() { InitStats(); }

    protected virtual void InitStats()
    {
        currentHealth = maxHealth;
        isDead        = false;
        // ❌ originalMoveSpeed 저장 제거
    }

    public virtual void ApplyStatMultiplier(float mult)
    {
        maxHealth     *= mult;
        currentHealth  = maxHealth;
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

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
    //  둔화 — TargetMove.speed만 사용
    // ──────────────────────────────────────────────
    public void ApplySlow(float slowRate, float duration)
    {
        if (isDead) return;

        TargetMove moveScript = GetComponent<TargetMove>();
        if (moveScript == null) return;

        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowCoroutine = StartCoroutine(SlowCoroutine(moveScript, slowRate, duration));
    }

    private IEnumerator SlowCoroutine(TargetMove moveScript, float slowRate, float duration)
    {
        // ✅ TargetMove.speed 기준으로만 관리
        float originalSpeed = moveScript.GetSpeed();
        float slowedSpeed   = originalSpeed * (1f - slowRate);

        moveScript.SetSpeed(slowedSpeed);
        Debug.Log($"[Enemy] 둔화 적용 — {originalSpeed} → {slowedSpeed} ({duration}초)");

        yield return new WaitForSeconds(duration);

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
            StopCoroutine(slowCoroutine);

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