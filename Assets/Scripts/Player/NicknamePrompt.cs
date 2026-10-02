using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// MainScene 전용 — 특정 스테이지에 도달했는데 아직 닉네임이 없으면 닉네임 설정 팝업을 띄웁니다.
///
/// [흐름]
///   StageManager (스테이지 시작/불러오기 때마다)
///       └─ NicknamePrompt.NotifyStage(world, stage)          ← static: StageManager 는 이 오브젝트를 몰라도 됨
///             └─ 조건 확인: 이름 없음 + 목표 스테이지 이상 + 이번 판(세이브)에 아직 안 띄움
///                   └─ openDelay 만큼 기다림 → 다른 팝업이 게임을 멈춘 동안(timeScale 0)은 계속 기다림
///                         └─ NicknameChangePanel.Open()  (이름이 없으므로 '최초 설정' 모드로 열림)
///
/// [왜 StageManager 에 static 메서드로 알리게 했나]
///   StageManager 가 이 컴포넌트를 [SerializeField] 로 들면, StageManager 가 씬 UI 쪽 스크립트에 묶입니다.
///   static 으로 "지금 몇 스테이지다" 만 알려 두면, StageManager 는 누가 듣는지 몰라도 되고
///   이 오브젝트가 없는 씬(에디터 테스트 등)에서도 아무 문제가 없습니다.
///
/// [팝업을 닫으면('나중에')]
///   ★ [자동 팝업 1회] 이제 앱을 다시 켜도 자동으로는 다시 뜨지 않습니다.
///   "자동 팝업을 띄웠다" 를 세이브(SaveData.nicknamePromptShown)에 기록하기 때문입니다.
///   이후에는 네임플레이트를 눌러 언제든 무료로 정할 수 있습니다 (NicknameChangePanel 이 이름 유무로 모드 결정).
///
/// ★ [도달 전 설정 잠금] 목표 스테이지 도달 "전" 에는 닉네임을 정할 수 없습니다.
///   NicknameChangePanel 이 IsFirstNameUnlocked 를 보고, 잠겨 있으면 입력을 막은 '잠김' 화면으로 엽니다.
///   도달 여부를 판단하는 규칙(목표 월드/스테이지)은 이 컴포넌트 한 곳에만 있습니다.
/// </summary>
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

    // ★ [자동 팝업 1회] 예전의 promptedFor(메모리에만 있던 "이번 실행에서 띄웠나") 는 지웠습니다.
    //   메모리 값은 앱을 끄면 사라져서 재접속하면 다시 떴습니다. 이제 세이브에 기록합니다 (MarkPromptShown).
    //   세이브에 두면 계정 삭제(save.json 삭제) 때 자동으로 함께 지워져, 새 게임에서는 다시 뜹니다.

    // ★ [도달 전 설정 잠금] 지금 씬에서 켜져 있는 NicknamePrompt.
    //   NicknameChangePanel 이 "잠겨 있나" 를 물어볼 때, 목표 스테이지 값(인스펙터)을 가진 인스턴스가 필요합니다.
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

    /// <summary>
    /// 이름이 없는 플레이어가 지금 닉네임을 "처음" 정할 수 있는가 (= 목표 스테이지에 도달했는가).
    /// NicknamePrompt 가 없는 씬에서는 true (잠금 규칙이 없으면 예전처럼 허용 — 다른 씬의 기능을 막지 않기 위해).
    /// </summary>
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

    /// <summary>
    /// StageManager 가 스테이지를 시작할 때마다 부릅니다.
    /// (EnemyRespawn.Instance.ResetStage(...) 를 부르는 모든 곳 바로 옆에 한 줄)
    /// </summary>
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
        // ★ [재검토] 배치 실수 방어
        //   이 컴포넌트를 닉네임 패널 오브젝트(또는 그 아래)에 붙이면, 패널이 시작할 때 자기 자신을 끄면서
        //   이 컴포넌트도 같이 꺼집니다 → OnEnable 이 안 돌아 스테이지 알림을 영영 못 받습니다 (에러도 없음).
        if (panel != null && GetComponentInParent<NicknameChangePanel>(true) == panel)
            Debug.LogError("[NicknamePrompt] 닉네임 패널 안에 붙어 있으면 패널과 함께 꺼져서 동작하지 않습니다. " +
                           "MainScene 의 항상 켜진 오브젝트로 옮겨 주세요.", this);
    }

    private void Start()
    {
        StartCoroutine(WarnIfNoStageNotify());
    }

    /// <summary>
    /// ★ [재검토] 테스트 도우미 — StageManager 에 한 줄(NotifyStage)을 빠뜨리면 팝업이 조용히 안 뜹니다.
    /// 이름 없는 상태로 MainScene 에 들어와 5초가 지나도 알림이 한 번도 없으면 경고를 남깁니다.
    /// (StageManager 가 DontDestroyOnLoad 라면 가챠/상점에서 돌아온 직후에도 뜰 수 있습니다 — 그땐 무시)
    /// </summary>
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

        // MainScene 을 떠날 때 "지금 스테이지" 를 지웁니다.
        // 안 지우면 계정 삭제 후 새 게임의 MainScene 이 켜지는 순간, 옛 게임의 스테이지(예: 5-3)를 보고
        // 1-1 인데도 팝업을 띄웁니다. 다음 MainScene 에서는 StageManager 가 새로 알려 줍니다.
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

        // SaveManager 가 없으면(에디터에서 MainScene 바로 실행) 등록해도 저장할 곳이 없으므로 띄우지 않음.
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null) return false;

        // ★ [자동 팝업 1회] 이 세이브에서 이미 자동으로 띄웠으면 다시 안 띄움 (앱을 다시 켜도 유지)
        if (sm.Current.nicknamePromptShown) return false;

        return true;
    }

    /// <summary>
    /// ★ [자동 팝업 1회] "자동 팝업을 띄웠다" 를 세이브에 기록하고 바로 저장합니다.
    ///
    /// [왜 팝업을 '띄울 때' 기록하나 (취소할 때가 아니라)]
    ///   취소 버튼만 기준으로 하면, 팝업이 떠 있는 채로 앱을 강제 종료한 경우 다음 접속에 또 뜹니다.
    ///   "한 번 보여 줬으면 끝" 이 요청이므로 보여 주는 순간 기록합니다.
    ///
    /// [왜 바로 Save() 하나]
    ///   다음 자동 저장(홈 버튼·종료)까지 기다리면, 그 전에 앱이 죽었을 때 기록이 사라져 재접속 시 또 뜹니다.
    ///   여기는 게임 씬(매니저가 진짜 값을 들고 있음)이라 전체 Save() 가 안전합니다.
    ///   값은 SaveManager.Current 에 직접 넣습니다 — Save() 는 Current 를 바탕으로 매니저 값을 덧씌우므로
    ///   이 필드는 그대로 파일까지 갑니다 (playerName 과 같은 방식).
    /// </summary>
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

        // 다른 팝업이 게임을 멈춘 동안(튜토리얼, 증강 카드 선택 등 timeScale 0)과
        // 닉네임 패널이 이미 열려 있는 동안(네임플레이트를 직접 누른 경우)은 기다립니다.
        // 한 화면에 멈춤 팝업이 둘 겹치면 한쪽을 닫을 때 게임이 멋대로 풀리거나 입력이 막힐 수 있습니다.
        while (Time.timeScale == 0f || (panel != null && panel.IsOpen))
            yield return null;

        pending = null;

        // 기다리는 사이 상황이 바뀌었을 수 있으므로 다시 확인 (예: 그 사이 네임플레이트로 이름을 정함)
        if (!ShouldPrompt()) yield break;

        MarkPromptShown();
        Debug.Log($"[NicknamePrompt] {lastWorld}-{lastStage} 도달, 이름 없음 → 닉네임 설정 팝업 (자동 팝업은 이번 1회)");
        panel.Open();
    }
}