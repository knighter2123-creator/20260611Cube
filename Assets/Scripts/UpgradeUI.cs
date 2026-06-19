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
    [SerializeField] private GameObject upgradePanel;   // 패널 전체 (열기/닫기용)

    [Header("스탯 행")]
    [SerializeField] private StatRow[] statRows;        // 인스펙터에서 3개 배열로 설정

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
    }

    void OnEnable()
    {
        lm = LevelUpManager.Instance;
        if (lm == null) return;

        lm.OnStatUpgraded += HandleStatUpgraded;
        RegisterButtonListeners();

        // 골드 변경 시 버튼 상태 갱신
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged += HandleGoldChanged;

        RefreshAll();
    }

    void OnDisable()
    {
        if (lm != null)
            lm.OnStatUpgraded -= HandleStatUpgraded;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged -= HandleGoldChanged;

        UnregisterButtonListeners();
    }

    // ══════════════════════════════════════════════
    //  버튼 리스너 등록 / 해제
    //  (인스펙터 OnClick 대신 코드로 등록 → 씬 전환 후 참조 끊김 방지)
    // ══════════════════════════════════════════════
    private void RegisterButtonListeners()
    {
        foreach (var row in statRows)
        {
            // 클로저 캡처를 위해 지역 변수로 복사
            var type = row.statType;

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

    /// <summary>강화 버튼 클릭 시 호출 (×1 / ×10 / ×100 공용)</summary>
    private void OnClickUpgrade(LevelUpManager.StatType type, int times)
    {
        if (lm == null) return;

        int successCount = lm.TryUpgradeMultiple(type, times);

        if (successCount == 0)
        {
            // 강화 실패 시 버튼 인터랙션으로 피드백
            // 필요하면 여기에 SFX나 흔들림 효과 추가
            Debug.Log($"[UpgradeUI] {type} 강화 실패 (Currency 부족 또는 최대 레벨)");
        }
        // 성공 시 OnStatUpgraded 이벤트 → HandleStatUpgraded → RefreshRow 호출
    }

    /// <summary>닫기 버튼 — 인스펙터 OnClick에 연결하거나 코드로 연결</summary>
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
    }

    // ══════════════════════════════════════════════
    //  이벤트 핸들러
    // ══════════════════════════════════════════════
   
    private void HandleStatUpgraded(LevelUpManager.StatType type) => RefreshRow(type);

    // ══════════════════════════════════════════════
    //  UI 갱신
    // ══════════════════════════════════════════════

    /// <summary>패널 열릴 때 전체 갱신</summary>
    private void RefreshAll()
    {
        // RefreshCurrency() 제거 — CurrencyManager.UpdateUI()가 자동 처리
        foreach (var row in statRows)
            RefreshRow(row.statType);
    }

    /// <summary>특정 스탯 행 전체 갱신 (강화 후 해당 행만 갱신)</summary>
    private void RefreshRow(LevelUpManager.StatType type)
    {
        foreach (var row in statRows)
        {
            if (row.statType != type) continue;

            int  upgradeLv = lm.GetUpgradeLevel(type);
            int  cost      = lm.GetUpgradeCost(type);
            bool isMaxed   = upgradeLv >= 5000;

            // 골드 비교를 CurrencyManager.Gold 기준으로 변경
            bool canAfford = CurrencyManager.Instance != null && CurrencyManager.Instance.Gold >= cost;

            if (row.upgradeLevelText != null)
                row.upgradeLevelText.text = isMaxed ? "MAX" : $"Lv. {upgradeLv:N0} / 5,000";

            if (row.costText != null)
                row.costText.text = isMaxed ? "-" : $"Cost : {cost:N0}";

            if (row.statValueText != null)
                row.statValueText.text = GetStatValueString(type);

            SetButtonInteractable(row.buttonX1,   !isMaxed);
            SetButtonInteractable(row.buttonX10,  !isMaxed);
            SetButtonInteractable(row.buttonX100, !isMaxed);
            break;
        }
    }

    /// <summary>StatType에 따라 현재 스탯 수치 문자열 반환</summary>
    private string GetStatValueString(LevelUpManager.StatType type)
    {
        var player = Player.Instance;
        if (player == null) return "-";

        return type switch
        {
            LevelUpManager.StatType.Damage     => $"{player.stat.baseDamage} ATK",
            LevelUpManager.StatType.CritChance => $"{player.stat.Critical:F1} %",
            LevelUpManager.StatType.CritDamage => $"{player.stat.CriticalMultiplier:F2} x",
            LevelUpManager.StatType.attackspd  => $"{player.stat.AttackSpd} SPD",
            _                                  => "-"
        };
    }

    /// <summary>버튼 Interactable 상태 + 색상 동시 적용</summary>
    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn == null) return;
        btn.interactable = interactable;
    }
    
    // 골드 바뀔 때 전체 행 버튼 상태 재갱신
    private void HandleGoldChanged(int _)
    {
        foreach (var row in statRows)
            RefreshRow(row.statType);
    }
}