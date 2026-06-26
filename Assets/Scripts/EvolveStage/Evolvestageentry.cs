using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StageScene에 배치하는 진화 스테이지 입장 버튼.
///   - 플레이어 레벨이 requiredLevel 이상일 때만 입장 가능
///   - 입장 직전 현재 스테이지 위치를 기억 → 클리어 후 그 자리로 복귀
/// 티어(30/50/70/100/200)마다 이 컴포넌트 + 버튼을 하나씩 두고
/// 각자 다른 EvolveStageData를 연결하면 됩니다.
/// </summary>
public class EvolveStageEntry : MonoBehaviour
{
    [Header("이 버튼이 입장할 진화 스테이지 티어")]
    [SerializeField] private EvolveStageData stageData;

    [Header("UI")]
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI lockText; // (선택) 잠금/안내 문구

    private bool _unlocked;
    void Start()
    {
        if (enterButton != null)
            enterButton.onClick.AddListener(TryEnter);

        RefreshLockState();
    }

    void Update()
    {
        // 아직 잠겨 있을 때만 계속 확인 (해제되면 더 볼 필요 없음 — 레벨은 안 내려가니까)
        if (!_unlocked) RefreshLockState();
    }

    void OnEnable()
    {
        // 레벨업으로 잠금이 풀렸을 수 있으니 활성화 때마다 갱신
        RefreshLockState();
    }

    private int PlayerLevel =>
        LevelUpManager.Instance != null ? LevelUpManager.Instance.CurrentLevel : 1;

    private void RefreshLockState()
    {
        if (stageData == null) return;
        
        bool unlocked = PlayerLevel >= stageData.requiredLevel;
        _unlocked = unlocked;
        
        if (enterButton != null)
            enterButton.interactable = unlocked;

        if (lockText != null)
            lockText.text = unlocked ? "" : $"Lv.{stageData.requiredLevel} 이상 입장 가능";
    }

    private void TryEnter()
    {
        if (stageData == null) return;

        if (PlayerLevel < stageData.requiredLevel)
        {
            Debug.Log($"[EvolveStageEntry] 레벨 부족: 현재 {PlayerLevel} / 필요 {stageData.requiredLevel}");
            RefreshLockState();
            return;
        }

        // 복귀할 원래 스테이지 위치 저장
        int world = StageManager.Instance != null ? StageManager.Instance.CurrentWorld : 1;
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;

        EvolveStageContext.Enter(stageData, world, stage);
        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.GoToEvolveStage();
    }
}