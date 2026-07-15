using Manager.currency;
using UnityEngine;

[CreateAssetMenu(fileName = "GuideQuestTable", menuName = "Game/Guide Quest Table")]
public class GuideQuestTable : ScriptableObject
{
    [Header("보상 — 보석 (스테이지 / 소환 / 레벨업 / 각성)")]
    [SerializeField] private int   gemBase   = 10;
    [SerializeField] private float gemGrowth = 1.06f;
    [SerializeField] private int   gemCap    = 5000;

    [Header("보상 — 골드 (적 처치 / 스탯 강화)")]
    [SerializeField] private int   goldBase   = 500;
    [SerializeField] private float goldGrowth = 1.12f;
    [SerializeField] private int   goldCap    = 0;      // 0이면 무제한

    [Header("기본 사이클 (각성 구간 이후 무한 반복)")]
    [Tooltip("이 순서로 반복됩니다. 각성 구간에서는 LevelUp 뒤에 EvolveClear가 자동 삽입됩니다.")]
    [SerializeField]
    private GuideQuestType[] baseCycle =
    {
        GuideQuestType.EnemyKill,
        GuideQuestType.StageClear,
        GuideQuestType.StatUpgrade,
        GuideQuestType.SummonCompanion,
        GuideQuestType.LevelUp
    };

    [Header("각성 — 레벨 달성 → 각성 1회 (순서 고정, 1회성)")]
    [Tooltip("이 레벨들을 순서대로 달성하며, 각 달성 직후 '각성 1회' 퀘스트가 삽입됩니다.")]
    [SerializeField]
    private int[] evolveLevels = { 30, 50, 70, 100, 200 };

    /// <summary>스탯 강화 순환 순서 (공격력 → 치명타 공격력 → 공격 속도 → 치명타 확률)</summary>
    private static readonly LevelUpManager.StatType[] StatCycle =
    {
        LevelUpManager.StatType.Damage,
        LevelUpManager.StatType.CritDamage,
        LevelUpManager.StatType.Attackspd,
        LevelUpManager.StatType.CritChance
    };

    [Header("적 처치")]
    [SerializeField] private int killBase = 10;
    [SerializeField] private int killPerCycle = 10;

    [Header("스테이지 클리어")]
    [SerializeField] private int stagesPerChapter = 10;
    [SerializeField] private int stageStep = 1;

    [Header("스탯 강화")]
    [SerializeField] private int statBase = 1;
    [SerializeField] private float statPerCycle = 0.5f;

    [Header("동료 소환")]
    [SerializeField] private int summonBase = 1;
    [SerializeField] private float summonPerCycle = 0.34f;

    [Header("레벨업 (누적 목표 레벨)")]
    [SerializeField] private int levelBase = 5;
    [SerializeField] private int levelPerCycle = 5;

    // 외부(매니저)에서 참조할 수 있으므로 호환용으로 유지
    public int CycleLength => Mathf.Max(1, baseCycle.Length);

    // ──────────────────────────────────────────────
    //  step → (퀘스트 종류 / 그라인드 사이클 번호 / 레벨 목표) 해석
    //  ★ 여기서 각성 삽입 로직을 처리합니다. (배열 대신 계산으로)
    // ──────────────────────────────────────────────
    private void ResolveStep(int step, out GuideQuestType type,
                             out int gCycle, out int levelTarget)
    {
        int baseLen    = Mathf.Max(1, baseCycle.Length);
        int milestones = evolveLevels != null ? evolveLevels.Length : 0;
        int superLen   = baseLen + 1;              // 각성 구간의 한 사이클 = 기본 + 각성 1
        int phase1Len  = milestones * superLen;    // 각성 구간 전체 길이

        levelTarget = 0;

        if (step < phase1Len)
        {
            // ── 각성 구간: 레벨 달성 → 각성 ──
            gCycle  = step / superLen;             // 0 .. milestones-1
            int pos = step % superLen;

            if (pos < baseLen)
            {
                type = baseCycle[pos];
                if (type == GuideQuestType.LevelUp)
                    levelTarget = evolveLevels[gCycle];   // 30/50/70/100/200
            }
            else
            {
                type = GuideQuestType.EvolveClear;        // 달성 직후 각성 1회
            }
        }
        else
        {
            // ── 무한 반복 구간: 각성 없음 ──
            int s   = step - phase1Len;
            gCycle  = milestones + s / baseLen;    // 사이클 번호를 연속으로 유지
            int pos = s % baseLen;
            type    = baseCycle[pos];

            if (type == GuideQuestType.LevelUp)
            {
                int lastTier = milestones > 0 ? evolveLevels[milestones - 1] : levelBase;
                int extra    = (gCycle - milestones) + 1;      // 1, 2, 3 ...
                levelTarget  = lastTier + levelPerCycle * extra; // 200 이후로 이어짐
            }
        }
    }

