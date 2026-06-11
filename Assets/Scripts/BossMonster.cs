using System.Collections;
using UnityEngine;

public class BossMonster : Enemy
{
    [Header("보스 전용 배율 (StageManager 스탯에 추가로 곱함)")]
    [Tooltip("HP에 추가로 곱할 배율. 기본 1.5배.")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;

    [Tooltip("재화에 추가로 곱할 배율. 기본 1.5배.")]
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;

    bool isDead = false;
    // ──────────────────────────────────────────────
    //  사망
    // ──────────────────────────────────────────────

    private void Die()
    {
        if (isDead) return;   // 중복 방지는 맨 앞에
        isDead = true;

        StopAllCoroutines();
       
        LevelUpManager.Instance.AddCurrency(
            Mathf.RoundToInt(100 * bossCurrencyMultiplier));

        Destroy(gameObject, 1.5f);
        // base.Die()는 호출하지 않음
        // → InvokeOnEnemyDied() 차단 → EnemyRespawn 반응 없음
    }

    private void TakeDamage(float damage)
    {
        if (isDead) return;
        isDead = true;
        StopAllCoroutines();
    }
}