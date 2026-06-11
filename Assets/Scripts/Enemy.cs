using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 적(Enemy, Boss)의 공통 부모 클래스.
///
/// [HP바 문제 해결]
///  InitHpBar()는 Start()에서 currentHp = maxHp 세팅 이후 1회만 호출합니다.
///  UpdateHpBar()는 TakeDamage() 때만 호출되며 maxValue는 건드리지 않습니다.
///
/// [스탯 적용 순서]
///  1. Inspector의 SerializeField 기본값 유지 (Awake)
///  2. ApplyStageStats()에서 StageManager 스탯으로 덮어씀 (Start)
///  3. currentHp = maxHp 세팅 후 InitHpBar() 호출 (Start)
///     → 이 순서를 지켜야 HP바 maxValue가 정확히 세팅됩니다.
/// </summary>
public class Enemy : MonoBehaviour
{
    public static event Action OnEnemyDied;
    public static event Action OnBossDied;

    [SerializeField] private GameObject damageTextPrefab;

    /// <summary>Inspector 기본값. ApplyStageStats()에서 StageManager 값으로 덮어씁니다.</summary>
    [SerializeField] protected int maxHp   = 100;
    protected int currentHp;
  
   

    protected bool  isDead   = false;
    public    bool  IsDead   => isDead;

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────
    protected virtual void Awake()
    {
        
    }

    protected virtual void Start()
    {
        // 2) 확정된 maxHp로 currentHp 세팅
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHp -= damage;

        if (DamageText.showDamage)
        {
            var obj = Instantiate(damageTextPrefab,
                                  transform.position + Vector3.up,
                                  Quaternion.identity);
            obj.GetComponent<DamageText>().SetDamage(damage);
        }

        if (currentHp <= 0) Die();
    }
    
    // ──────────────────────────────────────────────
    //  사망
    // ──────────────────────────────────────────────
    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        StopAllCoroutines();
        
        LevelUpManager.Instance.AddCurrency(100);

        InvokeOnEnemyDied();
        Destroy(gameObject, 1.5f);
    }

    protected void InvokeOnEnemyDied() => OnEnemyDied?.Invoke();
    protected void InvokeOnBossDied()  => OnBossDied?.Invoke();
}