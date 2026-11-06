using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// ── 탭 페이지 계약 ──

/// <summary>탭 페이지 계약 — OnEnable(켜짐)과 달리 OnTabShow 는 '지금 사용자가 보고 있다' (SetActive 를 안 쓰는 페이지도 알 수 있게).</summary>
public interface ITabPage
{
    void OnTabShow();
    void OnTabHide();
}

/// <summary>가이드 퀘스트가 특정 스탯 강화 항목을 지목할 수 있는 페이지 (지금은 UpgradeUI 만).</summary>
public interface IStatFocusTarget
{
    void FocusStat(LevelUpManager.StatType type);
}

// ── 탭 창 본체 ──

/// <summary>강화·스탯 등을 탭으로 전환하는 단일 창 — 여는 책임은 항상 켜진 TabWindow, 그리는 책임은 보일 때만 살아 있는 각 페이지 (마지막 탭은 id 로 기억).</summary>
// ⚠ 창 '밖'의 항상 켜진 오브젝트에 붙일 것 — 창 안이면 꺼진 동안 스크립트도 꺼져 열 수 없음
public class TabWindow : MonoBehaviour
{
    // ── [직렬화] 탭 1개 ──
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

    // ── 인스펙터 ──
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

    [Header("진입 버튼 아이콘 (선택)")]
    [Tooltip("창 상태에 따라 스프라이트를 바꿀 Image.\n" +
             "비워두면 Open Button 의 Target Graphic 을 씁니다.")]
    [SerializeField] private Image openButtonIcon;

    [Tooltip("창이 닫혀 있을 때의 아이콘 (예: ▲ 펼치기)")]
    [SerializeField] private Sprite iconWhenClosed;

    [Tooltip("창이 열려 있을 때의 아이콘 (예: ▼ 접기)")]
    [SerializeField] private Sprite iconWhenOpened;

    [Header("탭")]
    [SerializeField] private TabEntry[] tabs;
    [SerializeField] private int  defaultTabIndex   = 0;
    [SerializeField] private bool rememberLastTab   = true;

    [Tooltip("selectedMark 를 지정하지 않은 탭 버튼에 쓰는 색 (Button Transition 이 Color Tint 일 때만 동작)")]
    [SerializeField] private Color tabSelectedColor = new Color(0.35f, 0.62f, 1f);
    [SerializeField] private Color tabNormalColor   = Color.white;

    // 재화 표시는 하지 않음 — 창에 띄우려면 CurrencyHUD 를 붙일 것 (두 스크립트가 같은 텍스트에 쓰면 구독 순서에 따라 엇갈림)

    [Header("동료 배치 연동")]
    [Tooltip("동료 배치 모드가 시작되면 창을 잠시 숨기고, 끝나면 되살립니다.\n" +
             "배치는 '맵을 탭해서' 하는데, 창이 화면을 덮고 있으면 탭이 UI에 막혀 배치가 불가능합니다.")]
    [SerializeField] private bool hideWhileCompanionPlacing = true;

    [Header("가이드 퀘스트")]
    [Tooltip("가이드 퀘스트가 '스탯 강화'를 지목했을 때 열 탭의 id (보통 upgrade)")]
    [SerializeField] private string upgradeTabId = "upgrade";

    // ── 내부 상태 ──
    private int currentTab = -1;
    private const string LastTabPrefKey = "TabWindow.LastTab";

    // 구독한 인스턴스를 기억해 비교 (bool 이면 재생성된 매니저의 이벤트를 영영 못 받음)
    private GuideQuestManager           boundGuide;
    private CompanionPlacementController boundPlacement;

    // 배치 때문에 잠시 숨긴 상태인가 (유저가 직접 닫은 것과 구분해야 함)
    private bool hiddenForPlacement;

    private UnityAction[] tabActions;
    private UnityAction   openAction, closeAction, dimmedAction, toggleAction;

