using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보유 동료 목록 — TabWindow 의 한 탭.
///
/// 창을 열고 닫는 일, 배치 중 창을 숨겼다가 되살리는 일은 전부 TabWindow 가 합니다.
/// 이 스크립트는 "탭이 보일 때 목록을 그린다" 와 "탭을 떠나면 배치를 취소한다" 만 책임집니다.
/// </summary>
public class CompanionListUI : MonoBehaviour, ITabPage
{
    [Header("동료 목록")]
    [SerializeField] private Transform  companionListContent;
    [SerializeField] private GameObject companionItemPrefab;

    // ══════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════

    /// <summary>이 탭이 선택됐다 (창을 열 때, 배치가 끝나 창이 되살아날 때 포함) → 목록을 다시 만든다.</summary>
    public void OnTabShow() => RefreshCompanionList();

    /// <summary>
    /// 다른 탭으로 넘어갔다 → 진행 중인 배치 모드를 취소한다.
    ///
    /// ★ 이걸 빼먹으면 "배치할 위치를 탭하세요" 안내와 반투명 미리보기가 화면에 남은 채
    ///   강화 탭이 열립니다. 그 상태에서 맵을 누르면 동료가 배치돼 버립니다.
    /// </summary>
    public void OnTabHide()
    {
        CompanionPlacementController pc = CompanionPlacementController.Instance;
        if (pc != null) pc.CancelPlacement();
    }

    // ══════════════════════════════════════════════
    //  목록 갱신
    // ══════════════════════════════════════════════

    /// <summary>보유 동료로 목록을 다시 만든다. (탭을 보여줄 때마다 호출됨)</summary>
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
        //   Destroy 는 프레임 끝에 지우므로, SetActive(false) 로 레이아웃에서 즉시 빼 줍니다.
        //   (안 그러면 같은 프레임 동안 옛 아이템과 새 아이템이 함께 줄을 섭니다)
        for (int i = companionListContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = companionListContent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        CompanionManager cm = CompanionManager.Instance;
        IReadOnlyList<CompanionData> owned = cm != null ? cm.GetOwnedCompanionData() : null;
        if (owned == null || owned.Count == 0)
        {
            Debug.Log("[CompanionListUI] 보유 동료 없음");
            return;
        }

        foreach (CompanionData data in owned)
        {
            if (data == null) continue;

            GameObject item = Instantiate(companionItemPrefab, companionListContent);

            // ★ GetComponent 결과에 ?. 를 쓰지 않습니다. 에디터에서는 컴포넌트가 없을 때 '가짜 null' 객체가 와서
            //   ?. 를 통과해 버립니다. TryGetComponent 는 할당도 없고 그런 함정도 없습니다.
            if (item.TryGetComponent(out CompanionListItem ui))
                ui.Setup(data);
            else
                Debug.LogWarning("[CompanionListUI] 아이템 프리팹에 CompanionListItem 이 없습니다.", companionItemPrefab);
        }
    }
}
