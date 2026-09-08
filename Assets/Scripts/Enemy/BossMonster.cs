using UnityEngine;

/// <summary>
/// 일반 스테이지 보스. Enemy를 상속해 스탯 배율과 보상만 다르게 가져갑니다.
///
/// ★ 이번 수정으로 고친 버그 3가지
///
///  ① 배율 누적 (가장 심각)
///     기존:  maxHealth *= mult;
///     문제:  Enemy는 baseMaxHealth를 따로 보관해 "원본 × 배율"로 매번 새로 계산하는데,
///            여기서 *= 로 덮어써서 곱셈이 누적됩니다.
///            보스가 풀에서 재사용되는 순간 체력이 기하급수적으로 폭발해요.
///            (지금은 Die에서 Destroy를 하는 바람에 재사용이 안 돼서 "우연히" 안 터졌습니다)
///     수정:  baseMaxHealth에서 매번 새로 계산.
///
///  ② 풀에 반환하지 않고 Destroy
///     보스는 ObjectPoolManager.GetInactive로 꺼내 왔는데 Destroy로 없애버려서,
///     풀의 기록(instanceToPrefab)에 죽은 항목이 계속 쌓입니다.
///     수정:  Die()를 override하지 않고 부모의 것을 그대로 씁니다 → 자동으로 풀에 반환.
///
///  ③ InitStats의 보스 배율이 적용되지 않음
///     InitStats()는 아무도 호출하지 않는 죽은 코드였습니다.
///     즉 bossHpMultiplier(1.5배)가 지금까지 한 번도 안 먹고 있었어요.
///     수정:  ApplyStatMultiplier 한 곳에서 전부 계산.
///
/// ─── override 할 것과 하지 말 것 (학습 포인트) ─────────────────────────
/// 부모의 Die()는 콜라이더 끄기, 회색 이펙트 정리, 디버프 정리, 사망 연출,
/// 풀 반환까지 7단계를 순서대로 처리합니다. 자식이 이걸 통째로 덮어쓰면
/// 그 7단계를 전부 다시 써야 하고, 하나라도 빠지면 미묘한 버그가 됩니다.
///
/// 그래서 "달라지는 부분"만 구멍으로 뚫어둔 GrantRewards() / ReportKill()
/// 두 개만 override합니다. 코드가 절반 이하로 줄고, 나중에 부모 Die에
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
             "기존 코드는 스테이지가 올라가도 보스 골드가 그대로였습니다 — " +
             "의도한 것이었다면 체크 해제하세요.")]
    [SerializeField] private bool scaleGoldWithStage = true;

    [Header("보석 보상")]
    [SerializeField] private int baseRewardGem = 100;

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

    /// <summary>훅 ① — 보스 보상: 골드 + 보석 + 배율 적용된 경험치</summary>
    protected override void GrantRewards()
    {
        // base를 부르지 않습니다 — 잡몹용 rewardGold/rewardExp는 쓰지 않으니까요.
        float goldScale = scaleGoldWithStage ? statMult : 1f;

        CurrencyManager.Instance?.AddGold(
            Mathf.RoundToInt(baseRewardGold * bossCurrencyMultiplier * goldScale));

        CurrencyManager.Instance?.AddGem(baseRewardGem);

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

    // ★ Die()는 override하지 않습니다.
    //   부모 Enemy.Die()가 콜라이더 정리 → 디버프 정리 → HP바 제거 →
    //   GrantRewards() → ReportKill() → 사망 연출 → 풀 반환 순으로 전부 처리합니다.
}