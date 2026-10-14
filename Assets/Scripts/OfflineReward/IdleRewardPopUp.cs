using System;
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

    [Tooltip("상태 문구를 띄울 텍스트. 비워두면 문구를 표시하지 않습니다")]
    [SerializeField] private TMP_Text statusText;      // "오프라인 보상 최대"

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
        // 인스펙터 연결을 빠뜨려도 Awake가 중간에 죽지 않게 null 검사
        if (claimButton   != null) claimButton.onClick.AddListener(OnClaim);
        if (claim2xButton != null) claim2xButton.onClick.AddListener(OnClaim2x);
        if (closeButton   != null) closeButton.onClick.AddListener(Close);

        if (root != null) root.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  열기
    // ──────────────────────────────────────────────

    /// <summary>사용자가 버튼을 눌러 여는 경우. 받을 게 없어도 팝업은 열린다.</summary>
    public void Open() => Open(isAutoOpen: false);

    /// <summary>앱 시작 시 자동으로 여는 경우. 받을 게 없으면 열지 않는다.</summary>
    public void OpenOnAppStart() => Open(isAutoOpen: true);

    private void Open(bool isAutoOpen)
    {
        var manager = IdleRewardManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[IdlePopup] IdleRewardManager가 없습니다.");
            return;
        }

        // 이전: Preview() 튜플을 받아 여기서 다시 상태를 판정했다. 이제 판정된 결과를 받기만 한다.
        IdleRewardPreview preview = manager.Preview();

        GameLog.Info($"[IdlePopup] Open({(isAutoOpen ? "자동" : "수동")}) | " +
                  $"경과 {preview.Seconds:F0}초, 골드 {preview.Gold:F0}, exp {preview.Exp:F0}, 상태 {preview.State}");

        // 자동 열기인데 받을 게 없으면 열지 않는다 (앱 켤 때마다 빈 팝업이 뜨면 성가심)
        if (isAutoOpen && !preview.CanClaim)
        {
            Close();
            return;
        }

        // 이전: ① 받을 게 없을 때도 쌓인 금액(+37 등)을 보여줘 "아직 모인 보상이 없어요"와 어긋났고,
        //       ② 자르기 전 double 값을 보여줘 고스테이지에서 실제 지급액(int 상한)보다 큰 숫자가 떴다.
        int gold = preview.CanClaim ? IdleRewardManager.ToIntClamped(preview.Gold) : 0;
        int exp  = preview.CanClaim ? IdleRewardManager.ToIntClamped(preview.Exp)  : 0;

        SetText(elapsedText, preview.CanClaim ? FormatElapsed(preview.Seconds) : emptyElapsedMessage);
        SetText(goldText, $"+{gold:N0}");
        SetText(expText,  $"+{exp:N0}");

        RefreshStatusText(preview.State);
        RefreshClaimButtons(preview.CanClaim);

        if (root != null) root.SetActive(true);
    }

    private void RefreshStatusText(IdleRewardState state)
    {
        if (statusText == null) return;   // 안 쓰기로 했으면 비워두면 된다

        switch (state)
        {
            case IdleRewardState.NotEnough:
                statusText.text = string.Empty;   // elapsedText가 이미 안내 중이라 비운다
                break;
            case IdleRewardState.Claimable:
                statusText.text = claimableMessage;
                break;
            case IdleRewardState.Full:
                statusText.text = fullMessage;
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  수령
    // ──────────────────────────────────────────────

    private void OnClaim()
    {
        // 이전: IdleRewardStatus.EvaluateNow()로 검사 — 그 함수는 정리하며 없앴다.
        //       Claim() 안에서도 같은 검사를 하지만, 받을 게 없으면 팝업을 닫지 않기 위해 여기서도 본다.
        var manager = IdleRewardManager.Instance;
        if (manager == null || !manager.Preview().CanClaim) return;

        manager.Claim(1f);
        Close();
    }

    private void OnClaim2x()
    {
        // 젬을 쓰기 전에 받을 게 있는지 먼저 확인 (없는데 젬만 빠지면 안 됨)
        var manager = IdleRewardManager.Instance;
        if (manager == null || !manager.Preview().CanClaim) return;

        // 이전: CurrencyManager.Instance.SpendGem(...) — 같은 파일의 RefreshClaimButtons는 ?.로 null을
        //       대비하는데 여기만 무방비라, 매니저가 없으면 NullReferenceException이 났다.
        var currency = CurrencyManager.Instance;
        if (currency == null || !currency.SpendGem(claim2xGemCost))
        {
            GameLog.Info("[IdlePopup] 보석이 부족해 2배 수령 불가");
            RefreshClaimButtons(true);   // 일반 수령은 여전히 가능, 2배 버튼만 비활성화
            return;                      // 팝업은 닫지 않음
        }

        manager.Claim(2f);
        Close();
    }

    /// <summary>
    /// 수령 버튼 활성 상태 갱신.
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
    // 이전: double → float → int 로 두 번 변환해 직접 나눴다. TimeSpan이 시/분 계산을 대신한다.
    private static string FormatElapsed(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        int h = (int)t.TotalHours;
        int m = t.Minutes;

        if (h > 0) return $"{h}시간 {m}분 동안 자리를 비웠어요";
        return $"{m}분 동안 자리를 비웠어요";
    }

    // 인스펙터 칸을 비워둘 수 있게 하는 도우미 (null 검사를 한 곳에 모음)
    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }
}
