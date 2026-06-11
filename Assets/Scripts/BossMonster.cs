using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 몬스터 클래스.
/// EnemyBase를 상속받아 보스 전용 로직만 override합니다.
///
/// [EnemyBase와의 차이점]
///  ApplyStageStats : 스테이지 스탯에 보스 배율(HP ×3, Damage ×2) 추가 적용
///  AutoAttack      : 공격 주기 3.5초 (일반 몬스터 5초)
///  Die             : InvokeOnBossDied() + StageManager.ReportBossKilled() 호출
///                    (EnemyRespawn.HandleBossDied에서 중복 호출 금지)
///
/// [스탯 계산 흐름]
///  EnemyBase.Start()
///    → ApplyStageStats() 호출 (여기서 override된 버전 실행)
///    → base.ApplyStageStats() : StageManager 스탯 세팅
///    → 보스 배율 추가 곱연산
///    → currentHp = maxHp 세팅
/// </summary>
public class BossMonster : Enemy
{
    [Header("보스 전용 배율 (StageManager 스탯에 추가로 곱함)")]
    [Tooltip("HP에 추가로 곱할 배율. 기본 1.5배.")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;

    [Tooltip("재화에 추가로 곱할 배율. 기본 1.5배.")]
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;

    // ──────────────────────────────────────────────
    //  사망
    // ──────────────────────────────────────────────

    protected override void Die()
    {
        if (isDead) return;   // 중복 방지는 맨 앞에
        isDead = true;

        StopAllCoroutines();
       
        LevelUpManager.Instance.AddCurrency(
            Mathf.RoundToInt(100 * bossCurrencyMultiplier));

        // OnBossDied 이벤트 → BossSceneManager.HandleBossDied() 수신
        InvokeOnBossDied();

        Destroy(gameObject, 1.5f);
        // base.Die()는 호출하지 않음
        // → InvokeOnEnemyDied() 차단 → EnemyRespawn 반응 없음
    }
}