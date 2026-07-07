using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MissionPopup : MonoBehaviour
{
    [Header("마스터 미션")]
    [SerializeField] private MasterMissionUI masterMissionUI;  // 리스트 상단 고정 행
    
    [Header("탭 강조")]
    [SerializeField] private Image dailyTabImage;      // 일일 탭 배경 이미지
    [SerializeField] private Image weeklyTabImage;     // 주간 탭 배경 이미지
    [SerializeField] private Color selectedTabColor   = Color.white;
    [SerializeField] private Color unselectedTabColor = new Color(0.6f, 0.6f, 0.6f); // 흐리게
    
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
    
    private bool subscribed = false;

    private void Awake()
    {
        dailyTabButton.onClick.AddListener(() => ShowTab(MissionType.Daily));
        weeklyTabButton.onClick.AddListener(() => ShowTab(MissionType.Weekly));
        if (claimAllButton != null) claimAllButton.onClick.AddListener(OnClickClaimAll);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }
    
    private void Start()
    {
        TrySubscribe();   // OnEnable에서 Instance가 아직 null이었어도 여기서 재시도
    }


    private void OnEnable()
    {
        TrySubscribe();
    }
    
    private void TrySubscribe()
    {
        if (subscribed) return;
        var mgr = MissionManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[MissionPopup] MissionManager.Instance == null (구독 보류)");
            return;
        }

        mgr.OnMissionUpdated += OnMissionUpdated;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        var mgr = MissionManager.Instance;
        if (mgr != null) mgr.OnMissionUpdated -= OnMissionUpdated;
        subscribed = false;
    }

    public void Open()
    {
        TrySubscribe();          // 혹시 아직 구독 안 됐으면 여기서
        popupRoot.SetActive(true);
        ShowTab(currentTab);     // 열 때 현재 데이터로 리빌드 → 최신 진행도 반영
    }
    
    public void Close() => popupRoot.SetActive(false);

    private void ShowTab(MissionType type)
    {
       
        currentTab = type;
        Rebuild();
        if (masterMissionUI != null) masterMissionUI.Bind(currentTab);
        RefreshClaimAllButton();
        RefreshTabHighlight();
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
    
    private void RefreshTabHighlight()
    {
        if (dailyTabImage != null)
            dailyTabImage.color = (currentTab == MissionType.Daily) ? selectedTabColor : unselectedTabColor;
        if (weeklyTabImage != null)
            weeklyTabImage.color = (currentTab == MissionType.Weekly) ? selectedTabColor : unselectedTabColor;
    }
}