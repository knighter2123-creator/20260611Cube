using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MissionPopup : MonoBehaviour
{
    [Header("마스터 미션")]
    [SerializeField] private MasterMissionUI masterMissionUI;  // 리스트 상단 고정 행
    
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Transform contentParent;
    [SerializeField] private MissionItemUI itemPrefab;

    [Header("탭")]
    [SerializeField] private Button dailyTabButton;
    [SerializeField] private Button weeklyTabButton;

    [Header("버튼")]
    [SerializeField] private Button claimAllButton;
    [SerializeField] private Button closeButton;

    private readonly List<MissionItemUI> spawnedItems = new List<MissionItemUI>();
    private MissionType currentTab = MissionType.Daily;

    private void Awake()
    {
        dailyTabButton.onClick.AddListener(() => ShowTab(MissionType.Daily));
        weeklyTabButton.onClick.AddListener(() => ShowTab(MissionType.Weekly));
        if (claimAllButton != null) claimAllButton.onClick.AddListener(OnClickClaimAll);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionUpdated += OnMissionUpdated;
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionUpdated -= OnMissionUpdated;
    }

    public void Open()
    {
        popupRoot.SetActive(true);
        ShowTab(currentTab);
    }

    public void Close() => popupRoot.SetActive(false);

    private void ShowTab(MissionType type)
    {
        Debug.Log($"[MissionPopup] ShowTab: {type}");
        currentTab = type;
        Rebuild();
        if (masterMissionUI != null) masterMissionUI.Bind(currentTab);
        RefreshClaimAllButton();
    }

    private void Rebuild()
    {
        foreach (var item in spawnedItems)
            if (item != null) Destroy(item.gameObject);
        spawnedItems.Clear();

        var mgr = MissionManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[MissionPopup] MissionManager.Instance == null");  // ← 추가
            return;
        }

        var list = mgr.GetMissions(currentTab);
        Debug.Log($"[MissionPopup] {currentTab} 미션 개수: {list.Count}");        // ← 추가

        foreach (var data in list)
        {
            var ui = Instantiate(itemPrefab, contentParent);
            ui.Bind(data);
            spawnedItems.Add(ui);
        }
    }

    // 진행도 변화·수령 시: 현재 항목 새로고침 + 모두수령 버튼 갱신
    private void OnMissionUpdated()
    {
        foreach (var item in spawnedItems)
            if (item != null) item.Refresh();
        if (masterMissionUI != null) masterMissionUI.Refresh();
        RefreshClaimAllButton();
    }

    private void RefreshClaimAllButton()
    {
        var mgr = MissionManager.Instance;
        if (mgr == null || claimAllButton == null) return;

        claimAllButton.interactable = mgr.GetClaimableCount(currentTab) > 0;
    }

    private void OnClickClaimAll()
    {
        var mgr = MissionManager.Instance;
        if (mgr == null) return;

        int claimed = mgr.ClaimAll(currentTab);
        // ClaimAll 내부에서 OnMissionUpdated 발행 → 항목/버튼 자동 갱신
        if (claimed == 0) Debug.Log("[MissionPopup] 수령 가능한 미션 없음");
    }
}