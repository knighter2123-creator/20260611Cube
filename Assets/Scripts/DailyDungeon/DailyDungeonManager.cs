using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>일일 던전 씬 진행: BossKill = 제한시간 안에 보스 처치 / Defense = 버티기(침입 maxBreaches 회면 실패) — 클리어면 횟수 차감·보상 후 복귀, 실패면 보상·차감 없이 복귀.</summary>
// 씬: EvolveScene 복제 → EvolveStageManager 대신 이 컴포넌트 (SceneBinder·HpBar·웨이포인트 유지, 동료 목록 UI 가 없어 던전 안에선 배치 변경 불가)
public class DailyDungeonManager : MonoBehaviour
{
    public static DailyDungeonManager Instance;

    [Header("UI (선택)")]
    [SerializeField] private TextMeshProUGUI stageText;    // "골드 던전  Lv.3"
    [SerializeField] private TextMeshProUGUI timerText;    // 남은 시간
    [Tooltip("클리어/실패 결과 문구. 비우면 결과 표시 없이 바로 복귀합니다.")]
    [SerializeField] private TextMeshProUGUI resultText;
    [Tooltip("디펜스 규칙의 침입 횟수 \"침입 1 / 3\". 비우면 표시하지 않습니다. (BossKill 던전에서는 자동으로 숨김)")]
    [SerializeField] private TextMeshProUGUI breachText;

    [Header("이동 경로 (보스·디펜스 공용)")]
    [SerializeField] private Transform[] spawnWaypoints;

    [Header("보스 스폰 (BossKill)")]
    [Tooltip("던전 데이터에 bossPrefab 이 비어 있을 때 사용할 예비 프리팹")]
    [SerializeField] private DailyDungeonBoss fallbackBossPrefab;
    [Tooltip("waypoint 가 없을 때만 사용하는 고정 스폰 위치")]
    [SerializeField] private Transform bossSpawnPoint;

    [Header("디펜스 (Defense)")]
    [Tooltip("적이 여기 닿으면 침입 1회. 비우면 경로의 마지막 웨이포인트를 씁니다.\n" +
             "⚠ 첫 웨이포인트(스폰 위치) 근처에 두면 태어나자마자 침입으로 셉니다.")]
    [SerializeField] private Transform breachPoint;

    [Tooltip("도착 지점에서 이 거리(월드 단위) 안에 들어오면 침입. " +
             "한 번에 여러 마리를 흩뿌려 생성하면 경로가 그만큼 비켜나므로 spawnJitterRadius 보다 크게 두세요.")]
    [SerializeField] private float breachRadius = 0.5f;

    [Tooltip("한 주기에 2마리 이상 생성할 때 겹치지 않게 흩뿌리는 반지름 (EnemyRespawn 과 같은 방식)")]
    [SerializeField] private float spawnJitterRadius = 0.3f;

    [Tooltip("첫 적이 나오기까지 기다리는 시간(초)")]
    [SerializeField] private float firstSpawnDelay = 1f;

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

    /// <summary>지금 던전의 규칙. 데이터가 없으면(잘못된 단독 실행) 예전 동작인 BossKill.</summary>
    public DailyDungeonRule Rule => ActiveData != null ? ActiveData.rule : DailyDungeonRule.BossKill;

    public int BreachCount { get; private set; }
    public int MaxBreaches => ActiveData != null ? ActiveData.maxBreaches : 3;

    private float timeLeft;
    private bool  stageOver;

    // 마지막으로 표시한 '초' — StageManager.UpdateTimerUI 와 같은 이유로 초가 바뀔 때만 갱신합니다.
    private int lastTimerSecond = int.MinValue;

    private HpBar hpBarRoot;   // 매 스폰마다 씬을 뒤지지 않도록 캐싱

    private bool HasPath => spawnWaypoints != null && spawnWaypoints.Length > 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void fromEntry()
    {
        bool fromEntry = DailyDungeonContext.SelectedData != null;
        ActiveData  = fromEntry ? DailyDungeonContext.SelectedData  : fallbackData;
        ActiveLevel = fromEntry ? DailyDungeonContext.SelectedLevel : fallbackLevel;
    }

    void Start()
    {
        fromEntry();
        
        if (ActiveData != null) ActiveLevel = ActiveData.ClampLevel(ActiveLevel);

        if (stageText != null && ActiveData != null)
            stageText.text = $"{ActiveData.displayName}  Lv.{ActiveLevel}";

        if (resultText != null) resultText.gameObject.SetActive(false);

        hpBarRoot = FindFirstObjectByType<HpBar>();

        timeLeft = ActiveData != null ? ActiveData.timeLimit : 60f;
        UpdateTimerUI();

        if (Rule == DailyDungeonRule.Defense)
            StartDefense();
        else
            SpawnBoss();

        UpdateBreachUI();
    }

