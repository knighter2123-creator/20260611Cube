using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 던전 카드 1장 (골드 던전 / 소환권 던전 각각 하나씩 배치).
///   - 오늘 남은 입장 횟수 표시 (예: "남은 횟수 2/3")
///   - 난이도 선택 (◀ ▶) — 클리어한 최고 난이도 + 1 까지
///   - 선택한 난이도의 보상 미리보기
///   - 입장 → 확인 팝업("현재 동료 배치로 시작할까요?") → 던전 씬 (횟수는 클리어 시에만 차감)
///
/// 텍스트/버튼은 enterButton 과 confirmPopup 외에는 전부 선택입니다. 비우면 그 항목만 건너뜁니다.
///
/// TabWindow 의 탭 안에 둘 때는 EvolveStageEntry 와 같은 규칙을 지키세요.
///   ① 입장 직전에 창을 닫는다 (ownerWindow — 비우면 부모에서 자동으로 찾음)
///   ② TabWindow 인스펙터에서 이 탭을 Dont Remember As Last Tab 으로
/// </summary>
public class DailyDungeonEntry : MonoBehaviour
{
    [Header("던전")]
    [SerializeField] private DailyDungeonData dungeon;

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

    [Header("문구")]
    [SerializeField] private string labelEnter    = "입장";
    [SerializeField] private string labelNoEntry  = "횟수 소진";

    [Header("탭 창 연동 (선택)")]
    [SerializeField] private TabWindow ownerWindow;

    [Header("상태 갱신")]
    [Tooltip("보이는 동안 이 간격(초)마다 다시 그립니다. 켜 둔 채 오전 6시가 지나도 횟수가 다시 채워지게.")]
    [SerializeField] private float refreshInterval = 1f;

    private int   selectedLevel;   // 0 = 아직 고르지 않음 → 해금된 최고 난이도로 시작
    private float nextRefreshTime;

    void Awake()
    {
        if (ownerWindow == null)
            ownerWindow = GetComponentInParent<TabWindow>(true);
    }

    void Start()
    {
        if (enterButton     != null) enterButton.onClick.AddListener(OnEnterClicked);
        if (prevLevelButton != null) prevLevelButton.onClick.AddListener(SelectPrevLevel);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(SelectNextLevel);

        if (dungeon      == null) Debug.LogWarning("[일일 던전] Dungeon 데이터가 연결되지 않았습니다.", this);
        if (enterButton  == null) Debug.LogWarning("[일일 던전] Enter Button 이 연결되지 않았습니다.", this);
        if (confirmPopup == null) Debug.LogWarning("[일일 던전] Confirm Popup 이 연결되지 않았습니다. 확인 없이 바로 입장합니다.", this);

        Refresh();
    }

    void OnDestroy()
    {
        if (enterButton     != null) enterButton.onClick.RemoveListener(OnEnterClicked);
        if (prevLevelButton != null) prevLevelButton.onClick.RemoveListener(SelectPrevLevel);
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(SelectNextLevel);
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

    // ── 난이도 선택 ────────────────────────────────

    private void SelectPrevLevel() { selectedLevel = CurrentLevel() - 1; Refresh(); }
    private void SelectNextLevel() { selectedLevel = CurrentLevel() + 1; Refresh(); }

    /// <summary>선택 난이도를 [1, 해금된 최고] 로 맞춥니다. 처음엔 해금된 최고 난이도.</summary>
    private int CurrentLevel()
    {
        int max = DailyDungeonProgress.MaxUnlockedLevel(dungeon);
        if (selectedLevel <= 0) selectedLevel = max;
        selectedLevel = Mathf.Clamp(selectedLevel, 1, max);
        return selectedLevel;
    }

    // ── 화면 ───────────────────────────────────────

    private void Refresh()
    {
        if (dungeon == null) return;

        int level     = CurrentLevel();
        int maxLevel  = DailyDungeonProgress.MaxUnlockedLevel(dungeon);
        int remaining = DailyDungeonProgress.RemainingEntries(dungeon);
        bool canEnter = remaining > 0;

        SetText(nameText,   dungeon.displayName);
        SetText(remainText, $"남은 횟수 {remaining}/{dungeon.dailyEntries}");
        SetText(levelText,  $"난이도 {level}");
        SetText(rewardText, $"클리어 보상 : {dungeon.DescribeReward(level)}");
        SetText(enterButtonLabel, canEnter ? labelEnter : labelNoEntry);

        if (prevLevelButton != null) prevLevelButton.interactable = level > 1;
        if (nextLevelButton != null) nextLevelButton.interactable = level < maxLevel;
        if (enterButton     != null) enterButton.interactable     = canEnter;
    }

    private static void SetText(TextMeshProUGUI label, string msg)
    {
        if (label != null && label.text != msg) label.text = msg;
    }

    // ── 입장 ───────────────────────────────────────

    private void OnEnterClicked()
    {
        if (dungeon == null) return;

        if (DailyDungeonProgress.RemainingEntries(dungeon) <= 0)
        {
            Refresh();
            return;
        }

        int level = CurrentLevel();

        if (confirmPopup == null) { Enter(level); return; }

        confirmPopup.Show($"{dungeon.displayName}  Lv.{level}", BuildConfirmMessage(level), () => Enter(level));
    }

    private string BuildConfirmMessage(int level)
    {
        CompanionManager cm = CompanionManager.Instance;
        int placed = cm != null ? cm.PlacedCount   : 0;
        int max    = cm != null ? cm.MaxCompanions : 0;

        string msg = $"현재 배치된 동료 {placed}/{max}명으로 입장합니다.\n" +
                     "던전 안에서는 동료 배치를 바꿀 수 없습니다.\n\n" +
                     $"클리어 보상 : {dungeon.DescribeReward(level)}\n" +
                     $"남은 횟수 {DailyDungeonProgress.RemainingEntries(dungeon)}/{dungeon.dailyEntries} (클리어 시 1회 차감)";

        if (placed == 0)
            msg += "\n\n<color=#FF6060>배치된 동료가 없습니다!</color>";

        return msg + "\n\n이 배치로 시작하시겠습니까?";
    }

    private void Enter(int level)
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

        Debug.Log($"[일일 던전] 입장 — {dungeon.displayName} Lv.{level} " +
                  $"(남은 횟수 {DailyDungeonProgress.RemainingEntries(dungeon)}/{dungeon.dailyEntries})");

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
