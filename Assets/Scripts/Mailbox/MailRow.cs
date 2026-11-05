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

    [Tooltip("받은 우편의 줄을 이만큼 흐리게 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float claimedAlpha = 0.5f;

    // 이 줄은 Row 템플릿(MailboxCanvas > ... > Content > Row)의 인스펙터에서 고칩니다 — 복제본이 모두 따라감
    [Header("글자 (실행 중에 코드가 넣는 값)")]
    [SerializeField] private string claimLabel   = "받기";
    [SerializeField] private string claimedLabel = "받음";
    [Tooltip("게임이 모르는 보상이 든 우편의 보상 칸 글자")]
    [SerializeField] private string unknownRewardText = "업데이트 후 받을 수 있는 보상";

    /// <param name="canClaim">false 면 [받기] 를 막음 (다른 우편을 받는 중)</param>
    public void Set(MailItem mail, Action onClaim, bool canClaim)
    {
        if (titleText  != null) titleText.text  = mail.title;
        if (bodyText   != null) bodyText.text   = mail.body;
        if (rewardText != null)
            rewardText.text = mail.hasUnknownReward && !mail.claimed
                ? unknownRewardText
                : Mailbox.RewardText(mail.rewards);

        // 받은 우편은 줄 전체를 흐리게 — 안 받은 우편과 한눈에 구분
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = mail.claimed ? claimedAlpha : 1f;

        if (claimButton == null) return;

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(() => onClaim?.Invoke());
        claimButton.interactable = canClaim && !mail.claimed && !mail.hasUnknownReward;

        TMP_Text label = claimButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = mail.claimed ? claimedLabel : claimLabel;
    }
}
