using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스탯 강화 UI.
/// ★ 변경점: 행마다 있던 ×1/×10/×100 버튼 3개를 없애고,
///    패널 상단의 "공용 배수 버튼"(×1/×10/×100) + 행마다 "강화 버튼 1개" 구조로 바꿨습니다.
///    선택된 배수는 selectedMultiplier 하나에만 저장되고, 모든 행이 그 값을 함께 봅니다.
/// </summary>
public class UpgradeUI : MonoBehaviour, ITabPage, IStatFocusTarget
{
    // ══════════════════════════════════════════════
    //  [직렬화] 공용 배수 버튼 1개
    // ══════════════════════════════════════════════
    [System.Serializable]
    public struct MultiplierButton
    {
        [Tooltip("이 버튼이 의미하는 배수 (1 / 10 / 100)")]
        public int amount;

        [Tooltip("배수 선택 버튼")]
        public Button button;

        [Tooltip("선택됐을 때 켤 표시 오브젝트(테두리 등). 비워두면 아래의 색 방식으로 대체합니다")]
        public GameObject selectedMark;
    }

    // ══════════════════════════════════════════════
    //  [직렬화] 스탯 행 1개의 UI 묶음
    // ══════════════════════════════════════════════
    [System.Serializable]
    public struct StatRow
    {
        [Tooltip("이 행이 대응하는 StatType")]
        public LevelUpManager.StatType statType;

        [Tooltip("스탯 현재 수치 텍스트 (예: 10 → 12)")]
        public TMP_Text statValueText;

        [Tooltip("강화 레벨 텍스트 (예: Lv. 0 / 5000)")]
        public TMP_Text upgradeLevelText;

        [Tooltip("강화 버튼 안의 비용 텍스트 (선택한 배수만큼의 '누적' 비용)")]
        public TMP_Text costText;

        [Tooltip("강화 버튼 (배수는 상단 공용 버튼이 결정)")]
        public Button upgradeButton;
    }

    // ══════════════════════════════════════════════
    //  인스펙터 연결
    // ══════════════════════════════════════════════
    [Header("동작 모드")]
    [Tooltip("TabWindow 안의 한 탭으로 쓸 때 체크하세요.\n" +
             "체크하면 이 스크립트는 패널을 스스로 켜고 끄지 않고, 가이드 퀘스트 구독도 하지 않습니다.\n" +
             "그 두 가지는 TabWindow 가 대신합니다. (같은 오브젝트를 둘이 켜고 끄면 반드시 어긋납니다)")]
    [SerializeField] private bool useAsTabPage = false;

    [Header("공통 UI")]
    [Tooltip("단독 창 모드에서만 씁니다. ★ 이 컴포넌트가 붙은 오브젝트와 달라야 함 (같으면 Start에서 자신을 꺼버림)")]
    [SerializeField] private GameObject upgradePanel;   // 패널 전체 (열기/닫기용)

    [Header("공용 배수 버튼")]
    [SerializeField] private MultiplierButton[] multiplierButtons;

    [Tooltip("처음 켰을 때 선택돼 있을 배수")]
    [SerializeField] private int defaultMultiplier = 1;

    [Tooltip("마지막으로 고른 배수를 PlayerPrefs에 기억합니다")]
    [SerializeField] private bool rememberMultiplier = true;

    [Tooltip("selectedMark 를 지정하지 않은 버튼에 쓰는 색 (Button Transition 이 Color Tint 일 때만 동작)")]
    [SerializeField] private Color multiplierSelectedColor = new Color(0.35f, 0.62f, 1f);
    [SerializeField] private Color multiplierNormalColor   = Color.white;

    [Header("스탯 행")]
    [SerializeField] private StatRow[] statRows;

    [Header("설정")]
    [Tooltip("보유 골드가 '선택한 배수의 누적 비용'보다 적으면 버튼을 비활성화합니다")]
    [SerializeField] private bool disableWhenUnaffordable = true;

    [Tooltip("비용 텍스트 색 (살 수 있을 때 / 골드가 모자랄 때)")]
    [SerializeField] private Color costAffordableColor   = Color.white;
    [SerializeField] private Color costUnaffordableColor = new Color(1f, 0.35f, 0.35f);

