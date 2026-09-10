using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 오프라인 보상 팝업. 경과 시간 + 골드/exp 미리보기를 표시하고,
/// 수령 / 2배 수령 / 닫기를 처리한다.
///
/// ★ 이번 수정 (CS0414 경고 해결)
///   ① statusText 를 추가하고 claimableMessage / fullMessage 를 실제로 사용
///      → "값을 넣기만 하고 읽는 곳이 없다"는 경고가 사라지고, 기능도 완성됩니다
///   ② 누적 상한에 도달했는지 판정 → 상한이면 fullMessage 표시
///   ③ Awake 의 버튼 등록에 null 검사 추가 (아래 설명 참고)
///
/// ⚠️ 파일 이름 확인 (여전히 남아 있는 문제)
///   클래스 이름은 IdleRewardPopup 인데 파일 이름이 IdleRewardPopUp.cs 입니다(대문자 U).
///   윈도우는 파일 이름 대소문자를 구분하지 않아서 지금은 동작하지만,
///   맥/리눅스 빌드 머신이나 일부 유니티 버전에서는 컴포넌트를 못 찾습니다.
///   고치실 때는 **반드시 유니티 Project 창에서 이름을 바꾸세요.**
///   탐색기에서 바꾸면 .meta 파일이 따라오지 않아 씬의 연결이 끊어집니다.
/// </summary>
public class IdleRewardPopup : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject root;          // 팝업 전체 (켜고 끔)

    [Header("표시")]
    [SerializeField] private TMP_Text elapsedText;     // "2시간 37분 동안 자리를 비웠어요"
    [SerializeField] private TMP_Text goldText;        // "+12,400"
    [SerializeField] private TMP_Text expText;         // "+6,200"

    [Tooltip("상태 문구를 띄울 텍스트. 비워두면 문구를 표시하지 않습니다")]
    [SerializeField] private TMP_Text statusText;      // "오프라인 보상 최대" ★ 신규

    [Header("버튼")]
    [SerializeField] private Button claimButton;       // 보상 수령
    [SerializeField] private Button claim2xButton;     // 보상 2배 수령
    [SerializeField] private Button closeButton;       // 닫기 (좌상단)

    [Header("2배 수령 (젬 소비)")]
    [SerializeField] private int    claim2xGemCost = 100;   // 2배 수령에 드는 젬
    [SerializeField] private TMP_Text claim2xCostText;      // 버튼의 비용 표시 "젬 100" (선택)

    [Header("문구")]
    [SerializeField] private string claimableMessage    = "오프라인 보상 수령 가능";
    [SerializeField] private string fullMessage         = "오프라인 보상 최대";
    [SerializeField] private string emptyElapsedMessage = "아직 모인 오프라인 보상이 없어요";

    void Awake()
    {
        // ★ null 검사를 넣은 이유
        //   아래 RefreshClaimButtons() 는 claimButton 이 null 일 수 있다고 보고 검사하는데,
        //   여기서는 무방비로 .onClick 을 불렀습니다. 같은 파일 안에서 같은 필드에 대한
        //   가정이 두 가지면 반드시 한쪽이 틀립니다.
        //   실제로 인스펙터 연결을 하나 빠뜨리면 Awake 에서 NullReferenceException 이 터지고,
        //   그 순간 이 컴포넌트의 나머지 초기화가 통째로 중단됩니다.
        if (claimButton   != null) claimButton.onClick.AddListener(OnClaim);
        if (claim2xButton != null) claim2xButton.onClick.AddListener(OnClaim2x);
        if (closeButton   != null) closeButton.onClick.AddListener(Close);

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
        SetText(elapsedText, canClaim ? FormatElapsed(sec) : emptyElapsedMessage);
        SetText(goldText, $"+{gold:N0}");
        SetText(expText,  $"+{exp:N0}");

        RefreshStatusText(canClaim, sec);   // ★ 신규
        RefreshClaimButtons(canClaim);

        if (root != null) root.SetActive(true);
    }

    // ──────────────────────────────────────────────
    //  ★ 상태 문구 — claimableMessage / fullMessage 를 쓰는 곳
    // ──────────────────────────────────────────────

    /// <summary>
    /// 상단 상태 문구를 갱신합니다.
    ///
    /// ─── 왜 이 문구가 필요한가 (학습 포인트) ─────────────────────────
    /// IdleRewardManager.GetElapsedSeconds() 는 경과 시간을 최대 누적 시간으로
    /// **잘라서** 돌려줍니다.
    ///
    ///     return Math.Min(seconds, MaxAccrualSeconds);
    ///
    /// 그래서 3일을 비워도 팝업에는 "24시간 동안 자리를 비웠어요" 라고 뜹니다.
    /// 값 자체는 맞습니다 — 실제로 정산되는 시간이 24시간이니까요.
    /// 하지만 플레이어 입장에서는 "내가 3일을 비웠는데 왜 24시간이지?" 가 됩니다.
    ///
    /// "최대" 라고 한 줄 알려주면 오해가 사라지고, 동시에
    /// **더 자주 접속할 이유**가 생깁니다. 방치형에서 상한은 숨기는 게 아니라
    /// 보여줘야 하는 정보예요.
    ///
    /// ─── 판정을 왜 매니저에게 물어보는가 ────────────────────────────
    /// 여기에 24를 직접 적어두면, 인스펙터에서 maxAccrualHours 를 12로 바꿨을 때
    /// UI 는 여전히 24를 기준으로 판단합니다. 에러도 로그도 안 나는 종류의 버그죠.
    /// IdleRewardManager 가 MaxAccrualSeconds 프로퍼티를 열어둔 게 정확히 이 용도입니다.
    /// ──────────────────────────────────────────────────────────────
    /// </summary>
    private void RefreshStatusText(bool canClaim, double seconds)
    {
        if (statusText == null) return;   // 안 쓰기로 했으면 비워두면 됩니다

        if (!canClaim)
        {
            // 받을 게 없을 때는 elapsedText 가 이미 안내 문구를 띄우고 있으므로
            // 같은 말을 두 번 하지 않습니다.
            statusText.text = string.Empty;
            return;
        }

        double max = IdleRewardManager.Instance.MaxAccrualSeconds;

        // double 비교라 == 대신 여유를 둡니다. 시각 계산에서 소수점 오차가 남을 수 있어요.
        bool isFull = seconds >= max - 1.0;

        statusText.text = isFull ? fullMessage : claimableMessage;
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

            // 보상은 여전히 받을 수 있으므로 canClaim 은 true 그대로 넘깁니다.
            // 젬이 모자란 건 함수 안에서 따로 검사해 2배 버튼만 비활성화됩니다.
            RefreshClaimButtons(true);
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
        SetText(claim2xCostText, $"젬 {claim2xGemCost:N0}");

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

    /// <summary>
    /// 인스펙터 칸을 비워둘 수 있게 하는 작은 도우미.
    /// 같은 null 검사를 여러 곳에 반복해 적는 대신 한 곳에 모읍니다.
    /// </summary>
    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }
}