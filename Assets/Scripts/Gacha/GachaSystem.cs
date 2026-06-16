using System.Collections.Generic;
using UnityEngine;

public class GachaSystem : MonoBehaviour
{
    public static GachaSystem Instance;

    [Header("뽑기 비용 (보석)")]
    [SerializeField] private int cost1   = 300;
    [SerializeField] private int cost10  = 2700;
    [SerializeField] private int cost100 = 27000;

    [Header("등급별 확률 (합계 100)")]
    [SerializeField] private float chanceNormal    = 50f;
    [SerializeField] private float chanceRare      = 30f;
    [SerializeField] private float chanceEpic      = 15f;
    [SerializeField] private float chanceLegendary = 5f;

    [Header("동료 풀 (등급별)")]
    [SerializeField] private List<CompanionData> normalPool;
    [SerializeField] private List<CompanionData> rarePool;
    [SerializeField] private List<CompanionData> epicPool;
    [SerializeField] private List<CompanionData> legendaryPool;

    // ✅ 중복 조각 전환 수량 (등급별)
    [Header("중복 시 조각 전환량 (등급별)")]
    [SerializeField] private int fragmentNormal    = 5;
    [SerializeField] private int fragmentRare      = 10;
    [SerializeField] private int fragmentEpic      = 20;
    [SerializeField] private int fragmentLegendary = 50;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

    private List<GachaResult> Draw(int count, int cost)
    {
        List<GachaResult> results = new List<GachaResult>();

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("[Gacha] CurrencyManager가 없습니다.");
            return results;
        }

        if (!CurrencyManager.Instance.SpendGem(cost))
        {
            Debug.Log($"[Gacha] 보석 부족. 필요: {cost}");
            return results;
        }

        for (int i = 0; i < count; i++)
        {
            CompanionData data = GetRandomCompanion();
            if (data == null) continue;

            GachaResult result = ProcessResult(data);
            results.Add(result);
        }

        Debug.Log($"[Gacha] {count}회 뽑기 완료");
        return results;
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

            Debug.Log($"[Gacha] 중복 — {data.companionName} → 조각 +{result.fragmentGain}");
        }
        else
        {
            // ✅ 신규 → 동료 획득
            result.isDuplicate  = false;
            result.fragmentGain = 0;
            CompanionManager.Instance?.AddCompanion(data);

            Debug.Log($"[Gacha] 신규 — {data.companionName} 획득");
        }

        return result;
    }

    private bool IsAlreadyOwned(CompanionData data)
    {
        if (CompanionManager.Instance == null) return false;

        foreach (CompanionData owned in CompanionManager.Instance.GetOwnedCompanionData())
        {
            if (owned.companionName == data.companionName)
                return true;
        }
        return false;
    }

    private int GetFragmentAmount(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => fragmentNormal,
            CompanionGrade.Rare      => fragmentRare,
            CompanionGrade.Epic      => fragmentEpic,
            CompanionGrade.Legendary => fragmentLegendary,
            _                        => fragmentNormal
        };
    }

    // ──────────────────────────────────────────────
    //  등급 및 동료 랜덤 선택
    // ──────────────────────────────────────────────
    private CompanionData GetRandomCompanion()
    {
        CompanionGrade      grade = RollGrade();
        List<CompanionData> pool  = GetPool(grade);

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

    private List<CompanionData> GetPool(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => normalPool,
            CompanionGrade.Rare      => rarePool,
            CompanionGrade.Epic      => epicPool,
            CompanionGrade.Legendary => legendaryPool,
            _                        => normalPool
        };
    }
}