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

    [Header("상태 갱신")]
    [Tooltip("탭이 보이는 동안 이 간격(초)마다 입장 가능 여부를 다시 확인합니다.\n" +
             "세이브(클리어 기록)나 레벨이 탭을 연 '뒤에' 늦게 반영돼도 버튼이 곧 맞춰집니다.")]
    [SerializeField] private float refreshInterval = 0.5f;

    // ── 입장 가능 여부 (한 곳에서만 계산) ──
    private enum EntryState { AllCleared, Locked, Unlocked }

    // 마지막으로 화면에 반영한 상태. 같으면 다시 그리지 않습니다.
    // ★ TMP 의 text 는 같은 문자열을 넣어도 메시를 다시 만들 수 있어, 주기 갱신에서는 바뀔 때만 씁니다.
    private EntryState? shownState;
    private int         shownRequiredLevel = -1;
    private float       nextRefreshTime;

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
        // 켜질 때는 마지막 표시 기록을 믿지 않고 무조건 다시 그립니다
        // (꺼져 있는 동안 누군가 버튼/문구를 바꿨을 수 있음)
        RefreshLockState(force: true);
        Bind();
        nextRefreshTime = 0f;   // 다음 Update 에서 바로 한 번 더 확인
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
        RefreshLockState(force: true);
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

    /// <summary>
    /// ★ [수정] 입장 가능 여부를 계산하는 곳을 이 함수 하나로 모았습니다.
    ///   예전에는 RefreshLockState(버튼 표시) 와 TryEnter(실제 입장) 가 각자 같은 계산을 했고,
    ///   두 계산이 '서로 다른 시점' 에 돌면서 결과가 어긋났습니다.
    ///
    ///   어긋나던 흐름 (예: 플레이어 Lv.40, 30티어는 이미 클리어)
    ///     탭 표시 시점 : 클리어 기록/레벨이 아직 반영 전 → 다음 티어 = 30 → 40 ≥ 30 → 버튼 활성 ✗
    ///     버튼 클릭 시점: 기록 반영 완료          → 다음 티어 = 50 → 40 < 50 → 입장 거부 + 잠금 문구
    ///   → "버튼은 켜져 있는데 누르면 그제야 잠김 문구가 뜨는" 증상.
    ///
    ///   클리어 기록(SaveManager)에는 '바뀌었다' 는 이벤트가 없어서, 탭이 보이는 동안
    ///   짧은 간격으로 다시 확인합니다 (아래 Update). 티어가 5개뿐이라 비용은 무시할 수준입니다.
    /// </summary>
    private EntryState Evaluate(out EvolveStageData next)
    {
        next = GetNextTier();
        if (next == null) return EntryState.AllCleared;
        return PlayerLevel >= next.requiredLevel ? EntryState.Unlocked : EntryState.Locked;
    }

    private void RefreshLockState() => RefreshLockState(force: false);

    private void RefreshLockState(bool force)
    {
        EntryState state = Evaluate(out EvolveStageData next);
        int required = next != null ? next.requiredLevel : -1;

        // 화면에 보이는 상태와 같으면 아무것도 하지 않습니다 (주기 갱신 비용 최소화)
        if (!force && shownState == state && shownRequiredLevel == required) return;
        shownState         = state;
        shownRequiredLevel = required;

        if (enterButton != null)
            enterButton.interactable = state == EntryState.Unlocked;

        switch (state)
        {
            case EntryState.AllCleared:
                SetLockText("모든 진화 스테이지 클리어", show: true);
                break;
            case EntryState.Locked:
                // 잠김이면 "다음 티어"의 요구 레벨을 안내
                SetLockText($"Lv.{required} 이상 입장 가능", show: true);
                break;
            default:
                SetLockText("", show: false);
                break;
        }
    }

    /// <summary>
    /// 탭이 보이는 동안(= 이 오브젝트가 켜져 있는 동안)만 돕니다.
    /// 탭이 가려지면 TabWindow 가 오브젝트를 끄므로 자동으로 멈춥니다.
    /// </summary>
    void Update()
    {
        // unscaledTime — 설정창의 일시정지(timeScale 0)나 배속과 무관하게 같은 간격으로 확인
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);

        Bind();              // 매니저가 재생성됐다면 새 인스턴스로 갈아탐
        RefreshLockState();
    }

    private void SetLockText(string msg, bool show)
    {
        if (lockText == null) return;
        lockText.text = msg;
        lockText.gameObject.SetActive(show);
    }

    private void TryEnter()
    {
        // ★ [수정] 버튼 표시와 '같은 함수' 로 판정합니다.
        //   여기서 막혔다면 화면이 잠시 늦었던 것이므로, 화면을 즉시 맞춘 뒤 돌아갑니다.
        //   (정상이라면 잠긴 상태에서는 버튼이 비활성이라 여기까지 오지 않습니다)
        EntryState state = Evaluate(out EvolveStageData target);
        if (state != EntryState.Unlocked)
        {
            RefreshLockState(force: true);
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