    void Update()
    {
        if (stageOver) return;

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        UpdateTimerUI();

        if (timeLeft <= 0f)
        {
            // 시간이 다 되면: 보스 규칙은 실패, 디펜스 규칙은 끝까지 막아 낸 것이므로 클리어
            if (Rule == DailyDungeonRule.Defense) StageClear();
            else                                  StageFail("시간 초과");
            return;
        }

        if (Rule == DailyDungeonRule.Defense)
            CheckBreaches();
    }

    // ── 보스 (BossKill) ────────────────────────────

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

        Vector3 spawnPos = HasPath
            ? spawnWaypoints[0].position
            : (bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero);

        DailyDungeonBoss boss = Instantiate(prefab, spawnPos, Quaternion.identity);
        boss.InitForDungeon(ActiveData, ActiveLevel);   // ★ 반드시 호출 — 난이도 스탯 적용
        HapticManager.PlayHeavy();                      // 보스 등장 진동

        GameLog.Info($"[DailyDungeonManager] 보스 스폰 — {prefab.name} / " +
                  $"{(ActiveData != null ? ActiveData.id : "(데이터 없음)")} Lv.{ActiveLevel}");

        TargetMove move = boss.GetComponent<TargetMove>();
        if (move != null && HasPath)
            move.SetupPath(spawnWaypoints);

