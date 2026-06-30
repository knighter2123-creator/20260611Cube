using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 오프라인 보상 팝업. 경과 시간 + 골드/exp 미리보기를 표시하고,
/// 수령 / 2배 수령 / 닫기를 처리한다.
/// </summary>
public class IdleRewardPopup : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject root;          // 팝업 전체 (켜고 끔)

    [Header("표시")]
    [SerializeField] private TMP_Text elapsedText;     // "2시간 37분 동안 자리를 비웠어요"
    [SerializeField] private TMP_Text goldText;        // "+12,400"
    [SerializeField] private TMP_Text expText;         // "+6,200"

    [Header("버튼")]
    [SerializeField] private Button claimButton;       // 보상 수령
    [SerializeField] private Button claim2xButton;     // 보상 2배 수령
    [SerializeField] private Button closeButton;       // 닫기 (좌상단)

    void Awake()
    {
        claimButton.onClick.AddListener(OnClaim);
        claim2xButton.onClick.AddListener(OnClaim2x);
        closeButton.onClick.AddListener(Close);
        if (root != null) root.SetActive(false);
    }

    /// <summary>경과 시간이 있으면 팝업을 열고 미리보기를 채운다.</summary>
    public void Open()
    {
        var (gold, exp, sec) = IdleRewardManager.Instance.Preview();

        // 쌓인 게 없으면 열지 않음
        if (sec < 60 || (gold <= 0 && exp <= 0))
        {
            Close();
            return;
        }

        elapsedText.text = FormatElapsed(sec);
        goldText.text    = $"+{gold:N0}";
        expText.text     = $"+{exp:N0}";

        if (root != null) root.SetActive(true);
    }

    private void OnClaim()
    {
        IdleRewardManager.Instance.Claim(1f);
        Close();
    }

    private void OnClaim2x()
    {
        // TODO: 광고 시청/젬 소비 등 조건을 여기서 먼저 처리하고,
        //       성공했을 때만 아래를 호출하도록 연결하세요.
        IdleRewardManager.Instance.Claim(2f);
        Close();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    // 초 → "N시간 M분" / "M분" 문자열
    private string FormatElapsed(double seconds)
    {
        int total = Mathf.FloorToInt((float)seconds);
        int h = total / 3600;
        int m = (total % 3600) / 60;

        if (h > 0) return $"{h}시간 {m}분 동안 자리를 비웠어요";
        return $"{m}분 동안 자리를 비웠어요";
    }
}