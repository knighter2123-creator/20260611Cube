using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// ══════════════════════════════════════════════════════════
//  탭 페이지 계약
// ══════════════════════════════════════════════════════════

/// <summary>
/// 탭 창 안에 들어가는 페이지가 구현하는 계약.
///
/// ★ OnEnable/OnDisable 로도 대부분 되지만, 훅을 따로 두는 이유가 있습니다.
///   콘텐츠를 SetActive 로 켜고 끄지 않는 페이지(예: CanvasGroup 페이드만 쓰는 경우)도
///   있고, "창은 열려 있는데 다른 탭이 선택된 상태"를 페이지가 알아야 할 때가 있습니다.
///   OnEnable 은 "오브젝트가 켜졌다"이고 OnTabShow 는 "지금 사용자가 나를 보고 있다"입니다.
/// </summary>
public interface ITabPage
{
    void OnTabShow();
    void OnTabHide();
}

/// <summary>
/// 가이드 퀘스트가 "공격력 강화 항목을 보여줘" 라고 지목할 수 있는 페이지.
/// (지금은 UpgradeUI 만 구현합니다)
/// </summary>
public interface IStatFocusTarget
{
    void FocusStat(LevelUpManager.StatType type);
}

// ══════════════════════════════════════════════════════════
//  탭 창 본체
// ══════════════════════════════════════════════════════════