        hpBarRoot?.RegisterEnemy(boss.gameObject);
    }

    /// <summary>DailyDungeonBoss.ReportKill() 에서 호출 → 보상 지급 후 복귀.</summary>
    public void ReportBossKill()
    {
        if (Rule != DailyDungeonRule.BossKill) return;   // 디펜스 던전은 버티는 게 클리어 조건
        StageClear();
    }

    // ── 디펜스 (Defense) ───────────────────────────

    private void StartDefense()
    {
        if (ActiveData == null || ActiveData.defenseEnemyPrefab == null)
        {
            Debug.LogError("[DailyDungeonManager] 디펜스 규칙인데 DailyDungeonData 의 defenseEnemyPrefab 이 비어 있습니다. " +
                           "적이 나오지 않아 시간만 지나면 클리어됩니다 — 프리팹을 채워주세요.");
            return;
        }
        if (!HasPath)
        {
            Debug.LogError("[DailyDungeonManager] 디펜스 규칙에는 spawnWaypoints 가 필요합니다 (적이 따라갈 경로).");
            return;
        }

        if (breachPoint == null && spawnWaypoints.Length < 2)
            Debug.LogWarning("[DailyDungeonManager] 웨이포인트가 1개뿐이라 도착 지점 = 스폰 위치입니다. breachPoint 를 따로 지정하세요.");

        // 한 주기에 여러 마리를 꺼내므로 넉넉히 예열 (부족분만 채움)
        ObjectPoolManager.Instance?.Prewarm(ActiveData.defenseEnemyPrefab.gameObject,
                                            Mathf.Max(8, ActiveData.enemiesPerSpawn * 6));

        GameLog.Info($"[DailyDungeonManager] 디펜스 시작 — {ActiveData.id} Lv.{ActiveLevel} / " +
                  $"주기 {ActiveData.SpawnIntervalFor(ActiveLevel):F2}s × {ActiveData.enemiesPerSpawn}마리 / " +
                  $"체력 x{ActiveData.EnemyHpMultiplierFor(ActiveLevel):F2} / 침입 허용 {MaxBreaches - 1}회");

        StartCoroutine(DefenseSpawnLoop());
    }

    /// <summary>스폰 루프 — WaitForSeconds(배속 영향)라 일반 스테이지처럼 배속 시 적도 시간도 함께 빨라짐.</summary>
    private IEnumerator DefenseSpawnLoop()
    {
        if (firstSpawnDelay > 0f) yield return new WaitForSeconds(firstSpawnDelay);

        var wait = new WaitForSeconds(ActiveData.SpawnIntervalFor(ActiveLevel));
        while (!stageOver)
        {
            int count = Mathf.Max(1, ActiveData.enemiesPerSpawn);
            for (int i = 0; i < count; i++)
                SpawnDefenseEnemy(GetSpawnOffset(i, count));

            yield return wait;
        }
    }

    /// <summary>원 위에 등각으로 흩뿌린 오프셋 (EnemyRespawn.GetSpawnOffset 과 같은 방식).</summary>
    private Vector2 GetSpawnOffset(int index, int count)
    {
        if (count <= 1 || spawnJitterRadius <= 0f) return Vector2.zero;
        float angle = (360f / count) * index * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnJitterRadius;
    }

    /// <summary>풀에서 꺼내 초기화 후 활성화 (EnemyRespawn.Spawn 과 같은 순서: 꺼내기 → 스탯 → 경로 → SetActive → HP바).</summary>
    private void SpawnDefenseEnemy(Vector2 offset)
    {
        GameObject prefab   = ActiveData.defenseEnemyPrefab.gameObject;
        Vector3    spawnPos = spawnWaypoints[0].position + (Vector3)offset;

        GameObject obj = ObjectPoolManager.Instance != null
            ? ObjectPoolManager.Instance.GetInactive(prefab, spawnPos, Quaternion.identity)
            : InstantiateInactive(prefab, spawnPos);
        if (obj == null) return;

        if (obj.TryGetComponent(out Enemy enemy))
            enemy.OnSpawnFromPool(1f);   // DailyDungeonEnemy 는 배율을 무시하고 던전 데이터로 계산

        if (obj.TryGetComponent(out TargetMove move))
        {
            move.SetPathOffset(offset);
            move.SetupPath(spawnWaypoints);
        }

        obj.SetActive(true);
        hpBarRoot?.RegisterEnemy(obj);
    }

    private static GameObject InstantiateInactive(GameObject prefab, Vector3 pos)
    {
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        obj.SetActive(false);
        return obj;
    }

    /// <summary>도착 지점에 닿은 적을 거리 비교로 찾아 침입 처리 — 경로를 무한히 돌아서 닿는 순간 치우지 않으면 또 들어옴.</summary>
    private void CheckBreaches()
    {
        Vector2 goal = BreachPosition();
        float   r2   = breachRadius * breachRadius;

        // Breach() 가 Active 에서 자신을 빼므로 역순으로 돕니다 (StageManager.ClearAllActiveEnemies 와 같은 이유)
        for (int i = Enemy.Active.Count - 1; i >= 0; i--)
        {
            if (!(Enemy.Active[i] is DailyDungeonEnemy e) || e.isDead) continue;
            if (((Vector2)e.transform.position - goal).sqrMagnitude > r2) continue;

            e.Breach();
            BreachCount++;
            UpdateBreachUI();
            GameLog.Info($"[DailyDungeonManager] 침입! {BreachCount}/{MaxBreaches}");

            if (BreachCount >= MaxBreaches)
            {
                StageFail("방어 실패");
                return;
            }
        }
    }

    private Vector2 BreachPosition()
        => breachPoint != null ? (Vector2)breachPoint.position : (Vector2)spawnWaypoints[spawnWaypoints.Length - 1].position;

    // ── 클리어 / 실패 ──────────────────────────────

    private void StageClear()
    {
        if (stageOver) return;
        stageOver = true;

        string reward = GrantReward();

        OnStageClear?.Invoke();
        GameLog.Info($"[DailyDungeonManager] 클리어 ({Rule}) — {reward}");

        StartCoroutine(ReturnAfterDelay($"클리어!\n{reward}"));
    }

    private void StageFail(string reason)
    {
        if (stageOver) return;
        stageOver = true;

        OnStageFail?.Invoke();
        GameLog.Info($"[DailyDungeonManager] {reason} — 보상 없이 복귀");

        StartCoroutine(ReturnAfterDelay(reason));
    }

    /// <summary>입장 횟수 차감 + 보상 지급 + 클리어 기록 + 저장 (소탕과 같은 경로). 씬을 떠나기 전에 반드시 끝나야 합니다.</summary>
    private string GrantReward()
    {
        if (ActiveData == null) return "";

        // 입장 횟수는 클리어 때만 차감 (입장 UI 가 확인했으므로 보통 실패하지 않음)
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

    // ── UI ─────────────────────────────────────────

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int total = Mathf.FloorToInt(timeLeft);
        if (total == lastTimerSecond) return;
        lastTimerSecond = total;

        int m = Mathf.FloorToInt(timeLeft / 60f);
        int s = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text  = $"{m}:{s:D2}";

        // 보스 규칙은 시간이 줄수록 위험(빨강), 디펜스 규칙은 시간이 줄수록 클리어에 가까움(색 고정)
        timerText.color = Rule == DailyDungeonRule.BossKill && timeLeft <= 15f ? Color.red : Color.white;
    }

    private void UpdateBreachUI()
    {
        if (breachText == null) return;

        bool show = Rule == DailyDungeonRule.Defense;
        breachText.gameObject.SetActive(show);
        if (!show) return;

        breachText.text  = $"침입 {BreachCount} / {MaxBreaches}";
        breachText.color = BreachCount >= MaxBreaches - 1 ? Color.red : Color.white;   // 한 번만 더 뚫리면 실패
    }
}
