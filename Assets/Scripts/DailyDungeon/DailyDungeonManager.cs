using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 일일 던전 씬 진행 담당. (EvolveStageManager 와 같은 흐름)
///   보스 1마리 스폰 → 제한시간(기본 60초) 안에 처치하면 클리어 → 입장 횟수 차감 + 보상 지급 → 원래 스테이지로 복귀
///   시간 초과면 보상 없이 복귀하고, 입장 횟수도 차감하지 않습니다.
///
/// ★ 씬 구성: EvolveScene 을 복제해 DailyDungeonScene 으로 저장하고,
///   EvolveStageManager 컴포넌트를 지운 뒤 이 컴포넌트를 붙이세요.
///   SceneBinder(동료 복원) / HpBar / 웨이포인트는 그대로 쓰면 됩니다.
///   동료 목록 UI(CompanionListUI)를 두지 않으므로 던전 안에서는 배치를 바꿀 수 없습니다.
/// </summary>
public class DailyDungeonManager : MonoBehaviour
{
    public static DailyDungeonManager Instance;

    [Header("UI (선택)")]
    [SerializeField] private TextMeshProUGUI stageText;    // "골드 던전  Lv.3"
    [SerializeField] private TextMeshProUGUI timerText;    // 남은 시간
    [Tooltip("클리어/실패 결과 문구. 비우면 결과 표시 없이 바로 복귀합니다.")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("보스 스폰")]
    [Tooltip("던전 데이터에 bossPrefab 이 비어 있을 때 사용할 예비 프리팹")]
    [SerializeField] private DailyDungeonBoss fallbackBossPrefab;
    [SerializeField] private Transform[] spawnWaypoints;
    [Tooltip("waypoint 가 없을 때만 사용하는 고정 스폰 위치")]
    [SerializeField] private Transform bossSpawnPoint;

    [Header("결과")]
    [Tooltip("클리어/실패 후 복귀까지 기다리는 시간(초, 실제 시간). 결과 문구를 보여주는 용도")]
    [SerializeField] private float returnDelay = 1.5f;

    [Header("단독 테스트용 — 입장 경로 없이 씬 직접 실행 시")]
    [SerializeField] private DailyDungeonData fallbackData;
    [SerializeField] private int fallbackLevel = 1;

    public event Action OnStageClear;
    public event Action OnStageFail;

    public DailyDungeonData ActiveData  { get; private set; }
    public int              ActiveLevel { get; private set; }

    private float timeLeft;
    private bool  stageOver;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        bool fromEntry = DailyDungeonContext.SelectedData != null;
        ActiveData  = fromEntry ? DailyDungeonContext.SelectedData  : fallbackData;
        ActiveLevel = fromEntry ? DailyDungeonContext.SelectedLevel : fallbackLevel;
        if (ActiveData != null) ActiveLevel = ActiveData.ClampLevel(ActiveLevel);

        if (stageText != null && ActiveData != null)
            stageText.text = $"{ActiveData.displayName}  Lv.{ActiveLevel}";

        if (resultText != null) resultText.gameObject.SetActive(false);

        timeLeft = ActiveData != null ? ActiveData.timeLimit : 60f;
        UpdateTimerUI();
        SpawnBoss();
    }

    void Update()
    {
        if (stageOver) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            UpdateTimerUI();
            StageFail();
            return;
        }
        UpdateTimerUI();
    }

    // ── 보스 ───────────────────────────────────────

    private DailyDungeonBoss ResolveBossPrefab()
    {
        if (ActiveData != null && ActiveData.bossPrefab != null)
            return ActiveData.bossPrefab;
        return fallbackBossPrefab;
    }

    private void SpawnBoss()
    {
        DailyDungeonBoss prefab = ResolveBossPrefab();
        if (prefab == null)
        {
            Debug.LogError("[DailyDungeonManager] 보스 프리팹이 없습니다. " +
                           "DailyDungeonData 의 bossPrefab 또는 인스펙터의 fallbackBossPrefab 을 채워주세요.");
            return;
        }

        bool hasPath = spawnWaypoints != null && spawnWaypoints.Length > 0;
        Vector3 spawnPos = hasPath
            ? spawnWaypoints[0].position
            : (bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero);

        DailyDungeonBoss boss = Instantiate(prefab, spawnPos, Quaternion.identity);
        boss.InitForDungeon(ActiveData, ActiveLevel);   // ★ 반드시 호출 — 난이도 스탯 적용

        Debug.Log($"[DailyDungeonManager] 보스 스폰 — {prefab.name} / " +
                  $"{(ActiveData != null ? ActiveData.id : "(데이터 없음)")} Lv.{ActiveLevel}");

        TargetMove move = boss.GetComponent<TargetMove>();
        if (move != null && hasPath)
            move.SetupPath(spawnWaypoints);

        FindFirstObjectByType<HpBar>()?.RegisterEnemy(boss.gameObject);
    }

    // ── 클리어 / 실패 ──────────────────────────────

    /// <summary>DailyDungeonBoss.ReportKill() 에서 호출 → 보상 지급 후 복귀.</summary>
    public void ReportBossKill()
    {
        if (stageOver) return;
        stageOver = true;

        string reward = GrantReward();

        OnStageClear?.Invoke();
        Debug.Log($"[DailyDungeonManager] 클리어 — {reward}");

        StartCoroutine(ReturnAfterDelay($"클리어!\n{reward}"));
    }

    private void StageFail()
    {
        if (stageOver) return;
        stageOver = true;

        OnStageFail?.Invoke();
        Debug.Log("[DailyDungeonManager] 시간 초과 실패 — 보상 없이 복귀");

        StartCoroutine(ReturnAfterDelay("시간 초과"));
    }

    /// <summary>입장 횟수 차감 + 보상 지급 + 클리어 기록 + 저장 (소탕과 같은 경로). 씬을 떠나기 전에 반드시 끝나야 합니다.</summary>
    private string GrantReward()
    {
        if (ActiveData == null) return "";

        // 입장 횟수는 클리어했을 때만 차감합니다. (실패하면 횟수 그대로)
        // 입장 UI 가 남은 횟수를 확인하고 들여보냈고, 던전 도중 횟수가 줄어드는 경로는 없으므로 보통 실패하지 않습니다.
        if (!DailyDungeonProgress.CompleteClear(ActiveData, ActiveLevel))
            Debug.LogWarning($"[DailyDungeonManager] 남은 입장 횟수가 없는데 클리어했습니다 ({ActiveData.id}) — 단독 테스트 실행이 아니라면 확인이 필요합니다.");

        return ActiveData.DescribeReward(ActiveLevel);
    }

    private IEnumerator ReturnAfterDelay(string message)
    {
        if (resultText != null && returnDelay > 0f)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(returnDelay);   // 배속/일시정지와 무관하게
        }

        ReturnToStage();
    }

    /// <summary>잔여 적 정리 후 원래 스테이지로 복귀. (복귀 위치는 DailyDungeonContext → StageManager 가 복원)</summary>
    private void ReturnToStage()
    {
        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Enemy e = enemy.GetComponent<Enemy>();
            e?.RemoveHpBar();
            Destroy(enemy);
        }

        CompanionManager.Instance?.SavePlacementSnapshot();
        SceneLoader.Instance?.ReturnFromDailyDungeon();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(timeLeft / 60f);
        int s = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text  = $"{m}:{s:D2}";
        timerText.color = timeLeft <= 15f ? Color.red : Color.white;
    }
}
