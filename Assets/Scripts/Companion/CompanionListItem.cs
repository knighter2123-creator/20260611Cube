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
            //   목록 아이템은 흰 카드 위에 그려지므로 밝은 배경용 색을 씁니다 (일반 = 흰색이면 안 보임).
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

    // 상세 패널과 같은 형식 ("조각 37 / 100") — 문구 규칙은 CompanionStar 한 곳에 있습니다.
    // 목록에는 진화 풀이 없어서 최대 성급이어도 "/ 100" 으로 표시합니다 (진화 가능 여부는 상세 패널에서 확인).
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

    // ★ 그리드에서는 뒤에 있는 형제(오른쪽·아래 아이템)가 나중에 그려져서 버튼을 덮습니다.
    //   아이템 순서를 바꾸면 그리드 배치가 바뀌므로, 버튼 묶음에만 정렬을 덮어쓰는 Canvas 를 붙여 맨 위로 올립니다.
    //   클릭도 받아야 하므로 GraphicRaycaster 를 같이 붙입니다. (한 번만 만들고 재사용)
    private void BringActionButtonsToFront()
    {
        Canvas canvas = actionButtons.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = actionButtons.AddComponent<Canvas>();
            actionButtons.AddComponent<GraphicRaycaster>();
        }

        // 기준은 '실제로 정렬 값을 가진' 캔버스 = 정렬을 덮어쓴 가장 가까운 캔버스, 없으면 루트 캔버스.
        //   중첩 캔버스의 sortingOrder 는 실제로 쓰이지 않는 값(보통 0)이라, 그걸 기준으로 하면
        //   루트 순서가 더 클 때 버튼이 패널 전체 뒤로 가 버립니다.
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
