using System.Collections.Generic;
using UnityEngine;

/// <summary>보유 동료 목록 탭 — 탭이 보일 때 목록을 그리고 떠나면 배치를 취소하는 것만 (창 열기·배치 중 숨기기는 TabWindow).</summary>
public class CompanionListUI : MonoBehaviour, ITabPage
{
    [Header("동료 목록")]
    [SerializeField] private Transform  companionListContent;
    [SerializeField] private GameObject companionItemPrefab;

    // ── ITabPage — TabWindow 가 부른다 ──

    /// <summary>이 탭이 선택됐다 (창을 열 때, 배치가 끝나 창이 되살아날 때 포함) → 목록을 다시 만든다.</summary>
    public void OnTabShow() => RefreshCompanionList();

    /// <summary>다른 탭으로 가면 배치 취소 (안 하면 안내·미리보기가 남아 맵을 누르면 배치됨).</summary>
    public void OnTabHide()
    {
        CompanionPlacementController pc = CompanionPlacementController.Instance;
        if (pc != null) pc.CancelPlacement();
    }

    // ── 목록 갱신 ──

    /// <summary>보유 동료로 목록을 다시 만든다. (탭을 보여줄 때마다 호출됨)</summary>
    public void RefreshCompanionList()
    {
        if (companionListContent == null || companionItemPrefab == null)
        {
            Debug.LogWarning("[CompanionListUI] 목록 Content 또는 아이템 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        // 매번 전부 다시 만듦(수가 늘면 재사용 방식으로) — Destroy 는 프레임 끝이라 SetActive(false) 로 즉시 레이아웃에서 뺌
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
            GameLog.Info("[CompanionListUI] 보유 동료 없음");
            return;
        }

        foreach (CompanionData data in owned)
        {
            if (data == null) continue;

            GameObject item = Instantiate(companionItemPrefab, companionListContent);

            // TryGetComponent — 에디터의 GetComponent '가짜 null' 은 ?. 를 통과하므로
            if (item.TryGetComponent(out CompanionListItem ui))
                ui.Setup(data);
            else
                Debug.LogWarning("[CompanionListUI] 아이템 프리팹에 CompanionListItem 이 없습니다.", companionItemPrefab);
        }
    }
}
