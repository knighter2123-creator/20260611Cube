using System;
using System.Collections;
using UnityEngine;

/// <summary>MainScene 닉네임 자동 팝업: StageManager 가 static NotifyStage 로 위치를 알리면, 이름 없음 + 목표 스테이지 도달 + 아직 안 띄움일 때 다른 팝업이 끝나길 기다려 1회만 띄움 (세이브에 기록).</summary>
// static 알림이라 StageManager 는 이 컴포넌트를 몰라도 됨 / 목표 도달 전엔 IsFirstNameUnlocked=false 로 닉네임 패널이 잠김 화면 — 도달 규칙은 여기 한 곳
public class NicknamePrompt : MonoBehaviour
{
    [SerializeField] private NicknameChangePanel panel;

    [Header("도달 조건 (이 스테이지 이상이면 띄움)")]
    [Tooltip("예: 월드 1, 스테이지 5 → 1-5 에 들어선 순간(또는 그 이후 스테이지를 불러왔을 때)")]
    [SerializeField] private int requiredWorld = 1;
    [SerializeField] private int requiredStage = 5;

    [Header("연출")]
    [Tooltip("스테이지 전환 연출이 끝난 뒤 열리도록 잠깐 기다립니다 (실제 시간 기준 — 배속/일시정지 영향 없음)")]
    [SerializeField] private float openDelay = 1.5f;

    // ───────── static: StageManager 가 알려 준 "지금 스테이지" ─────────

    private static bool hasStage;
    private static int  lastWorld;
    private static int  lastStage;
    private static event Action StageNotified;

    // '띄웠는지'는 메모리가 아닌 세이브에 기록 (재접속 때 다시 안 뜨고, 계정 삭제 땐 함께 지워짐)

    // 지금 켜진 인스턴스 — 패널이 잠김 여부를 물을 때 목표 스테이지 값을 읽음
    private static NicknamePrompt active;

