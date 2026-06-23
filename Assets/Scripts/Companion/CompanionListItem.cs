using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionListItem : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI fragmentText;
    [SerializeField] private Button          iconButton;    // 미연결 시 Awake에서 자동 탐색

    [Header("배치/취소 버튼")]
    [SerializeField] private GameObject actionButtons;      // 버튼 묶음 루트 (기본 비활성)
    [SerializeField] private Button     placeButton;        // 배치 버튼
    [SerializeField] private Button     cancelButton;       // 취소(회수) 버튼

    private static readonly Color ColorNormal    = Color.white;
    private static readonly Color ColorRare      = new Color(0.6f, 0.2f, 0.8f);
    private static readonly Color ColorEpic      = new Color(1f, 0.4f, 0.7f);
    private static readonly Color ColorLegendary = new Color(0.6f, 1f, 0.4f);

    private CompanionData _data;

    void Awake()
    {
        if (iconButton == null)
            iconButton = GetComponent<Button>();
    }

    // SlotSelectPanel 인자 제거
    public void Setup(CompanionData data)
    {
        _data = data;

        if (data.icon != null)
            iconImage.sprite = data.icon;

        nameText.text  = data.companionName;
        nameText.color = GetGradeColor(data.grade);

        int fragmentCount = CompanionFragment.Instance?.GetFragment(data) ?? 0;
        fragmentText.text = $"조각 : {fragmentCount}";

        iconButton.onClick.RemoveAllListeners();
        iconButton.onClick.AddListener(ToggleActionButtons);

        placeButton.onClick.RemoveAllListeners();
        placeButton.onClick.AddListener(OnPlaceClicked);

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(OnCancelClicked);

        actionButtons.SetActive(false);
        RefreshActionButtons();
    }

    public void RefreshActionButtons()
    {
        bool isPlaced = IsCurrentlyPlaced();
        placeButton.gameObject.SetActive(!isPlaced);
        cancelButton.gameObject.SetActive(isPlaced);
    }

    private void ToggleActionButtons()
    {
        bool next = !actionButtons.activeSelf;
        actionButtons.SetActive(next);
        if (next) RefreshActionButtons();
    }

    private void OnPlaceClicked()
    {
        // 슬롯 패널 대신 맵 탭 배치 모드로 진입
        CompanionPlacementController.Instance?.BeginPlacement(_data, this);
        actionButtons.SetActive(false);
    }

    private void OnCancelClicked()
    {
        Companion companion = FindPlacedCompanion();
        if (companion != null)
            CompanionManager.Instance.RetrieveCompanion(companion);

        actionButtons.SetActive(false);
        RefreshActionButtons();
    }

    private bool IsCurrentlyPlaced() => FindPlacedCompanion() != null;

    private Companion FindPlacedCompanion()
    {
        var companions = CompanionManager.Instance?.GetOwnedCompanions();
        if (companions == null) return null;
        return companions.Find(c => c.Data == _data && c.IsPlaced);
    }

    private Color GetGradeColor(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => ColorNormal,
            CompanionGrade.Rare      => ColorRare,
            CompanionGrade.Epic      => ColorEpic,
            CompanionGrade.Legendary => ColorLegendary,
            _                        => ColorNormal
        };
    }
}