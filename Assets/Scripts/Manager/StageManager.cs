using System;
using UnityEngine;
using TMPro;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stageText;      // "1-1", "1-2" 표시용

    [Header("스테이지 설정")]
    [SerializeField] private int   killGoal       = 20;
    [SerializeField] private float timeLimit      = 184f;
    [SerializeField] private float statMultiplier = 1.03f;   // 스테이지당 스탯 배율

    public event Action OnStageClear;
    public event Action OnStageFail;

    private int   killCount   = 0;
    private float timeLeft;
    private bool  bossSpawned = false;
    private bool  stageOver   = false;

    private int   currentWorld = 1;   // "1-X"의 앞자리
    private int   currentStage = 1;   // "1-X"의 뒷자리
    private int   maxStagePerWorld = 10; // ✅ 월드당 최대 스테이지 수
    private float currentStatMult = 1f; // 누적 스탯 배율

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
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

        if (killCount >= killGoal)
        {
            bossSpawned = true;
            EnemyRespawn.Instance.SpawnBoss();
        }
    }

    public void ReportBossKill()
    {
        if (stageOver) return;
        StageClear();
    }

    // ── 내부 ───────────────────────────────────────

    private void StageClear()
    {
        stageOver = true;
        OnStageClear?.Invoke();
        Debug.Log($"[StageManager] {currentWorld}-{currentStage} 클리어!");

        currentStage++;

        // ✅ 현재 스테이지가 최대치를 초과하면 다음 월드 1스테이지로
        if (currentStage > maxStagePerWorld)
        {
            currentStage = 1;
            currentWorld++;
            Debug.Log($"[StageManager] 월드 변경 → {currentWorld}-{currentStage}");
        }

        currentStatMult *= statMultiplier;
        Debug.Log($"[StageManager] 다음 스테이지: {currentWorld}-{currentStage} / 스탯 배율: {currentStatMult:F4}");

        NextStage();
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();
        Debug.Log("[StageManager] 시간 초과 — 스테이지 실패");
    }

    private void NextStage()
    {
        // ✅ 기존 Enemy 전부 제거
        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            Destroy(enemy);

        // ✅ 스테이지 변수 초기화
        InitStage();

        // ✅ EnemyRespawn 리셋 (새 스테이지 스폰 시작)
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

    private void UpdateKillUI()
    {
        if (killCountText != null)
            killCountText.text = $"처치  {killCount} / {killGoal}";
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(timeLeft / 60f);
        int s = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text  = $"{m}:{s:D2}";
        timerText.color = timeLeft <= 30f ? Color.red : Color.white;
    }

    private void UpdateStageUI()
    {
        if (stageText != null)
            stageText.text = $"{currentWorld}-{currentStage}";
    }
}