    [Tooltip("비용을 '73만 2800' 처럼 한국식 만/억 단위로 표시합니다")]
    [SerializeField] private bool useKoreanNumberFormat = true;

    [Header("성능")]
    [Tooltip("골드 변화가 잦아도 최소 이 간격으로만 UI를 다시 그립니다 (0 이면 즉시)")]
    [SerializeField] private float refreshInterval = 0.1f;

    // ══════════════════════════════════════════════
    //  내부 상태
    // ══════════════════════════════════════════════

    // ★ bool 플래그(boundGold 등) 대신 "구독한 인스턴스 자체"를 들고 있습니다.
    //   이유: 이 게임의 매니저들은 씬 왕복 중 파괴 후 재생성되는 경우가 있습니다.
    //   bool 로만 관리하면 "구독했다=true" 인 채로 옛 인스턴스를 가리키게 되어,
    //   새 매니저의 이벤트를 영원히 못 받는 조용한 죽은 UI 가 됩니다.
    //   인스턴스를 비교하면 바뀐 순간 자동으로 갈아탑니다.
    private LevelUpManager    boundLm;
    private CurrencyManager   boundCurrency;
    private GuideQuestManager boundGuide;

    private LevelUpManager lm;   // 편의용 캐시 (= boundLm)

    // 골드 캐시
    private int  cachedGold;
    private bool hasGoldValue;

    // ★ 이번 구조의 핵심: 배수는 UI 전체가 공유하는 값 하나뿐
    private int selectedMultiplier = 1;
    private const string MultiplierPrefKey = "UpgradeUI.SelectedMultiplier";

    // statType → statRows 인덱스 (매 갱신마다 배열을 훑지 않도록)
    private readonly Dictionary<LevelUpManager.StatType, int> rowIndex =
        new Dictionary<LevelUpManager.StatType, int>();

    // RemoveAllListeners 대신 정확히 제거하기 위해 델리게이트를 보관
    private UnityAction[] rowActions;
    private UnityAction[] multiplierActions;

    // 갱신 쓰로틀
    private bool  refreshPending;
    private float nextRefreshTime;

    // 패널이 닫혀 있으면 갱신할 이유가 없다.
    //
    // ★ 단독 창 모드: 이 컴포넌트는 패널의 '부모'에 붙어 있어서 패널을 닫아도 계속 살아 있습니다.
    //   이 검사가 없으면 적을 잡을 때마다(=골드가 오를 때마다) 닫힌 UI를 계속 다시 그립니다.
    //
    // ★ 탭 모드: 이 컴포넌트가 탭 내용 안에 붙어 있어, 탭이 가려지면 컴포넌트째 꺼집니다.
    //   즉 "살아서 Update 가 돌고 있다 = 지금 보이고 있다" 이므로 항상 true 로 둡니다.
    private bool IsPanelOpen => useAsTabPage || upgradePanel == null || upgradePanel.activeInHierarchy;

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        BuildRowIndex();

        // ★ 탭 모드에서는 패널 소유권이 TabWindow 에 있습니다.
        //   여기에 참조가 남아 있으면 TabWindow 가 켠 내용을 이 스크립트가 도로 꺼버립니다.
        //   "같은 오브젝트를 두 주체가 SetActive 한다"는 추적하기 가장 괴로운 종류의 버그라
        //   아예 참조를 끊어 둡니다.
        if (useAsTabPage && upgradePanel != null)
        {
            Debug.LogWarning("[UpgradeUI] 탭 모드에서는 upgradePanel 을 쓰지 않습니다. " +
                             "패널을 켜고 끄는 일은 TabWindow 가 합니다. 참조를 무시합니다.", this);
            upgradePanel = null;
        }

        // upgradePanel 을 자기 자신으로 지정하면 Start에서 스스로를 꺼버려
        // 이후 아무 이벤트도 받지 못하는 상태가 됩니다. 툴팁 경고를 코드로도 막습니다.
        if (upgradePanel == gameObject)
        {
            Debug.LogError("[UpgradeUI] upgradePanel 에 이 컴포넌트가 붙은 오브젝트 자신이 들어가 있습니다. " +
                           "자식 패널 오브젝트를 넣어주세요. 자기 자신 참조는 무시합니다.", this);
            upgradePanel = null;
        }

