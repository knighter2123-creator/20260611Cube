using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IdleRewardButton : MonoBehaviour
{
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text previewText;   // "골드 1,200 / exp 600 (2시간 누적)"

    void Start()
    {
        claimButton.onClick.AddListener(OnClickClaim);
        RefreshPreview();
    }

    void OnEnable() => RefreshPreview();

    private void RefreshPreview()
    {
        var (gold, exp, sec) = IdleRewardManager.Instance.Preview();
        int h = (int)(sec / 3600);
        int m = (int)((sec % 3600) / 60);
        previewText.text = $"골드 {gold:N0} / exp {exp:N0}  ({h}시간 {m}분 누적)";
        claimButton.interactable = (gold > 0 || exp > 0);
    }

    private void OnClickClaim()
    {
        var (gold, exp) = IdleRewardManager.Instance.Claim();
        Debug.Log($"[IdleButton] 수령 — 골드 +{gold}, exp +{exp}");
        RefreshPreview();   // 수령 후 0으로 갱신
    }
}