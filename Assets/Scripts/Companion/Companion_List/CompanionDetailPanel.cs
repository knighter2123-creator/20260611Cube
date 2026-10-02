using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감에서 동료 아이콘을 눌렀을 때 뜨는 상세정보 창.
/// 이름 · 등급 · 성급(★)과 조각 · 기본 능력치 · 스킬을 보여주고,
/// 조각이 100개 이상이면 [성급 올리기] 로 성급을 올릴 수 있습니다 (CompanionFragment.TryStarUp).
///
/// ★ 붙이는 곳: Codex_Content 아래의 DetailPanel 오브젝트 (도감 탭 '안').
///   탭이 가려지면 같이 가려지고, CompanionCodexUI 가 OnTabHide 에서 닫아 줍니다.
///
/// ★ 이 창은 CompanionData / ActiveSkill 에 '적힌 값'을 보여줍니다.
///   실제 대미지는 플레이어 공격력이 더해지므로(ActiveSkill.CalcDamage) "플레이어 공격력 + N" 으로 표기합니다.
/// </summary>
public class CompanionDetailPanel : MonoBehaviour
{
    [Header("창")]
    [Tooltip("켜고 끌 오브젝트. 비워두면 이 오브젝트 자신")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button     closeButton;
    [Tooltip("창 뒤 어두운 배경 (선택). 누르면 닫힙니다")]
    [SerializeField] private Button     dimmedButton;

    [Header("기본 정보")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    gradeFrame;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text gradeText;
    [Tooltip("\"보유 중\" / \"미획득\" (선택)")]
    [SerializeField] private TMP_Text ownedText;

    [Header("성급 · 조각 (보유 중일 때만 표시)")]
    [Tooltip("\"★★☆☆☆\" 성급 표시 (선택). 폰트에 ★☆ 글리프가 있어야 합니다")]
    [SerializeField] private TMP_Text starText;
    [Tooltip("\"조각 37 / 100\" 표시 (선택)")]
    [SerializeField] private TMP_Text fragmentText;
    [Tooltip("[성급 올리기] 버튼 (선택). 조각이 충분할 때만 눌립니다")]
    [SerializeField] private Button   starUpButton;
    [Tooltip("[진화] 버튼 (선택). 최대 성급이고 위 등급 · 같은 스킬 동료가 풀에 있을 때 보이고, 조각이 충분하면 눌립니다")]
    [SerializeField] private Button   evolveButton;
    [Tooltip("(선택) 진화 버튼 글자. \"진화 → 희귀 공격\" 처럼 진화 대상 이름을 보여줍니다")]
    [SerializeField] private TMP_Text evolveButtonLabel;

    [Header("기본 능력치")]
    [SerializeField] private TMP_Text statsText;

    [Header("스킬")]
    [SerializeField] private Image    skillIconImage;
    [SerializeField] private TMP_Text skillNameText;
    [Tooltip("효과 요약 + 설명")]
    [SerializeField] private TMP_Text skillEffectText;

    [Header("미획득 표시")]
    [SerializeField] private Color lockedIconColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    // 문자열을 여러 번 이어 붙일 때 쓰는 버퍼. 매번 new 하지 않고 재사용합니다.
    private readonly StringBuilder sb = new StringBuilder(128);

    // 지금 보여주는 동료 — 성급을 올린 뒤 같은 내용으로 다시 그릴 때 씁니다.
    private CompanionData      shownData;
    private bool               shownOwned;
    private CompanionPoolAsset pool;   // 진화 대상을 찾을 풀 (도감이 Show 때 넘겨줌)

    /// <summary>
    /// 진화로 보유 목록이 바뀌었을 때 발생합니다. 도감이 격자(보유 표시 · 수집 수)를 다시 그리는 데 씁니다.
    /// (성급만 오른 경우는 보유 목록이 그대로라 발생하지 않습니다)
    /// </summary>
    public event Action OnOwnedChanged;

    private GameObject Root => root != null ? root : gameObject;

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        // ★ 리스너는 Awake 에서 한 번만. (Show 에서 걸면 열 때마다 쌓입니다)
        //   창이 꺼진 채로 시작해도, 처음 Show() 로 켜지는 순간 Awake 가 먼저 돌기 때문에 괜찮습니다.
        //
        // ★ 버튼의 On Click () 리스트는 인스펙터에서 비워두세요. 둘 다 걸면 한 번 클릭에 두 번 실행됩니다.
        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.AddListener(Hide);
        if (starUpButton != null) starUpButton.onClick.AddListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.AddListener(OnClickEvolve);

        // ★ root 는 '이 오브젝트 자신 / 부모 / 자식' 중 하나여야 합니다.
        //   전혀 상관없는 오브젝트를 넣으면, 이 스크립트가 붙은 오브젝트는 꺼진 채로 남을 수 있고
        //   그러면 Awake 가 영영 안 돌아 ✕ 버튼이 동작하지 않습니다. 에러도 안 납니다.
        bool related = root == null
                    || root == gameObject
                    || transform.IsChildOf(root.transform)      // root 가 부모 쪽
                    || root.transform.IsChildOf(transform);     // root 가 자식 쪽
        if (!related)
        {
            Debug.LogWarning("[CompanionDetailPanel] Root 가 이 오브젝트와 부모·자식 관계가 아닙니다. 연결을 확인하세요.", this);
        }
    }

    private void OnDestroy()
    {
        if (closeButton  != null) closeButton.onClick.RemoveListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.RemoveListener(Hide);
        if (starUpButton != null) starUpButton.onClick.RemoveListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.RemoveListener(OnClickEvolve);
    }

    // ══════════════════════════════════════════════
    //  성급 올리기
    // ══════════════════════════════════════════════

    /// <summary>조각 100개를 써서 성급 +1. 성공하면 바뀐 성급·조각·능력치로 창을 다시 그립니다.</summary>
    private void OnClickStarUp()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        if (growth == null || shownData == null) return;

        // 버튼을 막아 두지만, 조건 검사는 매니저가 한 번 더 합니다 (화면 상태가 늦게 갱신됐을 때 대비).
        if (growth.TryStarUp(shownData))
            Fill(shownData, shownOwned);
    }

