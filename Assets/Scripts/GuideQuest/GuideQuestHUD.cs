using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 이미지 속 가이드 퀘스트 위젯.
/// 미완료 → 해당 콘텐츠로 즉시 이동 / 완료 → 보상 수령 후 다음 단계.
/// ★ StageScene 소속으로 둘 것 (DontDestroyOnLoad 금지)
/// </summary>
public class GuideQuestHUD : MonoBehaviour
{
    [Header("보상")]
    [SerializeField] private Image rewardIcon;      // ★ 아이콘 하나를 스프라이트만 교체
    [SerializeField] private Sprite goldSprite;
    [SerializeField] private Sprite gemSprite;
    
    [Header("Text")]
    [SerializeField] private TMP_Text titleText;      // 가이드 퀘스트 46단계
    [SerializeField] private TMP_Text descText;       // 스테이지 1-20 클리어
    [SerializeField] private TMP_Text progressText;   // 10 / 20
    [SerializeField] private TMP_Text rewardText;     // 보상 젬 수량

    [Header("UI")]
    [SerializeField] private Button rootButton;       // 창 전체 버튼
    [SerializeField] private Image fillBar;           // (선택) 진행 게이지
    [SerializeField] private GameObject completeMark; // (선택) 완료 표시

    private void OnEnable()
    {
        var m = GuideQuestManager.Instance;
        if (m == null) return;

        m.OnQuestChanged    += HandleQuestChanged;
        m.OnProgressChanged += HandleProgressChanged;

        if (rootButton != null)
            rootButton.onClick.AddListener(OnClickRoot);

        // 현재 상태 즉시 반영
        if (m.Current != null)
        {
            HandleQuestChanged(m.Current);
            HandleProgressChanged(m.Progress, m.Current.requiredCount);
        }
    }

    private void OnDisable()
    {
        var m = GuideQuestManager.Instance;
        if (m != null)
        {
            m.OnQuestChanged    -= HandleQuestChanged;
            m.OnProgressChanged -= HandleProgressChanged;
        }

        if (rootButton != null)
            rootButton.onClick.RemoveListener(OnClickRoot);
    }

    // ── 이벤트 핸들러 ─────────────────────────────
    private void HandleQuestChanged(GuideQuest q)
    {
        if (titleText  != null) titleText.text  = q.Title;
        if (descText   != null) descText.text   = q.Description;
        if (rewardText != null) rewardText.text = q.rewardAmount.ToString("N0");

        // ★ 보상 재화에 맞는 아이콘으로 교체
        if (rewardIcon != null)
        {
            rewardIcon.sprite = (q.rewardType == Manager.currency.CurrencyType.Gold)
                ? goldSprite
                : gemSprite;
        }
    }

    private void HandleProgressChanged(long cur, long req)
    {
        if (progressText != null) progressText.text = $"{cur} / {req}";
        if (fillBar != null) fillBar.fillAmount = req > 0 ? (float)cur / req : 0f;

        bool done = cur >= req;
        if (completeMark != null) completeMark.SetActive(done);
    }

    // ── 클릭 ──────────────────────────────────────
    private void OnClickRoot()
    {
        var m = GuideQuestManager.Instance;
        if (m == null || m.Current == null) return;

        // 완료 → 보상 수령 (자동으로 다음 단계 갱신)
        if (m.IsComplete)
        {
            m.Claim();
            return;
        }

        // 미완료 → 해당 콘텐츠로 이동
        Navigate(m.Current);
    }

    private void Navigate(GuideQuest q)
    {
        var loader = SceneLoader.Instance;
        var m = GuideQuestManager.Instance;
        if (loader == null || m == null) return;

        switch (q.type)
        {
            // 전투 관련 → 스테이지로
            case GuideQuestType.EnemyKill:
            case GuideQuestType.StageClear:
            case GuideQuestType.LevelUp:
                loader.EnsureStageScene();
                break;

            // 동료 소환 → 가챠 (Additive)
            case GuideQuestType.SummonCompanion:
                if (!loader.IsGachaOpen)
                    loader.GoToGacha();
                break;

            // 스탯 강화 → 스테이지의 강화 UI 열기
            case GuideQuestType.StatUpgrade:
                bool alreadyInStage =
                    loader.CurrentScene == SceneLoader.STAGE_SCENE && !loader.IsGachaOpen;

                if (alreadyInStage)
                {
                    m.RequestStatFocus(q.statType, immediate: true);
                }
                else
                {
                    // 씬 전환 필요 → 예약 후 이동 (UpgradeUI.Start가 소비)
                    m.RequestStatFocus(q.statType, immediate: false);
                    loader.EnsureStageScene();
                }
                break;
        }
    }
}