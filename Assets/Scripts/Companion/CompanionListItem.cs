using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionListItem : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI fragmentText;
    [SerializeField] private Button          iconButton;    // 미연결 시 자동 탐색

    [Header("배치/취소 버튼")]
    [SerializeField] private GameObject actionButtons;      // 버튼 묶음 루트 (기본 비활성)
    [SerializeField] private Button     placeButton;        // 배치 버튼
    [SerializeField] private Button     cancelButton;       // 취소(회수) 버튼

    private CompanionData _data;
    private bool          _listenersBound;

    // ★ 리스너는 '한 번만' 겁니다 (CompanionCodexItem.Init 과 같은 이유).
    //   예전에는 Setup 마다 RemoveAllListeners → AddListener 를 했는데,
    //   RemoveAllListeners 는 다른 스크립트가 코드로 건 리스너까지 지워버립니다.
    //   Awake 가 아니라 Setup 에서 부르는 이유: 프리팹이 꺼진 채 저장돼 있으면 Awake 가 Setup 보다 늦을 수 있음.
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

        if (nameText != null)
        {
            nameText.text  = data.companionName;
            // ★ 등급 색은 도감과 같은 규칙(CompanionGradeStyle)을 씁니다. 색을 바꿀 땐 그 파일 한 곳만 고치면 됩니다.
            nameText.color = CompanionGradeStyle.GetColor(data.grade);
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

    private void SetFragmentText(int count)
    {
        if (fragmentText != null) fragmentText.text = $"조각 : {count}";
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
        actionButtons.SetActive(next);
        if (next) RefreshActionButtons();
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
