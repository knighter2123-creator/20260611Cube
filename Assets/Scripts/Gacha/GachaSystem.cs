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

    // ★ [도감] 신규 — 풀 에셋.
    //   연결하면 아래 구버전 리스트 대신 이 에셋을 씁니다. 도감도 같은 에셋을 봅니다.
    //   비워두면 예전과 똑같이 동작합니다 (기존 씬이 깨지지 않게 하기 위한 호환 경로).
    [Header("동료 풀 에셋 (권장) — 도감과 공유")]
    [Tooltip("연결하면 아래 등급별 리스트는 무시됩니다.\n" +
             "옮기는 법: 에셋 연결 → 이 컴포넌트 우클릭 → '풀 → 에셋으로 복사'")]
    [SerializeField] private CompanionPoolAsset poolAsset;

    [Header("동료 풀 (등급별) — 구버전. 위 에셋이 비었을 때만 사용")]
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

        // 보석 부족 → 빈 결과를 돌려줍니다. (호출한 UI 가 results.Count == 0 으로 판단)
        if (!CurrencyManager.Instance.SpendGem(cost))
            return results;   // 실패 → 여기서 리턴, 카운트 안 오름 (정상)

        for (int i = 0; i < count; i++)
        {
            CompanionData data = GetRandomCompanion();
            if (data == null) continue;

            GachaResult result = ProcessResult(data);
            results.Add(result);
        }

        // ★ 미션 진행도 리포트 (실제 뽑힌 개수만큼)
        if (results.Count > 0)
        {
            MissionManager.Instance?.ReportGachaPull(results.Count);

            // ★ 가이드 퀘스트: 동료 소환
            GuideQuestManager.Instance?.ReportSummon(results.Count);
        }

        // ★ 젬 차감 + 동료/조각 획득을 한 번에 저장 (루프 밖에서 1회)
        SaveManager.Instance?.Save();

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
        CompanionGrade grade = RollGrade();

        // ★ [도감] List → IReadOnlyList 로 바뀌었습니다. Count 와 [i] 는 그대로 쓸 수 있습니다.
        IReadOnlyList<CompanionData> pool = GetPool(grade);

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

    /// <summary>
    /// ★ [도감] 에셋이 있으면 에셋, 없으면 구버전 리스트.
    ///   List&lt;T&gt; 는 IReadOnlyList&lt;T&gt; 를 구현하므로 구버전 리스트도 그대로 돌려줄 수 있습니다.
    /// </summary>
    private IReadOnlyList<CompanionData> GetPool(CompanionGrade grade)
    {
        // ★ 'poolAsset != null' 은 유니티식 null 검사입니다 (연결 안 됨 / 파괴됨 모두 걸러냄).
        //   'poolAsset?.GetPool()' 처럼 ?. 를 쓰면 이 유니티식 검사를 건너뛰니 쓰지 않습니다.
        if (poolAsset != null) return poolAsset.GetPool(grade);

        return grade switch
        {
            CompanionGrade.Normal    => normalPool,
            CompanionGrade.Rare      => rarePool,
            CompanionGrade.Epic      => epicPool,
            CompanionGrade.Legendary => legendaryPool,
            _                        => normalPool
        };
    }

    // ──────────────────────────────────────────────
    //  [도감] 조회
    // ──────────────────────────────────────────────

    /// <summary>
    /// 가챠에 등록된 모든 동료. (중복 제거는 하지 않음 — 받는 쪽이 id 로 거릅니다)
    /// 도감이 풀 에셋을 직접 연결하지 않았을 때의 예비 경로입니다.
    /// </summary>
    public IEnumerable<CompanionData> GetAllPoolCompanions()
    {
        if (poolAsset != null)
        {
            foreach (var d in poolAsset.All()) yield return d;
            yield break;   // ★ yield 함수에서 '여기서 끝' 은 return 이 아니라 yield break 입니다
        }

        foreach (var d in EnumerateOrEmpty(normalPool))    yield return d;
        foreach (var d in EnumerateOrEmpty(rarePool))      yield return d;
        foreach (var d in EnumerateOrEmpty(epicPool))      yield return d;
        foreach (var d in EnumerateOrEmpty(legendaryPool)) yield return d;
    }

    // 리스트가 null 이면 빈 목록처럼 취급 (foreach 에 null 을 넣으면 예외가 납니다)
    private static IEnumerable<CompanionData> EnumerateOrEmpty(List<CompanionData> list)
        => list ?? (IEnumerable<CompanionData>)System.Array.Empty<CompanionData>();

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터의 구버전 리스트를 poolAsset 으로 복사합니다. (한 번만 쓰면 됨)
    /// 컴포넌트 제목줄 우클릭 → 메뉴 맨 아래에 나옵니다.
    /// </summary>
    [ContextMenu("풀 → 에셋으로 복사")]
    private void CopyLegacyPoolsToAsset()
    {
        if (poolAsset == null)
        {
            Debug.LogError("[Gacha] 먼저 Pool Asset 칸에 CompanionPool 에셋을 연결하세요.", this);
            return;
        }

        // Undo 에 기록해 두면 Ctrl+Z 로 되돌릴 수 있습니다.
        UnityEditor.Undo.RecordObject(poolAsset, "Copy Gacha Pools");
        poolAsset.CopyFrom(normalPool, rarePool, epicPool, legendaryPool);

        // ★ 코드로 에셋을 바꾸면 '바뀌었다' 고 알려야 저장됩니다. 안 하면 에디터를 끄는 순간 사라집니다.
        UnityEditor.EditorUtility.SetDirty(poolAsset);
        UnityEditor.AssetDatabase.SaveAssets();

        Debug.Log($"[Gacha] 구버전 풀을 '{poolAsset.name}' 에셋으로 복사했습니다.", poolAsset);
    }
#endif
}