        RestoreSelectedMultiplier();
    }

    private void BuildRowIndex()
    {
        rowIndex.Clear();
        if (statRows == null) { statRows = new StatRow[0]; return; }

        for (int i = 0; i < statRows.Length; i++)
        {
            var type = statRows[i].statType;
            if (rowIndex.ContainsKey(type))
            {
                // 중복이면 앞의 행만 갱신되고 뒤의 행은 영원히 멈춰 있게 됩니다
                Debug.LogWarning($"[UpgradeUI] statRows 에 {type} 이 두 번 이상 있습니다. " +
                                 "첫 번째 행만 갱신됩니다.", this);
                continue;
            }
            rowIndex.Add(type, i);
        }
    }

    /// <summary>저장해 둔 배수를 되살리고, 버튼 목록에 없는 값이면 보정한다.</summary>
    private void RestoreSelectedMultiplier()
    {
        if (multiplierButtons == null) multiplierButtons = new MultiplierButton[0];

        int want = Mathf.Max(1, defaultMultiplier);
        if (rememberMultiplier)
            want = Mathf.Max(1, PlayerPrefs.GetInt(MultiplierPrefKey, want));

        // ★ 저장된 값이 지금 버튼 구성에 없을 수 있습니다(×100 버튼을 나중에 뺀 경우 등).
        //   그대로 두면 아무 버튼도 선택 표시되지 않은 채 ×100 이 적용되는 유령 상태가 됩니다.
        if (!HasMultiplier(want))
            want = FirstValidMultiplier();

        selectedMultiplier = want;
    }

    private bool HasMultiplier(int amount)
    {
        for (int i = 0; i < multiplierButtons.Length; i++)
            if (multiplierButtons[i].amount == amount) return true;
        return false;
    }

    private int FirstValidMultiplier()
    {
        for (int i = 0; i < multiplierButtons.Length; i++)
            if (multiplierButtons[i].amount > 0) return multiplierButtons[i].amount;
        return 1;   // 배수 버튼을 아예 안 쓰는 구성이면 ×1 고정
    }

    private void Start()
    {
        // ★ 탭 모드에서는 여는 일에 일절 관여하지 않습니다.
        //   탭이 꺼져 있으면 이 Start 자체가 돌지 않으므로, 아래 예약 소비를 여기 두면
        //   "가이드 퀘스트가 강화창을 열어주지 않는" 조용한 고장이 됩니다.
        //   그래서 그 책임은 항상 켜져 있는 TabWindow.Start() 로 옮겼습니다.
        if (useAsTabPage) return;

        // 게임 시작 시 패널 숨김
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        // ★ 가이드 퀘스트: 씬 전환으로 넘어온 "강화 UI 열기" 예약 소비
        //   (OnEnable은 Start보다 먼저 실행되므로 예약은 여기서 처리해야 함)
        var gq = GuideQuestManager.Instance;
        if (gq != null && gq.TryConsumeStatFocus(out LevelUpManager.StatType type))
            FocusStat(type);
    }

    private void OnEnable()
    {
        RegisterButtonListeners();
        TryBindAll();
        RefreshMultiplierVisual();
        RefreshAllImmediate();
    }

    private void Update()
    {
        // ★ 원래는 OnEnable에서 단 한 번만 구독을 시도했습니다.
        //   그 시점에 매니저가 아직 없으면 조용히 실패하고, 그 세션 내내
        //   강화해도 UI가 갱신되지 않았습니다.
        //   지금은 "매니저 인스턴스가 바뀌었는지"까지 매 프레임 확인합니다.
        //   (static 필드 3번 비교라 비용은 사실상 0입니다)
        TryBindAll();

        // 쓰로틀된 갱신 처리
        // ★ Time.time 이 아니라 unscaledTime 을 씁니다.
        //   게임 속도(배속) 조절로 timeScale 이 바뀌어도 UI 갱신 주기는 일정해야 하고,
        //   일시정지(timeScale = 0)에서도 강화 UI는 살아 있어야 하기 때문입니다.
        if (refreshPending && Time.unscaledTime >= nextRefreshTime)
        {
            refreshPending = false;
            RefreshAllImmediate();
        }
    }

    private void OnDisable()
    {
        UnbindAll();
        UnregisterButtonListeners();
    }

    // ══════════════════════════════════════════════
    //  매니저 구독 (인스턴스 교체 대응)
    // ══════════════════════════════════════════════
    private void TryBindAll()
    {
        // ── LevelUpManager ──
        var currentLm = LevelUpManager.Instance;
        if (currentLm != boundLm)
        {
            // ★ Unity 의 != 는 "파괴된 오브젝트 == null" 로 처리해 줍니다.
            //   그래서 옛 인스턴스가 이미 파괴됐다면 아래 해제는 자연스럽게 건너뜁니다.
            if (boundLm != null) boundLm.OnStatUpgraded -= HandleStatUpgraded;

            boundLm = currentLm;
            lm      = currentLm;

            if (boundLm != null)
            {
                boundLm.OnStatUpgraded += HandleStatUpgraded;
                RefreshAllImmediate();
            }
        }

        // ── CurrencyManager ──
        var currentCm = CurrencyManager.Instance;
        if (currentCm != boundCurrency)
        {
            if (boundCurrency != null) boundCurrency.OnGoldChanged -= HandleGoldChanged;

            boundCurrency = currentCm;

            if (boundCurrency != null)
            {
                boundCurrency.OnGoldChanged += HandleGoldChanged;
                SyncGoldFromManager();      // 첫 이벤트를 기다리지 않고 바로 현재값을 읽는다
                RefreshAllImmediate();
            }
        }

        // ── GuideQuestManager ──
        // ★ 탭 모드에서는 TabWindow 가 구독합니다. 여기서도 구독하면 한 번의 포커스 요청에
        //   창을 여는 동작이 두 번 돌아갑니다.
        if (useAsTabPage) return;

        var currentGq = GuideQuestManager.Instance;
        if (currentGq != boundGuide)
        {
            if (boundGuide != null) boundGuide.OnFocusStatUpgrade -= FocusStat;

            boundGuide = currentGq;

            if (boundGuide != null)
                boundGuide.OnFocusStatUpgrade += FocusStat;
        }
    }

    private void UnbindAll()
    {
        if (boundLm != null)       boundLm.OnStatUpgraded        -= HandleStatUpgraded;
        if (boundCurrency != null) boundCurrency.OnGoldChanged   -= HandleGoldChanged;
        if (boundGuide != null)    boundGuide.OnFocusStatUpgrade -= FocusStat;

        boundLm       = null;
        boundCurrency = null;
        boundGuide    = null;
        lm            = null;
    }

    /// <summary>
    /// CurrencyManager 에서 현재 골드를 직접 읽어 캐시한다.
    ///
    /// ★ 원래는 OnGoldChanged 이벤트만으로 골드를 알았습니다.
    ///   그런데 CurrencyManager 는 Start 의 ApplyFrom 에서 이벤트를 한 번 쏘는데,
    ///   UpgradeUI 가 그보다 늦게 구독하면 그 한 번을 놓칩니다.
    ///   그러면 hasGoldValue 가 false 로 남아, 적을 한 마리 잡아 골드가 움직이기 전까지는
    ///   "살 수 없는 항목도 버튼이 멀쩡히 켜져 있는" 상태가 됩니다.
    ///   IsLoaded 로 '진짜 0원'과 '아직 안 불러옴'을 구분할 수 있으니 직접 읽습니다.
    /// </summary>
    private void SyncGoldFromManager()
    {
        var cm = boundCurrency;
        if (cm == null || !cm.IsLoaded) return;

        cachedGold   = cm.Gold;
        hasGoldValue = true;
    }

    // ══════════════════════════════════════════════
    //  버튼 리스너 등록 / 해제
    // ══════════════════════════════════════════════
    private void RegisterButtonListeners()
    {
        // ── 스탯 행: 행마다 버튼 1개 ──
        int n = statRows.Length;
        rowActions = new UnityAction[n];

        for (int i = 0; i < n; i++)
        {
            var type = statRows[i].statType;   // 클로저 캡처용 지역 복사
            rowActions[i] = () => OnClickUpgrade(type);

            // ★ Unity 오브젝트에 ?. 를 쓰면 파괴된 오브젝트를 살아있다고 판단합니다.
            //   != null 로 검사해야 Unity의 수명 검사가 동작합니다.
            if (statRows[i].upgradeButton != null)
                statRows[i].upgradeButton.onClick.AddListener(rowActions[i]);
        }

        // ── 공용 배수 버튼 ──
        int m = multiplierButtons.Length;
        multiplierActions = new UnityAction[m];

        for (int i = 0; i < m; i++)
        {
            int amount = Mathf.Max(1, multiplierButtons[i].amount);   // 클로저 캡처용 지역 복사
            multiplierActions[i] = () => SelectMultiplier(amount);

            if (multiplierButtons[i].button != null)
                multiplierButtons[i].button.onClick.AddListener(multiplierActions[i]);
        }
    }

    private void UnregisterButtonListeners()
    {
        // ★ RemoveAllListeners 는 다른 스크립트가 붙인 리스너까지 날려버립니다.
        //   등록해둔 델리게이트만 정확히 제거합니다.
        if (rowActions != null)
        {
            for (int i = 0; i < statRows.Length && i < rowActions.Length; i++)
                if (statRows[i].upgradeButton != null && rowActions[i] != null)
                    statRows[i].upgradeButton.onClick.RemoveListener(rowActions[i]);
            rowActions = null;
        }

        if (multiplierActions != null)
        {
            for (int i = 0; i < multiplierButtons.Length && i < multiplierActions.Length; i++)
                if (multiplierButtons[i].button != null && multiplierActions[i] != null)
                    multiplierButtons[i].button.onClick.RemoveListener(multiplierActions[i]);
            multiplierActions = null;
        }
    }

    // ══════════════════════════════════════════════
    //  배수 선택
    // ══════════════════════════════════════════════

    /// <summary>공용 배수 선택 (인스펙터 OnClick 에 직접 물려도 됩니다)</summary>
    public void SelectMultiplier(int amount)
    {
        selectedMultiplier = Mathf.Max(1, amount);

        if (rememberMultiplier)
        {
            PlayerPrefs.SetInt(MultiplierPrefKey, selectedMultiplier);
            // ★ PlayerPrefs.Save() 는 일부러 호출하지 않습니다.
            //   모바일에서 매 클릭마다 디스크 flush 를 걸면 프레임이 튑니다.
            //   앱이 정상 종료/백그라운드 진입할 때 Unity가 알아서 저장합니다.
        }

        RefreshMultiplierVisual();
        RefreshAllImmediate();   // 배수가 바뀌면 모든 행의 비용 표시가 바뀐다
    }

    private void RefreshMultiplierVisual()
    {
        for (int i = 0; i < multiplierButtons.Length; i++)
        {
            var mb = multiplierButtons[i];
            bool isSelected = (mb.amount == selectedMultiplier);

            if (mb.selectedMark != null)
            {
                mb.selectedMark.SetActive(isSelected);
                continue;
            }

            if (mb.button == null) continue;

            // ★ targetGraphic.color 를 직접 바꾸면 안 됩니다.
            //   Button 의 Transition 이 Color Tint 이면, 버튼이 매 상태 변화마다
            //   colors.normalColor 로 색을 덮어써서 우리가 칠한 색이 곧바로 사라집니다.
            //   ColorBlock 자체를 바꿔야 유지됩니다.
            var cb = mb.button.colors;
            cb.normalColor   = isSelected ? multiplierSelectedColor : multiplierNormalColor;
            cb.selectedColor = cb.normalColor;
            mb.button.colors = cb;
        }
    }

    // ══════════════════════════════════════════════
    //  버튼 콜백
    // ══════════════════════════════════════════════

    /// <summary>강화 버튼 클릭 — 배수는 공용 selectedMultiplier 를 사용</summary>
    private void OnClickUpgrade(LevelUpManager.StatType type)
    {
        if (lm == null) { TryBindAll(); }
        if (lm == null) return;

        int times = Mathf.Max(1, selectedMultiplier);
        int successCount = lm.TryUpgradeMultiple(type, times);

        if (successCount == 0)
        {
            Debug.Log($"[UpgradeUI] {type} 강화 실패 (Currency 부족 또는 최대 레벨)");
            RefreshAllImmediate();   // 실패해도 비용/보유량 표시는 최신으로
            return;
        }

        // ★ 가이드 퀘스트에 "실제 성공 횟수"만큼 보고
        //   OnStatUpgraded 이벤트를 구독하지 않는 이유: ×100의 발화 횟수를 신뢰할 수 없음
        GuideQuestManager.Instance?.ReportStatUpgrade(type, successCount);

        // ★ 골드가 줄었으므로 다른 행의 구매 가능 여부도 함께 바뀝니다.
        RefreshAllImmediate();
    }

    /// <summary>닫기 버튼 (단독 창 모드 전용 — 탭 모드에서는 TabWindow.Close 를 쓰세요)</summary>
    public void OnClickClose()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    /// <summary>열기 — 외부(HUD 버튼 등)에서 호출. 탭 모드에서는 '갱신'만 합니다.</summary>
    public void Open()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(true);

        // ★ 패널이 열리는 시점에 갱신
        //   OnEnable은 씬 로드 직후 1회만 발화하며, 그땐 Player.Instance가 아직 없을 수 있음.
        //   Open()은 UpgradeUI 자신을 켜는 게 아니라 자식 패널만 켜므로 OnEnable이 재발화하지 않는다.
        TryBindAll();
        RefreshMultiplierVisual();
        RefreshAllImmediate();
    }

    // ══════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════

    /// <summary>이 탭이 선택됐다. 탭이 켜지면서 OnEnable 도 돌지만, 갱신을 한 번 더 보장한다.</summary>
    public void OnTabShow()
    {
        TryBindAll();
        RefreshMultiplierVisual();
        RefreshAllImmediate();
    }

    /// <summary>다른 탭으로 넘어갔다. 예약된 갱신을 버려 헛일을 막는다.</summary>
    public void OnTabHide()
    {
        refreshPending = false;
    }

    /// <summary>가이드 퀘스트가 지정한 스탯 강화 항목을 열고 강조한다. (IStatFocusTarget)</summary>
    public void FocusStat(LevelUpManager.StatType type)
    {
        Open();   // 내부에서 RefreshAllImmediate 수행

        if (rowIndex.ContainsKey(type))
            Debug.Log($"[UpgradeUI] 가이드 퀘스트 포커스 → {GuideQuest.StatName(type)}");
        else
            Debug.LogWarning($"[UpgradeUI] statRows 에 {type} 행이 없어 포커스할 수 없습니다.", this);

        // 필요하면 여기서 하이라이트 / 스크롤 이동 처리
    }

    // ══════════════════════════════════════════════
    //  이벤트 핸들러
    // ══════════════════════════════════════════════

    // 한 행만 바뀌어도 골드가 줄었으므로 다른 행의 구매 가능 여부까지 바뀐다 → 전체 갱신 요청
    private void HandleStatUpgraded(LevelUpManager.StatType type) => RequestRefresh();

    private void HandleGoldChanged(int gold)
    {
        cachedGold   = gold;
        hasGoldValue = true;
        RequestRefresh();
    }

    // ══════════════════════════════════════════════
    //  UI 갱신
    // ══════════════════════════════════════════════

    /// <summary>"곧 다시 그려라" 예약. 골드처럼 초당 수십 번 오는 이벤트용.</summary>
    private void RequestRefresh()
    {
        if (!IsPanelOpen) return;             // 닫혀 있으면 그릴 이유가 없다 (Open() 에서 어차피 그린다)

        if (refreshInterval <= 0f) { RefreshAllImmediate(); return; }

        if (!refreshPending)
        {
            refreshPending  = true;
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }
    }

    private void RefreshAllImmediate()
    {
        refreshPending = false;

        // 이벤트를 놓쳤을 수도 있으니 그릴 때마다 현재 골드를 한 번 맞춰 둔다 (프로퍼티 읽기라 매우 쌈)
        SyncGoldFromManager();

        for (int i = 0; i < statRows.Length; i++)
            RefreshRowAt(i);
    }

    private void RefreshRowAt(int i)
    {
        if (i < 0 || i >= statRows.Length) return;

        StatRow row = statRows[i];
        LevelUpManager.StatType type = row.statType;

        // 스탯 수치는 매니저가 없어도 표시할 수 있습니다
        if (row.statValueText != null)
            row.statValueText.text = GetStatValueString(type);

        // ★ lm 은 있는데 PlayerStat 이 아직 주입되지 않은 구간이 존재합니다.
        //   그때 GetUpgradeCost 는 0을 돌려주므로, 검사하지 않으면
        //   "0 / 5,000, 비용 : 0" 이라는 거짓 정보를 띄우고 버튼도 눌리게 됩니다.
        if (lm == null || !lm.IsReady)
        {
            if (row.upgradeLevelText != null) row.upgradeLevelText.text = "-";
            if (row.costText != null)         row.costText.text         = "-";

            SetButtonInteractable(row.upgradeButton, false);
            return;
        }

        int  upgradeLv = lm.GetUpgradeLevel(type);
        int  maxLv     = lm.MaxUpgradeLevel;      // ★ 하드코딩 5000 제거 — 매니저의 상한을 그대로 사용
        bool isMaxed   = upgradeLv >= maxLv;

        if (row.upgradeLevelText != null)
            row.upgradeLevelText.text = isMaxed ? "MAX" : $"{upgradeLv:N0} / {maxLv:N0}";

        if (isMaxed)
        {
            if (row.costText != null)
            {
                row.costText.text  = "-";
                row.costText.color = costAffordableColor;
            }
            SetButtonInteractable(row.upgradeButton, false);
            return;
        }

        // ★ 여기서부터가 이번 구조의 핵심.
        //   "1회 비용"이 아니라 "선택한 배수만큼의 누적 비용"을 물어봅니다.
        //   남은 레벨이 배수보다 적으면 buyable 에 실제로 살 수 있는 횟수가 돌아옵니다.
        //   (예: 상한까지 3레벨 남았는데 ×100 을 골랐다면 buyable = 3, 비용도 3회분)
        int  times     = Mathf.Max(1, selectedMultiplier);
        long totalCost = lm.GetUpgradeCostMultiple(type, times, out int buyable);

        // ★ 비용 합계는 long 입니다. int 로 받으면 고레벨에서 21억을 넘는 순간
        //   음수로 뒤집혀 "공짜로 살 수 있는 것처럼" 보입니다.
        bool knowGold  = hasGoldValue;
        bool canAfford = !knowGold || cachedGold >= totalCost;
        bool usable    = buyable > 0 && (!disableWhenUnaffordable || canAfford);

        if (row.costText != null)
        {
            string costStr = useKoreanNumberFormat
                ? KoreanNumberFormatter.Format(totalCost)
                : totalCost.ToString("N0");

            // 상한 때문에 배수보다 적게 사게 되는 경우엔 몇 번인지 같이 보여줍니다
            row.costText.text  = (buyable < times) ? $"{costStr} (x{buyable})" : costStr;
            row.costText.color = (knowGold && !canAfford) ? costUnaffordableColor : costAffordableColor;
        }

        SetButtonInteractable(row.upgradeButton, usable);
    }

    private string GetStatValueString(LevelUpManager.StatType type)
    {
        var player = Player.Instance;
        if (player == null || player.stat == null) return "-";

        return type switch
        {
            LevelUpManager.StatType.Damage     => $"{player.stat.baseDamage}",
            LevelUpManager.StatType.CritChance => $"{player.stat.Critical:F1} %",
            LevelUpManager.StatType.CritDamage => $"{player.stat.CriticalMultiplier:F2} x",
            // ★ AttackSpd 는 '공격 쿨다운(ms)'이라 강화할수록 숫자가 줄어듭니다.
            //   원본처럼 raw 값을 그대로 보여주면 유저에겐 스탯이 나빠지는 것처럼 보입니다.
            //   초당 공격 횟수로 환산해 '올라가는 수치'로 표시합니다.
            LevelUpManager.StatType.Attackspd  => player.stat.AttackSpd > 0f
                                                    ? $"{1000f / player.stat.AttackSpd:F2} 회/초"
                                                    : "-",
            _                                  => "-"
        };
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn == null) return;
        btn.interactable = interactable;
    }
}