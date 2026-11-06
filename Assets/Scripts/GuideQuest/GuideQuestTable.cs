using Manager.currency;
using UnityEngine;

[CreateAssetMenu(fileName = "GuideQuestTable", menuName = "Game/Guide Quest Table")]
public class GuideQuestTable : ScriptableObject
{
    // 보석 보상은 단계와 무관한 고정값(gemBase × 종류별 배율, 또는 fixedAmount>0 이면 그 값), 골드만 단계마다 복리 증가 (에셋에 남은 옛 gemGrowth/gemCap 은 유니티가 무시)

    [System.Serializable]
    public class GemRewardAdjust
    {
        [Tooltip("gemBase 에 곱할 배율. 1 = gemBase 그대로. 결과는 반올림(0.5 올림), 0 으로 해도 최소 1개는 지급됩니다")]
        [Min(0f)] public float multiplier = 1f;

        [Tooltip("0보다 크면 gemBase·배율을 쓰지 않고 항상 이 양을 지급합니다. 0 = gemBase × 배율 사용")]
        [Min(0)] public int fixedAmount = 0;
    }

    [Header("보상 — 보석 기본값 (스테이지 / 소환 / 레벨업 / 각성)")]
    [Tooltip("보석 퀘스트 1회 보상의 기준값. 단계가 올라도 늘어나지 않습니다.\n" +
             "종류별 조정(배율 1, 고정 0)이 기본값이면 모든 보석 퀘스트가 이 값만큼 지급합니다.")]
    [SerializeField] private int gemBase = 10;

    // ★ [보석 보상 퀘스트별 조정] "= new ..." 초기화 덕분에 기존 에셋에서도 배율 1 / 고정 0 으로 채워집니다.
    [Header("보상 — 보석 퀘스트별 조정")]
    [SerializeField] private GemRewardAdjust stageClearGem = new GemRewardAdjust();
    [SerializeField] private GemRewardAdjust summonGem     = new GemRewardAdjust();
    [SerializeField] private GemRewardAdjust levelUpGem    = new GemRewardAdjust();
    [SerializeField] private GemRewardAdjust evolveGem     = new GemRewardAdjust();

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

    // 스테이지 퀘스트 간격 5 (1-1 → 1-6 → 1-11 …)
    // ⚠ 기존 GuideQuestTable.asset 은 옛 값이 유지되므로 인스펙터에서 직접 5로
    [Tooltip("스테이지 클리어 퀘스트가 한 번 완료될 때마다 목표 스테이지가 몇 칸씩 올라가는지. " +
             "기존 에셋에 이미 저장된 값은 이 기본값으로 자동 갱신되지 않으니 인스펙터에서 직접 5로 바꿔주세요.")]
    [SerializeField] private int stageStep = 5;

    [Header("스탯 강화")]
    [SerializeField] private int statBase = 1;
    [SerializeField] private float statPerCycle = 0.5f;

    // 동료 소환 퀘스트는 항상 1회 고정 (Build 의 SummonCompanion 분기)

    [Header("레벨업 (누적 목표 레벨)")]
    [Tooltip("레벨업 가이드 퀘스트의 목표 레벨 = levelBase + levelPerCycle × 사이클 번호. " +
             "기본값(5, 5) 기준: 5 → 10 → 15 → 20 → ... 로 항상 5단위씩 올라갑니다. " +
             "예전에는 각성 레벨(30/50/70/100/200)로 곧장 건너뛰었지만, 이제는 이 값을 거치며 " +
             "촘촘하게 올라가다가 각성 레벨에 정확히 맞을 때만 각성 퀘스트가 끼어듭니다.")]
    [SerializeField] private int levelBase = 5;
    [SerializeField] private int levelPerCycle = 5;

