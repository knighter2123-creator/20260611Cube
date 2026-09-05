using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

    [Header("설정")]
    [Tooltip("보유 골드가 1회 강화 비용보다 적으면 버튼을 비활성화합니다")]
    [SerializeField] private bool disableWhenUnaffordable = true;

    // ══════════════════════════════════════════════
    //  내부 상태
    // ══════════════════════════════════════════════
    private LevelUpManager lm;

    private bool boundStatUpgraded;
    private bool boundGold;
    private bool boundFocus;

    // 골드는 이벤트로만 받아 캐시한다 (CurrencyManager의 게터 이름에 의존하지 않기 위해)
    private int  cachedGold;
    private bool hasGoldValue;

    // statType → statRows 인덱스 (매 갱신마다 배열을 훑지 않도록)
    private readonly Dictionary<LevelUpManager.StatType, int> rowIndex =
        new Dictionary<LevelUpManager.StatType, int>();

    // RemoveAllListeners 대신 정확히 제거하기 위해 델리게이트를 보관
    private UnityAction[] actionsX1, actionsX10, actionsX100;

    // ══════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════
    private void Awake()
    {
        BuildRowIndex();

        // upgradePanel 을 자기 자신으로 지정하면 Start에서 스스로를 꺼버려
        // 이후 아무 이벤트도 받지 못하는 상태가 됩니다. 툴팁 경고를 코드로도 막습니다.
        if (upgradePanel == gameObject)
        {
            Debug.LogError("[UpgradeUI] upgradePanel 에 이 컴포넌트가 붙은 오브젝트 자신이 들어가 있습니다. " +
                           "자식 패널 오브젝트를 넣어주세요. 자기 자신 참조는 무시합니다.", this);
            upgradePanel = null;
        }
    }

    private void BuildRowIndex()
    {
        rowIndex.Clear();
        if (statRows == null) { statRows = new StatRow[0]; return; }

        for (int i = 0; i < statRows.Length; i++)
        {
            var type = statRows[i].statType;
            if (rowIndex.ContainsKey(type))
            {
                // 중복이면 앞의 행만 갱신되고 뒤의 행은 영원히 멈춰 있게 됩니다
                Debug.LogWarning($"[UpgradeUI] statRows 에 {type} 이 두 번 이상 있습니다. " +
                                 "첫 번째 행만 갱신됩니다.", this);
                continue;
            }
            rowIndex.Add(type, i);
        }
    }

    private void Start()
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

    private void OnEnable()
    {
        RegisterButtonListeners();
        TryBindAll();
        RefreshAll();
    }

    private void Update()
    {
        // ★ 원래는 OnEnable에서 단 한 번만 구독을 시도했습니다.
        //   그 시점에 매니저가 아직 없으면 조용히 실패하고, 그 세션 내내
        //   강화해도 UI가 갱신되지 않았습니다. 붙을 때까지 재시도합니다.
        if (!boundStatUpgraded || !boundGold || !boundFocus)
            TryBindAll();
    }

    private void OnDisable()
    {
        if (boundStatUpgraded && lm != null)
        {
            lm.OnStatUpgraded -= HandleStatUpgraded;
            boundStatUpgraded = false;
        }

        if (boundGold && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldChanged -= HandleGoldChanged;
            boundGold = false;
        }

        if (boundFocus && GuideQuestManager.Instance != null)
        {
            GuideQuestManager.Instance.OnFocusStatUpgrade -= FocusStat;
            boundFocus = false;
        }

        UnregisterButtonListeners();
    }

    private void TryBindAll()
    {
        if (!boundStatUpgraded)
        {
            if (lm == null) lm = LevelUpManager.Instance;
            if (lm != null)
            {
                lm.OnStatUpgraded += HandleStatUpgraded;
                boundStatUpgraded = true;
                RefreshAll();
            }
        }

        if (!boundGold && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldChanged += HandleGoldChanged;
            boundGold = true;
        }

        if (!boundFocus && GuideQuestManager.Instance != null)
        {
            GuideQuestManager.Instance.OnFocusStatUpgrade += FocusStat;
            boundFocus = true;
        }
    }

    // ══════════════════════════════════════════════
    //  버튼 리스너 등록 / 해제
    // ══════════════════════════════════════════════
    private void RegisterButtonListeners()
    {
        int n = statRows.Length;
        actionsX1   = new UnityAction[n];
        actionsX10  = new UnityAction[n];
        actionsX100 = new UnityAction[n];

        for (int i = 0; i < n; i++)
        {
            var type = statRows[i].statType;   // 클로저 캡처용 지역 복사

            actionsX1[i]   = () => OnClickUpgrade(type, 1);
            actionsX10[i]  = () => OnClickUpgrade(type, 10);
            actionsX100[i] = () => OnClickUpgrade(type, 100);

            // ★ Unity 오브젝트에 ?. 를 쓰면 파괴된 오브젝트를 살아있다고 판단합니다.
            //   != null 로 검사해야 Unity의 수명 검사가 동작합니다.
            if (statRows[i].buttonX1   != null) statRows[i].buttonX1.onClick.AddListener(actionsX1[i]);
            if (statRows[i].buttonX10  != null) statRows[i].buttonX10.onClick.AddListener(actionsX10[i]);
            if (statRows[i].buttonX100 != null) statRows[i].buttonX100.onClick.AddListener(actionsX100[i]);
        }
    }

    private void UnregisterButtonListeners()
    {
        if (actionsX1 == null) return;

        // ★ RemoveAllListeners 는 다른 스크립트가 붙인 리스너까지 날려버립니다.
        //   등록해둔 델리게이트만 정확히 제거합니다.
        for (int i = 0; i < statRows.Length && i < actionsX1.Length; i++)
        {
            if (statRows[i].buttonX1   != null && actionsX1[i]   != null) statRows[i].buttonX1.onClick.RemoveListener(actionsX1[i]);
            if (statRows[i].buttonX10  != null && actionsX10[i]  != null) statRows[i].buttonX10.onClick.RemoveListener(actionsX10[i]);
            if (statRows[i].buttonX100 != null && actionsX100[i] != null) statRows[i].buttonX100.onClick.RemoveListener(actionsX100[i]);
        }

        actionsX1 = actionsX10 = actionsX100 = null;
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
            RefreshAll();   // 실패해도 비용/보유량 표시는 최신으로
            return;
        }

        // ★ 가이드 퀘스트에 "실제 성공 횟수"만큼 보고
        //   OnStatUpgraded 이벤트를 구독하지 않는 이유: ×100의 발화 횟수를 신뢰할 수 없음
        GuideQuestManager.Instance?.ReportStatUpgrade(type, successCount);

        // ★ OnStatUpgraded 에만 의존하지 않고 직접 갱신합니다.
        //   골드가 줄었으므로 다른 행의 구매 가능 여부도 함께 바뀝니다.
        RefreshAll();
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
        TryBindAll();
        RefreshAll();
    }

    /// <summary>가이드 퀘스트가 지정한 스탯 강화 항목을 열고 강조한다.</summary>
    private void FocusStat(LevelUpManager.StatType type)
    {
        Open();   // 내부에서 RefreshAll 수행

        if (rowIndex.ContainsKey(type))
            Debug.Log($"[UpgradeUI] 가이드 퀘스트 포커스 → {GuideQuest.StatName(type)}");
        else
            Debug.LogWarning($"[UpgradeUI] statRows 에 {type} 행이 없어 포커스할 수 없습니다.", this);

        // 필요하면 여기서 하이라이트 / 스크롤 이동 처리
    }

    // ══════════════════════════════════════════════
    //  이벤트 핸들러
    // ══════════════════════════════════════════════
    private void HandleStatUpgraded(LevelUpManager.StatType type) => RefreshRow(type);

    // 골드가 바뀌면 모든 행의 구매 가능 여부가 달라진다
    private void HandleGoldChanged(int gold)
    {
        cachedGold   = gold;
        hasGoldValue = true;
        RefreshAll();
    }

    // ══════════════════════════════════════════════
    //  UI 갱신
    // ══════════════════════════════════════════════
    private void RefreshAll()
    {
        for (int i = 0; i < statRows.Length; i++)
            RefreshRowAt(i);
    }

    private void RefreshRow(LevelUpManager.StatType type)
    {
        if (rowIndex.TryGetValue(type, out int i))
            RefreshRowAt(i);
    }

    private void RefreshRowAt(int i)
    {
        if (i < 0 || i >= statRows.Length) return;

        StatRow row = statRows[i];
        LevelUpManager.StatType type = row.statType;

        // 스탯 수치는 매니저가 없어도 표시할 수 있습니다
        if (row.statValueText != null)
            row.statValueText.text = GetStatValueString(type);

        // ★ lm 은 있는데 PlayerStat 이 아직 주입되지 않은 구간이 존재합니다.
        //   그때 GetUpgradeCost 는 0을 돌려주므로, 검사하지 않으면
        //   "0 / 5,000, 비용 : 0" 이라는 거짓 정보를 띄우고 버튼도 눌리게 됩니다.
        if (lm == null || !lm.IsReady)
        {
            if (row.upgradeLevelText != null) row.upgradeLevelText.text = "-";
            if (row.costText != null)         row.costText.text         = "-";

            SetButtonInteractable(row.buttonX1,   false);
            SetButtonInteractable(row.buttonX10,  false);
            SetButtonInteractable(row.buttonX100, false);
            return;
        }

        int  upgradeLv = lm.GetUpgradeLevel(type);
        int  cost      = lm.GetUpgradeCost(type);
        int  maxLv     = lm.MaxUpgradeLevel;      // ★ 하드코딩 5000 제거 — 매니저의 상한을 그대로 사용
        bool isMaxed   = upgradeLv >= maxLv;

        if (row.upgradeLevelText != null)
            row.upgradeLevelText.text = isMaxed ? "MAX" : $"{upgradeLv:N0} / {maxLv:N0}";

        if (row.costText != null)
            row.costText.text = isMaxed ? "-" : $"비용 : {cost:N0}";

        // ★ 1회 비용조차 감당 못 하면 ×10 / ×100 도 반드시 실패하므로 함께 비활성화합니다.
        //   (골드를 아직 한 번도 통보받지 못했다면 잘못 잠그지 않도록 활성 유지)
        bool affordable = !disableWhenUnaffordable || !hasGoldValue || cachedGold >= cost;
        bool usable     = !isMaxed && affordable;

        SetButtonInteractable(row.buttonX1,   usable);
        SetButtonInteractable(row.buttonX10,  usable);
        SetButtonInteractable(row.buttonX100, usable);
    }

    private string GetStatValueString(LevelUpManager.StatType type)
    {
        var player = Player.Instance;
        if (player == null || player.stat == null) return "-";

        return type switch
        {
            LevelUpManager.StatType.Damage     => $"{player.stat.baseDamage}",
            LevelUpManager.StatType.CritChance => $"{player.stat.Critical:F1} %",
            LevelUpManager.StatType.CritDamage => $"{player.stat.CriticalMultiplier:F2} x",
            // ★ AttackSpd 는 '공격 쿨다운(ms)'이라 강화할수록 숫자가 줄어듭니다.
            //   원본처럼 raw 값을 그대로 보여주면 유저에겐 스탯이 나빠지는 것처럼 보입니다.
            //   초당 공격 횟수로 환산해 '올라가는 수치'로 표시합니다.
            LevelUpManager.StatType.Attackspd  => player.stat.AttackSpd > 0f
                                                    ? $"{1000f / player.stat.AttackSpd:F2} 회/초"
                                                    : "-",
            _                                  => "-"
        };
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn == null) return;
        btn.interactable = interactable;
    }
}