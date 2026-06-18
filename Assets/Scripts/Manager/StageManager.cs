using System;
using UnityEngine;
using TMPro;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("스테이지 설정")]
    [SerializeField] private int   killGoal       = 20;
    [SerializeField] private float timeLimit      = 184f;
    [SerializeField] private float statMultiplier = 1.03f;

    public event Action OnStageClear;
    public event Action OnStageFail;

    private int   killCount   = 0;
    private float timeLeft;
    private bool  bossSpawned = false;
    private bool  stageOver   = false;

    private int   currentWorld     = 1;
    private int   currentStage     = 1;
    private int   maxStagePerWorld = 10;
    private float currentStatMult  = 1f;

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

        // X-1 스테이지 실패 → 월드 처음(X-1)으로 되돌아감 (currentWorld 유지)
        // X-N 스테이지 실패 → 한 단계 되돌아감 (X-(N-1))
        if (currentStage == 1)
        {
            // X-1 실패 : 현재 월드 1스테이지 그대로 재시작 (스탯 배율도 되돌림)
            // currentWorld는 유지, currentStage는 1 유지
            // 현재 월드 시작 시점의 배율로 복구
            currentStatMult = Mathf.Pow(statMultiplier, (currentWorld - 1) * maxStagePerWorld);
            Debug.Log($"[StageManager] {currentWorld}-1 실패 → {currentWorld}-1 재시작 / 스탯 배율: {currentStatMult:F4}");
        }
        else
        {
            // X-N 실패 : 한 스테이지 되돌아감
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