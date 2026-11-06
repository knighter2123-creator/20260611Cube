using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>버튼 하나로 '아직 안 깬 가장 낮은 티어'에 입장 — 클리어 판정은 보상 지급 플래그 재사용(클리어=처치=보상), 텍스트 필드는 비우면 그 항목만 건너뜀.</summary>
// ⚠ 씬을 넘기는 탭: 입장 직전 창을 닫고(ownerWindow), TabWindow 에서 Dont Remember As Last Tab 체크
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

    // 마지막으로 반영한 상태 — 같으면 안 그림 (TMP 는 같은 문자열에도 메시를 재생성할 수 있어서)
    private EntryState? shownState;

    // 비교 기준은 티어 에셋 자체 (요구 레벨로 비교하면 레벨이 같은 다른 티어일 때 갱신을 건너뜀)
    private EvolveStageData shownTier;

    private float nextRefreshTime;

    // 구독한 인스턴스에서 해제 (Instance 로 해제하면 매니저가 바뀐 경우 옛 구독이 남음)
    private LevelUpManager boundLm;

    private int PlayerLevel =>
        LevelUpManager.Instance != null ? LevelUpManager.Instance.CurrentLevel : 1;

    void Awake()
    {
        // 정렬은 Awake 에서 — OnEnable 의 RefreshLockState 가 Start 보다 먼저 돌기 때문
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

    /// <summary>시작 시 인스펙터 연결 누락을 한 번 점검해 경고 (조용히 아무 일도 안 하는 증상의 원인 추적용).</summary>
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

    // ── ITabPage — TabWindow 가 부른다 ──

    /// <summary>탭 선택 시 잠금 상태 재계산 (클리어 플래그엔 이벤트가 없어 갱신 계기가 이것뿐).</summary>
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

    /// <summary>입장 가능 여부 판정을 한 곳에 (버튼 표시와 실제 입장이 어긋나지 않게).</summary>
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

    /// <summary>이번 티어 보상 한 줄 — 문구는 AwakeningManager 의 테이블에서 빌려 조립(두 곳 연결 방지), 없으면 공격력 버프만 표시.</summary>
    private string DescribeRewards(EvolveStageData tier)
    {
        AwakeningRewardTable table = AwakeningManager.Instance != null
            ? AwakeningManager.Instance.Table
            : null;

        AwakeningRewardTable.Tier entry = table != null ? table.FindByStage(tier) : null;

        if (entry != null) return entry.RewardSummary();

        // 테이블에 없는 티어 — 공격력 버프만 안내 (빈 화면보다 나음)
        return tier.damageBuffPercent > 0f
            ? $"공격력 +{tier.damageBuffPercent * 100f:0.#}%"
            : "";
    }

    /// <summary>탭이 보이는 동안만 도는 주기 갱신 (가려지면 오브젝트가 꺼져 자동 정지).</summary>
    void Update()
    {
        // unscaledTime — 설정창의 일시정지(timeScale 0)나 배속과 무관하게 같은 간격으로 확인
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);

        Bind();              // 매니저가 재생성됐다면 새 인스턴스로 갈아탐
        RefreshLockState();
    }

    /// <summary>텍스트를 채우고 표시/숨김 (null 필드는 건너뜀).</summary>
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
        // 버튼 표시와 같은 함수로 판정 — 막히면 화면이 늦었던 것이라 즉시 갱신 후 복귀
        EntryState state = Evaluate(out EvolveStageData target);
        if (state != EntryState.Unlocked)
        {
            GameLog.Info($"[진화 입장] 입장 거부 — 상태 {state} / 내 레벨 Lv.{PlayerLevel}");
            RefreshLockState(force: true);
            return;
        }

        GameLog.Info($"[진화 입장] 입장 — {target.displayName} (id: {target.id}, " +
                  $"요구 Lv.{target.requiredLevel} / 내 레벨 Lv.{PlayerLevel})");

        // 씬 전환 전 창을 닫음 (로딩 동안 창이 떠 있는 채로 넘어가지 않게)
        if (ownerWindow != null) ownerWindow.Close();

        // 복귀할 원래 스테이지 위치 저장 후 입장
        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        EvolveStageContext.Enter(target, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.GoToEvolveStage();
    }
}