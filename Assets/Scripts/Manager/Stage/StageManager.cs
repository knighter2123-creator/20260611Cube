using System;
using UnityEngine;
using TMPro;

// StageManager는 partial로 분리되어 있습니다.
//   StageManager.cs     — 핵심 상태 / 라이프사이클 / 진행 로직
//   StageManager.UI.cs  — UI 갱신 전용
public partial class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("스테이지 설정")]
    [SerializeField] private int   killGoal       = 20;
    [SerializeField] private float timeLimit      = 184f;
    [SerializeField] private float statMultiplier = 1.5f;

    public event Action OnStageClear;
    public event Action OnStageFail;

    public float StatMultiplier => statMultiplier;
    
    // 진화 스테이지 입장 버튼이 현재 위치를 읽을 수 있게 공개
    public int CurrentWorld => currentWorld;
    public int CurrentStage => currentStage;

    private int   killCount   = 0;
    private float timeLeft;
    private bool  bossSpawned = false;
    private bool  stageOver   = false;

    private int   currentWorld     = 1;
    private int   currentStage     = 1;
    private int   maxStagePerWorld = 10;
    private float currentStatMult  = 1f;
    public float CurrentStatMult => currentStatMult;

    void Awake()
    {
        
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 1) 진화 스테이지에서 복귀
        if (EvolveStageContext.HasReturn)
        {
            currentWorld    = EvolveStageContext.ReturnWorld;
            currentStage    = EvolveStageContext.ReturnStage;
            currentStatMult = Mathf.Pow(statMultiplier,
                (currentWorld - 1) * maxStagePerWorld + (currentStage - 1));
            EvolveStageContext.ClearReturn();
            NextStage();
            return;
        }

        // 2) 세이브된 진행도 복원  ← 이게 없으면 항상 1-1에서 시작
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            ApplyFrom(SaveManager.Instance.Current);
            NextStage();
            return;
        }

        // 3) 세이브 없음 — 처음부터
        InitStage();
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

    // ── 외부 호출 ──────────────────────────────────

    public void ReportEnemyKill()
    {
        if (stageOver || bossSpawned) return;

        killCount++;
        UpdateKillUI();

        // ★ 가이드 퀘스트: 적 처치
        GuideQuestManager.Instance?.ReportEnemyKill();

        if (killCount >= killGoal)
        {
            bossSpawned = true;
            EnemyRespawn.Instance.SpawnBoss();
        }
    }

    public void ReportBossKill()
    {
        if (stageOver) return;

        // ★ 보스는 ReportEnemyKill을 거치지 않으므로 여기서 별도 보고
        GuideQuestManager.Instance?.ReportEnemyKill();

        StageClear();
    }

    // ── 내부 진행 ──────────────────────────────────

    private void StageClear()
    {
        stageOver = true;
        OnStageClear?.Invoke();
        Debug.Log($"[StageManager] {currentWorld}-{currentStage} 클리어!");

        // ★ 가이드 퀘스트: 방금 클리어한 스테이지를 보고
        //   ※ 반드시 currentStage++ 이전에! 이후에 넣으면 다음 스테이지를 클리어했다고 보고됨
        GuideQuestManager.Instance?.ReportStageClear(currentWorld, currentStage);

        currentStage++;

        if (currentStage > maxStagePerWorld)
        {
            currentStage = 1;
            currentWorld++;
            Debug.Log($"[StageManager] 월드 변경 → {currentWorld}-{currentStage}");
        }

        currentStatMult *= statMultiplier;
        Debug.Log($"[StageManager] 다음 스테이지: {currentWorld}-{currentStage} / 스탯 배율: {currentStatMult:F4}");

        SaveManager.Instance?.Save();
        NextStage();
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();

        // X-1 실패 → 현재 월드 1스테이지 그대로 재시작
        // X-N 실패 → 한 단계 되돌아감
        if (currentStage == 1)
        {
            currentStatMult = Mathf.Pow(statMultiplier, (currentWorld - 1) * maxStagePerWorld);
            Debug.Log($"[StageManager] {currentWorld}-1 실패 → {currentWorld}-1 재시작 / 스탯 배율: {currentStatMult:F4}");
        }
        else
        {
            currentStage--;
            currentStatMult /= statMultiplier;
            Debug.Log($"[StageManager] 실패 → {currentWorld}-{currentStage} 재시작 / 스탯 배율: {currentStatMult:F4}");
        }

        NextStage();
    }

    private void NextStage()
    {
        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Enemy e = enemy.GetComponent<Enemy>();
            e?.RemoveHpBar();   // HpBar 먼저 제거
            Destroy(enemy);
        }

        InitStage();
        EnemyRespawn.Instance.ResetStage(currentStatMult);
    }

    private void InitStage()
    {
        killCount   = 0;
        timeLeft    = timeLimit;
        bossSpawned = false;
        stageOver   = false;

        UpdateKillUI();
        UpdateTimerUI();
        UpdateStageUI();
    }
}