using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotSelectPanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panel;

    [Header("슬롯 버튼")]
    [SerializeField] private Transform  slotButtonContent;
    [SerializeField] private GameObject slotButtonPrefab;

    [Header("선택 동료 표시 (선택)")]
    [SerializeField] private TextMeshProUGUI selectedNameText;

    [Header("취소 버튼")]
    [SerializeField] private Button cancelButton;   // 슬롯 선택 취소 버튼

    private CompanionData     _pendingData;
    private CompanionListItem _callerItem;

    void Awake()
    {
        panel.SetActive(false);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    public void Open(CompanionData data, CompanionListItem caller)
    {
        _pendingData = data;
        _callerItem  = caller;

        if (selectedNameText != null)
            selectedNameText.text = $"{data.companionName} — 슬롯 선택";

        BuildSlotButtons();
        panel.SetActive(true);
    }

    public void Close()
    {
        foreach (Transform child in slotButtonContent)
            Destroy(child.gameObject);

        _pendingData = null;
        _callerItem  = null;
        panel.SetActive(false);
    }

    private void OnCancelClicked()
    {
        // 배치 없이 패널만 닫음
        CompanionListItem callerItem = _callerItem;
        Close();
        callerItem?.RefreshActionButtons();
    }

    private void BuildSlotButtons()
    {
        foreach (Transform child in slotButtonContent)
            Destroy(child.gameObject);

        int slotCount = CompanionManager.Instance?.SlotCount ?? 0;

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = i;

            GameObject      btn    = Instantiate(slotButtonPrefab, slotButtonContent);
            Button          button = btn.GetComponent<Button>();
            TextMeshProUGUI label  = btn.GetComponentInChildren<TextMeshProUGUI>();

            Companion occupant = CompanionManager.Instance.GetSlotOccupant(slotIndex);

            if (label != null)
                label.text = occupant != null
                    ? $"슬롯 {slotIndex + 1}\n({occupant.CompanionName})"
                    : $"슬롯 {slotIndex + 1}\n(비어 있음)";

            button.onClick.AddListener(() => OnSlotSelected(slotIndex));
        }
    }

    private void OnSlotSelected(int slotIndex)
    {
        if (_pendingData == null) return;

        CompanionManager  cm         = CompanionManager.Instance;
        CompanionListItem callerItem = _callerItem;

        Companion occupant = cm.GetSlotOccupant(slotIndex);
        if (occupant != null)
            cm.RetrieveCompanion(occupant);

        Companion existing = cm.GetOwnedCompanions().Find(c => c.Data == _pendingData);
        if (existing != null)
            cm.RetrieveCompanion(existing);
        else
        {
            bool added = cm.AddCompanion(_pendingData);
            if (!added) { Close(); return; }
            var list = cm.GetOwnedCompanions();
            existing = list[list.Count - 1];
        }

        cm.PlaceCompanion(existing, slotIndex);
        Debug.Log($"[SlotSelectPanel] {_pendingData.companionName} → 슬롯 {slotIndex} 배치");

        Close();

        if (callerItem != null)
            callerItem.RefreshActionButtons();
    }
}