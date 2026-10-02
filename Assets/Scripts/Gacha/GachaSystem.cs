using System.Collections.Generic;
using UnityEngine;

public class GachaSystem : MonoBehaviour
{
    public static GachaSystem Instance;

    // 결제 규칙: 소환권이 소환 횟수만큼 있으면 소환권(1회 = 1장)을 쓰고, 모자라면 아래 보석 비용을 씁니다.
    [Header("뽑기 비용 (보석) — 소환권이 모자랄 때")]
    [SerializeField] private int cost1   = 300;
    [SerializeField] private int cost10  = 2700;
    [SerializeField] private int cost100 = 27000;

    [Header("등급별 확률 (합계 100)")]
    [SerializeField] private float chanceNormal    = 50f;
    [SerializeField] private float chanceRare      = 30f;
    [SerializeField] private float chanceEpic      = 15f;
    [SerializeField] private float chanceLegendary = 5f;

    // 가챠에서 나오는 동료 목록. 도감(CompanionCodexUI)도 같은 에셋을 봅니다.
    [Header("동료 풀 에셋 — 도감과 공유")]
    [Tooltip("CompanionPool 에셋을 연결하세요. 비어 있으면 소환할 수 없습니다.")]
    [SerializeField] private CompanionPoolAsset poolAsset;

