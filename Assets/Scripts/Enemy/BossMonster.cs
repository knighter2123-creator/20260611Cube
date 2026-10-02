using UnityEngine;

/// <summary>
/// 일반 스테이지 보스. Enemy를 상속해 스탯 배율과 보상만 다르게 가져갑니다.
///
/// ─── override 할 것과 하지 말 것 (학습 포인트) ─────────────────────────
/// 부모의 Die()는 콜라이더 끄기, 회색 이펙트 정리, 디버프 정리, 사망 연출,
/// 풀 반환까지 7단계를 순서대로 처리합니다. 자식이 이걸 통째로 덮어쓰면
/// 그 7단계를 전부 다시 써야 하고, 하나라도 빠지면 미묘한 버그가 됩니다.
///
/// 그래서 "달라지는 부분"만 구멍으로 뚫어둔 ApplyStatMultiplier() /
/// GrantRewards() / ReportKill() 만 override합니다. 나중에 부모 Die에
/// 새 단계가 추가돼도 보스는 자동으로 따라옵니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
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

    /// <summary>
    /// 스탯 계산. 부모를 먼저 부른 뒤 보스 배율을 "원본 기준"으로 다시 덮어씁니다.
    ///
    /// base.ApplyStatMultiplier(mult) 를 먼저 부르는 이유:
    ///   statMult 저장, HP바 갱신 같은 부수 작업을 부모가 이미 해주기 때문입니다.
    ///   그 뒤에 체력·방어력만 보스 값으로 다시 계산하면 됩니다.
    ///
    /// 계산식:  최종 체력 = 프리팹 원본 체력 × 스테이지 배율 × 보스 배율
    /// 어디에도 *= 가 없다는 점에 주목하세요. 몇 번을 호출해도 결과가 같습니다.
    /// </summary>
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
            Mathf.RoundToInt(baseRewardExp * bossExpMultiplier * statMult));
    }

    /// <summary>훅 ② — 보스는 킬 카운트가 아니라 "스테이지 클리어"를 보고</summary>
    protected override void ReportKill()
    {
        // base를 부르지 않습니다 — ReportEnemyKill로 가면 킬 카운트가 잘못 올라갑니다.
        StageManager.Instance?.ReportBossKill();
        MissionManager.Instance?.ReportBossKill();
    }
}