    // Open/Close 에 같은 버튼을 넣어 토글로 묶었는가 (해제할 때 같은 방식으로 떼야 함)
    private bool boundAsToggle;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    // 열린 창 목록 — 창 위에 겹친 HUD(SynergyHUD 등)가 열린 동안 탭을 양보하게
    private static readonly System.Collections.Generic.HashSet<TabWindow> openWindows =
        new System.Collections.Generic.HashSet<TabWindow>();

    /// <summary>열려 있는 TabWindow 가 하나라도 있는가.</summary>
    public static bool AnyOpen => openWindows.Count > 0;

    /// <summary>AnyOpen 이 바뀔 수 있을 때마다 불립니다.</summary>
    public static event Action AnyOpenChanged;

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우에도 이전 플레이의 값이 남지 않게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openWindows.Clear();
        AnyOpenChanged = null;
    }

    // ── Unity 생명주기 ──
    private void Awake()
    {
        if (tabs == null) tabs = new TabEntry[0];

        if (windowRoot == gameObject)
        {
            Debug.LogError("[TabWindow] windowRoot 에 이 컴포넌트가 붙은 오브젝트 자신이 들어가 있습니다. " +
                           "창 오브젝트를 따로 만들어 넣어주세요. 자기 자신 참조는 무시합니다.", this);
            windowRoot = null;
        }

        // windowRoot 가 비면 창이 조용히 안 열리므로 에러로 알림
        if (windowRoot == null)
            Debug.LogError("[TabWindow] Window Root 가 연결되지 않았습니다. 창을 열고 닫을 수 없습니다.", this);

        WarnIfWindowIsChildOfOpenButton();
        WarnIfNoWayToClose();
        WarnIfIconsAreHalfAssigned();

        currentTab = RestoreLastTabIndex();
    }

    /// <summary>아이콘을 한 장만 넣으면 반대 상태에서 그림이 바뀌지 않아 더 헷갈립니다.</summary>
    private void WarnIfIconsAreHalfAssigned()
    {
        bool closed = iconWhenClosed != null;
        bool opened = iconWhenOpened != null;

        if (closed == opened) return;   // 둘 다 있거나 둘 다 없으면 정상

        Debug.LogWarning("[TabWindow] 진입 버튼 아이콘이 한 장만 연결돼 있습니다. " +
                         "두 장을 모두 넣거나 둘 다 비워주세요. 지금은 아이콘 교체를 하지 않습니다.", this);
    }

    /// <summary>창이 여는 버튼의 자식이면 경고 — 창이 버튼을 덮어 다시 못 누르고, 하이어라키 순서상 다른 HUD 에 덮임.</summary>
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
        RefreshOpenState();

        // 모든 탭 내용도 꺼 둠 (켜 둔 채 저장한 탭이 겹쳐 보이지 않게)
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].contentRoot != null) tabs[i].contentRoot.SetActive(false);

        // 씬 전환으로 넘어온 "강화 UI 열기" 예약 소비 (꺼진 채 시작하는 UpgradeUI 는 Start 가 안 돌아서 여기서)
        var gq = GuideQuestManager.Instance;
        if (gq != null && gq.TryConsumeStatFocus(out LevelUpManager.StatType type))
            FocusStatUpgrade(type);
    }

    private void OnEnable()
    {
        // 남은 '배치 때문에 숨김' 상태 초기화 (안 하면 나중 배치가 끝날 때 창이 혼자 열림)
        hiddenForPlacement = false;

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

        // 창이 열린 채로 이 컴포넌트가 꺼지거나 파괴되면(씬 전환 등) 열린 창 목록에서 빼 줍니다.
        if (openWindows.Remove(this)) AnyOpenChanged?.Invoke();
    }

    // ── 버튼 리스너 ──
    private void RegisterListeners()
    {
        openAction   = Open;
        closeAction  = Close;
        dimmedAction = Close;
        toggleAction = Toggle;

        // Open·Close 가 같은 버튼이면 Toggle 하나만 등록 (각각 달면 한 클릭에 열리자마자 닫힘)
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
        // 등록한 것만 등록 때와 같은 방식(토글 여부)으로 뗌 (RemoveAllListeners 는 남의 리스너까지 지움)
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

    // ── 매니저 구독 ──
    private void TryBindManagers()
    {
        var gq = GuideQuestManager.Instance;
        if (gq != boundGuide)
        {
            if (boundGuide != null) boundGuide.OnFocusStatUpgrade -= FocusStatUpgrade;

            boundGuide = gq;

            if (boundGuide != null) boundGuide.OnFocusStatUpgrade += FocusStatUpgrade;
        }

        // 배치 컨트롤러는 씬마다 교체되므로 인스턴스 비교로 갈아탐
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

    /// <summary>배치 컨트롤러가 창 안에 있으면 에러 — 배치 시작 때 창과 함께 꺼져 조용히 배치가 안 됨.</summary>
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

    // ── 동료 배치 중 잠시 비켜주기 ──
    private void HandlePlacementBegan()
    {
        if (!hideWhileCompanionPlacing) return;
        if (!IsOpen || windowRoot == null) return;

        // ⚠ Close() 금지 — 동료 탭 OnTabHide 가 배치를 취소해 창이 되살아나는 무한 반복, 오브젝트만 끔
        windowRoot.SetActive(false);
        hiddenForPlacement = true;

        // 안 보이는 동안 버튼도 '열기' 모양으로
        RefreshOpenState();
    }

    private void HandlePlacementEnded()
    {
        // 유저가 직접 닫아둔 창을 배치가 끝났다고 멋대로 열지 않도록, 내가 숨긴 경우에만 되살립니다.
        if (!hiddenForPlacement) return;
        hiddenForPlacement = false;

        if (windowRoot != null) windowRoot.SetActive(true);

        // 배치 결과(배치 ↔ 취소 버튼)가 목록에 반영되도록 현재 탭을 다시 그립니다.
        ShowTab(currentTab, force: true);

        RefreshOpenState();
    }

    // ── 열기 / 닫기 ──
    public void Open()
    {
        // 구독부터 맞춤 (씬 로드 직후 프레임엔 boundPlacement 가 아직 null 일 수 있음)
        TryBindManagers();

        // 배치 중에 열면 여는 쪽이 이김 — 배치 취소 후 열기. 플래그를 먼저 무조건 내려야 취소 이벤트가 창을 두 번 그리거나 나중에 혼자 열지 않음
        hiddenForPlacement = false;

        if (boundPlacement != null && boundPlacement.IsPlacing)
            boundPlacement.CancelPlacement();

        if (windowRoot != null) windowRoot.SetActive(true);

        // 열 때마다 현재 탭을 다시 보여 줘 닫혀 있는 동안 놓친 갱신을 따라잡음
        ShowTab(currentTab, force: true);

        RefreshOpenState();
    }

    public void Close()
    {
        // 배치로 숨긴 상태에서 닫으면 배치도 취소 (안 하면 안내·미리보기만 남음) — 플래그를 먼저 내려야 창이 되살아나지 않음
        if (hiddenForPlacement)
        {
            hiddenForPlacement = false;
            if (boundPlacement != null) boundPlacement.CancelPlacement();   // ?. 는 파괴된 컨트롤러를 못 걸러 MissingReferenceException
        }
        else if (!IsOpen)
        {
            // 이미 닫혀 있으면 OnTabHide 를 또 보내지 않음 (동료 탭은 중복 호출 때 엉뚱한 취소)
            return;
        }

        // 대칭을 맞춰 현재 탭에 먼저 알린 뒤 끕니다.
        GetPage(currentTab)?.OnTabHide();

        // 탭 내용도 끔 — 남겨 두면 다른 탭으로 열 때 OnTabHide 가 한 번 더 와서 Show/Hide 짝이 깨짐
        if (currentTab >= 0 && currentTab < tabs.Length && tabs[currentTab].contentRoot != null)
            tabs[currentTab].contentRoot.SetActive(false);

        if (windowRoot != null) windowRoot.SetActive(false);

        RefreshOpenState();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else        Open();
    }

    // ── 탭 전환 ──
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

            // 끌 때 '알리고 → 끄기', 켤 때 '켜고 → 알리기' (먼저 알리면 OnEnable 전이라 빈 갱신)
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

        // 인덱스가 아닌 id 로 기억 (탭 순서를 바꿔도 같은 탭이 열리게)
        if (rememberLastTab && !tabs[index].dontRememberAsLastTab && !string.IsNullOrEmpty(tabs[index].id))
            PlayerPrefs.SetString(LastTabPrefKey, tabs[index].id);
    }

    /// <summary>id 로 탭 열기(없으면 무시) — 창이 닫혀 있으면 창까지 엶.</summary>
    public void ShowTab(string id)
    {
        int i = IndexOfTab(id);
        if (i < 0) return;

        if (!IsOpen)
        {
            currentTab = i;   // Open() 이 이 탭을 그대로 그려 준다 (중복 갱신 방지)
            Open();
            return;
        }

        ShowTab(i);
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

        // 기본값은 null 대신 빈 문자열 (네이티브 API 라 null 은 플랫폼마다 동작이 다름)
        string savedId = PlayerPrefs.GetString(LastTabPrefKey, string.Empty);
        int    i       = IndexOfTab(savedId);
        return i >= 0 ? i : fallback;
    }

    private ITabPage GetPage(int index)
    {
        if (index < 0 || index >= tabs.Length) return null;

        // 인스펙터가 인터페이스 필드를 직렬화 못 해 MonoBehaviour 로 받아 캐스팅 (아니면 null)
        return tabs[index].page as ITabPage;
    }

    /// <summary>창을 켜고 끈 직후에 부릅니다 — 진입 버튼 아이콘과 열린 창 목록(AnyOpen)을 함께 맞춥니다.</summary>
    private void RefreshOpenState()
    {
        RefreshOpenButtonIcon();

        bool changed = IsOpen ? openWindows.Add(this) : openWindows.Remove(this);
        if (changed) AnyOpenChanged?.Invoke();
    }

    /// <summary>창 상태에 맞춰 진입 버튼 아이콘 교체(▲ ↔ ▼) — overrideSprite 는 Sprite Swap 이 지워 버리므로 기본 sprite 를 바꿈.</summary>
    private void RefreshOpenButtonIcon()
    {
        if (iconWhenClosed == null || iconWhenOpened == null) return;

        // 지정 Image 가 없으면 버튼 Target Graphic (Image 가 아닐 수 있어 as)
        Image icon = openButtonIcon != null
            ? openButtonIcon
            : (openButton != null ? openButton.targetGraphic as Image : null);

        if (icon == null) return;

        icon.sprite = IsOpen ? iconWhenOpened : iconWhenClosed;
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

        // targetGraphic.color 는 Color Tint 가 덮으므로 ColorBlock 을 바꿈
        var cb = t.button.colors;
        cb.normalColor   = isSelected ? tabSelectedColor : tabNormalColor;
        cb.selectedColor = cb.normalColor;
        t.button.colors  = cb;
    }

    // ── 가이드 퀘스트 ──
    private void FocusStatUpgrade(LevelUpManager.StatType type)
    {
        int i = IndexOfTab(upgradeTabId);
        if (i < 0)
        {
            Debug.LogWarning($"[TabWindow] id 가 '{upgradeTabId}' 인 탭이 없어 강화 화면을 열 수 없습니다.", this);
            Open();   // 탭은 못 찾아도 창은 열어 준다
            return;
        }

        // 탭을 먼저 정하고 엶 (반대면 직전 탭을 한 번 다 그렸다 버림)
        currentTab = i;
        Open();     // 내부에서 ShowTab(currentTab, force: true) 수행

        // 페이지가 "어느 스탯인지"까지 받을 수 있으면 전달합니다.
        (tabs[i].page as IStatFocusTarget)?.FocusStat(type);
    }

}