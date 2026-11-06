using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>동료 도감 탭: 풀 에셋(없으면 GachaSystem 풀) + Extra + 보유 동료를 id 로 합쳐 등급순 격자로 보여 주고 누르면 상세창 — 보여주기만 하고 구독 없이 열 때마다 새로 읽음.</summary>
// [붙이는 곳] TabWindow 의 Codex_Content / CompanionPool 에셋에 넣으면 가챠와 도감에 동시 등록 / 보유 판정은 id 기준(가챠 중복과 같음)
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

    // ── 내부 상태 — 만들어 둔 칸들(재사용) ──
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

    // ── Unity 생명주기 ──
    private void Awake()
    {
        // 상세창은 닫힌 채 시작 (켜 둔 채 저장해도) — Unity 오브젝트라 ?. 대신 != null (?. 는 파괴를 못 거름)
        if (detailPanel != null)
        {
            detailPanel.Hide();
            // 진화로 보유 목록이 바뀌면 격자(보유 표시 · 수집 수)를 다시 그립니다.
            detailPanel.OnOwnedChanged += Refresh;
        }
    }

    private void OnDestroy()
    {
        if (detailPanel != null) detailPanel.OnOwnedChanged -= Refresh;
    }

    /// <summary>도감 풀 — 칸이 비면 가챠의 풀 (진화 대상도 여기서 찾음).</summary>
    private CompanionPoolAsset Pool
        => poolAsset != null ? poolAsset
         : GachaSystem.Instance != null ? GachaSystem.Instance.Pool : null;

    // ── ITabPage — TabWindow 가 부른다 ──

    /// <summary>도감 탭이 보이기 시작했다 → 목록을 다시 그린다.</summary>
    public void OnTabShow()
    {
        // 지난번에 열어둔 상세창이 남아 있으면 목록을 가리므로 닫고 시작합니다.
        if (detailPanel != null) detailPanel.Hide();
        Refresh();
    }

    /// <summary>탭이 가려지면 상세창 닫기 (안 하면 다음에 열 때 지난 상세창이 먼저 보임).</summary>
    public void OnTabHide()
    {
        if (detailPanel != null) detailPanel.Hide();
    }

    // ── 목록 갱신 ──
    public void Refresh()
    {
        if (gridContent == null || itemPrefab == null)
        {
            Debug.LogWarning("[CompanionCodexUI] Grid Content 또는 Item Prefab 이 연결되지 않았습니다.", this);
            return;
        }

        // 보유 목록을 먼저 읽음 (BuildEntries 가 '보유 중인데 풀에 없는 동료'를 끼워 넣음)
        BuildOwnedIds();
        BuildEntries();

        // 파괴된 칸을 먼저 뺌 (죽은 칸을 세면 새 칸을 안 만들어 그 순번 동료가 빠짐)
        spawnedItems.RemoveAll(item => item == null);

        // ── 칸이 모자란 만큼만 새로 만듦 (매번 Destroy → Instantiate 면 한 프레임 두 배 길이 + GC) ──
        while (spawnedItems.Count < entries.Count)
        {
            CompanionCodexItem item = Instantiate(itemPrefab, gridContent);
            item.Init(HandleItemClicked);
            spawnedItems.Add(item);
        }

        // ── 수집 수는 칸이 아닌 목록으로 셈 (칸 루프에서 세면 빠진 칸의 동료가 누락) ──
        int ownedCount = 0;
        foreach (CompanionData data in entries)
            if (ownedIds.Contains(data.id)) ownedCount++;

        // ── 앞에서부터 채우고, 남는 칸은 숨긴다 ──
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            CompanionCodexItem item = spawnedItems[i];

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

        // ① 가챠 풀 (내 칸 → 없으면 가챠의 풀)
        CompanionPoolAsset pool = Pool;
        if (pool != null)
        {
            foreach (CompanionData d in pool.All()) TryAddEntry(d);
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

        // ③ 보유 중인데 ①② 에 없는 동료(코드 지급·풀에서 빠뜨림 등)도 넣음 — 빠지면 수집 수가 작게 나옴, 에셋 수정용 경고는 id 당 1회
        AddOwnedButUnlisted();

        // ④ 정렬 — 등급 높은 순·같은 등급은 넣은 순서 유지 (List.Sort 는 불안정, 등급 4개라 높은 등급부터 훑어 담기, 임시 리스트는 ListPool)
        using (ListPool<CompanionData>.Get(out List<CompanionData> sorted))
        {
            for (CompanionGrade g = CompanionGrade.Legendary; g >= CompanionGrade.Normal; g--)
                foreach (CompanionData d in entries)
                    if (d.grade == g) sorted.Add(d);

            // 위 4등급 밖의 값(새 등급 추가 등)도 빠지지 않게 마지막에 붙입니다.
            if (sorted.Count != entries.Count)
                foreach (CompanionData d in entries)
                    if (d.grade > CompanionGrade.Legendary || d.grade < CompanionGrade.Normal) sorted.Add(d);

            entries.Clear();
            entries.AddRange(sorted);
        }
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
            // 같은 에셋 중복은 정상(한 번만 표시), 다른 에셋인데 id 가 같으면 세이브가 구분 못 하는 버그라 경고
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

        IReadOnlyList<CompanionData> owned = cm.GetOwnedCompanionData();
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

        // Instance 를 매번 새로 읽음 (재생성돼도 살아 있는 인스턴스)
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;   // 매니저가 없으면 전부 미획득으로 표시 (도감은 그래도 뜸)

        IReadOnlyList<CompanionData> owned = cm.GetOwnedCompanionData();
        if (owned == null) return;

        foreach (CompanionData d in owned)
            if (d != null && !string.IsNullOrEmpty(d.id))
                ownedIds.Add(d.id);
    }

    // ── 상세창 ──
    private void HandleItemClicked(CompanionData data, bool isOwned)
    {
        if (detailPanel == null)
        {
            Debug.LogWarning("[CompanionCodexUI] Detail Panel 이 연결되지 않아 상세정보를 열 수 없습니다.", this);
            return;
        }
        detailPanel.Show(data, isOwned, Pool);
    }
}