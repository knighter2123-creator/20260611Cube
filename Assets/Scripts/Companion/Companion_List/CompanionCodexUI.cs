using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 동료 도감 탭. (신규)
///
/// 게임에 존재하는 모든 동료를 격자로 보여주고, 아이콘을 누르면 상세창을 엽니다.
///
/// ■ 목록은 어디서 오나
///   1) Pool Asset (가챠와 같은 CompanionPool 에셋)          ← 권장
///   2) 비어 있으면 GachaSystem.Instance 의 풀                 ← 예비 경로
///   3) + Extra Companions (가챠에서 안 나오는 동료)
///   → 같은 id 는 한 번만, 등급 높은 순으로 정렬
///
///   CompanionPool 에셋에 동료를 넣기만 하면 가챠와 도감에 동시에 들어갑니다.
///   도감 쪽은 아무것도 건드릴 필요가 없습니다.
///
/// ■ 보유 여부는 어디서 오나
///   CompanionManager.GetOwnedCompanionData() 를 id 로 비교합니다.
///   (이름이나 에셋 참조가 아니라 id — 가챠 중복 판정과 같은 기준)
///
/// ■ 이 스크립트가 하지 않는 것
///   · 세이브를 읽거나 쓰지 않습니다. 보여주기만 합니다.
///   · 매니저 이벤트를 구독하지 않습니다. 탭이 열릴 때마다 새로 읽습니다.
///     동료를 얻는 곳(가챠)은 다른 씬이라, 도감을 보고 있는 동안 보유 목록이 바뀔 일이 없습니다.
///     구독이 없으니 "매니저가 재생성됐는데 옛 인스턴스를 붙잡고 있는" 문제도 생기지 않습니다.
///
/// ★ 붙이는 곳: TabWindow 의 Codex_Content (탭 안). CompanionListUI 와 같은 자리 규칙입니다.
/// </summary>
public class CompanionCodexUI : MonoBehaviour, ITabPage
{
    [Header("도감 목록 출처")]
    [Tooltip("가챠와 같은 CompanionPool 에셋. 비워두면 GachaSystem.Instance 의 풀을 씁니다.\n" +
             "★ GachaSystem 은 가챠 씬에서 생기므로, 가챠 씬에 가기 전에는 비어 있을 수 있습니다. 연결을 권장합니다.")]
    [SerializeField] private CompanionPoolAsset poolAsset;

    [Tooltip("가챠에서 나오지 않는 동료 (시작 동료, 이벤트 보상 등). 여기 넣으면 도감에도 뜹니다.")]
    [SerializeField] private List<CompanionData> extraCompanions = new List<CompanionData>();

    [Header("목록 UI")]
    [Tooltip("아이콘들이 들어갈 부모. 보통 ScrollView/Viewport/Content (Grid Layout Group 권장)")]
    [SerializeField] private Transform          gridContent;
    [SerializeField] private CompanionCodexItem itemPrefab;

    [Tooltip("\"수집 3 / 12\" 표시 (선택)")]
    [SerializeField] private TMP_Text progressText;

    [Header("상세 패널")]
    [SerializeField] private CompanionDetailPanel detailPanel;

    // ── 내부 상태 ──
    // 만들어 둔 칸들. 부수지 않고 재사용합니다.
    private readonly List<CompanionCodexItem> spawnedItems = new List<CompanionCodexItem>();

    // 이번에 표시할 동료 목록 (중복 제거·정렬 후)
    private readonly List<CompanionData> entries = new List<CompanionData>();

    // id → 먼저 등록된 에셋. 중복 판정 + "id 가 겹치는 서로 다른 에셋" 감지용
    private readonly Dictionary<string, CompanionData> entryById = new Dictionary<string, CompanionData>();

    // 보유 중인 동료 id. HashSet 이라 Contains 가 목록 길이와 무관하게 빠릅니다.
    private readonly HashSet<string> ownedIds = new HashSet<string>();

