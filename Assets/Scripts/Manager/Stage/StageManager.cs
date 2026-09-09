using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 스테이지 진행(킬 카운트 / 제한 시간 / 클리어·실패 / 월드-스테이지 번호) 담당.
///
/// ★ 이번 수정은 StageClear() 안의 딱 한 줄입니다. "★ 증강" 을 검색하세요.
///
/// 나머지 코드는 원본 그대로입니다.
/// (이 클래스는 partial 이므로, ShowBossNotice / ApplyFrom 등은
///  다른 파일에 있는 나머지 절반에 그대로 남아 있습니다 — 건드릴 필요 없습니다.)
///
/// ─── partial class가 뭔가요? (학습 포인트) ────────────────────────────
/// 하나의 클래스를 여러 파일에 나눠 쓰는 문법입니다. 컴파일할 때 합쳐져서
/// 완전히 같은 하나의 클래스가 돼요. Enemy.cs / Enemy.Debuffs.cs 처럼
/// "핵심 로직"과 "부가 기능"을 나눠두면 파일이 짧아져 읽기 쉬워집니다.
/// 단, 같은 클래스이므로 필드 이름이 겹치면 컴파일 오류가 납니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
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
            NextStage();          // ← 내부에서 ResetStage를 부르므로 프리팹도 함께 결정됨
            return;
        }

        // 2) 세이브된 진행도 복원  ← 이게 없으면 항상 1-1에서 시작
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            ApplyFrom(SaveManager.Instance.Current);
            NextStage();          // ← 여기도 마찬가지
            return;
        }

        // 3) 세이브 없음 — 처음부터
        InitStage();

        //   기존에는 InitStage()만 부르고 끝냈습니다. 예전 EnemyRespawn은
        //   Start()에서 스스로 스폰 루프를 돌렸기 때문에 그래도 적이 나왔죠.
        //   이제는 "어느 월드의 어느 프리팹을 쓸지"를 StageManager가 알려줘야
        //   스폰이 시작되므로, 신규 시작 경로에서도 반드시 호출해야 합니다.
        //   (이걸 빠뜨리면 1-1에서 적이 한 마리도 안 나옵니다)
        NotifyRespawner();
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

            // ★ 보스 출현 알림
            ShowBossNotice();

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

        // ※ 아래 세 줄은 반드시 currentStage++ 이전에!
        GuideQuestManager.Instance?.ReportStageClear(currentWorld, currentStage);
        ShowStageClearNotice();          // ← 여기로 이동, 파라미터 불필요

        // ★ 증강 ─────────────────────────────────────────────────────────
        //
        // 이 위치가 중요합니다. 바로 아래 currentStage++ 가 실행되고 나면
        // currentStage 는 이미 "다음 스테이지" 번호(1-10 클리어 → 2-1)라서,
        // 그 값을 넘기면 조건(stage == 10)에 걸리지 않아 카드가 영영 안 뜹니다.
        // 반드시 '방금 클리어한' 번호를 넘겨야 합니다.
        //
        // AugmentManager 는 openDelay(기본 1초) 만큼 기다렸다가 창을 엽니다.
        // 클리어 알림 연출을 잠깐 보여주고 카드를 띄우기 위해서입니다.
        // 창이 열리는 순간 Time.timeScale 이 0이 되므로,
        // 아래 NextStageDelayed() 코루틴의 대기도 함께 멈춥니다.
        // → 플레이어가 카드를 고르기 전에 다음 스테이지가 시작되는 일이 없습니다.
        //   (WaitForSeconds 는 timeScale 의 영향을 받는다는 성질을 이용한 것입니다.
        //    만약 그 코루틴이 WaitForSecondsRealtime 을 쓴다면 멈추지 않으니,
        //    openDelay 를 0으로 두거나 전환 대기시간을 늘려주세요.)
        // ────────────────────────────────────────────────────────────────
        AugmentManager.Instance?.OnStageCleared(currentWorld, currentStage);

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
        StartCoroutine(NextStageDelayed());
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

        ShowStageFailNotice();
        StartCoroutine(NextStageDelayed());
    }

    private void NextStage()
    {
        // OnDisable에서 자기 자신을 제거하므로 역순 순회
        //
        // ─── 왜 역순인가? (학습 포인트) ─────────────────────────────
        // 앞에서부터 돌면서 원소를 지우면, 지운 자리로 뒤 원소가 당겨오면서
        // 한 칸씩 건너뛰게 됩니다. 뒤에서부터 지우면 아직 방문하지 않은
        // 앞쪽 인덱스가 흔들리지 않아 안전합니다.
        // ────────────────────────────────────────────────────────
        var list = Enemy.Active;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Enemy e = list[i];
            if (e == null) continue;

            e.RemoveHpBar();
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Return(e.gameObject);
            else
                e.gameObject.SetActive(false);
        }

        InitStage();

        //   월드·스테이지 번호를 함께 넘겨 프리팹과 속도 배율까지 갱신합니다.
        NotifyRespawner();

        // ★ 증강 — 증강 매니저에도 "지금 몇 스테이지인지" 알려줍니다.
        //   등급 상승 보정(월드가 오를수록 고등급이 잘 나옴)과
        //   '이번 스테이지 동안' 버프 정리에 쓰입니다.
        AugmentManager.Instance?.SetCurrentStage(currentWorld, currentStage);
    }

    /// <summary>
    /// ★ 신규 — EnemyRespawn에게 "이번 스테이지 정보"를 전달하는 창구.
    ///
    /// 호출하는 곳이 두 군데(Start의 3번 분기, NextStage)라서 함수로 뺐습니다.
    /// 같은 코드를 두 번 쓰면 나중에 한쪽만 고치는 실수가 반드시 생깁니다.
    /// (DRY 원칙 — Don't Repeat Yourself)
    /// </summary>
    private void NotifyRespawner()
    {
        if (EnemyRespawn.Instance == null)
        {
            // ?. 대신 명시적으로 검사하고 경고를 남깁니다.
            // 조용히 넘어가면 "적이 안 나오는데 이유를 모르겠는" 상황이 되니까요.
            Debug.LogError("[StageManager] EnemyRespawn.Instance가 없습니다. " +
                           "씬에 EnemyRespawn이 있는지, 실행 순서가 StageManager보다 앞인지 확인하세요.");
            return;
        }

        EnemyRespawn.Instance.ResetStage(currentStatMult, currentWorld, currentStage);
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