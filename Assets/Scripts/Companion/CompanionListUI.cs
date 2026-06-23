using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CompanionListUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject companionListPanel;

    [Header("버튼")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("동료 목록")]
    [SerializeField] private Transform  companionListContent;
    [SerializeField] private GameObject companionItemPrefab;

    void Start()
    {
        openButton.onClick.AddListener(OpenCompanionList);
        closeButton.onClick.AddListener(CloseCompanionList);

        companionListPanel.SetActive(false);
    }

    private void OpenCompanionList()
    {
        companionListPanel.SetActive(true);
        RefreshCompanionList();
    }

    private void CloseCompanionList()
    {
        companionListPanel.SetActive(false);
        // 목록을 닫을 때 배치 모드가 진행 중이면 같이 취소
        CompanionPlacementController.Instance?.CancelPlacement();
    }

    private void RefreshCompanionList()
    {
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
            ui?.Setup(data);   // 더 이상 패널을 넘기지 않음
        }
    }
}