    /// <summary>
    /// 진화. 성공하면 창을 진화한 동료로 바꿔 보여주고, 도감에 보유 목록이 바뀌었다고 알립니다.
    /// (원래 동료는 보유 목록에서 사라지므로 그 창을 계속 보여줄 이유가 없습니다)
    /// </summary>
    private void OnClickEvolve()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        if (growth == null || shownData == null) return;

        if (!growth.TryEvolve(shownData, pool, out CompanionData target)) return;

        Fill(target, owned: true);
        OnOwnedChanged?.Invoke();
    }

    // ══════════════════════════════════════════════
    //  열기 / 닫기
    // ══════════════════════════════════════════════
    /// <param name="evolutionPool">진화 대상을 찾을 풀. null 이면 진화 버튼을 숨깁니다.</param>
    public void Show(CompanionData data, bool owned, CompanionPoolAsset evolutionPool)
    {
        if (data == null) return;

        pool = evolutionPool;

        Fill(data, owned);

        GameObject r = Root;
        r.SetActive(true);

        // ★ 같은 부모 아래에서 '맨 뒤' 형제로 옮깁니다.
        //   UI 는 하이어라키 아래쪽이 위에 그려지므로, 격자 ScrollView 보다 위에 뜨게 됩니다.
        //   (하이어라키에서 순서를 잘못 놓아도 창이 목록 뒤에 숨지 않게 하는 안전장치)
        r.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        GameObject r = Root;
        if (r.activeSelf) r.SetActive(false);
    }

    // ══════════════════════════════════════════════
    //  내용 채우기
    // ══════════════════════════════════════════════
    private void Fill(CompanionData data, bool owned)
    {
        shownData  = data;
        shownOwned = owned;

        CompanionFragment growth = CompanionFragment.Instance;
        int star = growth != null ? growth.GetStar(data) : CompanionStar.MIN_STAR;

        // ── 기본 정보 ──
        if (iconImage != null)
        {
            iconImage.sprite         = data.icon;
            iconImage.enabled        = data.icon != null;
            iconImage.preserveAspect = true;
            iconImage.color          = owned ? Color.white : lockedIconColor;
        }

        if (gradeFrame != null) gradeFrame.color = CompanionGradeStyle.GetColor(data.grade);
        if (nameText   != null) nameText.text    = data.companionName;

        // ★ 등급 글자에 색을 입힐 때 TMP 리치 텍스트(<color>)를 씁니다.
        //   TMP_Text 의 Rich Text 옵션이 켜져 있어야 합니다 (기본값: 켜짐).
        if (gradeText  != null) gradeText.text   = CompanionGradeStyle.GetColoredName(data.grade);

        if (ownedText  != null) ownedText.text   = owned ? "보유 중" : "<color=#888888>미획득</color>";

        FillGrowth(data, owned, star, growth);

        // ── 기본 능력치 ──
        ActiveSkill skill = data.ownedSkill;

        if (statsText != null)
        {
            sb.Clear();
            sb.Append("탐지 범위   <b>").Append(data.detectRange.ToString("0.#")).Append("</b>");

            if (skill != null)
            {
                // 에셋의 원래 숫자가 아니라 '이 동료 등급·성급의' 값을 보여줍니다.
                // 전투(Companion / CalcDamage)와 같은 함수를 부르므로 화면 수치와 실제 동작이 어긋나지 않습니다.
                sb.Append('\n').Append("스킬 피해   <b>플레이어 공격력 + ").Append(skill.GetDamage(data.grade, star).ToString("0.#")).Append("</b>");
                sb.Append('\n').Append("재사용 대기 <b>").Append(skill.GetCooldown(data.grade, star).ToString("0.#")).Append("초</b>");
            }
            statsText.text = sb.ToString();
        }

        // ── 스킬 ──
        // ★ 스킬이 연결 안 된 동료도 있을 수 있습니다. 그 경우에도 창이 깨지지 않게 처리합니다.
        //   (CompanionData 에서 ownedSkill 을 비워둔 에셋)
        if (skill == null)
        {
            if (skillIconImage  != null) skillIconImage.enabled = false;
            if (skillNameText   != null) skillNameText.text     = "고유 스킬 없음";
            if (skillEffectText != null) skillEffectText.text   = string.Empty;
            return;
        }

        if (skillIconImage != null)
        {
            skillIconImage.sprite         = skill.icon;
            skillIconImage.enabled        = skill.icon != null;
            skillIconImage.preserveAspect = true;
        }

        if (skillNameText != null) skillNameText.text = skill.skillName;

        if (skillEffectText != null)
        {
            // ★ 스킬마다 다른 효과 문장은 스킬 스스로 만듭니다 (ActiveSkill.GetEffectSummary).
            //   여기서 'skill is SkillSlow' 같은 분기를 하지 않으므로, 새 스킬을 추가해도 이 파일은 그대로입니다.
            // ★ [등급] 둔화율·스턴 시간 등도 등급마다 다르므로 이 동료의 등급을 넘깁니다.
            string summary = skill.GetEffectSummary(data.grade);
            string desc    = skill.description;

            sb.Clear();
            if (!string.IsNullOrEmpty(summary)) sb.Append(summary);
            if (!string.IsNullOrEmpty(desc))
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("<color=#AAAAAA>").Append(desc).Append("</color>");
            }
            skillEffectText.text = sb.ToString();
        }
    }

    /// <summary>
    /// 성급 · 조각 · [성급 올리기] / [진화] 버튼. 미획득 동료는 전부 숨깁니다 (성장시킬 대상이 아님).
    ///
    ///   최대 성급 전                 → [성급 올리기],  "조각 37 / 100"
    ///   최대 성급 + 진화 대상 있음    → [진화 → 희귀 공격],  "조각 37 / 100"
    ///   최대 성급 + 진화 대상 없음    → 버튼 없음,  "조각 37 (최대 성급)"
    /// 버튼은 조각이 100개 이상일 때만 눌립니다.
    /// </summary>
    private void FillGrowth(CompanionData data, bool owned, int star, CompanionFragment growth)
    {
        bool isMax = star >= CompanionStar.MAX_STAR;

        CompanionData evolveTarget = null;
        bool canEvolve = owned && isMax && growth != null && growth.CanEvolve(data, pool, out evolveTarget);
        bool hasNext   = !isMax || evolveTarget != null;   // 조각을 더 모을 이유가 있는가

        SetActive(starText,     owned);
        SetActive(fragmentText, owned);
        if (starUpButton != null) starUpButton.gameObject.SetActive(owned && !isMax);
        if (evolveButton != null) evolveButton.gameObject.SetActive(owned && evolveTarget != null);
        if (!owned) return;

        int fragments = growth != null ? growth.GetFragment(data) : 0;

        if (starText != null)
            starText.text = $"{CompanionStar.ToStars(star)}  {star}성";

        if (fragmentText != null)
            fragmentText.text = CompanionStar.FragmentLabel(fragments, hasNext);

        if (starUpButton != null)
            starUpButton.interactable = growth != null && growth.CanStarUp(data);

        if (evolveButton != null)
            evolveButton.interactable = canEvolve;

        if (evolveButtonLabel != null && evolveTarget != null)
            evolveButtonLabel.text = $"진화 → {CompanionGradeStyle.GetColoredName(evolveTarget.grade)} {evolveTarget.companionName}";
    }

    private static void SetActive(Component c, bool on)
    {
        if (c != null) c.gameObject.SetActive(on);
    }
}