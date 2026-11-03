using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>우편함 목록 한 줄 — 제목 / 내용 / 보상 + [받기]. Tools/UI 굽기/우편함 이 칸을 채웁니다.</summary>
public class MailRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button          claimButton;

    /// <param name="canClaim">false 면 [받기] 를 막음 (다른 우편을 받는 중, 게임이 모르는 보상)</param>
    public void Set(MailItem mail, Action onClaim, bool canClaim)
    {
        if (titleText  != null) titleText.text  = mail.title;
        if (bodyText   != null) bodyText.text   = mail.body;
        if (rewardText != null)
            rewardText.text = mail.hasUnknownReward
                ? "업데이트 후 받을 수 있는 보상"
                : Mailbox.RewardText(mail.rewards);

        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(() => onClaim?.Invoke());
            claimButton.interactable = canClaim && !mail.hasUnknownReward;
        }
    }
}
