using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도감 상세창 (보여 주기만) — 이름·등급·성급·능력치·성급별 스펙·스킬.</summary>
// [붙이는 곳] Codex_Content 아래 DetailPanel (탭이 가려지면 CompanionCodexUI 가 닫음)
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

    [Header("성급 (보유 중일 때만 표시)")]
    [Tooltip("\"★★☆☆☆\" 성급 표시 (선택). 폰트에 ★☆ 글리프가 있어야 합니다")]
    [SerializeField] private TMP_Text starText;

    [Header("예전 성장 UI — 항상 숨김 (지워도 됨)")]
    [SerializeField] private TMP_Text fragmentText;
    [SerializeField] private Button   starUpButton;
    [SerializeField] private Button   evolveButton;

    [Header("기본 능력치")]
    [SerializeField] private TMP_Text statsText;

    [Header("성급별 상세 스펙")]
    [SerializeField] private TMP_Text starTableText;
    [Tooltip("{star} {stars} {damage} {cooldown}")]
    [SerializeField] private string starTableLine = "{star}성  피해 +{damage}  재사용 {cooldown}초";
    [SerializeField] private Color  starTableCurrentColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color  starTableOtherColor   = new Color(0.67f, 0.67f, 0.67f);
    [SerializeField] private string starTableNoSkill      = "성급에 따라 바뀌는 능력치가 없습니다.";

    [Header("스킬")]
    [SerializeField] private Image    skillIconImage;
    [SerializeField] private TMP_Text skillNameText;
    [Tooltip("효과 요약 + 설명")]
    [SerializeField] private TMP_Text skillEffectText;

    [Header("미획득 표시")]
    [SerializeField] private Color lockedIconColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    // 문자열을 여러 번 이어 붙일 때 쓰는 버퍼. 매번 new 하지 않고 재사용합니다.
    private readonly StringBuilder sb = new StringBuilder(128);

    private GameObject Root => root != null ? root : gameObject;

    // ── Unity 생명주기 ──
    private void Awake()
    {
        // 리스너는 Awake 에서 한 번만 (Show 에서 걸면 쌓임) — ⚠ 인스펙터 On Click 은 비울 것
        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.AddListener(Hide);

        SetActive(fragmentText, false);
        SetActive(starUpButton, false);
        SetActive(evolveButton, false);

        // root 는 자신·부모·자식 중 하나여야 함 (무관하면 이 오브젝트가 꺼진 채 남아 ✕ 가 조용히 안 됨)
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
    }

    // ── 열기 / 닫기 ──
    public void Show(CompanionData data, bool owned)
    {
        if (data == null) return;

        Fill(data, owned);

        GameObject r = Root;
        r.SetActive(true);

        // 맨 뒤 형제로 옮겨 격자보다 위에 그려지게 (하이어라키 순서 실수 방어)
        r.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        GameObject r = Root;
        if (r.activeSelf) r.SetActive(false);
    }

    // ── 내용 채우기 ──
    private void Fill(CompanionData data, bool owned)
    {
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

        // 등급 글자 색은 TMP 리치 텍스트 (Rich Text 옵션 필요, 기본 켜짐)
        if (gradeText  != null) gradeText.text   = CompanionGradeStyle.GetColoredName(data.grade);

        if (ownedText  != null) ownedText.text   = owned ? "보유 중" : "<color=#888888>미획득</color>";

        SetActive(starText, owned);
        if (owned && starText != null) starText.text = $"{CompanionStar.ToStars(star)}  {star}성";

        // ── 기본 능력치 ──
        ActiveSkill skill = data.ownedSkill;
        FillStarTable(data, skill, owned ? star : 0);

        if (statsText != null)
        {
            sb.Clear();
            // ★ 시너지 계열 — 계열 판정은 SynergyTags.Of 한 곳에서 (전투 계산과 같은 함수)
            sb.Append("계열        <b>").Append(SynergyTags.GetColoredName(SynergyTags.Of(data))).Append("</b>\n");
            sb.Append("탐지 범위   <b>").Append(data.detectRange.ToString("0.#")).Append("</b>");

            if (skill != null)
            {
                // 이 동료 등급·성급의 값 — 전투와 같은 함수라 화면과 실제가 어긋나지 않음
                float cooldown = skill.GetCooldown(data.grade, star);
                sb.Append('\n').Append("스킬 피해   <b>플레이어 공격력 + ").Append(skill.GetDamage(data.grade, star).ToString("0.#")).Append("</b>");
                sb.Append('\n').Append("재사용 대기 <b>").Append(cooldown.ToString("0.#")).Append("초</b>");

                AppendSynergyNote(data, owned, cooldown);
            }
            statsText.text = sb.ToString();
        }

        // ── 스킬 (ownedSkill 이 빈 에셋이어도 창이 깨지지 않게) ──
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
            // 효과 문장은 스킬 스스로 만듦(새 스킬을 추가해도 이 파일 불변), 효과량이 등급마다 달라 등급을 넘김
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

    /// <summary>1성~최대 성급의 피해·재사용 대기 (currentStar 줄 강조, 0 = 강조 없음).</summary>
    private void FillStarTable(CompanionData data, ActiveSkill skill, int currentStar)
    {
        if (starTableText == null) return;

        if (skill == null) { starTableText.text = starTableNoSkill; return; }

        string cur   = ColorUtility.ToHtmlStringRGB(starTableCurrentColor);
        string other = ColorUtility.ToHtmlStringRGB(starTableOtherColor);

        sb.Clear();
        for (int s = CompanionStar.MIN_STAR; s <= CompanionStar.MAX_STAR; s++)
        {
            if (sb.Length > 0) sb.Append('\n');
            string line = ChatText.Fill(starTableLine,
                "star",     s.ToString(),
                "stars",    CompanionStar.ToStars(s),
                "damage",   skill.GetDamage(data.grade, s).ToString("0.#"),
                "cooldown", skill.GetCooldown(data.grade, s).ToString("0.#"));

            sb.Append("<color=#").Append(s == currentStar ? cur : other).Append('>');
            if (s == currentStar) sb.Append("<b>").Append(line).Append("</b>");
            else                  sb.Append(line);
            sb.Append("</color>");
        }
        starTableText.text = sb.ToString();
    }

    /// <summary>배치 중이고 시너지가 켜져 있으면 시너지 반영 수치를 한 줄 덧붙임 (기본 수치는 그대로, 쿨다운은 전투와 같은 상수로).</summary>
    private void AppendSynergyNote(CompanionData data, bool owned, float baseCooldown)
    {
        CompanionManager cm = CompanionManager.Instance;
        if (!owned || cm == null || !cm.IsPlaced(data.id)) return;

        float damageBonus = ActiveSkill.SynergyDamageFactor - 1f;
        float cdFactor    = ActiveSkill.SynergyCooldownFactor;
        bool  hasDamage   = !Mathf.Approximately(damageBonus, 0f);
        bool  hasCooldown = !Mathf.Approximately(cdFactor, 1f);
        if (!hasDamage && !hasCooldown) return;

        sb.Append("\n<color=#8CFF8C>시너지 적용 중: ");
        if (hasDamage)
            sb.Append(SynergyStatText.Describe(SynergyStat.SkillDamage, damageBonus));
        if (hasCooldown)
        {
            if (hasDamage) sb.Append(", ");
            float cd = Mathf.Max(baseCooldown * cdFactor, ActiveSkill.MIN_COOLDOWN);
            sb.Append("재사용 대기 ").Append(cd.ToString("0.#")).Append('초');
        }
        sb.Append("</color>");
    }

    private static void SetActive(Component c, bool on)
    {
        if (c != null) c.gameObject.SetActive(on);
    }
}