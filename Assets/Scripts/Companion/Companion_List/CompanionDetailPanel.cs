using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도감 상세창: 이름·등급·성급·조각·능력치(이 등급·성급 값)·스킬을 보여 주고 조각 100개 이상이면 [성급 올리기]/[진화] — 대미지는 "플레이어 공격력 + N" 으로 표기.</summary>
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

    /// <summary>진화로 보유 목록이 바뀌었을 때 — 도감 격자 다시 그리기용 (성급만 오르면 발생 안 함).</summary>
    public event Action OnOwnedChanged;

    private GameObject Root => root != null ? root : gameObject;

    // ── Unity 생명주기 ──
    private void Awake()
    {
        // 리스너는 Awake 에서 한 번만 (Show 에서 걸면 쌓임) — ⚠ 인스펙터 On Click 은 비울 것
        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        if (dimmedButton != null) dimmedButton.onClick.AddListener(Hide);
        if (starUpButton != null) starUpButton.onClick.AddListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.AddListener(OnClickEvolve);

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
        if (starUpButton != null) starUpButton.onClick.RemoveListener(OnClickStarUp);
        if (evolveButton != null) evolveButton.onClick.RemoveListener(OnClickEvolve);
    }

    // ── 성급 올리기 ──

    /// <summary>조각 100개를 써서 성급 +1. 성공하면 바뀐 성급·조각·능력치로 창을 다시 그립니다.</summary>
    private void OnClickStarUp()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        if (growth == null || shownData == null) return;

        // 버튼을 막아 두지만, 조건 검사는 매니저가 한 번 더 합니다 (화면 상태가 늦게 갱신됐을 때 대비).
        if (growth.TryStarUp(shownData))
            Fill(shownData, shownOwned);
    }

    /// <summary>진화 — 성공하면 창을 진화한 동료로 바꾸고 도감에 보유 목록 변경을 알림 (원래 동료는 사라짐).</summary>
    private void OnClickEvolve()
    {
        CompanionFragment growth = CompanionFragment.Instance;
        if (growth == null || shownData == null) return;

        if (!growth.TryEvolve(shownData, pool, out CompanionData target)) return;

        Fill(target, owned: true);
        OnOwnedChanged?.Invoke();
    }

    // ── 열기 / 닫기 ──
    /// <param name="evolutionPool">진화 대상을 찾을 풀. null 이면 진화 버튼을 숨깁니다.</param>
    public void Show(CompanionData data, bool owned, CompanionPoolAsset evolutionPool)
    {
        if (data == null) return;

        pool = evolutionPool;

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

        // 등급 글자 색은 TMP 리치 텍스트 (Rich Text 옵션 필요, 기본 켜짐)
        if (gradeText  != null) gradeText.text   = CompanionGradeStyle.GetColoredName(data.grade);

        if (ownedText  != null) ownedText.text   = owned ? "보유 중" : "<color=#888888>미획득</color>";

        FillGrowth(data, owned, star, growth);

        // ── 기본 능력치 ──
        ActiveSkill skill = data.ownedSkill;

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

    /// <summary>성급·조각 줄과 버튼(최대 전 = [성급 올리기], 최대 + 진화 대상 = [진화 → …], 없으면 버튼 없음) — 조각 100개 이상일 때만 눌림, 미획득은 숨김.</summary>
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