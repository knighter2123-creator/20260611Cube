using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감에서 동료 아이콘을 눌렀을 때 뜨는 상세정보 창. (신규)
/// 이름 · 등급 · 기본 능력치 · 스킬을 보여줍니다.
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
    }

    // ══════════════════════════════════════════════
    //  열기 / 닫기
    // ══════════════════════════════════════════════
    public void Show(CompanionData data, bool owned)
    {
        if (data == null) return;

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

        // ── 기본 능력치 ──
        ActiveSkill skill = data.ownedSkill;

        if (statsText != null)
        {
            sb.Clear();
            sb.Append("탐지 범위   <b>").Append(data.detectRange.ToString("0.#")).Append("</b>");

            if (skill != null)
            {
                sb.Append('\n').Append("스킬 피해   <b>플레이어 공격력 + ").Append(skill.damage.ToString("0.#")).Append("</b>");
                sb.Append('\n').Append("재사용 대기 <b>").Append(skill.cooldown.ToString("0.#")).Append("초</b>");
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
            string summary = skill.GetEffectSummary();
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
}
