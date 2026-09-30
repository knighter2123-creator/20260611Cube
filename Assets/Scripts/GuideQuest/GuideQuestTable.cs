using Manager.currency;
using UnityEngine;

[CreateAssetMenu(fileName = "GuideQuestTable", menuName = "Game/Guide Quest Table")]
public class GuideQuestTable : ScriptableObject
{
    // ──────────────────────────────────────────────
    //  ★ [보석 보상 퀘스트별 조정] 이번 변경
    // ──────────────────────────────────────────────
    //  예전: 보석 퀘스트 4종(스테이지/소환/레벨업/각성)이 모두 같은 공식
    //        gemBase × gemGrowth^step (상한 gemCap) 을 써서, 같은 단계면 항상 같은 양이었습니다.
    //  지금: 퀘스트 종류마다 "조정값" 을 하나씩 얹습니다.
    //        - 배율(multiplier)    : gemBase × 배율   (예: 각성 ×5)
    //        - 고정값(fixedAmount) : 0보다 크면 gemBase 를 무시하고 항상 이 값 (예: 소환 항상 100)
    //
    //  ★ [보석 성장·상한 삭제] 이번 변경
    //    gemGrowth(단계마다 복리 증가)와 gemCap(상한)을 없앴습니다.
    //    → 보석 보상은 이제 단계가 올라도 변하지 않는 "고정 보상" 입니다 (gemBase × 배율, 또는 고정값).
    //    상한은 "계속 커지는 값을 멈추는" 장치였으므로, 커지지 않게 된 지금은 필요 없어 종류별 상한(cap)도 함께 뺐습니다.
    //    골드 보상(goldBase / goldGrowth / goldCap)은 그대로 단계마다 증가합니다.
    //
    //  [삭제한 필드가 에셋에 남는 문제는?]
    //    GuideQuestTable.asset 파일 안에는 gemGrowth/gemCap 값이 텍스트로 남아 있지만,
    //    유니티는 스크립트에 없는 필드를 그냥 무시합니다(에러 없음). 다음에 에셋을 저장할 때 정리됩니다.

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

    /// <summary>
    /// 재화 종류별 보상 계산.
    ///   골드 : 단계가 오를수록 복리 증가 (예전 그대로, 상한 goldCap)
    ///   보석 : ★ 단계(step)와 무관한 고정 보상 — gemBase × 종류별 배율, 또는 종류별 고정값
    /// </summary>
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

        // ★ [보석 성장·상한 삭제] 성장률 1(= 단계와 무관), 상한 0(= 없음) 으로 같은 계산 함수를 재사용합니다.
        //   1^step 은 항상 1 이므로 결과는 gemBase × multiplier (반올림, 최소 1).
        //   계산을 따로 만들지 않고 Compound 를 재사용하면 반올림·오버플로·최소 1 규칙이 골드와 항상 같게 유지됩니다.
        return Compound(gemBase, 1f, 0, step, multiplier);
    }

    /// <summary>
    /// ★ [보석 보상 퀘스트별 조정] 퀘스트 종류 → 보석 조정값.
    /// switch 로 한 곳에 모아 두어, 나중에 보석 퀘스트 종류가 늘면 여기에 한 줄만 추가하면 됩니다.
    /// 목록에 없는 종류(null)는 gemBase 를 그대로 줍니다 — 새 종류를 추가하고 깜빡해도 보상이 0이 되지 않게.
    /// </summary>
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

    /// <summary>
    /// 복리 공식 base × growth^step × multiplier, 상한 cap(0 = 무제한), 최소 1.
    /// (예전 CalcReward 본문을 그대로 옮기고 multiplier 만 추가 — 골드/보석이 같은 계산을 공유)
    /// </summary>
    private static int Compound(int baseValue, float growth, int cap, int step, float multiplier)
    {
        double v = baseValue * System.Math.Pow(growth, step) * multiplier;

        // 안전장치: 단계가 아주 커지면 Pow 가 무한대가 되고, 거기에 배율 0 을 곱하면 NaN(숫자 아님)이 됩니다.
        // NaN 은 어떤 비교도 false 라 아래 상한 검사를 그냥 통과하므로 먼저 걸러 냅니다.
        // (지금은 골드 배율이 항상 1, 보석은 성장률 1 이라 실제로는 생기지 않지만, 값이 바뀌어도 안전하게)
        if (double.IsNaN(v)) v = 0;
        if (v > int.MaxValue) v = int.MaxValue;   // int 오버플로 방지

        // ★ [배포 전 검토] 반올림 방식을 "사사오입" 으로 명시.
        //   C# 의 Math.Round(x) 기본값은 '은행가 반올림(짝수 쪽으로)' 이라 22.5 → 22, 12.5 → 12 가 됩니다.
        //   예: gemBase 15 × 배율 1.5 = 22.5 → 기획자는 23 을 기대하는데 22 가 지급됨.
        //   보석은 이제 '기준값 × 배율' 이라 정확히 .5 가 자주 나오므로 AwayFromZero(0.5 는 올림)로 고정합니다.
        //   골드는 1.12^단계 같은 값이라 정확히 .5 가 거의 안 나와서 사실상 변화가 없습니다.
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