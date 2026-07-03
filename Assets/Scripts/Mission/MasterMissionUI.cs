using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MasterMissionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI descriptionText;  // "일일 미션 클리어"
    [SerializeField] private TextMeshProUGUI progressText;     // "8 / 8"
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI rewardText;       // 젬 수량
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;

    private MissionType type;

    public void Bind(MissionType missionType)
    {
        type = missionType;
        var mgr = MissionManager.Instance;
        var mm = mgr != null ? mgr.GetMasterMission(type) : null;

        if (mm != null)
        {
            descriptionText.text = mm.description;
            rewardText.text = mm.gemReward.ToString();
        }

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(OnClickClaim);
        Refresh();
    }

    public void Refresh()
    {
        var mgr = MissionManager.Instance;
        if (mgr == null) return;

        int done = mgr.CountCompleted(type);
        int total = mgr.CountTotal(type);
        progressText.text = $"{done} / {total}";
        progressFill.fillAmount = total > 0 ? (float)done / total : 0f;

        switch (mgr.GetMasterState(type))
        {
            case MissionManager.MissionState.InProgress:
                claimButton.interactable = false;
                claimButtonText.text = "진행 중";
                break;
            case MissionManager.MissionState.Claimable:
                claimButton.interactable = true;
                claimButtonText.text = "보상수령";
                break;
            case MissionManager.MissionState.Claimed:
                claimButton.interactable = false;
                claimButtonText.text = "완료";
                break;
        }
    }

    private void OnClickClaim()
    {
        if (MissionManager.Instance != null && MissionManager.Instance.TryClaimMaster(type))
            Refresh();
    }
}