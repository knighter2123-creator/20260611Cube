using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  [직렬화] 스탯 행 1개의 UI 묶음
    // ══════════════════════════════════════════════
    [System.Serializable]
    public struct StatRow
    {
        [Tooltip("이 행이 대응하는 StatType")]
        public LevelUpManager.StatType statType;

        [Tooltip("스탯 현재 수치 텍스트 (예: 10 → 12)")]
        public TMP_Text statValueText;

        [Tooltip("강화 레벨 텍스트 (예: Lv. 0 / 5000)")]
        public TMP_Text upgradeLevelText;

        [Tooltip("다음 강화 비용 텍스트 (예: 비용 : 80)")]
        public TMP_Text costText;

        [Tooltip("×1 강화 버튼")]
        public Button buttonX1;

        [Tooltip("×10 강화 버튼")]
        public Button buttonX10;

        [Tooltip("×100 강화 버튼")]
        public Button buttonX100;
    }

    // ══════════════════════════════════════════════
    //  인스펙터 연결
    // ══════════════════════════════════════════════
    [Header("공통 UI")]
    [Tooltip("★ 이 컴포넌트가 붙은 오브젝트와 달라야 함 (같으면 Start에서 자신을 꺼버림)")]
    [SerializeField] private GameObject upgradePanel;   // 패널 전체 (열기/닫기용)

    [Header("스탯 행")]
    [SerializeField] private StatRow[] statRows;

    // ══════════════════════════════════════════════
    //  내부 상태
    // ══════════════════════════════════════════════
    private LevelUpManager lm;

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    void Start()
    {
        // 게임 시작 시 패널 숨김
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        // ★ 가이드 퀘스트: 씬 전환으로 넘어온 "강화 UI 열기" 예약 소비
        //   (OnEnable은 Start보다 먼저 실행되므로 예약은 여기서 처리해야 함)
        var gq = GuideQuestManager.Instance;
        if (gq != null && gq.TryConsumeStatFocus(out LevelUpManager.StatType type))
            FocusStat(type);
    }

    void OnEnable()
    {
        lm = LevelUpManager.Instance;

        // ★ lm이 아직 없어도 버튼 리스너는 반드시 등록한다 (조기 리턴 금지)
        if (lm != null)
            lm.OnStatUpgraded += HandleStatUpgraded;

        RegisterButtonListeners();

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged += HandleGoldChanged;

        if (GuideQuestManager.Instance != null)
            GuideQuestManager.Instance.OnFocusStatUpgrade += FocusStat;

        RefreshAll();
    }

    void OnDisable()
    {
        if (lm != null)
            lm.OnStatUpgraded -= HandleStatUpgraded;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged -= HandleGoldChanged;

        // ★ 구독 해제
        if (GuideQuestManager.Instance != null)
            GuideQuestManager.Instance.OnFocusStatUpgrade -= FocusStat;

        UnregisterButtonListeners();
    }

    // ══════════════════════════════════════════════
    //  버튼 리스너 등록 / 해제
    // ══════════════════════════════════════════════
    private void RegisterButtonListeners()
    {
        foreach (var row in statRows)
        {
            var type = row.statType;   // 클로저 캡처용 지역 복사

            row.buttonX1?.onClick.AddListener(() => OnClickUpgrade(type, 1));
            row.buttonX10?.onClick.AddListener(() => OnClickUpgrade(type, 10));
            row.buttonX100?.onClick.AddListener(() => OnClickUpgrade(type, 100));
        }
    }

    private void UnregisterButtonListeners()
    {
        foreach (var row in statRows)
        {
            row.buttonX1?.onClick.RemoveAllListeners();
            row.buttonX10?.onClick.RemoveAllListeners();
            row.buttonX100?.onClick.RemoveAllListeners();
        }
    }

    // ══════════════════════════════════════════════
    //  버튼 콜백
    // ══════════════════════════════════════════════

    /// <summary>강화 버튼 클릭 (×1 / ×10 / ×100 공용)</summary>
    private void OnClickUpgrade(LevelUpManager.StatType type, int times)
    {
        if (lm == null) lm = LevelUpManager.Instance;
        if (lm == null) return;

        int successCount = lm.TryUpgradeMultiple(type, times);

        if (successCount == 0)
        {
            Debug.Log($"[UpgradeUI] {type} 강화 실패 (Currency 부족 또는 최대 레벨)");
            return;
        }

        // ★ 가이드 퀘스트에 "실제 성공 횟수"만큼 보고
        //   OnStatUpgraded 이벤트를 구독하지 않는 이유: ×100의 발화 횟수를 신뢰할 수 없음
        GuideQuestManager.Instance?.ReportStatUpgrade(type, successCount);

        // UI 갱신은 OnStatUpgraded → HandleStatUpgraded → RefreshRow가 처리
    }

    /// <summary>닫기 버튼</summary>
    public void OnClickClose()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    /// <summary>열기 — 외부(HUD 버튼 등)에서 호출</summary>
    public void Open()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(true);

        // ★ 패널이 열리는 시점에 갱신
        //   OnEnable은 씬 로드 직후 1회만 발화하며, 그땐 Player.Instance가 아직 없을 수 있음.
        //   Open()은 UpgradeUI 자신을 켜는 게 아니라 자식 패널만 켜므로 OnEnable이 재발화하지 않는다.
        if (lm == null) lm = LevelUpManager.Instance;
        RefreshAll();
    }

    /// <summary>가이드 퀘스트가 지정한 스탯 강화 항목을 열고 강조한다.</summary>
    private void FocusStat(LevelUpManager.StatType type)
    {
        Open();

        foreach (var row in statRows)
        {
            if (row.statType != type) continue;

            Debug.Log($"[UpgradeUI] 가이드 퀘스트 포커스 → {GuideQuest.StatName(type)}");
            RefreshRow(type);
            // 필요하면 여기서 하이라이트 / 스크롤 이동 처리
            break;
        }
    }

    // ══════════════════════════════════════════════
    //  이벤트 핸들러
    // ══════════════════════════════════════════════
    private void HandleStatUpgraded(LevelUpManager.StatType type) => RefreshRow(type);

    // 골드 바뀔 때 전체 행 버튼 상태 재갱신
    private void HandleGoldChanged(int _)
    {
        foreach (var row in statRows)
            RefreshRow(row.statType);
    }

    // ══════════════════════════════════════════════
    //  UI 갱신
    // ══════════════════════════════════════════════
    private void RefreshAll()
    {
        foreach (var row in statRows)
            RefreshRow(row.statType);
    }

    private void RefreshRow(LevelUpManager.StatType type)
    {
        if (lm == null) return;

        foreach (var row in statRows)
        {
            if (row.statType != type) continue;

            int  upgradeLv = lm.GetUpgradeLevel(type);
            int  cost      = lm.GetUpgradeCost(type);
            bool isMaxed   = upgradeLv >= 5000;

            if (row.upgradeLevelText != null)
                row.upgradeLevelText.text = isMaxed ? "MAX" : $"{upgradeLv:N0} / 5,000";

            if (row.costText != null)
                row.costText.text = isMaxed ? "-" : $"비용 : {cost:N0}";

            if (row.statValueText != null)
                row.statValueText.text = GetStatValueString(type);

            SetButtonInteractable(row.buttonX1,   !isMaxed);
            SetButtonInteractable(row.buttonX10,  !isMaxed);
            SetButtonInteractable(row.buttonX100, !isMaxed);
            break;
        }
    }

    private string GetStatValueString(LevelUpManager.StatType type)
    {
        var player = Player.Instance;
        if (player == null) return "-";

        return type switch
        {
            LevelUpManager.StatType.Damage     => $"{player.stat.baseDamage}",
            LevelUpManager.StatType.CritChance => $"{player.stat.Critical:F1} %",
            LevelUpManager.StatType.CritDamage => $"{player.stat.CriticalMultiplier:F2} x",
            LevelUpManager.StatType.Attackspd  => $"{player.stat.AttackSpd}",
            _                                  => "-"
        };
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn == null) return;
        btn.interactable = interactable;
    }
}