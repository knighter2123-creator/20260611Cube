using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 일일 던전 입장 패널 1개. 던전 데이터를 여러 개 넣고 선택 버튼으로 하나를 골라 입장합니다.
///   - 던전 선택 (골드 던전 / 소환권 던전 …) — 고른 던전 기준으로 아래 항목을 다시 그림
///   - 오늘 남은 입장 횟수 표시 (예: "남은 횟수 2/3") — 횟수는 던전마다 따로 (각 던전의 dailyEntries 회)
///   - 난이도 선택 (◀ ▶) — 클리어한 최고 난이도 + 1 까지, 던전마다 따로 기억
///   - 선택한 난이도의 보상 미리보기
///   - 소탕 — 클리어한 난이도는 던전에 들어가지 않고 바로 보상 (횟수 1회 차감)
///   - 입장 횟수 추가 — 확인 팝업 후 재화 차감 (던전마다 하루 최대 extraEntryCosts 길이만큼, 비용은 점점 증가)
///   - 입장 → 확인 팝업("현재 동료 배치로 시작할까요?") → 던전 씬 (횟수는 클리어 시에만 차감)
///
/// 텍스트/버튼은 enterButton 과 confirmPopup 외에는 전부 선택입니다. 비우면 그 항목만 건너뜁니다.
/// 던전이 1개뿐이면 선택 버튼은 비워 두어도 됩니다.
///
/// TabWindow 의 탭 안에 둘 때는 EvolveStageEntry 와 같은 규칙을 지키세요.
///   ① 입장 직전에 창을 닫는다 (ownerWindow — 비우면 부모에서 자동으로 찾음)
///   ② TabWindow 인스펙터에서 이 탭을 Dont Remember As Last Tab 으로
/// </summary>
public class DailyDungeonEntry : MonoBehaviour
{
    [Serializable]
    public struct DungeonTab
    {
        public DailyDungeonData data;

        [Tooltip("이 던전을 고르는 버튼 (선택 — 던전이 1개면 비워도 됨)")]
        public Button button;

        [Tooltip("선택됐을 때 켤 표시 오브젝트 (선택). 비우면 버튼 색으로 표시")]
        public GameObject selectedMark;

        [Tooltip("버튼 아래에 띄울 이 던전의 남은 횟수 (선택). 예: \"2/3\"")]
        public TextMeshProUGUI remainText;
    }

    [Header("던전 (여러 개 가능)")]
    [SerializeField] private DungeonTab[] dungeons;

    [Tooltip("selectedMark 를 지정하지 않은 선택 버튼에 쓰는 색 (Button Transition 이 Color Tint 일 때만 동작)")]
    [SerializeField] private Color tabSelectedColor = new Color(0.35f, 0.62f, 1f);
    [SerializeField] private Color tabNormalColor   = Color.white;

    [Header("UI — 필수")]
    [SerializeField] private Button              enterButton;
    [SerializeField] private DungeonConfirmPopup confirmPopup;