    // 중복 시 조각 전환량은 풀 에셋(CompanionPoolAsset)에 있습니다 — 진화(도감 씬)와 같은 값을 쓰기 위해.

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (poolAsset == null)
            Debug.LogError("[Gacha] Pool Asset 이 연결되지 않았습니다. 소환 결과가 비어 재화가 환불됩니다.", this);
    }

    // ──────────────────────────────────────────────
    //  뽑기 결과 구조체
    // ──────────────────────────────────────────────
    public struct GachaResult
    {
        public CompanionData data;
        public bool          isDuplicate;  // 중복 여부
        public int           fragmentGain; // 조각 획득량
    }

    // ──────────────────────────────────────────────
    //  뽑기 공개 API
    // ──────────────────────────────────────────────
    public List<GachaResult> DrawOne()    => Draw(1,   cost1);
    public List<GachaResult> DrawTen()    => Draw(10,  cost10);
    public List<GachaResult> DrawHundred()=> Draw(100, cost100);

    /// <summary>count 회 소환의 보석 비용 (1 / 10 / 100 회만 정의됨, 그 외는 -1).</summary>
    public int GemCostFor(int count) => count switch
    {
        1   => cost1,
        10  => cost10,
        100 => cost100,
        _   => -1
    };

    /// <summary>지금 count 회 소환하면 소환권으로 결제되는가. (버튼 비용 표시용 — 결제 규칙과 같은 판단)</summary>
    public static bool WillPayWithTickets(int count) => GachaTicket.Count >= count;

    /// <summary>어떤 재화로 결제했는가 — 결과가 없을 때 같은 재화로 되돌려 주기 위해 기억합니다.</summary>
    private enum Payment { None, Ticket, Gem }

    private List<GachaResult> Draw(int count, int gemCost)
    {
        List<GachaResult> results = new List<GachaResult>();

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("[Gacha] CurrencyManager가 없습니다.");
            return results;
        }

        // 재화 부족 → 빈 결과를 돌려줍니다. (호출한 UI 가 results.Count == 0 으로 판단)
        Payment paid = Pay(count, gemCost);
        if (paid == Payment.None)
            return results;

        for (int i = 0; i < count; i++)
        {
            CompanionData data = GetRandomCompanion();
            if (data == null) continue;

            GachaResult result = ProcessResult(data);
            results.Add(result);
        }

        if (results.Count > 0)
        {
            // ★ 미션 진행도 / 가이드 퀘스트 리포트 (실제 뽑힌 개수만큼)
            MissionManager.Instance?.ReportGachaPull(results.Count);
            GuideQuestManager.Instance?.ReportSummon(results.Count);
        }
        else
        {
            // 풀이 비어 한 장도 못 뽑았으면 결제를 되돌립니다. (안 그러면 재화만 사라집니다)
            Refund(paid, count, gemCost);
        }

        // ★ 재화 차감 + 동료/조각 획득을 한 번에 저장 (루프 밖에서 1회)
        SaveManager.Instance?.Save();

        return results;
    }

    /// <summary>소환권 우선, 모자라면 보석. 둘 다 안 되면 아무것도 차감하지 않고 None.</summary>
    private static Payment Pay(int count, int gemCost)
    {
        if (GachaTicket.TrySpend(count)) return Payment.Ticket;
        if (gemCost > 0 && CurrencyManager.Instance.SpendGem(gemCost)) return Payment.Gem;
        return Payment.None;
    }

    private static void Refund(Payment paid, int count, int gemCost)
    {
        Debug.LogWarning("[Gacha] 뽑을 동료가 없어 결제를 되돌립니다. 동료 풀을 확인하세요.");

        if (paid == Payment.Ticket) GachaTicket.Add(count);
        else if (paid == Payment.Gem) CurrencyManager.Instance?.AddGem(gemCost);
    }

    // ──────────────────────────────────────────────
    //  중복 처리
    // ──────────────────────────────────────────────
    private GachaResult ProcessResult(CompanionData data)
    {
        GachaResult result = new GachaResult { data = data };

        bool alreadyOwned = IsAlreadyOwned(data);

        if (alreadyOwned)
        {
            // ✅ 중복 → 조각으로 전환
            result.isDuplicate  = true;
            result.fragmentGain = GetFragmentAmount(data.grade);
            CompanionFragment.Instance?.AddFragment(data, result.fragmentGain);
        }
        else
        {
            // ✅ 신규 → 동료 획득
            result.isDuplicate  = false;
            result.fragmentGain = 0;
            CompanionManager.Instance?.AddCompanion(data);
        }

        return result;
    }

    /// <summary>
    /// 이미 보유한 동료인가 — 이름이나 에셋 참조가 아니라 id 로 비교합니다.
    /// (진단용 로그를 정리하면서, 세던 개수 변수(ownedCount)도 쓸 곳이 없어져 함께 뺐습니다)
    /// </summary>
    private bool IsAlreadyOwned(CompanionData data)
    {
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return false;   // 매니저가 없으면 신규로 처리 (기존 동작 유지)

        foreach (CompanionData owned in cm.GetOwnedCompanionData())
        {
            if (owned != null && owned.id == data.id)
                return true;
        }
        return false;
    }

    private int GetFragmentAmount(CompanionGrade grade)
        => poolAsset != null ? poolAsset.DuplicateFragments(grade) : 0;

    /// <summary>가챠가 쓰는 풀 에셋. 도감이 자기 칸을 비워 뒀을 때의 예비 경로입니다.</summary>
    public CompanionPoolAsset Pool => poolAsset;

    // ──────────────────────────────────────────────
    //  등급 및 동료 랜덤 선택
    // ──────────────────────────────────────────────
    private CompanionData GetRandomCompanion()
    {
        CompanionGrade grade = RollGrade();

        // 'poolAsset != null' 은 유니티식 null 검사입니다 (연결 안 됨 / 파괴됨 모두 걸러냄).
        IReadOnlyList<CompanionData> pool = poolAsset != null ? poolAsset.GetPool(grade) : null;

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[Gacha] {grade} 풀이 비어있습니다.");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    private CompanionGrade RollGrade()
    {
        float roll = Random.Range(0f, 100f);

        // ✅ 누적 방식으로 chanceNormal까지 전부 사용
        float legendaryThreshold = chanceLegendary;
        float epicThreshold      = legendaryThreshold + chanceEpic;
        float rareThreshold      = epicThreshold + chanceRare;
        float normalThreshold    = rareThreshold + chanceNormal; // ✅ chanceNormal 사용

        if (roll < legendaryThreshold) return CompanionGrade.Legendary;
        if (roll < epicThreshold)      return CompanionGrade.Epic;
        if (roll < rareThreshold)      return CompanionGrade.Rare;
        if (roll < normalThreshold)    return CompanionGrade.Normal;

        // ✅ 합계가 100이 아닐 경우 Normal로 폴백
        Debug.LogWarning($"[Gacha] 확률 합계가 100이 아닙니다. 현재 합계: {normalThreshold}");
        return CompanionGrade.Normal;
    }
}