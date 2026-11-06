using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionListItem : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI fragmentText;
    [Tooltip("시너지 계열 배지 ([딜러] 등). 카드 모서리에 따로 두어 이름이 길어도 카드 밖으로 밀려나지 않게 합니다.")]
    [SerializeField] private TextMeshProUGUI tagText;
    [SerializeField] private Button          iconButton;    // 미연결 시 자동 탐색

    [Header("배치/취소 버튼")]
    [SerializeField] private GameObject actionButtons;      // 버튼 묶음 루트 (기본 비활성)
    [SerializeField] private Button     placeButton;        // 배치 버튼
    [SerializeField] private Button     cancelButton;       // 취소(회수) 버튼

    private CompanionData _data;
    private bool          _listenersBound;

    // 리스너는 한 번만 (RemoveAllListeners 는 남의 리스너까지 지움) — 꺼진 프리팹은 Awake 가 늦어 Setup 에서 호출
    private void BindListenersOnce()
    {
        if (_listenersBound) return;
        _listenersBound = true;

        if (iconButton == null) iconButton = GetComponent<Button>();

        if (iconButton   != null) iconButton.onClick.AddListener(ToggleActionButtons);
        if (placeButton  != null) placeButton.onClick.AddListener(OnPlaceClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnEnable()
    {
        // 조각 수가 바뀌면 (가챠 중복 등) 목록을 다시 열지 않아도 숫자가 갱신되게 합니다.
        CompanionFragment fragment = CompanionFragment.Instance;
        if (fragment != null) fragment.OnFragmentChanged += HandleFragmentChanged;
    }

    void OnDisable()
    {
        CompanionFragment fragment = CompanionFragment.Instance;
        if (fragment != null) fragment.OnFragmentChanged -= HandleFragmentChanged;
    }

    void OnDestroy()
    {
        if (!_listenersBound) return;
        if (iconButton   != null) iconButton.onClick.RemoveListener(ToggleActionButtons);
        if (placeButton  != null) placeButton.onClick.RemoveListener(OnPlaceClicked);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    public void Setup(CompanionData data)
    {
        if (data == null) return;
        _data = data;

        BindListenersOnce();

        if (iconImage != null && data.icon != null)
            iconImage.sprite = data.icon;

        // 계열 배지는 카드 모서리(tagText)에 따로 (이름 뒤면 긴 이름에서 밀려남), 글자만 계열 색
        if (tagText != null)
        {
            SynergyTag tag = SynergyTags.Of(data);
            tagText.richText = true;
            tagText.text     = tag == SynergyTag.None ? string.Empty : $"[{SynergyTags.GetColoredName(tag)}]";
            tagText.gameObject.SetActive(tag != SynergyTag.None);
        }

        if (nameText != null)
        {
            nameText.text = data.companionName;
            // 등급 색은 CompanionGradeStyle 의 밝은 배경용 (흰 카드에서 일반=흰색이 안 보여서)
            nameText.color = CompanionGradeStyle.GetColorOnLight(data.grade);
        }

        CompanionFragment fragment = CompanionFragment.Instance;
        SetFragmentText(fragment != null ? fragment.GetFragment(data) : 0);

        if (actionButtons != null) actionButtons.SetActive(false);
        RefreshActionButtons();
    }

    private void HandleFragmentChanged(string companionId, int count)
    {
        if (_data != null && _data.id == companionId)
            SetFragmentText(count);
    }

    // 상세 패널과 같은 "조각 37 / 100" (목록엔 진화 풀이 없어 최대 성급이어도 / 100)
    private void SetFragmentText(int count)
    {
        if (fragmentText != null) fragmentText.text = CompanionStar.FragmentLabel(count);
    }

    public void RefreshActionButtons()
    {
        bool isPlaced = IsCurrentlyPlaced();
        if (placeButton  != null) placeButton.gameObject.SetActive(!isPlaced);
        if (cancelButton != null) cancelButton.gameObject.SetActive(isPlaced);
    }

    private void ToggleActionButtons()
    {
        if (actionButtons == null) return;

        bool next = !actionButtons.activeSelf;
        if (next) BringActionButtonsToFront();
        actionButtons.SetActive(next);
        if (next) RefreshActionButtons();
    }

    // 뒤 형제 아이템이 버튼을 덮으므로 버튼 묶음에만 정렬 덮어쓰기 Canvas + GraphicRaycaster (한 번만 생성)
    private void BringActionButtonsToFront()
    {
        Canvas canvas = actionButtons.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = actionButtons.AddComponent<Canvas>();
            actionButtons.AddComponent<GraphicRaycaster>();
        }

        // 기준은 정렬을 덮어쓴 가장 가까운 캔버스(없으면 루트) — 중첩 캔버스의 sortingOrder 는 안 쓰이는 값이라 기준이면 버튼이 뒤로 감
        Canvas basis = FindSortingCanvas(transform.parent);
        canvas.overrideSorting = true;
        if (basis != null) canvas.sortingLayerID = basis.sortingLayerID;
        canvas.sortingOrder = (basis != null ? basis.sortingOrder : 0) + 1;
    }

    private static Canvas FindSortingCanvas(Transform from)
    {
        Canvas found = null;
        for (Transform t = from; t != null; t = t.parent)
        {
            if (!t.TryGetComponent(out Canvas c) || !c.enabled) continue;
            found = c;
            if (c.overrideSorting || c.isRootCanvas) break;
        }
        return found;
    }

    private void OnPlaceClicked()
    {
        // 슬롯 패널 대신 맵 탭 배치 모드로 진입
        CompanionPlacementController pc = CompanionPlacementController.Instance;
        if (pc != null) pc.BeginPlacement(_data, this);

        if (actionButtons != null) actionButtons.SetActive(false);
    }

    private void OnCancelClicked()
    {
        CompanionManager cm = CompanionManager.Instance;
        Companion companion = FindPlacedCompanion();
        if (cm != null && companion != null)
        {
            cm.RetrieveCompanion(companion);
            // ★ 회수도 유저가 직접 한 변경이라 배치(ConfirmPlace)와 똑같이 바로 저장합니다.
            SaveManager.Instance?.Save();
        }

        if (actionButtons != null) actionButtons.SetActive(false);
        RefreshActionButtons();
    }

    private bool IsCurrentlyPlaced() => FindPlacedCompanion() != null;

    private Companion FindPlacedCompanion()
    {
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || _data == null) return null;

        Companion c = cm.FindOwnedCompanion(_data.id);
        return c != null && c.IsPlaced ? c : null;
    }
}
