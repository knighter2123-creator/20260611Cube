using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>스탯 강화 탭: 상단 공용 배수(×1/×10/×100) 하나를 모든 행이 공유하고, 행마다 누적 비용과 '현재 → 강화 후' 미리보기를 표시 (골드 변화는 refreshInterval 로 묶어서 다시 그림).</summary>
// 창 열기·가이드 퀘스트 "강화 창 열기" 구독은 TabWindow 가 담당 (같은 오브젝트를 둘이 켜고 끄면 어긋나서)
public class UpgradeUI : MonoBehaviour, ITabPage, IStatFocusTarget
{
    // ── [직렬화] 공용 배수 버튼 1개 ──
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

    // ── [직렬화] 스탯 행 1개의 UI 묶음 ──
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

    // ── 인스펙터 연결 ──

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

    [Tooltip("비용을 '45.6M' 처럼 K/M/B/T 단위로 줄여 표시합니다. 끄면 '45,600,000' 처럼 전부 표시")]
    [FormerlySerializedAs("useKoreanNumberFormat")]
    [SerializeField] private bool useShortNumberFormat = true;

    [Tooltip("스탯 수치를 '현재 → 강화 후' 로 표시합니다. (선택한 배수만큼 강화했을 때)\n" +
             "글자가 칸을 넘치면 끄세요 — 그러면 예전처럼 현재 값만 보입니다.")]
    [SerializeField] private bool showNextValuePreview = true;

    [Header("성능")]
    [Tooltip("골드 변화가 잦아도 최소 이 간격으로만 UI를 다시 그립니다 (0 이면 즉시)")]
    [SerializeField] private float refreshInterval = 0.1f;

    // ── 내부 상태 ──

    // 구독 여부를 bool 대신 '구독한 인스턴스'로 기억 — 매니저가 재생성되면 바뀐 순간 갈아탐 (bool 이면 옛 인스턴스에 묶인 죽은 UI)
    private LevelUpManager    boundLm;
    private CurrencyManager   boundCurrency;

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

    // 탭이 가려지면 컴포넌트째 꺼지므로 Update 가 돈다 = 보이는 중 ('닫혀 있으면 안 그림' 검사 불필요)

