using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 스테이지 진행(킬 카운트 / 제한 시간 / 클리어·실패 / 월드-스테이지 번호) 담당.
///
/// 흐름
///   Start  : 시작 위치 결정 (진화·던전 복귀 → 세이브 → 1-1) 후 스폰 시작
///   진행   : 잡몹 killGoal 마리 처치 → 남은 잡몹을 즉시 치우고(ClearAllActiveEnemies) 보스 등장
///   종료   : 보스 처치 = 클리어(다음 스테이지), 시간 초과 = 실패(한 단계 후퇴)
///            → 알림이 끝나면 NextStage() 로 다음 판 시작
///
/// 나머지 절반은 partial 파일에 있습니다.
///   StageManager.UI.cs     — 킬/타이머/스테이지 텍스트
///   StageManager.notice.cs — 알림 패널, NextStageDelayed
///   StageManager.Save.cs   — CaptureTo / ApplyFrom
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

    // 이전: [SerializeField] statMultiplier — 인스펙터 값(1.3)을 다른 클래스가 읽을 수 없어
    //       IdleRewardManager가 1.5로 복사해 쓰다 어긋났다. 이제 StageFormula에서 읽는다.

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

        //   EnemyRespawn은 "어느 월드의 어느 프리팹을 쓸지"를 StageManager가 알려줘야
        //   스폰을 시작하므로, 신규 시작 경로에서도 반드시 호출해야 합니다.
        //   (이걸 빠뜨리면 1-1에서 적이 한 마리도 안 나옵니다)
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

    /// <summary>
    /// 위치를 바꾸고 스탯 배율을 공식으로 다시 계산합니다.
    /// (곱셈을 누적하면 float 오차가 쌓여 세이브 로드 때와 값이 미세하게 달라지므로 항상 재계산)
    /// </summary>
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

            // ★ 킬카운트 ─────────────────────────────────────────────────
            // 스테이지 카운트(킬 카운트)에 도달한 순간, 아직 화면에 남아 있는
            // 잡몹을 죽이지 않고 즉시 전부 풀로 반환합니다.
            //
            // 왜 "죽이지" 않는가: Enemy.Die()를 부르면 GrantRewards()가 같이 돌아서
            // 남아 있던 마리 수만큼 골드/경험치가 추가로 지급되고, ReportKill()로
            // 킬 카운트/가이드 퀘스트/미션까지 한 번 더 보고됩니다. 그러면 "몇 마리를
            // 잡아야 보스가 나오는가"라는 규칙 자체가 흔들립니다. 그래서 Die()를
            // 거치지 않고 ClearAllActiveEnemies()로 바로 풀에 돌려보냅니다
            // (스테이지 전환 시 잔여 적을 치우는 방식과 완전히 동일합니다).
            //
            // 방금 killGoal을 채운 그 적 자신은 이미 Enemy.Die()에서
            // Active 목록에서 제거된 뒤라(맨 위 Active.Remove(this)),
            // 여기서 다시 건드리지 않습니다 — 이중 반환 걱정이 없습니다.
            // ─────────────────────────────────────────────────────────────
            ClearAllActiveEnemies();

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
        GameLog.Info($"[StageManager] {currentWorld}-{currentStage} 클리어!");

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

        SaveManager.Instance?.Save();
        StartCoroutine(NextStageDelayed());
    }

    private void StageFail()
    {
        stageOver = true;
        OnStageFail?.Invoke();

        // X-1 실패 → 현재 월드 1스테이지 그대로 재시작
        // X-N 실패 → 한 단계 되돌아감
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

        // ★ 증강 — 증강 매니저에도 "지금 몇 스테이지인지" 알려줍니다.
        //   등급 상승 보정(월드가 오를수록 고등급이 잘 나옴)과
        //   '이번 스테이지 동안' 버프 정리에 쓰입니다.
        AugmentManager.Instance?.SetCurrentStage(currentWorld, currentStage);
    }

    /// <summary>
    /// ★ 신규(추출) — 지금 살아있는 일반 적(Enemy.Active)을 전부 즉시 풀로 반환합니다.
    /// Die()를 거치지 않으므로 보상 지급도, 처치 보고(킬 카운트/가이드 퀘스트/미션)도 일어나지 않습니다.
    ///
    /// 원래는 NextStage() 안에만 있던 로직입니다. killGoal 도달 시(ReportEnemyKill)에도
    /// 똑같이 "잡몹을 죽이지 않고 즉시 치운다"가 필요해서 메서드로 뽑아 두 곳에서 재사용합니다.
    /// </summary>
    private void ClearAllActiveEnemies()
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

            // ★ 수정 — Die()를 거치지 않고 강제로 치우기 때문에, "나 죽었다"를 나타내는
            // isDead를 여기서 대신 세워줘야 합니다.
            //
            // 왜 필요한가: 총알(발사체)이 이미 날아가고 있다가 한 프레임 늦게
            // TakeDamage()를 호출하는 경우가 있습니다. 방금 여기서 풀로 반환한 적인데
            // isDead가 여전히 false라면, 그 뒤늦은 TakeDamage() 호출이 (이미 반환되어
            // 대기 중이거나, 심하면 다른 스폰에 재사용된) 같은 오브젝트에 대해 Die()를
            // 한 번 더 실행시킵니다.
            //   → 보상(골드/경험치) 중복 지급, 킬 카운트/미션 중복 집계,
            //     그리고 ObjectPoolManager.Return()이 같은 오브젝트에 대해 두 번 불려
            //     풀 내부 리스트가 꼬이는(이중 반환) 문제로 이어질 수 있습니다.
            // Enemy.TakeDamage() 맨 앞의 `if (isDead) return;` 가드가 이걸 막아주므로,
            // Die()를 부르지 않고 치울 때는 isDead를 직접 세워서 같은 가드를 켜 둡니다.
            e.isDead = true;

            e.RemoveHpBar();
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Return(e.gameObject);
            else
                e.gameObject.SetActive(false);
        }
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