/// <summary>
/// 강화 / 스탯 등을 탭으로 전환하는 단일 창.
///
/// ★ 이 컴포넌트는 창 '밖'의 항상 켜져 있는 오브젝트에 붙입니다.
///   (SettingsPanel / PlayerStatusUI 와 같은 규칙입니다.
///    창 안에 붙이면 창이 꺼진 동안 이 스크립트도 꺼져서 '여는 동작' 자체가 불가능해집니다)
///
/// 책임 분배:
///   - 여는 책임      → TabWindow (항상 켜져 있음)
///   - 그리는 책임    → 각 탭 페이지 (보일 때만 살아 있음)
/// </summary>
public class TabWindow : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  [직렬화] 탭 1개
    // ══════════════════════════════════════════════
    [Serializable]
    public struct TabEntry
    {
        [Tooltip("탭 식별자. 마지막 탭 기억에 쓰이므로 순서를 바꿔도 안전하도록 문자열입니다 (예: upgrade, status)")]
        public string id;

        [Tooltip("이 탭을 선택하는 버튼")]
        public Button button;

        [Tooltip("이 탭의 내용 루트. 탭 전환 시 켜고 꺼집니다")]
        public GameObject contentRoot;

        [Tooltip("ITabPage 를 구현한 컴포넌트 (선택). 비워두면 켜고 끄기만 합니다")]
        public MonoBehaviour page;

        [Tooltip("선택됐을 때 켤 표시 오브젝트 (선택)")]
        public GameObject selectedMark;

        [Tooltip("체크하면 이 탭은 '마지막으로 본 탭' 으로 기억하지 않습니다.\n" +
                 "★ 각성 스테이지 진입처럼 씬을 넘기는 탭은 반드시 체크하세요.\n" +
                 "  안 하면 스테이지에서 돌아와 창을 열 때마다 그 탭이 다시 떠서 진입 버튼을 계속 마주치게 됩니다.\n" +
                 "(struct 기본값이 false = 기억함 이므로, 평범한 탭은 건드릴 필요가 없습니다)")]
        public bool dontRememberAsLastTab;
    }

    // ══════════════════════════════════════════════
    //  인스펙터
    // ══════════════════════════════════════════════
    [Header("창")]
    [Tooltip("★ 이 컴포넌트가 붙은 오브젝트와 달라야 합니다 (같으면 자기 자신을 꺼버림)")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("창을 여는 버튼 (세븐나이츠의 우상단 화살표 자리).\n" +
             "Close Button 에 같은 버튼을 넣으면 '한 번 더 누르면 닫힘'(토글)으로 동작합니다.")]
    [SerializeField] private Button openButton;

    [Tooltip("창 안의 ✕ 버튼. Open Button 과 같은 버튼을 넣으면 토글이 됩니다")]
    [SerializeField] private Button closeButton;

    [Tooltip("어두운 배경을 눌러도 닫히게 하려면 연결 (선택)")]
    [SerializeField] private Button dimmedButton;

    [Header("탭")]
    [SerializeField] private TabEntry[] tabs;
    [SerializeField] private int  defaultTabIndex   = 0;
    [SerializeField] private bool rememberLastTab   = true;

    [Tooltip("selectedMark 를 지정하지 않은 탭 버튼에 쓰는 색 (Button Transition 이 Color Tint 일 때만 동작)")]
    [SerializeField] private Color tabSelectedColor = new Color(0.35f, 0.62f, 1f);
    [SerializeField] private Color tabNormalColor   = Color.white;

    // ★ 재화(골드/젬) 표시는 이 스크립트가 하지 않습니다.
    //   그 일은 이미 CurrencyHUD 가 하고 있고, 여기서도 하면 같은 텍스트에 두 스크립트가
    //   각자 다른 포맷으로 써넣게 됩니다. 한 번의 골드 변화에 두 리스너가 순서대로 돌고
    //   '나중에 구독한 쪽'이 이기는데, 그 순서는 씬 구성에 따라 갈립니다.
    //
    //   창 상단에 재화를 띄우려면 그 오브젝트에 CurrencyHUD 를 하나 더 붙이세요.
    //   창이 열릴 때 OnEnable 로 구독하고 닫힐 때 OnDisable 로 해제하므로 그대로 맞아떨어집니다.
    //   "재화를 그리는 코드"는 프로젝트에 한 곳만 있어야 합니다.

    [Header("동료 배치 연동")]
    [Tooltip("동료 배치 모드가 시작되면 창을 잠시 숨기고, 끝나면 되살립니다.\n" +
             "배치는 '맵을 탭해서' 하는데, 창이 화면을 덮고 있으면 탭이 UI에 막혀 배치가 불가능합니다.")]
    [SerializeField] private bool hideWhileCompanionPlacing = true;

    [Header("가이드 퀘스트")]
    [Tooltip("가이드 퀘스트가 '스탯 강화'를 지목했을 때 열 탭의 id (보통 upgrade)")]
    [SerializeField] private string upgradeTabId = "upgrade";

    // ══════════════════════════════════════════════
    //  내부 상태
    // ══════════════════════════════════════════════
    private int currentTab = -1;
    private const string LastTabPrefKey = "TabWindow.LastTab";

    // ★ bool 플래그가 아니라 '구독한 인스턴스'를 들고 비교합니다.
    //   이 프로젝트의 매니저들은 씬 왕복 중 파괴 후 재생성되는 경우가 있어서,
    //   bool 로 관리하면 옛 인스턴스를 붙잡은 채 새 매니저의 이벤트를 영영 못 받습니다.
    private GuideQuestManager           boundGuide;
    private CompanionPlacementController boundPlacement;

    // 배치 때문에 잠시 숨긴 상태인가 (유저가 직접 닫은 것과 구분해야 함)
    private bool hiddenForPlacement;

    private UnityAction[] tabActions;
    private UnityAction   openAction, closeAction, dimmedAction, toggleAction;

    // Open/Close 에 같은 버튼을 넣어 토글로 묶었는가 (해제할 때 같은 방식으로 떼야 함)
    private bool boundAsToggle;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        if (tabs == null) tabs = new TabEntry[0];

        if (windowRoot == gameObject)
        {
            Debug.LogError("[TabWindow] windowRoot 에 이 컴포넌트가 붙은 오브젝트 자신이 들어가 있습니다. " +
                           "창 오브젝트를 따로 만들어 넣어주세요. 자기 자신 참조는 무시합니다.", this);
            windowRoot = null;
        }

        // ★ windowRoot 가 비어 있으면 IsOpen 이 영원히 false 라, 창은 안 열리는데
        //   버튼은 멀쩡히 눌리고 에러도 안 납니다. 연결을 빠뜨렸을 때 조용히 넘어가지 않게 막습니다.
        if (windowRoot == null)
            Debug.LogError("[TabWindow] Window Root 가 연결되지 않았습니다. 창을 열고 닫을 수 없습니다.", this);

        WarnIfWindowIsChildOfOpenButton();
        WarnIfNoWayToClose();

        currentTab = RestoreLastTabIndex();
    }

    /// <summary>
    /// 창을 '여는 버튼의 자식' 으로 두면 두 가지가 걸립니다.
    ///
    ///   ① 닫을 수 없게 됩니다 — 창이 버튼을 덮으면 그 버튼이 더 이상 눌리지 않습니다.
    ///      자식은 부모 위에 그려지고, 창의 Image 가 Raycast Target 이면 클릭을 전부 가져갑니다.
    ///   ② 다른 HUD 뒤에 그려집니다 — UI 그리는 순서는 하이어라키 순서입니다.
    ///      작은 버튼의 자식인 전체 화면 창은, 그 버튼보다 아래에 있는 형제 HUD 들에 덮입니다.
    ///
    /// 그리고 버튼이 Layout Group 안에 있으면 버튼 크기가 바뀔 때 창의 RectTransform 도 끌려다닙니다.
    /// </summary>
    private void WarnIfWindowIsChildOfOpenButton()
    {
        if (windowRoot == null || openButton == null) return;
        if (!windowRoot.transform.IsChildOf(openButton.transform)) return;

        Debug.LogWarning("[TabWindow] 창(Window Root)이 여는 버튼의 자식으로 들어가 있습니다. " +
                         "창이 버튼을 덮으면 그 버튼을 다시 누를 수 없고, 다른 HUD 요소가 창 위에 그려질 수 있습니다. " +
                         "창은 Canvas 바로 아래(또는 전용 루트)로 옮기고, 버튼에는 이 컴포넌트만 두는 편이 안전합니다.",
                         this);
    }

    /// <summary>열기만 있고 닫을 길이 없는 구성을 잡아냅니다.</summary>
    private void WarnIfNoWayToClose()
    {
        bool toggleable   = openButton != null && openButton == closeButton;
        bool hasCloseBtn  = closeButton != null && closeButton != openButton;
        bool hasDimmed    = dimmedButton != null;

        if (toggleable || hasCloseBtn || hasDimmed) return;

        Debug.LogWarning("[TabWindow] 창을 닫을 수단이 없습니다. " +
                         "Close Button(창 안의 ✕) 또는 Dimmed Button 을 연결하거나, " +
                         "Open Button 과 같은 버튼을 Close Button 에 넣어 토글로 쓰세요.", this);
    }

    private void Start()
    {
        // 시작 시 창은 닫아 둡니다.
        if (windowRoot != null) windowRoot.SetActive(false);

        // 모든 탭 내용도 꺼 둡니다.
        // ★ 이걸 빼먹으면 씬에 켜둔 채 저장한 탭이 창 뒤에 겹쳐 보입니다.
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].contentRoot != null) tabs[i].contentRoot.SetActive(false);

        // ★ 씬 전환으로 넘어온 "강화 UI 열기" 예약 소비.
        //   원래 UpgradeUI.Start() 가 하던 일인데, 이제 UpgradeUI 는 탭 안에서
        //   꺼진 채로 시작하므로 Start 가 아예 돌지 않습니다.
        //   "여는 책임"은 항상 켜져 있는 이쪽이 가져가는 게 맞습니다.
        var gq = GuideQuestManager.Instance;
        if (gq != null && gq.TryConsumeStatFocus(out LevelUpManager.StatType type))
            FocusStatUpgrade(type);
    }

    private void OnEnable()
    {
        RegisterListeners();
        TryBindManagers();
    }

    private void Update()
    {
        // 매니저 인스턴스가 바뀌었는지 매 프레임 확인 (static 필드 비교 2번이라 비용은 사실상 0)
        TryBindManagers();
    }

    private void OnDisable()
    {
        UnbindManagers();
        UnregisterListeners();
    }

    // ══════════════════════════════════════════════
    //  버튼 리스너
    // ══════════════════════════════════════════════
    private void RegisterListeners()
    {
        openAction   = Open;
        closeAction  = Close;
        dimmedAction = Close;
        toggleAction = Toggle;

        // ★ Open 과 Close 에 '같은 버튼'을 넣은 경우
        //   그대로 각각 등록하면 한 번의 클릭에 onClick 이 두 리스너를 연달아 실행합니다.
        //   Open() → Close() 순서로 돌아 창이 열리자마자 닫히고, 화면에는 아무것도 안 보입니다.
        //   에러도 안 나서 "버튼이 안 눌린다"고 오해하기 딱 좋은 조합이라 코드로 흡수합니다.
        //   같은 버튼을 넣었다는 건 '토글' 의도이므로 Toggle 하나만 답니다.
        boundAsToggle = openButton != null && openButton == closeButton;

        if (boundAsToggle)
        {
            openButton.onClick.AddListener(toggleAction);
        }
        else
        {
            if (openButton  != null) openButton.onClick.AddListener(openAction);
            if (closeButton != null) closeButton.onClick.AddListener(closeAction);
        }

        if (dimmedButton != null) dimmedButton.onClick.AddListener(dimmedAction);

        tabActions = new UnityAction[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;                       // ★ 클로저 캡처용 지역 복사 (i 를 그대로 쓰면 전부 마지막 값이 됩니다)
            tabActions[i] = () => ShowTab(index);

            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(tabActions[i]);
        }
    }

    private void UnregisterListeners()
    {
        // ★ RemoveAllListeners 는 다른 스크립트가 붙인 리스너까지 날립니다. 등록한 것만 정확히 뗍니다.
        //   등록할 때와 같은 방식(토글이었는지 아닌지)으로 떼야 리스너가 남지 않습니다.
        if (boundAsToggle)
        {
            if (openButton != null && toggleAction != null) openButton.onClick.RemoveListener(toggleAction);
        }
        else
        {
            if (openButton  != null && openAction  != null) openButton.onClick.RemoveListener(openAction);
            if (closeButton != null && closeAction != null) closeButton.onClick.RemoveListener(closeAction);
        }

        if (dimmedButton != null && dimmedAction != null) dimmedButton.onClick.RemoveListener(dimmedAction);

        openAction = closeAction = dimmedAction = toggleAction = null;
        boundAsToggle = false;

        if (tabActions != null)
        {
            for (int i = 0; i < tabs.Length && i < tabActions.Length; i++)
                if (tabs[i].button != null && tabActions[i] != null)
                    tabs[i].button.onClick.RemoveListener(tabActions[i]);
            tabActions = null;
        }
    }

    // ══════════════════════════════════════════════
    //  매니저 구독
    // ══════════════════════════════════════════════
    private void TryBindManagers()
    {
        var gq = GuideQuestManager.Instance;
        if (gq != boundGuide)
        {
            if (boundGuide != null) boundGuide.OnFocusStatUpgrade -= FocusStatUpgrade;

            boundGuide = gq;

            if (boundGuide != null) boundGuide.OnFocusStatUpgrade += FocusStatUpgrade;
        }

        // ★ 배치 컨트롤러는 씬에 속해 있어 씬을 넘나들면 통째로 교체됩니다.
        //   매니저들과 같은 방식으로 인스턴스를 비교해 갈아탑니다.
        var pc = CompanionPlacementController.Instance;
        if (pc != boundPlacement)
        {
            if (boundPlacement != null)
            {
                boundPlacement.OnPlacementBegan -= HandlePlacementBegan;
                boundPlacement.OnPlacementEnded -= HandlePlacementEnded;
            }

            boundPlacement = pc;

            if (boundPlacement != null)
            {
                boundPlacement.OnPlacementBegan += HandlePlacementBegan;
                boundPlacement.OnPlacementEnded += HandlePlacementEnded;
                WarnIfPlacementControllerIsInsideWindow();
            }
        }
    }

    /// <summary>
    /// ★ 가장 흔하게 걸릴 셋업 실수를 코드로 잡아냅니다.
    ///   배치 컨트롤러가 창 '안'에 있으면, 배치를 시작하는 순간 창이 숨겨지면서
    ///   컨트롤러도 같이 꺼집니다. Update() 가 멈추니 맵을 아무리 눌러도 배치가 되지 않고,
    ///   에러도 로그도 하나 안 나옵니다. (안내 문구 hintText 도 같이 사라집니다)
    /// </summary>
    private void WarnIfPlacementControllerIsInsideWindow()
    {
        if (windowRoot == null || boundPlacement == null) return;
        if (!boundPlacement.transform.IsChildOf(windowRoot.transform)) return;

        Debug.LogError("[TabWindow] CompanionPlacementController 가 탭 창 안에 있습니다. " +
                       "배치 모드에서는 창이 숨겨지므로 컨트롤러도 함께 꺼져 배치가 불가능해집니다. " +
                       "창 밖(항상 켜져 있는 오브젝트)으로 옮겨주세요. 안내 문구와 미리보기도 마찬가지입니다.",
                       boundPlacement);
    }

    private void UnbindManagers()
    {
        if (boundGuide != null) boundGuide.OnFocusStatUpgrade -= FocusStatUpgrade;

        if (boundPlacement != null)
        {
            boundPlacement.OnPlacementBegan -= HandlePlacementBegan;
            boundPlacement.OnPlacementEnded -= HandlePlacementEnded;
        }

        boundGuide     = null;
        boundPlacement = null;
    }

    // ══════════════════════════════════════════════
    //  동료 배치 중 잠시 비켜주기
    // ══════════════════════════════════════════════
    private void HandlePlacementBegan()
    {
        if (!hideWhileCompanionPlacing) return;
        if (!IsOpen || windowRoot == null) return;

        // ★ 여기서 Close() 를 부르면 안 됩니다.
        //   Close() 는 현재 탭에 OnTabHide 를 알리고, 동료 탭의 OnTabHide 는
        //   CancelPlacement() 를 호출합니다. 그러면 방금 시작한 배치가 즉시 취소되고,
        //   그 취소가 다시 '배치 종료' 를 쏴서 창이 되살아납니다 — 배치가 영원히 불가능해집니다.
        //   지금 필요한 건 '잠깐 비켜주기' 뿐이므로 오브젝트만 끕니다.
        windowRoot.SetActive(false);
        hiddenForPlacement = true;
    }

    private void HandlePlacementEnded()
    {
        // 유저가 직접 닫아둔 창을 배치가 끝났다고 멋대로 열지 않도록, 내가 숨긴 경우에만 되살립니다.
        if (!hiddenForPlacement) return;
        hiddenForPlacement = false;

        if (windowRoot != null) windowRoot.SetActive(true);

        // 배치 결과(배치 ↔ 취소 버튼)가 목록에 반영되도록 현재 탭을 다시 그립니다.
        ShowTab(currentTab, force: true);
    }

    // ══════════════════════════════════════════════
    //  열기 / 닫기
    // ══════════════════════════════════════════════
    public void Open()
    {
        // ★ 구독을 먼저 맞춥니다.
        //   아래에서 boundPlacement 를 보는데, 씬이 막 로드된 프레임이라면 아직 null 일 수 있습니다.
        //   그 상태로 지나가면 배치 모드가 살아 있는 채 창이 열려 화면이 덮입니다.
        TryBindManagers();

        // ★ 배치 모드 중에 창을 열면 화면이 다시 덮여 배치 탭이 UI 에 막힙니다.
        //   진입 버튼(화살표)은 창이 숨은 동안에도 HUD 에 그대로 보이므로 실제로 눌릴 수 있습니다.
        //   창을 여는 쪽이 이긴다 — 배치를 취소하고 엽니다.
        //
        //   ★ hiddenForPlacement 를 먼저 내리는 이유: 그대로 두면 CancelPlacement 가 쏘는
        //     OnPlacementEnded 가 창을 한 번 되살리고, 이어서 Open() 이 또 한 번 그립니다.
        //     동료 탭이 열려 있었다면 목록을 두 번 통째로 다시 만들게 됩니다.
        if (boundPlacement != null && boundPlacement.IsPlacing)
        {
            hiddenForPlacement = false;
            boundPlacement.CancelPlacement();
        }

        if (windowRoot != null) windowRoot.SetActive(true);

        // ★ 열 때마다 현재 탭을 다시 보여줍니다.
        //   ShowTab 안에서 OnTabShow 가 불리므로, 닫혀 있는 동안 놓친 갱신이 여기서 따라잡힙니다.
        //   "이벤트 하나를 놓쳐도 거짓 정보가 화면까지 도달하지 못하게" 하는 이중 안전장치입니다.
        ShowTab(currentTab, force: true);
    }

    public void Close()
    {
        // ★ 배치 때문에 숨겨둔 상태에서 닫기가 들어오면(설정 버튼, 상점 버튼 등)
        //   배치 모드를 먼저 정리해야 합니다. 안 그러면 창은 닫혔는데 미리보기와
        //   "배치할 위치를 탭하세요" 안내만 화면에 남고, 맵을 누르면 동료가 배치됩니다.
        //
        //   순서가 중요합니다. hiddenForPlacement 를 먼저 내려야
        //   CancelPlacement 가 쏘는 OnPlacementEnded 가 창을 되살리지 않습니다.
        if (hiddenForPlacement)
        {
            hiddenForPlacement = false;
            boundPlacement?.CancelPlacement();
        }
        else if (!IsOpen)
        {
            // 이미 닫혀 있으면 OnTabHide 를 또 보내지 않습니다.
            // (동료 탭의 OnTabHide 는 CancelPlacement 를 부르므로, 중복 호출이 엉뚱한 취소를 만듭니다)
            return;
        }

        // 대칭을 맞춰 현재 탭에 먼저 알린 뒤 끕니다.
        GetPage(currentTab)?.OnTabHide();

        if (windowRoot != null) windowRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else        Open();
    }

    // ══════════════════════════════════════════════
    //  탭 전환
    // ══════════════════════════════════════════════
    public void ShowTab(int index) => ShowTab(index, force: false);

    private void ShowTab(int index, bool force)
    {
        if (tabs.Length == 0) return;

        index = Mathf.Clamp(index, 0, tabs.Length - 1);
        if (!force && index == currentTab) return;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = (i == index);
            var  t  = tabs[i];

            // ★ 끌 때는 "알리고 → 끈다", 켤 때는 "켜고 → 알린다" 순서입니다.
            //   켜기 전에 OnTabShow 를 부르면 페이지의 OnEnable(구독/초기화)이 아직 안 돌아서
            //   갱신이 빈 상태로 한 번 지나갑니다.
            if (!on)
            {
                if (t.contentRoot != null && t.contentRoot.activeSelf)
                    GetPage(i)?.OnTabHide();

                if (t.contentRoot != null) t.contentRoot.SetActive(false);
            }
            else
            {
                if (t.contentRoot != null) t.contentRoot.SetActive(true);
                GetPage(i)?.OnTabShow();
            }

            SetTabVisual(i, on);
        }

        currentTab = index;

        // ★ 인덱스가 아니라 id 를 저장합니다.
        //   인덱스로 저장하면 나중에 탭 순서를 바꾸거나 중간에 하나 끼워 넣는 순간
        //   유저가 마지막에 본 탭과 다른 탭이 열립니다.
        if (rememberLastTab && !tabs[index].dontRememberAsLastTab && !string.IsNullOrEmpty(tabs[index].id))
            PlayerPrefs.SetString(LastTabPrefKey, tabs[index].id);
    }

    /// <summary>id 로 탭을 엽니다. 없으면 아무 일도 하지 않습니다.</summary>
    public void ShowTab(string id)
    {
        int i = IndexOfTab(id);
        if (i >= 0) ShowTab(i);
    }

    private int IndexOfTab(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].id == id) return i;
        return -1;
    }

    private int RestoreLastTabIndex()
    {
        int fallback = Mathf.Clamp(defaultTabIndex, 0, Mathf.Max(0, tabs.Length - 1));
        if (!rememberLastTab) return fallback;

        // ★ 기본값으로 null 을 넘기지 않습니다. PlayerPrefs 는 네이티브로 문자열을 넘기는 API라
        //   null 기본값은 플랫폼에 따라 동작이 갈립니다. 빈 문자열이 안전합니다.
        string savedId = PlayerPrefs.GetString(LastTabPrefKey, string.Empty);
        int    i       = IndexOfTab(savedId);
        return i >= 0 ? i : fallback;
    }

    private ITabPage GetPage(int index)
    {
        if (index < 0 || index >= tabs.Length) return null;

        // ★ 인스펙터는 인터페이스 타입 필드를 직렬화하지 못합니다.
        //   그래서 MonoBehaviour 로 받아 여기서 캐스팅합니다. 아니면 null 이 돌아올 뿐 터지지 않습니다.
        return tabs[index].page as ITabPage;
    }

    private void SetTabVisual(int index, bool isSelected)
    {
        var t = tabs[index];

        if (t.selectedMark != null)
        {
            t.selectedMark.SetActive(isSelected);
            return;
        }

        if (t.button == null) return;

        // ★ targetGraphic.color 를 직접 칠하면 Button 의 Color Tint 트랜지션이 곧바로 덮어씁니다.
        //   ColorBlock 자체를 바꿔야 유지됩니다.
        var cb = t.button.colors;
        cb.normalColor   = isSelected ? tabSelectedColor : tabNormalColor;
        cb.selectedColor = cb.normalColor;
        t.button.colors  = cb;
    }

    // ══════════════════════════════════════════════
    //  가이드 퀘스트
    // ══════════════════════════════════════════════
    private void FocusStatUpgrade(LevelUpManager.StatType type)
    {
        int i = IndexOfTab(upgradeTabId);
        if (i < 0)
        {
            Debug.LogWarning($"[TabWindow] id 가 '{upgradeTabId}' 인 탭이 없어 강화 화면을 열 수 없습니다.", this);
            Open();   // 탭은 못 찾아도 창은 열어 준다
            return;
        }

        // ★ 탭을 먼저 정하고 엽니다. 순서를 반대로 하면
        //   Open() 이 '직전에 보던 탭'을 한 번 전부 그린 뒤 곧바로 강화 탭으로 넘어갑니다.
        //   직전 탭이 동료 목록이면 리스트를 통째로 만들었다가 그대로 버리게 됩니다.
        currentTab = i;
        Open();     // 내부에서 ShowTab(currentTab, force: true) 수행

        // 페이지가 "어느 스탯인지"까지 받을 수 있으면 전달합니다.
        (tabs[i].page as IStatFocusTarget)?.FocusStat(type);
    }

}