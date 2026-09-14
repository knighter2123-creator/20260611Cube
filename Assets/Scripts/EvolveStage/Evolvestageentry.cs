using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 하나로 "아직 클리어하지 않은 가장 낮은 티어"에 순차 입장.
/// 예: 플레이어가 50레벨이어도 30티어를 안 깼으면 30으로 입장.
///
/// 클리어 판정은 기존 보상 지급 플래그(SaveManager.IsEvolveRewardClaimed)를 재사용한다.
/// 진화 스테이지는 클리어 = 보스 처치 = 보상 지급이 동시에 일어나므로
/// EvolveBoss.GrantReward가 남긴 플래그가 곧 "클리어 기록"이다. (별도 저장 불필요)
///
/// TabWindow 의 한 탭으로 들어갈 수 있다. 다만 이 탭은 '보여주는 탭'이 아니라
/// **씬을 넘기는 탭**이라 아래 두 가지를 반드시 지켜야 한다.
///   ① 입장 직전에 창을 닫는다  (ownerWindow)
///   ② 마지막 탭으로 기억하지 않는다  (TabWindow 인스펙터의 Dont Remember As Last Tab)
/// </summary>
public class EvolveStageEntry : MonoBehaviour, ITabPage
{
    [Header("진화 스테이지 티어 (요구 레벨 낮은 순으로 연결)")]
    [SerializeField] private EvolveStageData[] stageTiers;   // 30,50,70,100,200

    [Header("UI")]
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI lockText;

    [Header("탭 창 연동 (선택)")]
    [Tooltip("탭으로 쓸 때 연결. 비워두면 부모에서 자동으로 찾습니다.\n" +
             "입장 직전에 창을 닫는 용도입니다.")]
    [SerializeField] private TabWindow ownerWindow;

    // ★ 구독한 인스턴스를 들고 있다가 그 인스턴스에서 해제합니다.
    //   LevelUpManager.Instance 로 해제하면, 그 사이 매니저가 교체된 경우
    //   '새 매니저에서 있지도 않은 구독을 빼고' 옛 매니저에는 구독이 남습니다.
    private LevelUpManager boundLm;

    private int PlayerLevel =>
        LevelUpManager.Instance != null ? LevelUpManager.Instance.CurrentLevel : 1;

    void Awake()
    {
        // ★ 정렬을 Start 에서 Awake 로 옮겼습니다.
        //   실행 순서는 Awake → OnEnable → Start 입니다.
        //   정렬이 Start 에 있으면, 그보다 먼저 도는 OnEnable 의 RefreshLockState() 가
        //   **정렬되지 않은 배열**로 "다음 티어"를 고릅니다.
        //   인스펙터 연결 순서가 뒤섞여 있으면 첫 화면에 엉뚱한 요구 레벨이 뜹니다.
        //   (탭 모드에서는 탭을 처음 열 때 Awake~Start 가 한꺼번에 돌아 더 잘 드러납니다)
        if (stageTiers != null)
            System.Array.Sort(stageTiers, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.requiredLevel.CompareTo(b.requiredLevel);
            });

        if (ownerWindow == null)
            ownerWindow = GetComponentInParent<TabWindow>(true);   // 꺼져 있는 부모까지 포함해 탐색
    }

    void Start()
    {
        if (enterButton != null)
            enterButton.onClick.AddListener(TryEnter);

        RefreshLockState();
    }

    void OnDestroy()
    {
        if (enterButton != null)
            enterButton.onClick.RemoveListener(TryEnter);
    }

    void OnEnable()
    {
        RefreshLockState();
        Bind();
    }

    void OnDisable()
    {
        Unbind();
    }

    private void Bind()
    {
        var lm = LevelUpManager.Instance;
        if (lm == boundLm) return;

        Unbind();
        boundLm = lm;
        if (boundLm != null) boundLm.OnLevelUp += OnPlayerLevelUp;
    }

    private void Unbind()
    {
        if (boundLm != null) boundLm.OnLevelUp -= OnPlayerLevelUp;
        boundLm = null;
    }

    private void OnPlayerLevelUp(int newLevel) => RefreshLockState();

    // ══════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════

    /// <summary>
    /// 이 탭이 선택됐다 → 잠금 상태를 다시 계산한다.
    ///
    /// ★ OnEnable 에서도 하지만 여기서 한 번 더 하는 이유:
    ///   클리어 여부는 SaveManager 의 플래그라 이벤트가 없습니다.
    ///   진화 스테이지를 깨고 돌아온 직후 탭을 열면, 갱신 계기가 이것뿐입니다.
    /// </summary>
    public void OnTabShow()
    {
        Bind();
        RefreshLockState();
    }

    public void OnTabHide() { }

    // ── 다음에 들어갈 티어 = 아직 클리어(보상 지급) 안 된 것 중 가장 낮은 것 ──
    private EvolveStageData GetNextTier()
    {
        if (stageTiers == null || SaveManager.Instance == null) return null;

        foreach (var tier in stageTiers)   // 낮은 순 정렬 전제 (Awake 에서 정렬)
        {
            if (tier == null) continue;
            if (!SaveManager.Instance.IsEvolveRewardClaimed(tier.id))   // ★ 기존 플래그 재사용
                return tier;               // 첫 번째 미클리어 티어
        }
        return null;   // 전부 클리어함
    }

    private void RefreshLockState()
    {
        EvolveStageData next = GetNextTier();

        // 모든 티어 클리어 → 버튼 비활성 + 안내
        if (next == null)
        {
            if (enterButton != null) enterButton.interactable = false;
            SetLockText("모든 진화 스테이지 클리어", show: true);
            return;
        }

        bool unlocked = PlayerLevel >= next.requiredLevel;

        if (enterButton != null)
            enterButton.interactable = unlocked;

        // 잠김이면 "다음 티어"의 요구 레벨을 안내, 해제되면 숨김
        if (unlocked)
            SetLockText("", show: false);
        else
            SetLockText($"Lv.{next.requiredLevel} 이상 입장 가능", show: true);
    }

    private void SetLockText(string msg, bool show)
    {
        if (lockText == null) return;
        lockText.text = msg;
        lockText.gameObject.SetActive(show);
    }

    private void TryEnter()
    {
        EvolveStageData target = GetNextTier();
        if (target == null)
        {
            Debug.Log("[EvolveStageEntry] 모든 진화 스테이지를 클리어했습니다.");
            return;
        }

        if (PlayerLevel < target.requiredLevel)
        {
            Debug.Log($"[EvolveStageEntry] 레벨 부족: 현재 {PlayerLevel} / 필요 {target.requiredLevel} (티어 {target.id})");
            RefreshLockState();
            return;
        }

        // ★ 씬을 넘기기 전에 창을 닫습니다.
        //   씬이 바뀌면 어차피 창도 파괴되지만, 로딩이 한 프레임이라도 걸리면
        //   그 사이 창이 떠 있는 채로 화면이 넘어갑니다.
        //   그리고 진화 스테이지에서 돌아왔을 때 창이 닫힌 상태로 시작하는 게 자연스럽습니다.
        if (ownerWindow != null) ownerWindow.Close();

        // 복귀할 원래 스테이지 위치 저장 후 입장
        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        EvolveStageContext.Enter(target, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.GoToEvolveStage();
    }
}