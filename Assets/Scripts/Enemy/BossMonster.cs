using UnityEngine;

/// <summary>일반 스테이지 보스 — Die() 뼈대는 부모 것을 그대로 쓰고 스탯 배율·보상·처치 보고 훅만 override (단계 누락 방지, 부모 변경 자동 반영).</summary>
public class BossMonster : Enemy
{
    [Header("보스 전용 배율")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;
    [SerializeField] private float bossDefenceMultiplier  = 1.5f;
    [SerializeField] private float bossExpMultiplier      = 2.0f;
    [SerializeField] private int   baseRewardExp          = 10;
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;

    [Header("골드 보상")]
    [SerializeField] private int baseRewardGold = 100;

    [Tooltip("체크하면 보스 골드도 스테이지 배율(statMult)만큼 함께 커집니다.\n" +
             "해제하면 스테이지와 무관하게 고정 골드를 줍니다.")]
    [SerializeField] private bool scaleGoldWithStage = true;

    [Header("보석 보상")]
    [SerializeField] private int baseRewardGem = 100;

    [Header("소환권 보상")]
    [Tooltip("보스를 처치할 때마다 주는 소환권 수. 스테이지 배율과 무관한 고정값입니다.")]
    [Min(0)]
    [SerializeField] private int rewardGachaTicket = 1;

    /// <summary>부모(배율 저장·HP바 갱신)를 먼저 부른 뒤 체력·방어력을 원본 × 스테이지 × 보스 배율로 대입 (*= 없음 → 여러 번 불러도 같은 결과).</summary>
    public override void ApplyStatMultiplier(float mult)
    {
        base.ApplyStatMultiplier(mult);

        maxHealth     = baseMaxHealth * mult * bossHpMultiplier;
        defence       = baseDefence   * bossDefenceMultiplier;
        currentHealth = maxHealth;
    }

    /// <summary>훅 ① — 보스 보상: 골드 + 보석 + 소환권 + 배율 적용된 경험치</summary>
    protected override void GrantRewards()
    {
        // base를 부르지 않습니다 — 잡몹용 rewardGold/rewardExp는 쓰지 않으니까요.
        float goldScale = scaleGoldWithStage ? statMult : 1f;

        CurrencyManager.Instance?.AddGold(
            Mathf.RoundToInt(baseRewardGold * bossCurrencyMultiplier * goldScale));

        CurrencyManager.Instance?.AddGem(baseRewardGem);

        // 저장은 바로 뒤 ReportKill → StageManager.StageClear 의 Save() 에 함께 실립니다.
        GachaTicket.Add(rewardGachaTicket);

        LevelUpManager.Instance?.AddExp(
            (long)System.Math.Round(baseRewardExp * bossExpMultiplier * expMult));
    }

    /// <summary>훅 ② — 보스는 킬 카운트가 아니라 "스테이지 클리어"를 보고</summary>
    protected override void ReportKill()
    {
        // base를 부르지 않습니다 — ReportEnemyKill로 가면 킬 카운트가 잘못 올라갑니다.
        StageManager.Instance?.ReportBossKill();
        MissionManager.Instance?.ReportBossKill();
    }
}