    // "StageManager 에 NotifyStage 줄을 안 넣었다" 는 경고를 앱 실행 중 한 번만 띄우기 위한 표시
    private static bool warnedNoNotify;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasStage = false;
        lastWorld = lastStage = 0;
        StageNotified = null;
        active = null;
        warnedNoNotify = false;
    }

    // ───────── ★ [도달 전 설정 잠금] 외부(NicknameChangePanel)에서 묻는 곳 ─────────

    /// <summary>지금 처음 이름을 정할 수 있는가(목표 스테이지 도달) — 이 컴포넌트가 없는 씬에선 true (다른 씬 기능을 막지 않게).</summary>
    public static bool IsFirstNameUnlocked
    {
        get
        {
            if (active == null) return true;
            return hasStage && active.IsReached(lastWorld, lastStage);
        }
    }

    /// <summary>잠겨 있을 때 보여 줄 안내 문구. 목표 스테이지 숫자를 인스펙터 값에서 만들어 두 곳에 숫자가 따로 적히지 않게 합니다.</summary>
    public static string LockedMessage =>
        active != null
            ? $"{active.requiredWorld}-{active.requiredStage} 스테이지에 도달하면\n닉네임을 정할 수 있습니다"
            : "아직 닉네임을 정할 수 없습니다";

    /// <summary>StageManager 가 스테이지를 시작할 때마다 호출 (ResetStage 옆 한 줄).</summary>
    public static void NotifyStage(int world, int stage)
    {
        hasStage  = true;
        lastWorld = world;
        lastStage = stage;
        StageNotified?.Invoke();
    }

    // ───────── 인스턴스 ─────────

    private Coroutine pending;   // 기다리는 중인 팝업 (중복 예약 방지)

    private void Awake()
    {
        // 배치 실수 방어 — 패널 안에 붙으면 패널과 함께 꺼져 알림을 조용히 못 받음
        if (panel != null && GetComponentInParent<NicknameChangePanel>(true) == panel)
            Debug.LogError("[NicknamePrompt] 닉네임 패널 안에 붙어 있으면 패널과 함께 꺼져서 동작하지 않습니다. " +
                           "MainScene 의 항상 켜진 오브젝트로 옮겨 주세요.", this);
    }

    private void Start()
    {
        StartCoroutine(WarnIfNoStageNotify());
    }

    /// <summary>5초 동안 스테이지 알림이 없으면 경고 — StageManager 의 NotifyStage 한 줄이 빠지면 팝업이 조용히 안 뜨므로.</summary>
    private IEnumerator WarnIfNoStageNotify()
    {
        yield return new WaitForSecondsRealtime(5f);
        if (!hasStage && !warnedNoNotify && !PlayerProfile.HasName && SaveManager.Instance != null)
        {
            warnedNoNotify = true;
            Debug.LogWarning("[NicknamePrompt] 5초 동안 StageManager 의 스테이지 알림이 없습니다. " +
                             "StageManager 에서 EnemyRespawn.Instance.ResetStage(...) 옆에 " +
                             "NicknamePrompt.NotifyStage(currentWorld, currentStage); 를 넣었는지 확인하세요.", this);
        }
    }

    private void OnEnable()
    {
        active = this;   // ★ [도달 전 설정 잠금]
        StageNotified += TryPrompt;
        TryPrompt();   // 구독 전에 이미 알림이 왔을 수 있으므로 한 번 확인 (이벤트 기반의 기본 짝)
    }

    private void OnDisable()
    {
        if (active == this) active = null;   // ★ 나를 가리킬 때만 비움 (다른 인스턴스를 지우지 않게 — 싱글턴 OnDestroy 와 같은 패턴)
        StageNotified -= TryPrompt;
        pending = null;   // 비활성화되면 코루틴은 유니티가 알아서 멈춤 → 참조만 비움

        // 떠날 때 '지금 스테이지'를 지움 (안 지우면 계정 삭제 후 새 게임 1-1 에서 옛 스테이지를 보고 팝업)
        hasStage = false;
    }

    private void TryPrompt()
    {
        if (pending != null) return;      // 이미 열 예정
        if (!ShouldPrompt()) return;
        pending = StartCoroutine(OpenWhenFree());
    }

    private bool ShouldPrompt()
    {
        if (panel == null) return false;
        if (PlayerProfile.HasName) return false;                       // 이미 이름 있음
        if (!hasStage || !IsReached(lastWorld, lastStage)) return false;

        // SaveManager 가 없으면 저장할 곳이 없어 안 띄움
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null) return false;

        // ★ [자동 팝업 1회] 이 세이브에서 이미 자동으로 띄웠으면 다시 안 띄움 (앱을 다시 켜도 유지)
        if (sm.Current.nicknamePromptShown) return false;

        return true;
    }

    /// <summary>'자동 팝업을 띄웠다'를 보여 주는 순간 세이브에 기록하고 바로 저장 — 취소 기준이면 강제 종료 시 또 뜸, 게임 씬이라 전체 Save 가 안전.</summary>
    private static void MarkPromptShown()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null) return;

        sm.Current.nicknamePromptShown = true;
        sm.Save();
    }

    private bool IsReached(int world, int stage)
    {
        // 월드가 더 크면 무조건 도달, 같은 월드면 스테이지 비교
        return world > requiredWorld || (world == requiredWorld && stage >= requiredStage);
    }

    private IEnumerator OpenWhenFree()
    {
        // WaitForSecondsRealtime: timeScale 영향을 받지 않음 (배속 3배여도 1.5초, 일시정지여도 흐름)
        yield return new WaitForSecondsRealtime(openDelay);

        // 다른 멈춤 팝업(timeScale 0)이나 닉네임 패널이 열려 있는 동안 대기 (겹치면 닫을 때 게임이 멋대로 풀림)
        while (Time.timeScale == 0f || (panel != null && panel.IsOpen))
            yield return null;

        pending = null;

        // 기다리는 사이 이름을 정했을 수 있어 다시 확인
        if (!ShouldPrompt()) yield break;

        MarkPromptShown();
        GameLog.Info($"[NicknamePrompt] {lastWorld}-{lastStage} 도달, 이름 없음 → 닉네임 설정 팝업 (자동 팝업은 이번 1회)");
        panel.Open();
    }
}