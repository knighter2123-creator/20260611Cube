using Manager.currency;
using UnityEngine;

[CreateAssetMenu(fileName = "GuideQuestTable", menuName = "Game/Guide Quest Table")]
public class GuideQuestTable : ScriptableObject
{
    [Header("보상 — 보석 (스테이지 / 소환 / 레벨업)")]
    [SerializeField] private int   gemBase   = 10;
    [SerializeField] private float gemGrowth = 1.06f;   // 단계마다 6% 복리
    [SerializeField] private int   gemCap    = 5000;    // 0이면 무제한

    [Header("보상 — 골드 (적 처치 / 스탯 강화)")]
    [SerializeField] private int   goldBase   = 500;
    [SerializeField] private float goldGrowth = 1.12f;  // 골드는 인플레가 빠르므로 성장률 ↑
    [SerializeField] private int   goldCap    = 0;      // 0이면 무제한 (int 상한까지)
    
    [Header("퀘스트 순환 순서")]
    [Tooltip("이 순서대로 무한 반복됩니다. 한 바퀴 = 1 사이클")]
    [SerializeField]
    private GuideQuestType[] cycleOrder =
    {
        GuideQuestType.EnemyKill,
        GuideQuestType.StageClear,
        GuideQuestType.StatUpgrade,
        GuideQuestType.SummonCompanion,
        GuideQuestType.LevelUp
    };

    /// <summary>스탯 강화 순환 순서 (요구사항 고정: 공격력 → 치명타 공격력 → 공격 속도 → 치명타 확률)</summary>
    private static readonly LevelUpManager.StatType[] StatCycle =
    {
        LevelUpManager.StatType.Damage,
        LevelUpManager.StatType.CritDamage,
        LevelUpManager.StatType.Attackspd,
        LevelUpManager.StatType.CritChance
    };

    [Header("적 처치")]
    [SerializeField] private int killBase = 10;      // 1사이클 목표
    [SerializeField] private int killPerCycle = 10;  // 사이클마다 증가량

    [Header("스테이지 클리어")]
    [SerializeField] private int stagesPerChapter = 10; // 챕터당 스테이지 수
    [SerializeField] private int stageStep = 1;         // 사이클마다 몇 스테이지씩 전진할지

    [Header("스탯 강화")]
    [SerializeField] private int statBase = 1;
    [SerializeField] private float statPerCycle = 0.5f;  // 2사이클마다 +1회

    [Header("동료 소환")]
    [SerializeField] private int summonBase = 1;
    [SerializeField] private float summonPerCycle = 0.34f; // 3사이클마다 +1회

    [Header("레벨업 (누적 목표 레벨)")]
    [SerializeField] private int levelBase = 5;
    [SerializeField] private int levelPerCycle = 5;

    [Header("옵션")]
    [Tooltip("체크 시 조건 충족 즉시 자동으로 보상 지급 후 다음 단계로 진행")]
    public bool autoClaim = false;

    public int CycleLength => Mathf.Max(1, cycleOrder.Length);

    /// <summary>step(0-based)에 해당하는 퀘스트를 생성한다. 순수 함수 — 단계 제한 없음.</summary>
    public GuideQuest Build(int step)
    {if (step < 0) step = 0;

        int cycle = step / CycleLength;
        GuideQuestType type = cycleOrder[step % CycleLength];

        GuideQuest q = new GuideQuest
        {
            step = step,
            type = type,
            rewardType = GuideQuest.GetRewardType(type)   // ★ 종류에 따라 재화 결정
        };
        q.rewardAmount = CalcReward(q.rewardType, step);  // ★ 재화별 공식 적용

        switch (type)
        {
            case GuideQuestType.EnemyKill:
                q.requiredCount = killBase + (long)killPerCycle * cycle;
                break;

            case GuideQuestType.StageClear:
                int perChapter = Mathf.Max(1, stagesPerChapter);   // ★ 0 나눗셈 / 0-0 방지
                int totalStage = 1 + cycle * Mathf.Max(1, stageStep);

                q.targetChapter = (totalStage - 1) / perChapter + 1;
                q.targetStage   = (totalStage - 1) % perChapter + 1;
                q.requiredCount = 1;

                // 안전 가드 — 어떤 경우에도 1-1 미만이 나오지 않도록
                if (q.targetChapter < 1) q.targetChapter = 1;
                if (q.targetStage   < 1) q.targetStage   = 1;
                break;

            case GuideQuestType.StatUpgrade:
                q.statType = StatCycle[cycle % StatCycle.Length];
                q.requiredCount = statBase + Mathf.FloorToInt(cycle * statPerCycle);
                break;

            case GuideQuestType.SummonCompanion:
                q.requiredCount = summonBase + Mathf.FloorToInt(cycle * summonPerCycle);
                break;

            case GuideQuestType.LevelUp:
                q.requiredCount = levelBase + (long)levelPerCycle * cycle;
                break;
        }

        if (q.requiredCount < 1) q.requiredCount = 1;
        return q;
    }

    /// <summary>재화 종류별 보상 계산. 단계가 오를수록 복리 증가.</summary>
    private int CalcReward(CurrencyType currency, int step)
    {
        int   baseValue;
        float growth;
        int   cap;

        if (currency == CurrencyType.Gold)
        {
            baseValue = goldBase;
            growth    = goldGrowth;
            cap       = goldCap;
        }
        else
        {
            baseValue = gemBase;
            growth    = gemGrowth;
            cap       = gemCap;
        }

        double v = baseValue * System.Math.Pow(growth, step);

        // ★ int 오버플로 방지 — 복리라 단계가 높아지면 쉽게 넘친다
        if (v > int.MaxValue) v = int.MaxValue;

        int amount = (int)System.Math.Round(v);
        if (cap > 0 && amount > cap) amount = cap;
        return amount < 1 ? 1 : amount;
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        // ★ 0이면 Build()에서 0-0이 나온다. 최소 1 보장.
        if (stagesPerChapter < 1) stagesPerChapter = 10;
        if (stageStep < 1) stageStep = 1;
        if (killBase < 1) killBase = 1;
        if (levelBase < 1) levelBase = 1;
        if (statBase < 1) statBase = 1;
        if (summonBase < 1) summonBase = 1;
    }
#endif
}
