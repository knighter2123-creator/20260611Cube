using System;
using UnityEngine;
using TMPro;

/// <summary>스테이지 진행: 시작 위치(복귀 → 세이브 → 1-1) 결정 → 잡몹 killGoal 처치 시 남은 잡몹을 치우고 보스 등장 → 보스 처치 = 클리어(다음), 시간 초과 = 실패(한 단계 후퇴).</summary>
// partial 구성: .UI(킬·타이머·스테이지 텍스트) / .notice(알림·NextStageDelayed) / .Save(CaptureTo·ApplyFrom)
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

    // 스탯 배율은 StageFormula 한 곳에서 (인스펙터 값이면 다른 클래스가 복사해 쓰다 어긋남)

    public event Action OnStageClear;
    public event Action OnStageFail;

    public float StatMultiplier => StageFormula.StatMultiplier;

    // 진화 스테이지 입장 버튼이 현재 위치를 읽을 수 있게 공개
    public int CurrentWorld => currentWorld;
    public int CurrentStage => currentStage;

    private int   killCount   = 0;
    private float timeLeft;
    private bool  bossSpawned = false;
    private bool  stageOver   = false;

    private int   currentWorld     = 1;
    private int   currentStage     = 1;
    private float currentStatMult  = 1f;
    public float CurrentStatMult => currentStatMult;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 시작 위치는 우선순위대로 ① 진화 스테이지·일일 던전 복귀 → ② 세이브 → ③ 처음(1-1)
        if (TryRestoreReturnPoint() || TryRestoreSave())
        {
            NextStage();          // ← 내부에서 ResetStage를 부르므로 프리팹도 함께 결정됨
            return;
        }

        // ③ 세이브 없음 — 처음부터
        InitStage();

        // 신규 시작에서도 반드시 알림 (EnemyRespawn 은 알려 줘야 스폰을 시작해서, 빠지면 1-1 에 적이 안 나옴)
        NotifyRespawner();
    }

    /// <summary>진화 스테이지 / 일일 던전에서 돌아왔다면 원래 위치로 되돌립니다.</summary>
    private bool TryRestoreReturnPoint()
    {
        if (EvolveStageContext.HasReturn)
        {
            SetPosition(EvolveStageContext.ReturnWorld, EvolveStageContext.ReturnStage);
            EvolveStageContext.ClearReturn();
            return true;
        }

        if (DailyDungeonContext.HasReturn)
        {
            SetPosition(DailyDungeonContext.ReturnWorld, DailyDungeonContext.ReturnStage);
            DailyDungeonContext.ClearReturn();
            return true;
        }

        return false;
    }

    /// <summary>세이브된 진행도 복원. ← 이게 없으면 항상 1-1에서 시작</summary>
    private bool TryRestoreSave()
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.HasSave()) return false;

        ApplyFrom(SaveManager.Instance.Current);
        return true;
    }

    /// <summary>위치를 바꾸고 스탯 배율을 공식으로 재계산 (곱셈 누적은 float 오차로 로드 때와 값이 달라짐).</summary>
    private void SetPosition(int world, int stage)
    {
        currentWorld    = world;
        currentStage    = stage;
        currentStatMult = StageFormula.StatMult(world, stage);
    }

    void Update()
    {
        if (stageOver) return;

        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        UpdateTimerUI();

        if (timeLeft <= 0f)
            StageFail();
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

            // 남은 잡몹은 Die() 없이 즉시 풀로 반환 — Die() 면 보상·킬 보고가 추가로 일어나 규칙이 흔들림 (방금 죽은 적은 이미 Active 에서 빠짐)
            ClearAllActiveEnemies();

            // ★ 보스 출현 알림
            ShowBossNotice();

            if (EnemyRespawn.Instance != null)
                EnemyRespawn.Instance.SpawnBoss();
            else
                Debug.LogError("[StageManager] EnemyRespawn.Instance 가 없어 보스를 소환하지 못했습니다. 씬에 EnemyRespawn 이 있는지 확인하세요.");
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
        GameLog.Info($"[StageManager] {currentWorld}-{currentStage} 클리어!");

        // ※ 아래 세 줄은 반드시 currentStage++ 이전에!
        GuideQuestManager.Instance?.ReportStageClear(currentWorld, currentStage);
        ShowStageClearNotice();          // ← 여기로 이동, 파라미터 불필요

        // ★ 증강 — 반드시 '방금 클리어한' 번호로 (++ 후면 10 이 아니라 다음 번호라 카드가 영영 안 뜸)
        //   창이 열리면 timeScale 0 → NextStageDelayed 의 WaitForSeconds 도 멈춰 고르기 전엔 다음 판이 안 시작됨 (Realtime 으로 바꾸면 깨짐)
        AugmentManager.Instance?.OnStageCleared(currentWorld, currentStage);

        // ★ 랭킹 — 최고 클리어 기록 갱신 (마찬가지로 currentStage++ 이전, 아래 Save() 에 함께 기록됨)
        StageRecord.ReportClear(currentWorld, currentStage);

        if (currentStage < StageFormula.MaxStagePerWorld)
        {
            SetPosition(currentWorld, currentStage + 1);
        }
        else
        {
            SetPosition(currentWorld + 1, 1);
            GameLog.Info($"[StageManager] 월드 변경 → {currentWorld}-{currentStage}");
        }

        GameLog.Info($"[StageManager] 다음 스테이지: {currentWorld}-{currentStage} / 스탯 배율: {currentStatMult:F4}");

        // 저장은 잠시 뒤로 미룸 — 이 프레임엔 클리어 연출·증강·랭킹이 몰려 있어 동기 직렬화·파일 쓰기까지 겹치면 튐
        if (SaveManager.Instance != null) SaveManager.Instance.RequestSave();
        StartCoroutine(NextStageDelayed());
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();

        // 실패 시 한 단계 후퇴 (X-1 은 그대로 재시작)
        SetPosition(currentWorld, Mathf.Max(1, currentStage - 1));
        GameLog.Info($"[StageManager] 실패 → {currentWorld}-{currentStage} 재시작 / 스탯 배율: {currentStatMult:F4}");

        ShowStageFailNotice();
        StartCoroutine(NextStageDelayed());
    }

    private void NextStage()
    {
        // ★ 잔여 적 정리 — ClearAllActiveEnemies()로 추출 (아래 참고)
        ClearAllActiveEnemies();

        InitStage();

        //   월드·스테이지 번호를 함께 넘겨 프리팹과 속도 배율까지 갱신합니다.
        NotifyRespawner();

        // 증강 매니저에 현재 위치 알림 (월드별 등급 보정용)
        AugmentManager.Instance?.SetCurrentStage(currentWorld, currentStage);
    }

    /// <summary>살아 있는 일반 적을 Die() 없이 즉시 풀로 반환(보상·처치 보고 없음) — 스테이지 전환과 killGoal 도달 두 곳에서 사용.</summary>
    private void ClearAllActiveEnemies()
    {
        // OnDisable 에서 목록에서 빠지므로 역순 순회
        var list = Enemy.Active;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Enemy e = list[i];
            if (e == null) continue;

            // isDead 를 직접 세움 — 늦게 도착한 총알의 TakeDamage 가 반환된 적에 Die() 를 또 실행해 보상 중복·풀 이중 반환이 생기지 않게
            e.isDead = true;

            e.RemoveHpBar();
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Return(e.gameObject);
            else
                e.gameObject.SetActive(false);
        }
    }

    /// <summary>EnemyRespawn 에 이번 스테이지 정보(배율·월드·스테이지)를 전달하는 단일 창구 (두 곳에서 호출).</summary>
    private void NotifyRespawner()
    {
        if (EnemyRespawn.Instance == null)
        {
            // 조용히 넘어가면 적이 안 나오는 원인을 못 찾아 에러로 알림
            Debug.LogError("[StageManager] EnemyRespawn.Instance가 없습니다. " +
                           "씬에 EnemyRespawn이 있는지, 실행 순서가 StageManager보다 앞인지 확인하세요.");
            return;
        }

        EnemyRespawn.Instance.ResetStage(currentStatMult, currentWorld, currentStage);
        
        NicknamePrompt.NotifyStage(currentWorld, currentStage);
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
        ShowStageStartNotice();
    }
}