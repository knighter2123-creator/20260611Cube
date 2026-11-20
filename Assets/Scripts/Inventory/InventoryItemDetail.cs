using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인벤토리 아이템 상세창 — 조각이면 성급 표시와 [성급 올리기]/[진화].</summary>
public class InventoryItemDetail : MonoBehaviour
{
    [Header("창")]
    [Tooltip("비우면 이 오브젝트 자신")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button     closeButton;
    [SerializeField] private Button     dimmedButton;

    [Header("기본 정보")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    gradeFrame;
    [SerializeField] private Color    defaultFrameColor = Color.white;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("동료 조각일 때만 켜짐")]
    [SerializeField] private GameObject fragmentSection;
    [SerializeField] private TMP_Text   companionText;
    [SerializeField] private TMP_Text   progressText;
    [SerializeField] private Button     starUpButton;
    [SerializeField] private Button     evolveButton;
    [SerializeField] private TMP_Text   evolveButtonLabel;
    [SerializeField] private TMP_Text   noticeText;
    [Tooltip("'상승 확률 nn%' 글자 (Tools/UI 굽기/인벤토리 상세 - 성급 확률 글자 가 만듦). 비우면 조각 수 줄 뒤에 붙임")]
    [SerializeField] private TMP_Text   starUpRateText;
    [Tooltip("진화 대상을 찾을 풀. 비우면 GachaSystem 의 풀")]
    [SerializeField] private CompanionPoolAsset pool;

    [Header("문장 — {count} {name} {stars} {star} {grade}")]
    [SerializeField] private string countFormat      = "보유 {count}개";
    [SerializeField] private string companionFormat  = "{grade} {name}  {stars} {star}성";
    [SerializeField] private string evolveFormat     = "진화 → {grade} {name}";
    [SerializeField] private string notOwnedNotice   = "동료를 보유해야 조각을 쓸 수 있습니다.";
    [SerializeField] private string maxStarNotice    = "최대 성급입니다. 더 이상 성장할 수 없습니다.";
    [SerializeField] private string starUpDone       = "{name} {star}성이 되었습니다!";
    [Tooltip("{spent} = 소모한 조각, {refund} = 돌려받은 조각")]
    [SerializeField] private string starUpFailed     = "성급 상승에 실패하였습니다.\n조각 {spent}개 소모, {refund}개 환급";
    [Tooltip("{rate} = 성공 확률 (예: 60, 12.5)")]
    [SerializeField] private string starUpRateFormat = "상승 확률 {rate}%";
    [Tooltip("서버에서 확률을 아직 못 받았을 때 (이때는 성급 올리기를 막음)")]
    [SerializeField] private string starUpRateLoading = "상승 확률 확인 중...";
    [SerializeField] private string evolveDone       = "{name}(으)로 진화했습니다!";

    private string shownId;
    private string resultMessage;   // 방금 한 성장 결과 — 있으면 상태 안내보다 우선

    private GameObject Root => root != null ? root : gameObject;

    private CompanionPoolAsset Pool
        => pool != null ? pool : GachaSystem.Instance != null ? GachaSystem.Instance.Pool : null;

    private void Awake()
    {
        RemoveListeners();   // 두 번 붙지 않게 먼저 뗌
        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.AddListener(Hide);
        if (starUpButton != null) starUpButton.onClick.AddListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.AddListener(OnClickEvolve);
    }

    private void OnDestroy()
    {
        RemoveListeners();
        Inventory.OnItemChanged -= HandleItemChanged;
        StarUpChart.OnChanged   -= Fill;
    }

    private void RemoveListeners()
    {
        if (closeButton  != null) closeButton.onClick.RemoveListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.RemoveListener(Hide);
        if (starUpButton != null) starUpButton.onClick.RemoveListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.RemoveListener(OnClickEvolve);
    }

    // ── 열기 / 닫기 ──

    public void Show(string itemId)
    {
        shownId       = itemId;
        resultMessage = null;
        Fill();

        Inventory.OnItemChanged -= HandleItemChanged;
        Inventory.OnItemChanged += HandleItemChanged;
        StarUpChart.OnChanged   -= Fill;   // 확률을 늦게 받아도 바로 표시
        StarUpChart.OnChanged   += Fill;

        GameObject r = Root;
        r.SetActive(true);
        r.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        Inventory.OnItemChanged -= HandleItemChanged;
        StarUpChart.OnChanged   -= Fill;
        shownId = null;
        GameObject r = Root;
        if (r.activeSelf) r.SetActive(false);
    }

    private void HandleItemChanged(string itemId, int count)
    {
        if (itemId == shownId) Fill();
    }

    // ── 채우기 ──

    private void Fill()
    {
        if (string.IsNullOrEmpty(shownId)) return;

        ItemDef       def       = ItemChart.Resolve(shownId);
        int           count     = Inventory.Count(shownId);
        CompanionData companion = ItemChart.Companion(def);

        ItemChart.ApplyIcon(iconImage, def);
        if (gradeFrame      != null) gradeFrame.color     = ItemChart.FrameColor(def, defaultFrameColor);
        if (nameText        != null) nameText.text        = def.name;
        if (countText       != null) countText.text       = ChatText.Fill(countFormat, "count", count.ToString("N0"));
        if (descriptionText != null) descriptionText.text = def.description ?? string.Empty;

        if (fragmentSection != null) fragmentSection.SetActive(companion != null);
        if (starUpRateText  != null) starUpRateText.gameObject.SetActive(false);   // 성급 올리기가 보일 때만 FillFragment 가 켬
        if (companion != null) FillFragment(companion, count);
    }

    /// <summary>'상승 확률 nn%' — 서버 확률이 없으면 '확인 중'.</summary>
    private string StarUpRateLabel(CompanionFragment growth, CompanionData c)
    {
        if (growth == null || !growth.TryGetStarUpRate(c, out float rate)) return starUpRateLoading;
        return ChatText.Fill(starUpRateFormat, "rate", rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void FillFragment(CompanionData c, int count)
    {
        CompanionFragment growth = CompanionFragment.Instance;
        CompanionManager  cm     = CompanionManager.Instance;

        bool owned = cm != null && cm.IsOwned(c.id);
        int  star  = owned && growth != null ? growth.GetStar(c) : 0;
        bool isMax = star >= CompanionStar.MAX_STAR;

        CompanionData target = null;
        bool canEvolve = growth != null && growth.CanEvolve(c, Pool, out target);
        bool canStarUp = growth != null && growth.CanStarUp(c);

        if (companionText != null)
            companionText.text = ChatText.Fill(companionFormat,
                "grade", CompanionGradeStyle.GetColoredName(c.grade),
                "stars", CompanionStar.ToStars(star),
                "star",  star.ToString(),
                "name",  c.companionName);

        bool   showStarUp = owned && !isMax;
        string rateLabel  = showStarUp ? StarUpRateLabel(growth, c) : null;

        if (progressText != null)
        {
            string label = CompanionStar.FragmentLabel(count, !isMax || target != null);
            if (showStarUp && starUpRateText == null) label += "   " + rateLabel;   // 확률 글자가 없으면 조각 줄 뒤에
            progressText.text = label;
        }

        if (starUpRateText != null)
        {
            starUpRateText.gameObject.SetActive(showStarUp);
            if (showStarUp) starUpRateText.text = rateLabel;
        }

        if (starUpButton != null)
        {
            starUpButton.gameObject.SetActive(showStarUp);
            starUpButton.interactable = canStarUp;
        }
        if (evolveButton != null)
        {
            evolveButton.gameObject.SetActive(owned && isMax && target != null);
            evolveButton.interactable = canEvolve;
        }
        if (evolveButtonLabel != null && target != null)
            evolveButtonLabel.text = ChatText.Fill(evolveFormat,
                "grade", CompanionGradeStyle.GetColoredName(target.grade), "name", target.companionName);

        if (noticeText != null)
            noticeText.text = resultMessage
                           ?? (!owned ? notOwnedNotice : isMax && target == null ? maxStarNotice : string.Empty);
    }

    // ── 버튼 ──

    private CompanionData ShownCompanion() => ItemChart.Companion(ItemChart.Resolve(shownId));

    private void OnClickStarUp()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        CompanionData c = ShownCompanion();
        if (growth == null || c == null) return;

        CompanionFragment.StarUpResult r = growth.TryStarUp(c);
        switch (r.outcome)
        {
            case CompanionFragment.StarUpOutcome.Success:
                resultMessage = ChatText.Fill(starUpDone, "name", c.companionName, "star", r.newStar.ToString());
                break;
            case CompanionFragment.StarUpOutcome.Failed:
                resultMessage = ChatText.Fill(starUpFailed, "spent", r.spent.ToString(), "refund", r.refunded.ToString());
                break;
            default:
                return;
        }
        Fill();
    }

    private void OnClickEvolve()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        CompanionData c = ShownCompanion();
        if (growth == null || c == null || !growth.TryEvolve(c, Pool, out CompanionData target)) return;

        resultMessage = ChatText.Fill(evolveDone, "name", target.companionName);
        Fill();
    }
}
