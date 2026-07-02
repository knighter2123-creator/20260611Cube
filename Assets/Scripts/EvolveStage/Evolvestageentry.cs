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
/// </summary>
public class EvolveStageEntry : MonoBehaviour
{
    [Header("진화 스테이지 티어 (요구 레벨 낮은 순으로 연결)")]
    [SerializeField] private EvolveStageData[] stageTiers;   // 30,50,70,100,200

    [Header("UI")]
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI lockText;

    private int PlayerLevel =>
        LevelUpManager.Instance != null ? LevelUpManager.Instance.CurrentLevel : 1;

    void Start()
    {
        // 요구 레벨 낮은 순 정렬 (인스펙터 연결 순서가 뒤섞여도 안전)
        if (stageTiers != null)
            System.Array.Sort(stageTiers, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.requiredLevel.CompareTo(b.requiredLevel);
            });

        if (enterButton != null)
            enterButton.onClick.AddListener(TryEnter);

        RefreshLockState();
    }

    void OnEnable()
    {
        RefreshLockState();
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnLevelUp += OnPlayerLevelUp;
    }

    void OnDisable()
    {
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnLevelUp -= OnPlayerLevelUp;
    }

    private void OnPlayerLevelUp(int newLevel) => RefreshLockState();

    // ── 다음에 들어갈 티어 = 아직 클리어(보상 지급) 안 된 것 중 가장 낮은 것 ──
    private EvolveStageData GetNextTier()
    {
        if (stageTiers == null || SaveManager.Instance == null) return null;

        foreach (var tier in stageTiers)   // 낮은 순 정렬 전제
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

        // 복귀할 원래 스테이지 위치 저장 후 입장
        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        EvolveStageContext.Enter(target, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.GoToEvolveStage();
    }
}