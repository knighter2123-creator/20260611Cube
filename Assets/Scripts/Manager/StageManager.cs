using System;
using UnityEngine;
using TMPro;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;  // "처치 0 / 20"
    [SerializeField] private TextMeshProUGUI timerText;      // "1:59"

    [Header("스테이지 설정")]
    [SerializeField] private int killGoal    = 20;
    [SerializeField] private float timeLimit = 124f;         // 2분

    public event Action OnStageClear;
    public event Action OnStageFail;

    private int   killCount  = 0;
    private float timeLeft;
    private bool  bossSpawned = false;
    private bool  stageOver   = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        timeLeft = timeLimit;
        UpdateKillUI();
        UpdateTimerUI();
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

    /// <summary>Enemy 사망 시 호출</summary>
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

    /// <summary>Boss 사망 시 호출</summary>
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
        Debug.Log("[StageCount] 스테이지 클리어!");
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();
        Debug.Log("[StageCount] 시간 초과 — 스테이지 실패");
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
        timerText.text = $"{m}:{s:D2}";

        // 30초 이하 빨간색 경고
        timerText.color = timeLeft <= 30f ? Color.red : Color.white;
    }
}