    // 같은 경고를 탭을 열 때마다 반복하지 않기 위한 기록
    private bool warnedNoSource;
    private readonly HashSet<string> warnedUnlistedIds = new HashSet<string>();

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        // ★ 상세창은 처음에 닫혀 있어야 합니다. 씬에 켜둔 채 저장해도 여기서 정리합니다.
        //
        // ★ 'detailPanel?.Hide()' 대신 'if (detailPanel != null)' 을 쓰는 이유
        //   UnityEngine.Object 의 == null 은 유니티가 따로 정의한 검사라서,
        //   "연결 안 됨" 뿐 아니라 "파괴됨" 도 null 로 취급합니다.
        //   그런데 ?. 는 C# 언어 기능이라 이 유니티 검사를 건너뜁니다.
        //   파괴된 오브젝트에 ?. 를 쓰면 null 이 아니라고 보고 들어가서 MissingReferenceException 이 납니다.
        //   (learnings 의 UnassignedReferenceException 이야기와 같은 뿌리입니다)
        if (detailPanel != null) detailPanel.Hide();
    }

    // ══════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════

    /// <summary>도감 탭이 보이기 시작했다 → 목록을 다시 그린다.</summary>
    public void OnTabShow()
    {
        // 지난번에 열어둔 상세창이 남아 있으면 목록을 가리므로 닫고 시작합니다.
        if (detailPanel != null) detailPanel.Hide();
        Refresh();
    }

    /// <summary>
    /// 다른 탭으로 넘어갔거나 창이 닫혔다 → 상세창을 닫는다.
    ///
    /// ★ 이걸 안 하면 상세창이 켜진 상태(activeSelf = true)로 남아서,
    ///   다음에 도감 탭을 열었을 때 목록 대신 지난번 상세창이 먼저 보입니다.
    /// </summary>
    public void OnTabHide()
    {
        if (detailPanel != null) detailPanel.Hide();
    }

    // ══════════════════════════════════════════════
    //  목록 갱신
    // ══════════════════════════════════════════════
    public void Refresh()
    {
        if (gridContent == null || itemPrefab == null)
        {
            Debug.LogWarning("[CompanionCodexUI] Grid Content 또는 Item Prefab 이 연결되지 않았습니다.", this);
            return;
        }

        // ★ [수정] 순서를 바꿨습니다: 보유 목록을 '먼저' 읽습니다.
        //   BuildEntries 가 "보유 중인데 풀에 없는 동료"를 목록에 끼워 넣어야 하기 때문입니다.
        BuildOwnedIds();
        BuildEntries();

        // ── 칸이 모자라면 그만큼만 새로 만든다 ──
        // ★ CompanionListUI 는 매번 전부 Destroy → Instantiate 합니다.
        //   도감은 동료 수가 계속 늘어나는 화면이라 처음부터 재사용 방식으로 만듭니다.
        //   · Destroy 는 프레임 끝에 실행돼서, 한 프레임 동안 옛 칸 + 새 칸이 같이 존재합니다
        //     (Grid 가 순간적으로 두 배 길이가 됨)
        //   · 탭을 열 때마다 GC 쓰레기가 생깁니다
        while (spawnedItems.Count < entries.Count)
        {
            CompanionCodexItem item = Instantiate(itemPrefab, gridContent);
            item.Init(HandleItemClicked);
            spawnedItems.Add(item);
        }

        // ── 수집 수는 '칸' 이 아니라 '목록' 으로 셉니다 ──
        // ★ [수정] 예전에는 아래 칸 채우기 루프 안에서 셌습니다.
        //   그 루프는 'item == null 이면 continue' 라서, 칸 하나가 사라져 있으면
        //   그 동료는 보유 중이어도 숫자에서 빠졌습니다.
        //   "화면 그리기" 와 "숫자 세기" 는 서로 다른 일이라 따로 둡니다.
        int ownedCount = 0;
        foreach (CompanionData data in entries)
            if (ownedIds.Contains(data.id)) ownedCount++;

        // ── 앞에서부터 채우고, 남는 칸은 숨긴다 ──
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            CompanionCodexItem item = spawnedItems[i];
            if (item == null) continue;   // 누가 칸을 지웠을 때 대비 (정상 흐름에선 없음)

            if (i < entries.Count)
            {
                CompanionData data = entries[i];
                item.gameObject.SetActive(true);
                item.Bind(data, ownedIds.Contains(data.id));
            }
            else
            {
                // 동료 수가 줄어드는 일은 드물지만(에셋에서 뺐을 때), 빈 칸이 남지 않게 끕니다.
                item.gameObject.SetActive(false);
            }
        }

        if (progressText != null)
            progressText.text = $"수집 {ownedCount} / {entries.Count}";
    }

    /// <summary>도감에 올릴 동료 목록을 만든다 (출처 합치기 → 중복 제거 → 정렬).</summary>
    private void BuildEntries()
    {
        entries.Clear();
        entryById.Clear();

        // ① 가챠 풀
        if (poolAsset != null)
        {
            foreach (CompanionData d in poolAsset.All()) TryAddEntry(d);
        }
        else if (GachaSystem.Instance != null)
        {
            foreach (CompanionData d in GachaSystem.Instance.GetAllPoolCompanions()) TryAddEntry(d);
        }
        else if (!warnedNoSource)
        {
            warnedNoSource = true;
            Debug.LogWarning("[CompanionCodexUI] Pool Asset 이 비어 있고 GachaSystem 도 아직 없습니다. " +
                             "가챠 씬에 가기 전에는 도감이 비어 보입니다. Pool Asset 을 연결하세요.", this);
        }

        // ② 가챠 밖 동료
        if (extraCompanions != null)
            foreach (CompanionData d in extraCompanions) TryAddEntry(d);

        // ③ [수정] 보유 중인데 ①② 어디에도 없는 동료
        // ★ 가져본 동료는 "게임에 존재하는 동료" 가 분명하니 도감에서 빠지면 안 됩니다.
        //   빠지면 수집 숫자가 실제 보유 수보다 작게 나옵니다. (이번 증상의 가장 흔한 원인)
        //   예) 시작 동료처럼 코드로 지급하는 동료, 풀 에셋으로 옮기면서 빠뜨린 동료,
        //       GachaSystem 이 아직 구버전 리스트를 쓰고 있어 에셋과 내용이 다른 경우
        //   목록에는 넣어 주되, 에셋 쪽을 고칠 수 있게 경고는 id 당 한 번만 남깁니다.
        AddOwnedButUnlisted();

        // ④ 정렬 — 등급 높은 순.
        // ★ LINQ 의 OrderBy 는 '안정 정렬' 입니다. 같은 등급끼리는 에셋에 넣은 순서가 유지됩니다.
        //   List.Sort 는 안정 정렬이 아니라서 같은 등급의 순서가 열 때마다 바뀔 수 있습니다.
        //   ToList() 가 새 리스트를 만들긴 하지만, 탭을 열 때 한 번이라 성능 영향은 무시할 수준입니다.
        List<CompanionData> sorted = entries.OrderByDescending(d => d.grade).ToList();
        entries.Clear();
        entries.AddRange(sorted);
    }

    private void TryAddEntry(CompanionData d)
    {
        // 인스펙터에서 칸만 늘려두고 비워둔 경우
        if (d == null) return;

        // ★ id 가 비어 있으면 보유 판정을 할 수 없습니다 (세이브도 누락됩니다 — CompanionManager 경고와 같은 원인).
        if (string.IsNullOrEmpty(d.id))
        {
            Debug.LogWarning($"[CompanionCodexUI] '{d.companionName}' 의 id 가 비어 있어 도감에서 제외합니다.", d);
            return;
        }

        if (entryById.TryGetValue(d.id, out CompanionData existing))
        {
            // 같은 에셋이 여러 풀에 들어 있는 경우 → 정상. 조용히 한 번만 표시.
            // ★ '다른 에셋' 인데 id 가 같다면 → 세이브가 둘을 구분하지 못하는 진짜 버그. 알립니다.
            if (existing != d)
                Debug.LogError($"[CompanionCodexUI] 서로 다른 동료 '{existing.name}' 와 '{d.name}' 의 id 가 " +
                               $"'{d.id}' 로 같습니다. 세이브에서 둘이 섞입니다. id 를 고쳐주세요.", d);
            return;
        }

        entryById.Add(d.id, d);
        entries.Add(d);
    }

    private void AddOwnedButUnlisted()
    {
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        List<CompanionData> owned = cm.GetOwnedCompanionData();
        if (owned == null) return;

        foreach (CompanionData d in owned)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) continue;
            if (entryById.ContainsKey(d.id)) continue;   // 이미 풀에 있음 → 정상

            // HashSet.Add 는 '처음 넣을 때만 true' 를 돌려줍니다 → 경고를 한 번만 찍는 관용구
            if (warnedUnlistedIds.Add(d.id))
                Debug.LogWarning($"[CompanionCodexUI] 보유 중인 '{d.companionName}'({d.id}) 이(가) " +
                                 "CompanionPool 에도 Extra Companions 에도 없습니다. 도감에는 표시하지만, " +
                                 "둘 중 한 곳에 추가해 두세요 (미보유 유저의 도감에는 안 보입니다).", d);

            TryAddEntry(d);
        }
    }

    /// <summary>현재 보유 중인 동료 id 를 모은다.</summary>
    private void BuildOwnedIds()
    {
        ownedIds.Clear();

        // ★ Instance 를 매번 새로 읽습니다 (필드에 보관하지 않음).
        //   매니저가 재생성돼도 항상 '지금 살아 있는' 인스턴스를 봅니다.
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;   // 매니저가 없으면 전부 미획득으로 표시 (도감은 그래도 뜸)

        List<CompanionData> owned = cm.GetOwnedCompanionData();
        if (owned == null) return;

        foreach (CompanionData d in owned)
            if (d != null && !string.IsNullOrEmpty(d.id))
                ownedIds.Add(d.id);
    }

    // ══════════════════════════════════════════════
    //  상세창
    // ══════════════════════════════════════════════
    private void HandleItemClicked(CompanionData data, bool isOwned)
    {
        if (detailPanel == null)
        {
            Debug.LogWarning("[CompanionCodexUI] Detail Panel 이 연결되지 않아 상세정보를 열 수 없습니다.", this);
            return;
        }
        detailPanel.Show(data, isOwned);
    }
}