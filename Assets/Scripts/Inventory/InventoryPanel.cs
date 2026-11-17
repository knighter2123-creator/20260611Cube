using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인벤토리 창 — 가진 아이템을 격자로, 누르면 상세창. 단독 창·탭 둘 다 가능.</summary>
public class InventoryPanel : MonoBehaviour, ITabPage
{
    [Serializable]
    public struct FilterTab
    {
        [Tooltip("보여 줄 itemType (비우면 전체)")]
        public string     itemType;
        public Button     button;
        public GameObject selectedMark;
    }

    [Header("창 (탭으로 쓰면 비워 둠)")]
    [Tooltip("비우면 이 오브젝트 자신")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Button     closeButton;
    [SerializeField] private Button     dimButton;

    [Header("목록")]
    [SerializeField] private Transform     gridContent;
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private GameObject    emptyMark;
    [Tooltip("{count} = 종류 수")]
    [SerializeField] private TMP_Text      summaryText;
    [SerializeField] private string        summaryFormat = "아이템 {count}종";

    [Header("분류 탭 (선택)")]
    [SerializeField] private FilterTab[]   filters = new FilterTab[0];

    [Header("상세창")]
    [SerializeField] private InventoryItemDetail detail;

    private readonly List<InventorySlot> slots = new List<InventorySlot>();
    private readonly List<(ItemDef def, int count)> rows = new List<(ItemDef, int)>();
    private int  filterIndex = -1;   // -1 = 전체
    private bool dirty;

    private void Awake()
    {
        for (int i = 0; i < filters.Length; i++)
        {
            int index = i;
            if (filters[i].button != null) filters[i].button.onClick.AddListener(() => SelectFilter(index));
        }
        if (filters.Length > 0) filterIndex = 0;
        if (detail != null) detail.Hide();
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (dimButton   != null) dimButton.onClick.AddListener(Close);
    }

    private GameObject Window => windowRoot != null ? windowRoot : gameObject;

    public void Open()
    {
        GameObject w = Window;
        if (w.activeSelf) Refresh();
        else              w.SetActive(true);
        w.transform.SetAsLastSibling();
    }

    public void Close() => Window.SetActive(false);

    private void OnEnable()
    {
        Inventory.OnChanged += MarkDirty;
        ItemChart.OnChanged += MarkDirty;
        Refresh();
    }

    private void OnDisable()
    {
        Inventory.OnChanged -= MarkDirty;
        ItemChart.OnChanged -= MarkDirty;
        if (detail != null) detail.Hide();
    }

    // 한 프레임에 여러 번 바뀌어도 1번만 그림
    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        if (dirty) Refresh();
    }

    public void OnTabShow() => MarkDirty();   // 켜질 때 OnEnable 이 이미 그림
    public void OnTabHide() { if (detail != null) detail.Hide(); }

    private void SelectFilter(int index)
    {
        filterIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        dirty = false;
        if (gridContent == null || slotPrefab == null)
        {
            Debug.LogWarning("[InventoryPanel] Grid Content 또는 Slot Prefab 이 연결되지 않았습니다.", this);
            return;
        }

        string type = filterIndex >= 0 && filterIndex < filters.Length ? filters[filterIndex].itemType : null;

        rows.Clear();
        foreach (KeyValuePair<string, int> kv in Inventory.Snapshot())
        {
            ItemDef def = ItemChart.Resolve(kv.Key);
            if (!string.IsNullOrEmpty(type) && def.itemType != type) continue;
            rows.Add((def, kv.Value));
        }
        rows.Sort((a, b) =>
        {
            int c = a.def.sortOrder.CompareTo(b.def.sortOrder);
            return c != 0 ? c : string.CompareOrdinal(a.def.name, b.def.name);
        });

        slots.RemoveAll(s => s == null);
        while (slots.Count < rows.Count)
        {
            InventorySlot s = Instantiate(slotPrefab, gridContent);
            s.Init(OpenDetail);
            slots.Add(s);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool on = i < rows.Count;
            slots[i].gameObject.SetActive(on);
            if (on) slots[i].Bind(rows[i].def, rows[i].count);
        }

        if (emptyMark   != null) emptyMark.SetActive(rows.Count == 0);
        if (summaryText != null) summaryText.text = ChatText.Fill(summaryFormat, "count", rows.Count.ToString());

        for (int i = 0; i < filters.Length; i++)
            if (filters[i].selectedMark != null) filters[i].selectedMark.SetActive(i == filterIndex);
    }

    private void OpenDetail(ItemDef def)
    {
        if (detail == null)
        {
            Debug.LogWarning("[InventoryPanel] 상세창(InventoryItemDetail)이 연결되지 않았습니다.", this);
            return;
        }
        detail.Show(def.itemId);
    }
}
