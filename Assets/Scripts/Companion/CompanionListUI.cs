using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 동료 목록.
///
/// 두 가지 모드로 씁니다.
///   · 단독 패널 모드 — 기존처럼 열기 버튼으로 패널을 켜고 끕니다.
///   · 탭 모드       — TabWindow 의 한 탭으로 들어갑니다. 패널을 켜고 끄는 일은 TabWindow 가 합니다.
/// </summary>
public class CompanionListUI : MonoBehaviour, ITabPage
{
    [Header("동작 모드")]
    [Tooltip("TabWindow 안의 한 탭으로 쓸 때 체크하세요.\n" +
             "체크하면 companionListPanel 과 openButton 을 쓰지 않습니다. TabWindow 가 담당합니다.")]
    [SerializeField] private bool useAsTabPage = false;

    [Header("패널")]
    [SerializeField] private GameObject companionListPanel;

    [Header("버튼")]
    [SerializeField] private Button openButton;
    

    [Header("동료 목록")]
    [SerializeField] private Transform  companionListContent;
    [SerializeField] private GameObject companionItemPrefab;

    void Awake()
    {
        // ★ 탭 모드에서는 패널 소유권을 TabWindow 에 넘깁니다.
        //   둘이 같은 오브젝트를 SetActive 하면 "탭을 바꿨는데 목록이 도로 켜지는" 상태가 됩니다.
        if (useAsTabPage)
        {
            companionListPanel = null;
            openButton         = null;
        }
    }

    void Start()
    {
        // ★ 원래는 null 검사가 하나도 없었습니다.
        //   탭 모드처럼 openButton 을 비워두는 구성에서는 이 줄에서 NullReferenceException 이 나고,
        //   예외가 나면 Start 가 거기서 끊겨 뒤의 초기화가 통째로 실행되지 않습니다.
        //   "연결을 하나 빠뜨렸을 때 게임이 멈추지 않게" — 스탯창에서 쓴 원칙과 같습니다.
        if (openButton  != null) openButton.onClick.AddListener(OpenCompanionList);

        if (companionListPanel != null) companionListPanel.SetActive(false);
    }

    void OnDestroy()
    {
        // 등록한 리스너는 등록한 쪽이 해제합니다.
        if (openButton  != null) openButton.onClick.RemoveListener(OpenCompanionList);
    }

    // ══════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════

    /// <summary>이 탭이 선택됐다 → 목록을 다시 만든다.</summary>
    public void OnTabShow() => RefreshCompanionList();

    /// <summary>
    /// 다른 탭으로 넘어갔다 → 진행 중인 배치 모드를 취소한다.
    ///
    /// ★ 이걸 빼먹으면 "배치할 위치를 탭하세요" 안내와 반투명 미리보기가 화면에 남은 채
    ///   강화 탭이 열립니다. 그 상태에서 맵을 누르면 동료가 배치돼 버립니다.
    /// </summary>
    public void OnTabHide() => CompanionPlacementController.Instance?.CancelPlacement();

    // ══════════════════════════════════════════════
    //  단독 패널 모드
    // ══════════════════════════════════════════════
    public void OpenCompanionList()
    {
        if (companionListPanel != null) companionListPanel.SetActive(true);
        RefreshCompanionList();
    }

    // ══════════════════════════════════════════════
    //  목록 갱신
    // ══════════════════════════════════════════════

    /// <summary>보유 동료로 목록을 다시 만든다. (탭을 열 때마다 호출됨)</summary>
    public void RefreshCompanionList()
    {
        if (companionListContent == null || companionItemPrefab == null)
        {
            Debug.LogWarning("[CompanionListUI] 목록 Content 또는 아이템 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        // ★ 매번 전부 지우고 다시 만듭니다.
        //   동료가 최대 6명이라 지금은 문제가 없지만, 수가 늘어나면
        //   '이미 있는 아이템은 Setup 만 다시 호출' 하는 재사용 방식으로 바꾸는 게 맞습니다.
        //   Destroy/Instantiate 는 GC 쓰레기를 만들고, 탭은 자주 열리니까요.
        foreach (Transform child in companionListContent)
            Destroy(child.gameObject);

        List<CompanionData> owned = CompanionManager.Instance?.GetOwnedCompanionData();
        if (owned == null || owned.Count == 0)
        {
            Debug.Log("[CompanionListUI] 보유 동료 없음");
            return;
        }

        foreach (CompanionData data in owned)
        {
            GameObject item = Instantiate(companionItemPrefab, companionListContent);
            CompanionListItem ui = item.GetComponent<CompanionListItem>();
            ui?.Setup(data);
        }
    }
}