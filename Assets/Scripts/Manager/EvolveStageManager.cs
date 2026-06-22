using System;
using TMPro;
using UnityEngine;

public class EvolveStageManager : MonoBehaviour
{
   
 public static EvolveStageManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("스테이지 설정")]
    [SerializeField] private int   killGoal       = 1;
    [SerializeField] private float timeLimit      = 184f;
    

    public event Action OnStageClear;
    public event Action OnStageFail;

    private int   killCount   = 0;
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

        NextStage();
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();

        Debug.Log("스테이지 실패");        

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

        SceneLoader.Instance.GoToStage();
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

}