    // ── Unity 생명주기 ──
    private void Awake()
    {
        BuildRowIndex();
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

        // 저장값이 지금 버튼에 없으면(버튼을 뺀 경우) 선택 표시 없이 적용되는 유령 상태가 되므로 보정
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

    // 가이드 퀘스트의 "강화 UI 열기" 예약은 항상 켜진 TabWindow.Start 가 소비 (탭이 꺼져 있으면 여기 Start 가 안 돎)

    private void OnEnable()
    {
        RegisterButtonListeners();
        TryBindAll();
        RefreshMultiplierVisual();
        RefreshAllImmediate();
    }

    private void Update()
    {
        // 매 프레임 매니저 인스턴스가 바뀌었는지 확인 (OnEnable 한 번이면 그때 없을 때 세션 내내 갱신 안 됨, 비교라 비용 거의 0)
        TryBindAll();

        // 묶어 둔 갱신 — 배속·일시정지에도 일정하게 unscaledTime
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

    // ── 매니저 구독 (인스턴스 교체 대응) ──
    private void TryBindAll()
    {
        // ── LevelUpManager ──
        var currentLm = LevelUpManager.Instance;
        if (currentLm != boundLm)
        {
            // 옛 인스턴스가 파괴됐으면 Unity != null 이 걸러 해제를 건너뜀
            if (boundLm != null) boundLm.OnStatUpgraded -= HandleStatUpgraded;

            boundLm = currentLm;

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
        // GuideQuestManager.OnFocusStatUpgrade 는 TabWindow 가 구독합니다 (여기서도 하면 창 열기가 두 번 돎).
    }

    private void UnbindAll()
    {
        if (boundLm != null)       boundLm.OnStatUpgraded      -= HandleStatUpgraded;
        if (boundCurrency != null) boundCurrency.OnGoldChanged -= HandleGoldChanged;

        boundLm       = null;
        boundCurrency = null;
    }

    /// <summary>현재 골드를 직접 읽어 캐시 — 늦게 구독하면 복원 때의 이벤트를 놓치므로 (IsLoaded 로 미준비와 0원을 구분).</summary>
    private void SyncGoldFromManager()
    {
        var cm = boundCurrency;
        if (cm == null || !cm.IsLoaded) return;

        cachedGold   = cm.Gold;
        hasGoldValue = true;
    }

    // ── 버튼 리스너 등록 / 해제 ──
    private void RegisterButtonListeners()
    {
        // ── 스탯 행: 행마다 버튼 1개 ──
        int n = statRows.Length;
        rowActions = new UnityAction[n];

        for (int i = 0; i < n; i++)
        {
            var type = statRows[i].statType;   // 클로저 캡처용 지역 복사
            rowActions[i] = () => OnClickUpgrade(type);

            // Unity 오브젝트는 ?. 대신 != null (파괴 검사)
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
        // RemoveAllListeners 는 남의 리스너까지 지우므로 등록한 것만 제거
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

    // ── 배수 선택 ──

    /// <summary>공용 배수 선택 (인스펙터 OnClick 에 직접 물려도 됩니다)</summary>
    public void SelectMultiplier(int amount)
    {
        selectedMultiplier = Mathf.Max(1, amount);

        if (rememberMultiplier)
        {
            PlayerPrefs.SetInt(MultiplierPrefKey, selectedMultiplier);
            // PlayerPrefs.Save 는 일부러 안 부름 — 클릭마다 디스크 flush 는 프레임이 튐 (종료·백그라운드 때 Unity 가 저장)
        }

        RefreshMultiplierVisual();
        RefreshAllImmediate();   // 배수가 바뀌면 모든 행의 비용 · 미리보기가 바뀐다
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

            // targetGraphic.color 는 Color Tint 가 곧바로 덮어쓰므로 ColorBlock 을 바꿈
            var cb = mb.button.colors;
            cb.normalColor   = isSelected ? multiplierSelectedColor : multiplierNormalColor;
            cb.selectedColor = cb.normalColor;
            mb.button.colors = cb;
        }
    }

    // ── 버튼 콜백 ──

    /// <summary>강화 버튼 클릭 — 배수는 공용 selectedMultiplier 를 사용</summary>
    private void OnClickUpgrade(LevelUpManager.StatType type)
    {
        if (boundLm == null) TryBindAll();
        if (boundLm == null) return;

        int times = Mathf.Max(1, selectedMultiplier);
        int successCount = boundLm.TryUpgradeMultiple(type, times);

        if (successCount == 0)
        {
            GameLog.Info($"[UpgradeUI] {type} 강화 실패 (Currency 부족 또는 최대 레벨)");
            RefreshAllImmediate();   // 실패해도 비용/보유량 표시는 최신으로
            return;
        }

        // 가이드 퀘스트에 실제 성공 횟수만큼 보고 (OnStatUpgraded 는 ×100 때 발화 횟수를 믿을 수 없음)
        GuideQuestManager.Instance?.ReportStatUpgrade(type, successCount);

        // ★ 골드가 줄었으므로 다른 행의 구매 가능 여부도 함께 바뀝니다.
        RefreshAllImmediate();
    }

    // ── ITabPage — TabWindow 가 부름 ──

    /// <summary>이 탭이 선택됐다. 탭이 켜지면서 OnEnable 도 돌지만, 갱신을 한 번 더 보장한다.</summary>
    public void OnTabShow() => RefreshNow();

    /// <summary>매니저 구독을 확인하고 배수 표시·모든 행을 바로 다시 그린다.</summary>
    private void RefreshNow()
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
        RefreshNow();   // 탭은 TabWindow 가 이미 열었음

        if (rowIndex.ContainsKey(type))
            GameLog.Info($"[UpgradeUI] 가이드 퀘스트 포커스 → {GuideQuest.StatName(type)}");
        else
            Debug.LogWarning($"[UpgradeUI] statRows 에 {type} 행이 없어 포커스할 수 없습니다.", this);

        // 필요하면 여기서 하이라이트 / 스크롤 이동 처리
    }

    // ── 이벤트 핸들러 ──

    // 한 행만 바뀌어도 골드가 줄었으므로 다른 행의 구매 가능 여부까지 바뀐다 → 전체 갱신 요청
    private void HandleStatUpgraded(LevelUpManager.StatType type) => RequestRefresh();

    private void HandleGoldChanged(int gold)
    {
        cachedGold   = gold;
        hasGoldValue = true;
        RequestRefresh();
    }

    // ── UI 갱신 ──

    /// <summary>"곧 다시 그려라" 예약. 골드처럼 초당 수십 번 오는 이벤트용.</summary>
    private void RequestRefresh()
    {
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

        // 일단 현재 값만 표시 (강화 가능하면 아래에서 '현재 → 다음'으로 덮어씀)
        if (row.statValueText != null)
            row.statValueText.text = GetStatValueString(type);

        // PlayerStat 주입 전엔 비용이 0 으로 나와 거짓 정보·버튼 활성이 되므로 IsReady 확인
        LevelUpManager lm = boundLm;
        if (lm == null || !lm.IsReady)
        {
            if (row.upgradeLevelText != null) row.upgradeLevelText.text = "-";
            if (row.costText != null)         row.costText.text         = "-";

            SetButtonInteractable(row.upgradeButton, false);
            return;
        }

        int  upgradeLv = lm.GetUpgradeLevel(type);
        int  maxLv     = lm.MaxUpgradeLevel;      // ★ 하드코딩 5000 제거 — 매니저의 상한을 그대로 사용
        bool isMaxed   = lm.IsMaxUpgraded(type);  // 최대 판정도 매니저 한 곳에서 (TryUpgrade 와 같은 기준)

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

        // 선택 배수만큼의 누적 비용 — 상한까지 남은 레벨이 적으면 buyable 이 실제 횟수 (예: 3 남음 + ×100 → 3회분)
        int  times     = Mathf.Max(1, selectedMultiplier);
        long totalCost = lm.GetUpgradeCostMultiple(type, times, out int buyable);

        // 비용 합계는 long (int 면 21억을 넘을 때 음수가 돼 공짜처럼 보임)
        bool knowGold  = hasGoldValue;
        bool canAfford = !knowGold || cachedGold >= totalCost;
        bool usable    = buyable > 0 && (!disableWhenUnaffordable || canAfford);

        // '현재 → 다음' 미리보기 — 비용과 같은 buyable 기준, 골드가 부족해도 보여 줌(모을 동기)
        if (showNextValuePreview && buyable > 0 && row.statValueText != null)
        {
            float next = lm.PreviewStatValue(type, buyable);
            row.statValueText.text = $"{GetStatValueString(type)} → {FormatStatValue(type, next)}";
        }

        if (row.costText != null)
        {
            string costStr = useShortNumberFormat
                ? NumberFormat.Short(totalCost)
                : totalCost.ToString("N0");

            // 상한 때문에 배수보다 적게 사게 되는 경우엔 몇 번인지 같이 보여줍니다
            row.costText.text  = (buyable < times) ? $"{costStr} (x{buyable})" : costStr;
            row.costText.color = (knowGold && !canAfford) ? costUnaffordableColor : costAffordableColor;
        }

        SetButtonInteractable(row.upgradeButton, usable);
    }

    // ── 스탯 수치 문자열 ──

    /// <summary>현재 스탯 값을 화면용 문자열로. Player 가 없으면 "-".</summary>
    private string GetStatValueString(LevelUpManager.StatType type)
    {
        var player = Player.Instance;
        if (player == null || player.stat == null) return "-";

        PlayerStat s = player.stat;

        float raw = type switch
        {
            LevelUpManager.StatType.Damage     => s.baseDamage,
            LevelUpManager.StatType.CritChance => s.Critical,
            LevelUpManager.StatType.CritDamage => s.CriticalMultiplier,
            LevelUpManager.StatType.Attackspd  => s.AttackSpd,
            _                                  => float.NaN
        };

        return FormatStatValue(type, raw);
    }

    /// <summary>스탯 값 → 화면 문자열 — 현재 값과 다음 값이 같은 모양이 되도록 한 함수로 포맷.</summary>
    private static string FormatStatValue(LevelUpManager.StatType type, float raw)
    {
        if (float.IsNaN(raw)) return "-";

        switch (type)
        {
            case LevelUpManager.StatType.Damage:
                return $"{raw:N0}";

            case LevelUpManager.StatType.CritChance:
                // F2 — 레벨당 약 0.0194% 라 F1 이면 ×1 미리보기가 안 오르는 것처럼 보임
                return $"{raw:F2} %";

            case LevelUpManager.StatType.CritDamage:
                return $"{raw:F2} x";

            case LevelUpManager.StatType.Attackspd:
                // AttackSpd 는 쿨다운(ms)이라 줄수록 좋음 → 초당 공격 횟수로 환산 (하한은 스탯창과 같은 MIN_ATTACK_SPD, 0 나누기 방어 겸)
                return $"{1000f / Mathf.Max(raw, PlayerStat.MIN_ATTACK_SPD):F2} 회/초";

            default:
                return "-";
        }
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn == null) return;
        btn.interactable = interactable;
    }
}