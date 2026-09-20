using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 하나로 "아직 클리어하지 않은 가장 낮은 티어"에 순차 입장.
/// 예: 플레이어가 50레벨이어도 30티어를 안 깼으면 30으로 입장.
///
/// 클리어 판정은 기존 보상 지급 플래그(SaveManager.IsEvolveRewardClaimed)를 재사용한다.
/// 진화 스테이지는 클리어 = 보스 처치 = 보상 지급이 동시에 일어나므로
/// EvolveBoss.GrantRewards가 남긴 플래그가 곧 "클리어 기록"이다. (별도 저장 불필요)
///
/// TabWindow 의 한 탭으로 들어갈 수 있다. 다만 이 탭은 '보여주는 탭'이 아니라
/// **씬을 넘기는 탭**이라 아래 두 가지를 반드시 지켜야 한다.
///   ① 입장 직전에 창을 닫는다  (ownerWindow)
///   ② 마지막 탭으로 기억하지 않는다  (TabWindow 인스펙터의 Dont Remember As Last Tab)
///
/// ═══ ★ 이번 수정 요약 ═══════════════════════════════════════════════
///   ① 패널 표시용 텍스트 3개 추가 — 티어 이름 / 요구 레벨 / 보상 미리보기
///      전부 [SerializeField] 이고 **비워두면 그 항목만 건너뜁니다.**
///      기존에 lockText 하나만 연결해 두셨어도 그대로 동작합니다.
///   ② 입장 버튼 라벨 교체 지원 (진입 / 모두 클리어)
///   ③ 화면 갱신 캐시 기준을 requiredLevel → 티어 에셋 자체로 변경
///      (이름·보상이 바뀌는데 요구 레벨만 같으면 갱신을 건너뛰던 문제)
///   ④ 연결 누락 진단 로그 추가 — "패널은 열리는데 아무 일도 안 일어난다"의 원인을
///      콘솔에서 바로 알 수 있게
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class EvolveStageEntry : MonoBehaviour, ITabPage
{
    [Header("진화 스테이지 티어 (요구 레벨 낮은 순으로 연결)")]
    [SerializeField] private EvolveStageData[] stageTiers;   // 30,50,70,100,200

    [Header("UI — 필수")]
    [Tooltip("각성 스테이지로 들어가는 '진입' 버튼. 이게 비어 있으면 패널은 열려도 입장할 수 없습니다.")]
    [SerializeField] private Button enterButton;

    [Header("UI — 선택 (비우면 그 항목만 표시하지 않습니다)")]
    [Tooltip("잠김/전부 클리어 안내 문구")]
    [SerializeField] private TextMeshProUGUI lockText;

    [Tooltip("다음에 들어갈 티어 이름. EvolveStageData.displayName 을 그대로 씁니다.")]
    [SerializeField] private TextMeshProUGUI tierNameText;

    [Tooltip("입장 조건 레벨. 예: \"입장 조건  Lv.50\"")]
    [SerializeField] private TextMeshProUGUI requiredLevelText;

    [Tooltip("이번 티어를 깨면 받는 보상 미리보기.\n" +
             "예: \"공격력 +30%  |  발사체 +1\"\n" +
             "각성 보상 테이블(AwakeningManager.Table)에서 자동으로 읽어옵니다.")]
    [SerializeField] private TextMeshProUGUI rewardText;

    [Tooltip("진입 버튼 안의 글자. 연결하면 상태에 따라 문구가 바뀝니다.")]
    [SerializeField] private TextMeshProUGUI enterButtonLabel;

    [Header("문구")]
    [SerializeField] private string labelEnter      = "진입";
    [SerializeField] private string labelAllCleared = "모두 클리어";

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

    // ★ [수정] 예전에는 requiredLevel(int)로 비교했습니다.
    //   이름과 보상 문구까지 화면에 띄우게 되면서, "요구 레벨은 같은데 티어가 바뀐" 경우
    //   (밸런스 조정으로 두 티어의 요구 레벨이 겹치거나, 티어 에셋을 교체한 경우)
    //   갱신을 건너뛰어 옛 이름/보상이 남습니다.
    //   화면이 무엇으로 그려졌는지 비교하려면, 그릴 때 쓴 '대상 자체'를 기억하는 게 정확합니다.
    private EvolveStageData shownTier;

    private float nextRefreshTime;

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

        ValidateSetup();
        RefreshLockState();
    }

    /// <summary>
    /// 연결 누락을 시작할 때 한 번 점검합니다.
    ///
    /// ★ 왜 이걸 따로 두는가 (학습 포인트)
    ///   "패널은 열리는데 아무 일도 안 일어난다"는 증상은 원인이 여러 개입니다.
    ///   버튼 미연결 / 티어 배열 비어 있음 / TabWindow 에 탭 미등록 …
    ///   에러가 나지 않고 조용히 아무것도 안 하기 때문에 찾는 데 시간이 오래 걸려요.
    ///
    ///   유니티에서 인스펙터 연결 누락은 가장 흔한 사고 유형입니다.
    ///   "없으면 경고를 남긴다"를 습관으로 만들면 디버깅 시간이 크게 줄어듭니다.
    /// </summary>
    private void ValidateSetup()
    {
        if (enterButton == null)
            Debug.LogWarning("[진화 입장] Enter Button 이 연결되지 않았습니다. " +
                             "패널 안의 '진입' 버튼을 연결하세요. 지금은 입장할 방법이 없습니다.", this);

        if (stageTiers == null || stageTiers.Length == 0)
            Debug.LogWarning("[진화 입장] Stage Tiers 가 비어 있습니다. " +
                             "EvolveStageData 에셋 5개를 연결하세요.", this);

        if (SaveManager.Instance == null)
            Debug.LogWarning("[진화 입장] SaveManager 를 찾지 못했습니다. 클리어 기록을 읽을 수 없습니다.", this);

        if (rewardText != null && AwakeningManager.Instance == null)
            Debug.LogWarning("[진화 입장] 보상 미리보기를 연결했지만 AwakeningManager 가 없습니다. " +
                             "공격력 버프만 표시됩니다.", this);
    }

    void OnDestroy()
    {
        if (enterButton != null)
            enterButton.onClick.RemoveListener(TryEnter);
    }

    void OnEnable()
    {
        // 켜질 때는 마지막 표시 기록을 믿지 않고 무조건 다시 그립니다
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
    /// 입장 가능 여부를 계산하는 곳을 이 함수 하나로 모았습니다.
    /// (버튼 표시와 실제 입장이 서로 다른 계산을 하면 반드시 어긋납니다)
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

        // 화면에 보이는 상태와 같으면 아무것도 하지 않습니다 (주기 갱신 비용 최소화)
        if (!force && shownState == state && shownTier == next) return;
        shownState = state;
        shownTier  = next;

        if (enterButton != null)
            enterButton.interactable = state == EntryState.Unlocked;

        if (state == EntryState.AllCleared)
        {
            SetText(tierNameText,      "모든 각성 완료", show: true);
            SetText(requiredLevelText, "",               show: false);
            SetText(rewardText,        "",               show: false);
            SetText(lockText,          "모든 진화 스테이지 클리어", show: true);
            SetText(enterButtonLabel,  labelAllCleared,  show: true);
            return;
        }

        // ── 잠김 / 입장 가능 공통: 티어 정보를 채웁니다 ──
        SetText(tierNameText,      next.displayName,                show: true);
        SetText(requiredLevelText, $"입장 조건  Lv.{next.requiredLevel}", show: true);
        SetText(rewardText,        $"클리어 보상 :  {DescribeRewards(next)}",           show: true);
        SetText(enterButtonLabel,  labelEnter,                      show: true);

        if (state == EntryState.Locked)
            SetText(lockText, $"Lv.{next.requiredLevel} 이상 입장 가능", show: true);
        else
            SetText(lockText, "", show: false);
    }

    /// <summary>
    /// 이번 티어를 깨면 받는 보상을 한 줄로 만듭니다.
    ///
    /// 문구 조립은 AwakeningRewardTable.Tier.RewardSummary() 가 합니다.
    /// 여기서는 "어느 티어인지" 만 넘기고, 각성 시스템이 없거나 테이블에
    /// 그 티어가 없으면 공격력 버프만이라도 보여줍니다.
    ///
    /// ★ 테이블을 이 컴포넌트에 직접 연결하지 않고 AwakeningManager 에서 빌려오는 이유:
    ///   같은 에셋을 두 곳에 연결해 두면 나중에 교체할 때 한 곳만 바뀝니다.
    /// </summary>
    private string DescribeRewards(EvolveStageData tier)
    {
        AwakeningRewardTable table = AwakeningManager.Instance != null
            ? AwakeningManager.Instance.Table
            : null;

        AwakeningRewardTable.Tier entry = table != null ? table.FindByStage(tier) : null;

        if (entry != null) return entry.RewardSummary();

        // 각성 보상 테이블에 없는 티어 — 공격력 버프만 안내합니다.
        // (테이블 연결을 깜빡했을 때 화면이 텅 비는 것보다는 낫습니다)
        return tier.damageBuffPercent > 0f
            ? $"공격력 +{tier.damageBuffPercent * 100f:0.#}%"
            : "";
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

    /// <summary>
    /// 텍스트를 채우고 보이거나 숨깁니다.
    /// 연결되지 않은(null) 필드는 조용히 건너뜁니다 — 그래서 원하는 것만 연결하면 됩니다.
    /// </summary>
    private static void SetText(TextMeshProUGUI label, string msg, bool show)
    {
        if (label == null) return;

        // 빈 문자열이면 어차피 보여줄 게 없으니 숨깁니다.
        bool visible = show && !string.IsNullOrEmpty(msg);

        label.text = msg;
        label.gameObject.SetActive(visible);
    }

    private void TryEnter()
    {
        // ★ 버튼 표시와 '같은 함수' 로 판정합니다.
        //   여기서 막혔다면 화면이 잠시 늦었던 것이므로, 화면을 즉시 맞춘 뒤 돌아갑니다.
        EntryState state = Evaluate(out EvolveStageData target);
        if (state != EntryState.Unlocked)
        {
            Debug.Log($"[진화 입장] 입장 거부 — 상태 {state} / 내 레벨 Lv.{PlayerLevel}");
            RefreshLockState(force: true);
            return;
        }

        Debug.Log($"[진화 입장] 입장 — {target.displayName} (id: {target.id}, " +
                  $"요구 Lv.{target.requiredLevel} / 내 레벨 Lv.{PlayerLevel})");

        // ★ 씬을 넘기기 전에 창을 닫습니다.
        //   씬이 바뀌면 어차피 창도 파괴되지만, 로딩이 한 프레임이라도 걸리면
        //   그 사이 창이 떠 있는 채로 화면이 넘어갑니다.
        if (ownerWindow != null) ownerWindow.Close();

        // 복귀할 원래 스테이지 위치 저장 후 입장
        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        EvolveStageContext.Enter(target, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.GoToEvolveStage();
    }
}