    /// <summary>step(0-based)에 해당하는 퀘스트를 생성한다. 순수 함수 — 단계 제한 없음.</summary>
    public GuideQuest Build(int step)
    {
        if (step < 0) step = 0;

        ResolveStep(step, out GuideQuestType type, out int gCycle, out int levelTarget);

        GuideQuest q = new GuideQuest
        {
            step       = step,
            type       = type,
            rewardType = GuideQuest.GetRewardType(type)   // 종류에 따라 재화 결정
        };
        q.rewardAmount = CalcReward(q.rewardType, step);

        switch (type)
        {
            case GuideQuestType.EnemyKill:
                q.requiredCount = killBase + (long)killPerCycle * gCycle;
                break;

            case GuideQuestType.StageClear:
                int perChapter = Mathf.Max(1, stagesPerChapter);
                int totalStage = 1 + gCycle * Mathf.Max(1, stageStep);

                q.targetChapter = (totalStage - 1) / perChapter + 1;
                q.targetStage   = (totalStage - 1) % perChapter + 1;
                q.requiredCount = 1;

                if (q.targetChapter < 1) q.targetChapter = 1;
                if (q.targetStage   < 1) q.targetStage   = 1;
                break;

            case GuideQuestType.StatUpgrade:
                q.statType      = StatCycle[gCycle % StatCycle.Length];
                q.requiredCount = statBase + Mathf.FloorToInt(gCycle * statPerCycle);
                break;

            case GuideQuestType.SummonCompanion:
                q.requiredCount = summonBase + Mathf.FloorToInt(gCycle * summonPerCycle);
                break;

            case GuideQuestType.LevelUp:
                q.requiredCount = levelTarget;    // 각성 구간=티어 레벨, 이후=이어지는 목표
                break;

            case GuideQuestType.EvolveClear:
                q.requiredCount = 1;              // 각성 1회
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
            baseValue = goldBase; growth = goldGrowth; cap = goldCap;
        }
        else
        {
            baseValue = gemBase;  growth = gemGrowth;  cap = gemCap;
        }

        double v = baseValue * System.Math.Pow(growth, step);
        if (v > int.MaxValue) v = int.MaxValue;   // int 오버플로 방지

        int amount = (int)System.Math.Round(v);
        if (cap > 0 && amount > cap) amount = cap;
        return amount < 1 ? 1 : amount;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stagesPerChapter < 1) stagesPerChapter = 10;
        if (stageStep < 1) stageStep = 1;
        if (killBase  < 1) killBase  = 1;
        if (levelBase < 1) levelBase = 1;
        if (statBase  < 1) statBase  = 1;
        if (summonBase< 1) summonBase= 1;

        // 각성 레벨은 1 이상, 오름차순 권장 (실수 방지용 최소 가드)
        if (evolveLevels != null)
            for (int i = 0; i < evolveLevels.Length; i++)
                if (evolveLevels[i] < 1) evolveLevels[i] = 1;
    }
#endif
}