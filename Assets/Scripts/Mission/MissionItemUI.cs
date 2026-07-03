using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionItemUI : MonoBehaviour
{
    [Header("완료 하이라이트")]
    [SerializeField] private Image highlightBackground;
    [SerializeField] private Color normalColor    = Color.white;
    [SerializeField] private Color claimableColor = new Color(1f, 0.9f, 0.5f); // 노란빛
    
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI progressText;   // "120 / 300"
    [SerializeField] private Image progressFill;             // Image Type: Filled
    [SerializeField] private TextMeshProUGUI rewardText;     // 젬 수량
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;

    private string missionId;

    public void Bind(MissionData data)
    {
        missionId = data.id;
        descriptionText.text = data.description;
        rewardText.text = data.gemReward.ToString();

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(OnClickClaim);

        Refresh();
    }

    public void Refresh()
    {
        var mgr = MissionManager.Instance;
        if (mgr == null) return;

        var data = mgr.GetMissionData(missionId);
        var progress = mgr.GetProgress(missionId);
        if (data == null || progress == null) return;

        int cur = Mathf.Min(progress.currentCount, data.requiredCount);
        progressFill.fillAmount = data.requiredCount > 0 ? (float)cur / data.requiredCount : 0f;

        var state = mgr.GetState(missionId);
        switch (state)
        {
            case MissionManager.MissionState.InProgress:
                progressText.text = $"{cur} / {data.requiredCount}";
                claimButton.interactable = false;
                claimButtonText.text = "진행 중";
                break;
            case MissionManager.MissionState.Claimable:
                progressText.text = "임무 완료";   // 이미지처럼 완료 시 문구 전환
                claimButton.interactable = true;
                claimButtonText.text = "수령";
                break;
            case MissionManager.MissionState.Claimed:
                progressText.text = "임무 완료";
                claimButton.interactable = false;
                claimButtonText.text = "완료";
                break;
        }

        // 완료(수령 가능) 상태만 하이라이트
        if (highlightBackground != null)
            highlightBackground.color = (state == MissionManager.MissionState.Claimable)
                ? claimableColor : normalColor;
    }

    private void OnClickClaim()
    {
        if (MissionManager.Instance != null && MissionManager.Instance.TryClaim(missionId))
            Refresh(); // OnMissionUpdated 로도 갱신되지만 즉시 반영
    }
}