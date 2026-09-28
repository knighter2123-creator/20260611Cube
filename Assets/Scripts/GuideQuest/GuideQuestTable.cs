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

    [Header("기본 사이클 (무한 반복)")]
    [Tooltip("이 순서로 반복됩니다. 레벨업 목표가 각성 레벨(아래 Evolve Levels)에 " +
             "정확히 도달하는 사이클에서는, 그 사이클의 레벨업 퀘스트 바로 다음에 " +
             "'각성 1회' 퀘스트가 자동으로 삽입됩니다.")]
    [SerializeField]
    private GuideQuestType[] baseCycle =
    {
        GuideQuestType.EnemyKill,
        GuideQuestType.StageClear,
        GuideQuestType.StatUpgrade,
        GuideQuestType.SummonCompanion,
        GuideQuestType.LevelUp
    };

    [Header("각성 — 이 레벨들에 도달할 때마다 각성 1회 퀘스트 삽입")]
    [Tooltip("반드시 levelBase + levelPerCycle × n 꼴의 값이어야 레벨업 퀘스트 진행 중 정확히 " +
             "이 레벨을 목표로 잡는 사이클이 생겨서 각성이 삽입됩니다 (기본값 기준: 30/50/70/100/200 " +
             "은 모두 5의 배수라 문제없습니다). 오름차순으로 넣어주세요.")]
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

    // ★ 1 → 5로 변경: 스테이지 클리어 가이드 퀘스트가 이제 5스테이지 단위로 반복됩니다
    //   (예: 1-1 → 1-6 → 1-11 → ...). 스크립트의 기본값을 바꿔도 "이미 만들어진"
    //   GuideQuestTable.asset 에 저장된 값은 그대로 유지되니, 인스펙터에서 직접 5로 바꿔주세요.
    [Tooltip("스테이지 클리어 퀘스트가 한 번 완료될 때마다 목표 스테이지가 몇 칸씩 올라가는지. " +
             "기존 에셋에 이미 저장된 값은 이 기본값으로 자동 갱신되지 않으니 인스펙터에서 직접 5로 바꿔주세요.")]
    [SerializeField] private int stageStep = 5;

    [Header("스탯 강화")]
    [SerializeField] private int statBase = 1;
    [SerializeField] private float statPerCycle = 0.5f;

    // ★ 동료 소환 퀘스트를 항상 1회 고정으로 요구하기로 하면서 summonBase/summonPerCycle(증가폭)
    //   필드는 더 이상 쓰이지 않아 삭제했습니다. (Build()의 SummonCompanion 분기 참고)

    [Header("레벨업 (누적 목표 레벨)")]
    [Tooltip("레벨업 가이드 퀘스트의 목표 레벨 = levelBase + levelPerCycle × 사이클 번호. " +
             "기본값(5, 5) 기준: 5 → 10 → 15 → 20 → ... 로 항상 5단위씩 올라갑니다. " +
             "예전에는 각성 레벨(30/50/70/100/200)로 곧장 건너뛰었지만, 이제는 이 값을 거치며 " +
             "촘촘하게 올라가다가 각성 레벨에 정확히 맞을 때만 각성 퀘스트가 끼어듭니다.")]
    [SerializeField] private int levelBase = 5;
    [SerializeField] private int levelPerCycle = 5;

    // 외부(매니저)에서 참조할 수 있으므로 호환용으로 유지
    public int CycleLength => Mathf.Max(1, baseCycle.Length);

    // ──────────────────────────────────────────────
    //  각성이 삽입되는 "사이클 번호" 계산
    // ──────────────────────────────────────────────
    //
    // 레벨업 퀘스트의 목표 레벨은 항상 levelBase + levelPerCycle × 사이클번호 이므로,
    // 반대로 "이 레벨이 몇 번째 사이클에서 나오는가"를 evolveLevels 각 값에서 거꾸로 계산합니다.
    // (기본값 예: levelBase=5, levelPerCycle=5 → 레벨 30은 사이클 5, 레벨 50은 사이클 9 ...)
    //
    // evolveLevels 개수가 많아야 5~10개 수준이라, 매번 다시 계산해도 비용은 무시할 수준입니다
    // (캐싱해서 얻는 이득보다, 인스펙터에서 값을 바꿨을 때 캐시가 낡아버릴 위험이 더 큽니다).
    private int[] BuildEvolveCycleIndices()
    {
        if (evolveLevels == null || evolveLevels.Length == 0)
            return System.Array.Empty<int>();

        int step = Mathf.Max(1, levelPerCycle);
        var list = new System.Collections.Generic.List<int>(evolveLevels.Length);

        foreach (int targetLevel in evolveLevels)
        {
            int offset = targetLevel - levelBase;

            // levelBase/levelPerCycle 진행으로는 정확히 도달할 수 없는 레벨입니다.
            // (예: levelPerCycle을 3으로 바꿨는데 evolveLevels에 30 대신 31이 남아있는 경우)
            // → 각성 퀘스트가 영영 삽입되지 못하므로, 조용히 넘기지 않고 경고를 남깁니다.
            if (offset < 0 || offset % step != 0)
            {
                Debug.LogWarning($"[GuideQuestTable] 각성 레벨 {targetLevel}은(는) levelBase({levelBase}) + " +
                                  $"levelPerCycle({step}) 진행으로는 정확히 도달하지 못해 건너뜁니다. " +
                                  "인스펙터의 Evolve Levels 값을 확인하세요.", this);
                continue;
            }

            list.Add(offset / step);
        }

        list.Sort();   // 인스펙터에 오름차순이 아니게 잘못 입력해도 안전하도록 방어
        return list.ToArray();
    }

    // ──────────────────────────────────────────────
    //  step → (퀘스트 종류 / 그라인드 사이클 번호 / 레벨 목표) 해석
    // ──────────────────────────────────────────────
    private void ResolveStep(int step, out GuideQuestType type,
                             out int gCycle, out int levelTarget)
    {
        int baseLen = Mathf.Max(1, baseCycle.Length);
        levelTarget = 0;

        int[] evolveCycles = BuildEvolveCycleIndices();   // 각성이 삽입되는 사이클 번호 (오름차순)

        int cycleCursor = 0;   // 지금부터 확인할 사이클 번호
        int stepCursor  = 0;   // cycleCursor 시작 지점까지 이미 소비된 step 수

        // evolveCycles를 순서대로 훑으면서, step이 "이번 각성 지점까지의 구간" 안에 있는지 확인합니다.
        // 구간 하나 = (cycleCursor .. evolveCycle 까지의 일반 사이클들) + (evolveCycle 사이클 끝의 각성 삽입 1스텝)
        for (int i = 0; i < evolveCycles.Length; i++)
        {
            int evolveCycle = evolveCycles[i];
            if (evolveCycle < cycleCursor) continue;   // 정렬이 깨졌거나 값이 겹치는 경우를 방어

            int plainCycles  = evolveCycle - cycleCursor;      // evolveCycle 이전까지의 순수 일반 사이클 수
            int stepsNormal  = (plainCycles + 1) * baseLen;    // + evolveCycle 자신의 퀘스트 5개
            int stepsSegment = stepsNormal + 1;                // + 각성 삽입 1스텝

            if (step < stepCursor + stepsSegment)
            {
                int local = step - stepCursor;

                if (local < stepsNormal)
                {
                    // cycleCursor .. evolveCycle 구간의 평범한 퀘스트들 (evolveCycle 자신의 5개 포함)
                    gCycle = cycleCursor + local / baseLen;
                    int pos = local % baseLen;
                    type = baseCycle[pos];
                    if (type == GuideQuestType.LevelUp)
                        levelTarget = levelBase + levelPerCycle * gCycle;
                }
                else
                {
                    // evolveCycle 사이클의 마지막 슬롯 — 그 레벨업 퀘스트를 깬 직후 삽입되는 각성 1회
                    gCycle = evolveCycle;
                    type = GuideQuestType.EvolveClear;
                }
                return;
            }

            stepCursor += stepsSegment;
            cycleCursor = evolveCycle + 1;
        }

        // 등록된 각성 레벨을 모두 지난 뒤 — 각성 삽입 없이 무한 반복 (레벨업 목표는 계속 5단위 상승)
        int remaining = step - stepCursor;
        gCycle = cycleCursor + remaining / baseLen;
        type = baseCycle[remaining % baseLen];
        if (type == GuideQuestType.LevelUp)
            levelTarget = levelBase + levelPerCycle * gCycle;
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
                // ★ 요청대로 사이클과 무관하게 항상 1회 고정 (예전에는 summonBase + summonPerCycle × gCycle 로 증가했음)
                q.requiredCount = 1;
                break;

            case GuideQuestType.LevelUp:
                q.requiredCount = levelTarget;    // ResolveStep이 계산한 목표 레벨 (5단위로 상승)
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
        if (levelPerCycle < 1) levelPerCycle = 1;
        if (statBase  < 1) statBase  = 1;

        // 각성 레벨은 1 이상, 오름차순 권장 (실수 방지용 최소 가드)
        if (evolveLevels != null)
            for (int i = 0; i < evolveLevels.Length; i++)
                if (evolveLevels[i] < 1) evolveLevels[i] = 1;
    }
#endif
}