    [Header("UI — 선택")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI remainText;     // "남은 횟수 2/3"
    [SerializeField] private TextMeshProUGUI levelText;      // "난이도 3"
    [SerializeField] private TextMeshProUGUI rewardText;     // "클리어 보상 : 골드 1,500"
    [SerializeField] private Button          prevLevelButton;
    [SerializeField] private Button          nextLevelButton;
    [SerializeField] private TextMeshProUGUI enterButtonLabel;

    [Header("소탕 (선택)")]
    [SerializeField] private Button          sweepButton;
    [SerializeField] private TextMeshProUGUI sweepButtonLabel;

    [Header("입장 횟수 추가 (선택)")]
    [SerializeField] private Button          buyEntryButton;
    [SerializeField] private TextMeshProUGUI buyEntryLabel;     // "횟수 +1 (보석 300)"

    [Header("문구")]
    [SerializeField] private string labelEnter    = "입장";
    [SerializeField] private string labelNoEntry  = "횟수 소진";
    [Tooltip("횟수 추가 버튼 문구. {0} = 비용 문구(예: 보석 300), {1} = 오늘 추가한 횟수, {2} = 하루 최대 추가 횟수")]
    [SerializeField] private string labelBuyEntry = "횟수 +1 ({0})  {1}/{2}";
    [SerializeField] private string labelBuyMax   = "추가 횟수 소진";
    [SerializeField] private string labelSweep         = "소탕";
    [SerializeField] private string labelSweepLocked   = "클리어 후 소탕";
    [Tooltip("선택 버튼 아래 남은 횟수 문구. {0} = 남은 횟수, {1} = 하루 횟수")]
    [SerializeField] private string labelTabRemain = "{0}/{1}";

    [Header("탭 창 연동 (선택)")]
    [SerializeField] private TabWindow ownerWindow;

    [Header("상태 갱신")]
    [Tooltip("보이는 동안 이 간격(초)마다 다시 그립니다. 켜 둔 채 오전 6시가 지나도 횟수가 다시 채워지게.")]
    [SerializeField] private float refreshInterval = 1f;

    // 마지막으로 고른 던전 — 던전에서 돌아와 씬이 다시 로드돼도 같은 던전이 선택돼 있도록 static
    private static string lastSelectedId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => lastSelectedId = null;

    private int           selectedIndex;
    private int[]         selectedLevels;   // 던전별 선택 난이도. 0 = 아직 고르지 않음 → 해금된 최고 난이도로 시작
    private UnityAction[] tabListeners;
    private float         nextRefreshTime;

    /// <summary>지금 선택된 던전 (없으면 null).</summary>
    private DailyDungeonData Selected =>
        dungeons != null && selectedIndex >= 0 && selectedIndex < dungeons.Length ? dungeons[selectedIndex].data : null;

    void Awake()
    {
        if (ownerWindow == null)
            ownerWindow = GetComponentInParent<TabWindow>(true);

        int count = dungeons != null ? dungeons.Length : 0;
        selectedLevels = new int[count];

        selectedIndex = FirstValidIndex();
        for (int i = 0; i < count; i++)
            if (dungeons[i].data != null && dungeons[i].data.id == lastSelectedId) { selectedIndex = i; break; }
    }

    void Start()
    {
        if (enterButton     != null) enterButton.onClick.AddListener(OnEnterClicked);
        if (prevLevelButton != null) prevLevelButton.onClick.AddListener(SelectPrevLevel);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(SelectNextLevel);
        if (buyEntryButton  != null) buyEntryButton.onClick.AddListener(OnBuyEntryClicked);
        if (sweepButton     != null) sweepButton.onClick.AddListener(OnSweepClicked);

        int count = dungeons != null ? dungeons.Length : 0;
        tabListeners = new UnityAction[count];
        for (int i = 0; i < count; i++)
        {
            if (dungeons[i].button == null) continue;
            int index = i;   // 클로저가 루프 변수를 공유하지 않도록 복사
            tabListeners[i] = () => SelectDungeon(index);
            dungeons[i].button.onClick.AddListener(tabListeners[i]);
        }

        if (Selected     == null) Debug.LogWarning("[일일 던전] Dungeons 에 던전 데이터가 연결되지 않았습니다.", this);
        if (enterButton  == null) Debug.LogWarning("[일일 던전] Enter Button 이 연결되지 않았습니다.", this);
        if (confirmPopup == null) Debug.LogWarning("[일일 던전] Confirm Popup 이 연결되지 않았습니다. 확인 없이 바로 입장합니다.", this);

        Refresh();
    }

    void OnDestroy()
    {
        if (enterButton     != null) enterButton.onClick.RemoveListener(OnEnterClicked);
        if (prevLevelButton != null) prevLevelButton.onClick.RemoveListener(SelectPrevLevel);
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(SelectNextLevel);
        if (buyEntryButton  != null) buyEntryButton.onClick.RemoveListener(OnBuyEntryClicked);
        if (sweepButton     != null) sweepButton.onClick.RemoveListener(OnSweepClicked);

        if (tabListeners != null)
            for (int i = 0; i < tabListeners.Length; i++)
                if (tabListeners[i] != null && dungeons[i].button != null)
                    dungeons[i].button.onClick.RemoveListener(tabListeners[i]);
    }

    void OnEnable()
    {
        DailyDungeonProgress.OnChanged += Refresh;
        Refresh();
        nextRefreshTime = 0f;
    }

    void OnDisable()
    {
        DailyDungeonProgress.OnChanged -= Refresh;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        Refresh();
    }

    // ── 던전 선택 ──────────────────────────────────

    private int FirstValidIndex()
    {
        if (dungeons != null)
            for (int i = 0; i < dungeons.Length; i++)
                if (dungeons[i].data != null) return i;
        return 0;
    }

    private void SelectDungeon(int index)
    {
        if (dungeons == null || index < 0 || index >= dungeons.Length || dungeons[index].data == null) return;

        selectedIndex  = index;
        lastSelectedId = dungeons[index].data.id;
        Refresh();
    }

    private void RefreshTabs()
    {
        if (dungeons == null) return;

        for (int i = 0; i < dungeons.Length; i++)
        {
            bool isSelected = i == selectedIndex;
            DungeonTab t = dungeons[i];

            if (t.data != null)
                SetText(t.remainText, string.Format(labelTabRemain,
                        DailyDungeonProgress.RemainingEntries(t.data), DailyDungeonProgress.TotalEntries(t.data)));

            if (t.selectedMark != null)
            {
                if (t.selectedMark.activeSelf != isSelected) t.selectedMark.SetActive(isSelected);
                continue;
            }

            if (t.button == null) continue;

            // targetGraphic.color 를 직접 칠하면 Color Tint 트랜지션이 덮어쓰므로 ColorBlock 을 바꿉니다. (TabWindow 와 같은 방식)
            Color normal = isSelected ? tabSelectedColor : tabNormalColor;
            var cb = t.button.colors;
            if (cb.normalColor == normal) continue;
            cb.normalColor   = normal;
            cb.selectedColor = normal;
            t.button.colors  = cb;
        }
    }

    // ── 난이도 선택 ────────────────────────────────

    private void SelectPrevLevel() { SetSelectedLevel(CurrentLevel() - 1); Refresh(); }
    private void SelectNextLevel() { SetSelectedLevel(CurrentLevel() + 1); Refresh(); }

    private void SetSelectedLevel(int level)
    {
        if (selectedLevels != null && selectedIndex < selectedLevels.Length)
            selectedLevels[selectedIndex] = level;
    }

    /// <summary>선택한 던전의 난이도를 [1, 해금된 최고] 로 맞춥니다. 처음엔 해금된 최고 난이도. 던전마다 따로 기억합니다.</summary>
    private int CurrentLevel()
    {
        int max = DailyDungeonProgress.MaxUnlockedLevel(Selected);
        if (selectedLevels == null || selectedIndex >= selectedLevels.Length) return max;

        int level = selectedLevels[selectedIndex];
        if (level <= 0) level = max;
        level = Mathf.Clamp(level, 1, max);
        selectedLevels[selectedIndex] = level;
        return level;
    }

    // ── 화면 ───────────────────────────────────────

    private void Refresh()
    {
        RefreshTabs();

        DailyDungeonData dungeon = Selected;
        if (dungeon == null) return;

        int level     = CurrentLevel();
        int maxLevel  = DailyDungeonProgress.MaxUnlockedLevel(dungeon);
        int remaining = DailyDungeonProgress.RemainingEntries(dungeon);
        bool canEnter = remaining > 0;

        SetText(nameText,   dungeon.displayName);
        SetText(remainText, RemainSummary(dungeon));
        SetText(levelText,  $"난이도 {level}");
        SetText(rewardText, $"클리어 보상 : {dungeon.DescribeReward(level)}");
        SetText(enterButtonLabel, canEnter ? labelEnter : labelNoEntry);

        if (prevLevelButton != null) prevLevelButton.interactable = level > 1;
        if (nextLevelButton != null) nextLevelButton.interactable = level < maxLevel;
        if (enterButton     != null) enterButton.interactable     = canEnter;

        int sweepLevel = SweepLevel(dungeon, level);
        SetText(sweepButtonLabel, sweepLevel > 0 ? $"{labelSweep} Lv.{sweepLevel}" : labelSweepLocked);
        if (sweepButton != null) sweepButton.interactable = sweepLevel > 0 && canEnter;

        RefreshBuyEntry(dungeon);
    }

    // ── 소탕 ───────────────────────────────────────

    /// <summary>
    /// 소탕할 난이도. 선택한 난이도를 클리어했으면 그 난이도, 아직이면(기본 선택인 '다음 도전 난이도') 최고 클리어 난이도.
    /// 클리어한 난이도가 없으면 0.
    /// </summary>
    private static int SweepLevel(DailyDungeonData dungeon, int selectedLevel)
        => Mathf.Min(selectedLevel, DailyDungeonProgress.HighestCleared(dungeon));

    private void OnSweepClicked()
    {
        // 누른 시점의 던전/난이도를 붙잡아 둡니다 — 팝업이 떠 있는 동안 선택이 바뀌어도 확인한 대상으로 소탕되도록
        DailyDungeonData dungeon = Selected;
        if (dungeon == null) return;

        int level = SweepLevel(dungeon, CurrentLevel());
        if (!DailyDungeonProgress.CanSweep(dungeon, level))
        {
            Refresh();
            return;
        }

        string msg = $"{dungeon.displayName} 난이도 {level}을(를) 진행 없이 클리어 처리합니다.\n\n" +
                     $"클리어 보상 : {dungeon.DescribeReward(level)}\n" +
                     $"{RemainSummary(dungeon)} (1회 차감)\n\n" +
                     "소탕하시겠습니까?";
        AskThen($"소탕  Lv.{level}", msg, () => Sweep(dungeon, level));
    }

    private void Sweep(DailyDungeonData dungeon, int level)
    {
        // 확인을 누른 시점에 다시 판정합니다 (TrySweep 이 조건을 확인하고, 안 맞으면 아무것도 바꾸지 않음)
        bool ok = DailyDungeonProgress.TrySweep(dungeon, level);
        Refresh();

        if (ok)
            Notify("소탕 완료",
                $"{dungeon.displayName}  Lv.{level}\n\n" +
                $"획득 : {dungeon.DescribeReward(level)}\n\n" +
                RemainSummary(dungeon));
        else
            Notify("소탕 실패",
                $"소탕할 수 없습니다.\n(입장 횟수 또는 클리어 기록을 확인하세요)\n\n{RemainSummary(dungeon)}");
    }

    private void RefreshBuyEntry(DailyDungeonData dungeon)
    {
        int cost = DailyDungeonProgress.NextPurchaseCost(dungeon);
        bool canBuy = cost >= 0;

        SetText(buyEntryLabel, canBuy
            ? string.Format(labelBuyEntry, dungeon.DescribeCost(cost),
                            DailyDungeonProgress.PurchasedToday(dungeon), dungeon.MaxExtraEntries)
            : labelBuyMax);

        // 재화가 부족해도 버튼은 켜 둡니다 — 누르면 부족 알림을 띄웁니다 (OnBuyEntryClicked). 끄는 건 하루 최대 횟수를 다 썼을 때만.
        if (buyEntryButton != null)
            buyEntryButton.interactable = canBuy;
    }

    // ── 횟수 추가 ──────────────────────────────────

    private void OnBuyEntryClicked()
    {
        // 누른 시점의 던전/비용을 붙잡아 둡니다 — 팝업이 떠 있는 동안 선택이 바뀌어도 확인한 던전에 추가되도록
        DailyDungeonData dungeon = Selected;
        if (dungeon == null) return;

        int cost = DailyDungeonProgress.NextPurchaseCost(dungeon);
        if (cost < 0)
        {
            Refresh();
            return;
        }

        if (!DailyDungeonProgress.CanAffordNextPurchase(dungeon))
        {
            Notify("재화 부족", $"입장 횟수를 추가하려면 {dungeon.DescribeCost(cost)}이(가) 필요합니다.");
            return;
        }

        string msg = $"{dungeon.DescribeCost(cost)}을(를) 사용해\n" +
                     $"{dungeon.displayName} 입장 횟수를 1회 추가합니다.\n\n" +
                     $"오늘 추가한 횟수 {DailyDungeonProgress.PurchasedToday(dungeon)}/{dungeon.MaxExtraEntries}\n\n" +
                     "추가하시겠습니까?";
        AskThen("입장 횟수 추가", msg, () => BuyEntry(dungeon, cost));
    }

    private void BuyEntry(DailyDungeonData dungeon, int confirmedCost)
    {
        // 팝업이 떠 있는 사이 다른 곳에서 추가/초기화돼 비용이 바뀌었으면, 확인한 금액과 다르므로 진행하지 않습니다.
        if (DailyDungeonProgress.NextPurchaseCost(dungeon) != confirmedCost)
        {
            Debug.Log($"[일일 던전] 횟수 추가 취소 — 비용이 바뀜 ({dungeon.id})");
            Refresh();
            return;
        }

        DailyDungeonProgress.TryPurchaseEntry(dungeon);
        Refresh();
    }

    private static void SetText(TextMeshProUGUI label, string msg)
    {
        if (label != null && label.text != msg) label.text = msg;
    }

    /// <summary>"남은 횟수 2/5"</summary>
    private static string RemainSummary(DailyDungeonData dungeon)
        => $"남은 횟수 {DailyDungeonProgress.RemainingEntries(dungeon)}/{DailyDungeonProgress.TotalEntries(dungeon)}";

    /// <summary>확인 팝업을 띄우고 확인 시 action 실행. 팝업이 연결돼 있지 않으면 바로 실행합니다.</summary>
    private void AskThen(string title, string message, Action action)
    {
        if (confirmPopup == null) { action(); return; }
        confirmPopup.Show(title, message, action);
    }

    /// <summary>알림 팝업 (확인 버튼만). 팝업이 연결돼 있지 않으면 콘솔에만 남깁니다.</summary>
    private void Notify(string title, string message)
    {
        if (confirmPopup != null) confirmPopup.ShowMessage(title, message);
        else Debug.Log($"[일일 던전] {title} — {message}");
    }

    // ── 입장 ───────────────────────────────────────

    private void OnEnterClicked()
    {
        // 누른 시점의 던전을 붙잡아 둡니다 — 팝업이 떠 있는 동안 선택이 바뀌어도 확인한 던전으로 들어가도록
        DailyDungeonData dungeon = Selected;
        if (dungeon == null) return;

        if (DailyDungeonProgress.RemainingEntries(dungeon) <= 0)
        {
            Refresh();
            return;
        }

        int level = CurrentLevel();

        AskThen($"{dungeon.displayName}  Lv.{level}", BuildConfirmMessage(dungeon, level), () => Enter(dungeon, level));
    }

    private string BuildConfirmMessage(DailyDungeonData dungeon, int level)
    {
        CompanionManager cm = CompanionManager.Instance;
        int placed = cm != null ? cm.PlacedCount   : 0;
        int max    = cm != null ? cm.MaxCompanions : 0;

        string msg = $"현재 배치된 동료 {placed}/{max}명으로 입장합니다.\n" +
                     "던전 안에서는 동료 배치를 바꿀 수 없습니다.\n\n" +
                     $"클리어 보상 : {dungeon.DescribeReward(level)}\n" +
                     $"{RemainSummary(dungeon)} (클리어 시 1회 차감)";

        if (placed == 0)
            msg += "\n\n<color=#FF6060>배치된 동료가 없습니다!</color>";

        return msg + "\n\n이 배치로 시작하시겠습니까?";
    }

    private void Enter(DailyDungeonData dungeon, int level)
    {
        // 팝업이 떠 있는 사이 상태가 바뀌었을 수 있으니 확인을 누른 시점에 다시 판정합니다.
        // 횟수는 여기서 차감하지 않습니다 — 클리어했을 때만 DailyDungeonManager 가 차감합니다.
        if (!DailyDungeonProgress.IsLevelUnlocked(dungeon, level) ||
            DailyDungeonProgress.RemainingEntries(dungeon) <= 0)
        {
            Debug.Log($"[일일 던전] 입장 거부 — {dungeon.id} Lv.{level}");
            Refresh();
            return;
        }

        Debug.Log($"[일일 던전] 입장 — {dungeon.displayName} Lv.{level} ({RemainSummary(dungeon)})");

        lastSelectedId = dungeon.id;   // 돌아왔을 때 이 던전이 선택돼 있도록

        if (ownerWindow != null) ownerWindow.Close();

        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        DailyDungeonContext.Enter(dungeon, level, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();

        // 지금까지의 스테이지 진행/배치를 기록 (던전 씬에는 StageManager 가 없어 거기서는 진행을 저장하지 못함)
        if (SaveManager.Instance != null) SaveManager.Instance.Save();

        SceneLoader.Instance?.GoToDailyDungeon();
    }
}