    // ── 각성 삽입 사이클 = (evolveLevel − levelBase) / levelPerCycle 역산 (몇 개뿐이라 캐시 없이 매번 계산 — 인스펙터 변경 시 낡지 않게) ──
    private int[] BuildEvolveCycleIndices()
    {
        if (evolveLevels == null || evolveLevels.Length == 0)
            return System.Array.Empty<int>();

        int step = Mathf.Max(1, levelPerCycle);
        var list = new System.Collections.Generic.List<int>(evolveLevels.Length);

        foreach (int targetLevel in evolveLevels)
        {
            int offset = targetLevel - levelBase;

            // 진행으로 정확히 도달 못 하는 레벨 — 각성 퀘스트가 영영 안 나오므로 경고
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

    // ── step → (퀘스트 종류 / 그라인드 사이클 번호 / 레벨 목표) 해석 ──
    private void ResolveStep(int step, out GuideQuestType type,
                             out int gCycle, out int levelTarget)
    {
        int baseLen = Mathf.Max(1, baseCycle.Length);
        levelTarget = 0;

        int[] evolveCycles = BuildEvolveCycleIndices();   // 각성이 삽입되는 사이클 번호 (오름차순)

        int cycleCursor = 0;   // 지금부터 확인할 사이클 번호
        int stepCursor  = 0;   // cycleCursor 시작 지점까지 이미 소비된 step 수

        // 구간(일반 사이클들 + 끝의 각성 1스텝)을 차례로 훑어 step 이 속한 구간을 찾음
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
        q.rewardAmount = CalcReward(q.rewardType, type, step);   // ★ 퀘스트 종류(type)도 넘김

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

    /// <summary>보상 계산 — 골드는 단계별 복리(상한 goldCap), 보석은 단계 무관 고정(gemBase × 배율 또는 고정값).</summary>
    private int CalcReward(CurrencyType currency, GuideQuestType type, int step)
    {
        // 골드 — 변경 없음
        if (currency == CurrencyType.Gold)
            return Compound(goldBase, goldGrowth, goldCap, step, 1f);

        // 보석 — 종류별 조정값 (해당 없는 종류면 null → gemBase 그대로)
        GemRewardAdjust adjust = GetGemAdjust(type);

        // 고정값이 있으면 공식 대신 그 값 (단계와 무관)
        if (adjust != null && adjust.fixedAmount > 0)
            return adjust.fixedAmount;

        float multiplier = adjust != null ? adjust.multiplier : 1f;

        // 성장률 1·상한 0 으로 Compound 재사용 → gemBase × 배율 (반올림·최소 1 규칙을 골드와 공유)
        return Compound(gemBase, 1f, 0, step, multiplier);
    }

    /// <summary>퀘스트 종류 → 보석 조정값 (한 곳에 모음, 목록에 없으면 null → gemBase 그대로라 0 보상 방지).</summary>
    private GemRewardAdjust GetGemAdjust(GuideQuestType type)
    {
        switch (type)
        {
            case GuideQuestType.StageClear:      return stageClearGem;
            case GuideQuestType.SummonCompanion: return summonGem;
            case GuideQuestType.LevelUp:         return levelUpGem;
            case GuideQuestType.EvolveClear:     return evolveGem;
            default:                             return null;
        }
    }

    /// <summary>base × growth^step × multiplier, 상한 cap(0=무제한), 최소 1 — 골드/보석 공용.</summary>
    private static int Compound(int baseValue, float growth, int cap, int step, float multiplier)
    {
        double v = baseValue * System.Math.Pow(growth, step) * multiplier;

        // NaN 차단 — 무한대×0 이면 NaN 이 상한 검사를 그냥 통과함
        if (double.IsNaN(v)) v = 0;
        if (v > int.MaxValue) v = int.MaxValue;   // int 오버플로 방지

        // 사사오입(AwayFromZero) — 기본 Math.Round 는 은행가 반올림이라 22.5 → 22 가 됨
        int amount = (int)System.Math.Round(v, System.MidpointRounding.AwayFromZero);
        if (cap > 0 && amount > cap) amount = cap;
        return amount < 1 ? 1 : amount;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stagesPerChapter < 1) stagesPerChapter = 10;
        if (gemBase < 1) gemBase = 1;   // ★ [보석 성장·상한 삭제] 0 이하면 어차피 최소 1 로 지급되므로 인스펙터 값도 맞춰 둠
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