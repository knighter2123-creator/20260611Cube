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
    
    [Header("2배 수령 (젬 소비)")]
    [SerializeField] private int    claim2xGemCost = 100;   // 2배 수령에 드는 젬
    [SerializeField] private TMP_Text claim2xCostText;       // 버튼의 비용 표시 "젬 100" (선택)

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
        Debug.Log($"[IdlePopup] Open 호출됨 | 경과 {sec:F0}초, 골드 {gold}, exp {exp}");
        
        if (sec < 60 || (gold <= 0 && exp <= 0))
        {
            Close();
            return;
        }

        elapsedText.text = FormatElapsed(sec);
        goldText.text    = $"+{gold:N0}";
        expText.text     = $"+{exp:N0}";

        RefreshClaim2xButton();          // ★ 추가

        if (root != null) root.SetActive(true);
    }

    private void OnClaim()
    {
        IdleRewardManager.Instance.Claim(1f);
        Close();
    }

    private void OnClaim2x()
    {
        var (gold, exp, _) = IdleRewardManager.Instance.Preview();
        if (gold <= 0 && exp <= 0) return;   // 줄 게 없으면 젬 차감도 안 함
        // 젬 차감 성공 시에만 2배 지급
        if (!CurrencyManager.Instance.SpendGem(claim2xGemCost))
        {
            Debug.Log("[IdlePopup] 보석이 부족해 2배 수령 불가");
            RefreshClaim2xButton();   // 버튼 비활성으로 갱신
            return;                    // 팝업은 닫지 않음 (다시 시도 or 일반 수령 가능)
        }

        IdleRewardManager.Instance.Claim(2f);
        Close();
    }
    
    // 젬 보유량에 따라 2배 버튼 활성/비활성 + 비용 표시
    private void RefreshClaim2xButton()
    {
        if (claim2xCostText != null)
            claim2xCostText.text = $"젬 {claim2xGemCost:N0}";

        bool canAfford = (CurrencyManager.Instance?.Gem ?? 0) >= claim2xGemCost;
        if (claim2xButton != null)
            claim2xButton.interactable = canAfford;   // 젬 부족하면 버튼 비활성
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