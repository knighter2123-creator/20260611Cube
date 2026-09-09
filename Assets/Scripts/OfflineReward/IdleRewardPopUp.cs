using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 오프라인 보상 팝업. 경과 시간 + 골드/exp 미리보기를 표시하고,
/// 수령 / 2배 수령 / 닫기를 처리한다.
///
/// ★ 이번 수정
///   ① 자동 열기 / 수동 열기 분리
///      - 앱 시작 시 자동 호출: 받을 게 없으면 조용히 닫는다 (기존 동작)
///      - 버튼으로 수동 호출: 받을 게 없어도 팝업은 열되, 수령 버튼을 비활성화한다
///   ② 상태 판정을 IdleRewardStatus로 일원화 (메인 화면 안내 텍스트와 항상 일치)
///   ③ offlineText는 메인 화면 상시 표시로 옮겼으므로 팝업에서는 선택 사항
///
/// ⚠️ 파일 이름 확인
///   클래스 이름은 IdleRewardPopup 인데 파일 이름이 IdleRewardPopUp.cs 였습니다(대문자 U).
///   유니티는 MonoBehaviour의 파일 이름과 클래스 이름이 정확히 같아야 컴포넌트를
///   인식합니다. 지금 정상 동작 중이라면 실제 파일명은 이미 맞을 가능성이 크지만,
///   한 번 확인해 보세요. 이 파일은 클래스 이름에 맞춰 IdleRewardPopup.cs로 저장했습니다.
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
    [SerializeField] private TMP_Text claim2xCostText;      // 버튼의 비용 표시 "젬 100" (선택)

    [Header("문구")]
    [SerializeField] private string claimableMessage = "오프라인 보상 수령 가능";
    [SerializeField] private string fullMessage      = "오프라인 보상 최대";
    [SerializeField] private string emptyElapsedMessage = "아직 모인 오프라인 보상이 없어요";

    void Awake()
    {
        claimButton.onClick.AddListener(OnClaim);
        claim2xButton.onClick.AddListener(OnClaim2x);
        closeButton.onClick.AddListener(Close);
        if (root != null) root.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  열기
    // ──────────────────────────────────────────────

    /// <summary>
    /// 사용자가 버튼을 눌러 여는 경우. 받을 게 없어도 팝업은 열립니다.
    ///
    /// 버튼 OnClick에는 이 함수를 연결하세요.
    /// </summary>
    public void Open() => Open(isAutoOpen: false);

    /// <summary>
    /// 앱 시작 시 자동으로 여는 경우. 받을 게 없으면 조용히 닫습니다.
    ///
    /// ★ 앱 시작 시 팝업을 띄우던 코드를 이 함수로 바꿔주세요.
    ///   (기존 호출부가 Open()이면, 이제 매번 빈 팝업이 뜨게 됩니다)
    /// </summary>
    public void OpenOnAppStart() => Open(isAutoOpen: true);

    /// <summary>
    /// ─── bool 매개변수 대신 이름 있는 함수 두 개를 둔 이유 (학습 포인트) ────
    /// 호출부에서 Open(true) 라고 적혀 있으면, 나중에 읽을 때
    /// "true가 뭐였지?" 하고 이 파일을 다시 열어봐야 합니다.
    /// OpenOnAppStart() 라고 적혀 있으면 그럴 필요가 없죠.
    ///
    /// 실제 로직은 private 함수 하나에 모아두고, 겉으로 드러나는 입구만
    /// 의미 있는 이름으로 두 개 만드는 방식입니다. 코드 중복 없이 읽기만 좋아집니다.
    /// ────────────────────────────────────────────────────────────────
    /// </summary>
    private void Open(bool isAutoOpen)
    {
        if (IdleRewardManager.Instance == null)
        {
            Debug.LogWarning("[IdlePopup] IdleRewardManager가 없습니다.");
            return;
        }

        var (gold, exp, sec) = IdleRewardManager.Instance.Preview();
        IdleRewardState state = IdleRewardStatus.Evaluate(gold, exp, sec);
        bool canClaim = IdleRewardStatus.CanClaim(state);

        Debug.Log($"[IdlePopup] Open({(isAutoOpen ? "자동" : "수동")}) | " +
                  $"경과 {sec:F0}초, 골드 {gold}, exp {exp}, 상태 {state}");

        // 자동 열기인데 받을 게 없으면 조용히 닫는다 (앱 켤 때마다 빈 팝업이 뜨면 성가심)
        if (isAutoOpen && !canClaim)
        {
            Close();
            return;
        }

        // ── 표시 채우기 ──
        elapsedText.text = canClaim ? FormatElapsed(sec) : emptyElapsedMessage;
        goldText.text    = $"+{gold:N0}";
        expText.text     = $"+{exp:N0}";

       
        RefreshClaimButtons(canClaim);

        if (root != null) root.SetActive(true);
    }

    

    // ──────────────────────────────────────────────
    //  수령
    // ──────────────────────────────────────────────

    private void OnClaim()
    {
        // 버튼이 비활성이면 눌릴 수 없지만, 다른 코드가 직접 호출할 수도 있으므로
        // 실제 지급 직전에 한 번 더 확인합니다.
        // (UI 상태만 믿지 않는다 — 서버가 없는 로컬 게임에서도 지켜두면 좋은 습관입니다)
        if (!IdleRewardStatus.CanClaim(IdleRewardStatus.EvaluateNow())) return;

        IdleRewardManager.Instance.Claim(1f);
        Close();
    }

    private void OnClaim2x()
    {
        if (!IdleRewardStatus.CanClaim(IdleRewardStatus.EvaluateNow())) return;

        // 젬 차감 성공 시에만 2배 지급
        if (!CurrencyManager.Instance.SpendGem(claim2xGemCost))
        {
            Debug.Log("[IdlePopup] 보석이 부족해 2배 수령 불가");
            RefreshClaimButtons(true);   // 버튼 비활성으로 갱신
            return;                      // 팝업은 닫지 않음 (다시 시도 or 일반 수령 가능)
        }

        IdleRewardManager.Instance.Claim(2f);
        Close();
    }

    /// <summary>
    /// 수령 버튼들의 활성 상태를 갱신합니다.
    ///
    /// 두 버튼의 조건이 다릅니다:
    ///   일반 수령 : 받을 보상이 있는가
    ///   2배 수령  : 받을 보상이 있는가 AND 젬이 충분한가
    /// </summary>
    private void RefreshClaimButtons(bool canClaim)
    {
        if (claim2xCostText != null)
            claim2xCostText.text = $"젬 {claim2xGemCost:N0}";

        if (claimButton != null)
            claimButton.interactable = canClaim;

        bool canAfford = (CurrencyManager.Instance?.Gem ?? 0) >= claim2xGemCost;
        if (claim2xButton != null)
            claim2xButton.interactable = canClaim && canAfford;
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