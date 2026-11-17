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
    [Tooltip("진화 대상을 찾을 풀. 비우면 GachaSystem 의 풀")]
    [SerializeField] private CompanionPoolAsset pool;

    [Header("문장 — {count} {name} {stars} {star} {grade}")]
    [SerializeField] private string countFormat      = "보유 {count}개";
    [SerializeField] private string companionFormat  = "{grade} {name}  {stars} {star}성";
    [SerializeField] private string evolveFormat     = "진화 → {grade} {name}";
    [SerializeField] private string notOwnedNotice   = "동료를 보유해야 조각을 쓸 수 있습니다.";
    [SerializeField] private string maxStarNotice    = "최대 성급입니다. 더 이상 성장할 수 없습니다.";
    [SerializeField] private string starUpDone       = "{name} {star}성이 되었습니다!";
    [SerializeField] private string evolveDone       = "{name}(으)로 진화했습니다!";

    private string shownId;
    private string resultMessage;   // 방금 한 성장 결과 — 있으면 상태 안내보다 우선

    private GameObject Root => root != null ? root : gameObject;

    private CompanionPoolAsset Pool
        => pool != null ? pool : GachaSystem.Instance != null ? GachaSystem.Instance.Pool : null;

    private void Awake()
    {
        Listener();
    }

    private void OnDestroy()
    {
        Listener();
        Inventory.OnItemChanged -= HandleItemChanged;
    }

    void Listener()
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

        GameObject r = Root;
        r.SetActive(true);
        r.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        Inventory.OnItemChanged -= HandleItemChanged;
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
        if (companion != null) FillFragment(companion, count);
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

        if (progressText != null) progressText.text = CompanionStar.FragmentLabel(count, !isMax || target != null);

        if (starUpButton != null)
        {
            starUpButton.gameObject.SetActive(owned && !isMax);
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
        if (growth == null || c == null || !growth.TryStarUp(c)) return;

        resultMessage = ChatText.Fill(starUpDone, "name", c.companionName, "star", growth.GetStar(c).ToString());
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
