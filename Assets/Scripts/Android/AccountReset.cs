using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;   // 입력 차단막(Image, GraphicRaycaster)

/// <summary>
/// '계정 삭제' — 플레이어 진행 데이터를 지우고 게임을 "막 설치한 상태" 로 다시 시작합니다.
///
/// [사용법] (GameSettingManager.AccountReset.cs 가 이 순서로 부릅니다)
///   ① if (!AccountReset.TryWipe("LoginScene", out string error)) { 실패 안내; return; }
///   ② (호출한 쪽이 자기 UI 정리 — 패널 닫기, 리스너 해제 등)
///   ③ AccountReset.RestartFromLogin();
///
/// [지우는 것] 게임 진행만
///   - save.json (레벨, 강화, 스테이지, 재화, 동료, 미션, 가이드 퀘스트, 이름, 튜토리얼 완료 여부, 방치 보상 시각)
///   - PlayerPrefs: 증강 카드 스택, 예전 방식 이름(PlayerName)
/// [남기는 것] 기기 설정
///   - 블룸 강도/토글, 화면 번쩍임, 진동 on/off
///   - UI 편의값: 탭 창 마지막 탭, 강화 배수(×1/×10/×100)
///
/// ─────────────────────────────────────────────────────────
/// [왜 파일만 지우면 안 되는가]
///   매니저들은 DontDestroyOnLoad 로 살아 있으면서 옛 값을 메모리에 들고 있습니다.
///   파일을 지워도 다음 Save() 한 번에 그 값이 고스란히 다시 기록됩니다.
///   그래서 "저장 잠금 → 데이터 삭제 → 매니저를 전부 새로 만들기" 세 단계가 한 세트입니다.
///
/// [왜 매니저를 '되돌리지' 않고 '새로 만드는가']
///   매니저마다 ApplyFrom(new SaveData()) 를 부르는 방법도 있지만,
///   SaveData 에 없는 런타임 상태(배치된 동료 오브젝트, 진행 중 코루틴, 캐시, 이벤트 구독)는 그대로 남습니다.
///   매니저를 새로 만들면 "앱을 처음 켰을 때" 와 완전히 같은 경로를 타므로 빠뜨릴 게 없습니다.
///   이미 검증된 경로(첫 실행)를 재사용하는 것이 새 경로를 만드는 것보다 안전합니다.
///
/// [순서 — 이게 이 파일의 전부입니다]
///   ① 저장 잠금             SaveManager.LockForReset()
///   ② 데이터 삭제           save.json + PlayerPrefs(진행 데이터만)
///   (+) 입력 차단막         RestartFromLogin() 순간부터 모든 터치 차단
///   ③ 게임 씬 내리기       빈 임시 씬으로 옮긴 뒤 MainScene 을 언로드   ← 매니저가 "살아 있는" 상태에서
///   ④ 매니저 전부 파괴      DontDestroyOnLoad 에 있는 게임 오브젝트 전부
///   ⑤ LoginScene 새로 로드  새 ManagerRoot / SaveManager 가 "세이브 없음" 으로 시작 → 잠금 자동 해제
///
///   ★ ③ 과 ④ 의 순서가 중요합니다.
///     매니저를 먼저 파괴하면, MainScene 오브젝트들이 언로드되면서 OnDisable/OnDestroy 에서
///     이미 죽은 매니저를 부릅니다 (예: GameSettingManager.OnDisable → GameManager.Instance?.Unregister...).
///     ?. 는 "파괴됨" 을 검사하지 못하므로 죽은 오브젝트의 메서드가 실행되고 MissingReferenceException 이 날 수 있습니다.
///     MainScene 을 먼저 내리면, 그 정리는 평소 '로그인 화면으로 돌아가기' 와 똑같은 조건(매니저 생존)에서 일어납니다.
///
///   ★ ④ 와 ⑤ 사이에 한 프레임을 쉬는 이유
///     Destroy() 는 즉시 지우지 않고 프레임 끝에 지웁니다. 그 전에 LoginScene 이 로드되면
///     새 ManagerRoot 의 Awake 가 "기존 루트가 아직 있네" 하고 자기 자신을 지워 버립니다.
///     (ManagerRoot.Awake 의 중복 제거 규칙) 한 프레임 쉬면 옛 루트의 OnDestroy 가 Instance 를 비운 뒤라 안전합니다.
///
/// [계정 전환에도 재사용]
///   "매니저를 전부 새로 만들고 LoginScene 부터 다시" 는 계정 전환에도 똑같이 필요합니다
///   (매니저들이 이전 계정의 레벨·골드를 메모리에 들고 있으므로).
///   TryBeginAccountSwitch 는 TryWipe 의 "지우지 않는" 버전 — 저장 잠금만 걸고 같은 RestartFromLogin 을 탑니다.
///   TryWipe 가 지우는 세이브·증강은 "지금 계정" 의 것만입니다 (SaveManager / AugmentManager 쪽에서 처리).
/// </summary>
public class AccountReset : MonoBehaviour
{
    /// <summary>초기화가 진행 중인가. 설정 패널이 버튼·뒤로가기를 막을 때 씁니다.</summary>
    public static bool IsRunning { get; private set; }

    private static string pendingLoginScene;
    private static string pendingReason;   // 로그용: "계정 삭제" / "계정 전환"

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Reload Domain 을 끈 에디터에서 이전 플레이의 값이 남지 않게 (SaveManager 와 같은 이유)
        IsRunning = false;
        pendingLoginScene = null;
        pendingReason = null;
    }

    // ═════════════════════════════════════════════════════════
    //  1단계 — 지우기 (동기, 실패할 수 있음)
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// 저장을 잠그고 진행 데이터를 지웁니다. 실패하면 아무것도 바꾸지 않은 채(잠금 해제) false 를 돌려줍니다.
    ///
    /// ★ 왜 '지우기' 와 '다시 시작' 을 두 함수로 나눴나
    ///   지우기는 실패할 수 있습니다(저장소 오류). 실패했는데 이미 씬을 내리고 있으면 되돌릴 방법이 없습니다.
    ///   그래서 "실패할 수 있는 일" 을 먼저 끝내고, 성공이 확정된 뒤에만 "되돌릴 수 없는 일" 을 시작합니다.
    ///   호출한 쪽은 그 사이에 자기 UI 를 정리할 수 있습니다.
    /// </summary>
    public static bool TryWipe(string loginSceneName, out string error)
    {
        // ① 저장 잠금 — 반드시 삭제보다 먼저. 순서가 바뀌면 지운 직후 끼어든 Save() 가 파일을 되살립니다.
        if (!TryLockForRestart(loginSceneName, "계정 삭제", out error)) return false;

        // ② 세이브 파일 삭제
        if (!SaveManager.TryDeleteSaveFile(out string fileError))
        {
            // 파일이 그대로 남아 있으므로 게임을 그대로 계속하면 됩니다. 잠금만 풀어 줍니다.
            CancelRestart();
            error = "데이터를 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            Debug.LogError($"[AccountReset] 세이브 파일 삭제 실패로 초기화를 중단합니다: {fileError}");
            return false;
        }

        // ② PlayerPrefs 의 진행 데이터 삭제 (설정값은 건드리지 않음)
        AugmentManager.DeleteAllSavesForReset();   // 증강 카드 스택
        PlayerProfile.DeleteLegacyPrefsForReset(); // 예전 방식 이름 — 안 지우면 LoginScene 에서 옛 이름이 이관돼 되살아남
        PlayerPrefs.Save();                        // PlayerPrefs 는 Save() 전까지 메모리에만 있음 → 바로 앱이 꺼져도 안전하게

        Debug.Log("[AccountReset] 진행 데이터 삭제 완료. RestartFromLogin() 으로 매니저를 새로 만듭니다.");
        return true;
    }

    /// <summary>
    /// 계정 전환용 1단계 — TryWipe 와 같지만 아무것도 지우지 않습니다.
    /// 저장 잠금만 걸어서, 매니저들이 파괴되는 동안 "이전 계정 값" 이 새 계정 파일에 기록되지 않게 합니다.
    /// (호출한 쪽은 잠금 뒤에 SaveManager.SetActiveAccount(새 계정) → RestartFromLogin() 순서로 부릅니다)
    ///
    /// [왜 잠금이 꼭 필요한가]
    ///   계정을 바꾼 순간부터 SavePath 는 새 계정 파일을 가리킵니다. 그런데 매니저들은 아직 이전 계정 값을 들고 있어서,
    ///   씬을 내리는 동안 누가 Save() 를 한 번만 불러도(OnDestroy, 홈 버튼) 이전 계정 진행이 새 계정 파일로 들어갑니다.
    ///   잠금은 새 SaveManager 가 태어날 때(= 이전 매니저가 모두 사라졌을 때) 풀립니다 — 계정 삭제와 같은 원리.
    /// </summary>
    public static bool TryBeginAccountSwitch(string loginSceneName, out string error)
        => TryLockForRestart(loginSceneName, "계정 전환", out error);

    /// <summary>
    /// 재시작 공통 준비 — 진행 중 검사 → 씬 확인 → 저장 잠금 → 대기 상태 기록.
    ///
    /// ★ 로드할 수 없는 씬 이름이면 "되돌릴 수 없는 일을 시작하기 전에" 멈춥니다.
    ///   지운 뒤에 로드가 실패하면 빈 화면에 갇힌 채 데이터만 사라집니다.
    ///   (Build Settings 의 Scenes In Build 에 없거나 이름 오타)
    /// </summary>
    private static bool TryLockForRestart(string loginSceneName, string reason, out string error)
    {
        error = string.Empty;

        if (IsRunning)
        {
            error = "이미 초기화가 진행 중입니다.";
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(loginSceneName))
        {
            error = "로그인 화면을 찾을 수 없습니다.";
            Debug.LogError($"[AccountReset] '{loginSceneName}' 씬을 로드할 수 없어 {reason}을(를) 하지 않습니다. " +
                           "Build Settings 의 Scenes In Build 에 들어 있는지 확인하세요. 아무것도 바꾸지 않았습니다.");
            return false;
        }

        SaveManager.LockForReset();

        IsRunning         = true;
        pendingLoginScene = loginSceneName;
        pendingReason     = reason;
        return true;
    }

    /// <summary>TryLockForRestart 를 되돌립니다 (1단계에서 실패했을 때).</summary>
    private static void CancelRestart()
    {
        SaveManager.UnlockAfterReset();
        IsRunning         = false;
        pendingLoginScene = null;
        pendingReason     = null;
    }

    // ═════════════════════════════════════════════════════════
    //  2단계 — 새로 시작 (비동기, 되돌릴 수 없음)
    // ═════════════════════════════════════════════════════════

    /// <summary>TryWipe 성공 뒤에 호출합니다. 매니저를 전부 새로 만들고 LoginScene 으로 갑니다.</summary>
    public static void RestartFromLogin()
    {
        if (!IsRunning || string.IsNullOrEmpty(pendingLoginScene))
        {
            Debug.LogError("[AccountReset] TryWipe() 가 성공하지 않았는데 RestartFromLogin() 이 불렸습니다. 무시합니다.");
            return;
        }

        // ★ 코루틴은 MonoBehaviour 가 있어야 돌 수 있습니다.
        //   그런데 지금 이 코드를 부른 설정 패널은 곧 MainScene 과 함께 사라집니다.
        //   주인이 파괴되면 그 주인이 돌리던 코루틴도 멈추므로,
        //   초기화 전용 오브젝트를 따로 만들어 DontDestroyOnLoad 로 끝까지 살려 둡니다.
        var go = new GameObject("[AccountReset]", typeof(RectTransform));
        DontDestroyOnLoad(go);

        // 입력 차단막 — 이 한 줄을 부르는 순간부터 화면 전체의 터치를 막는다.
        CreateInputBlocker(go);

        var runner = go.AddComponent<AccountReset>();
        runner.StartCoroutine(runner.RestartRoutine(pendingLoginScene));
    }

    /// <summary>
    /// 초기화가 끝날 때까지 모든 UI 터치를 막는 반투명 검은 막.
    ///
    /// [왜 필요한가]
    ///   MainScene 이 내려가는 데는 몇 프레임이 걸립니다(느린 기기에선 더 김). 그동안 설정 패널의
    ///   '로그인 화면으로 돌아가기' 나 HUD 의 상점·가챠 버튼이 여전히 눌립니다.
    ///   그중 하나라도 SceneManager.LoadScene 을 부르면 초기화 코루틴과 씬 로드가 뒤엉킵니다.
    ///     예) ReturnToLogin → LoginScene 이 먼저 로드 → 새 ManagerRoot 는 "기존 루트가 있네" 하고 자폭
    ///         → 이어서 코루틴이 기존 루트를 파괴 → 매니저가 하나도 없는 LoginScene
    ///   확인 팝업의 Dim 은 선택 항목이고 설정 패널 크기만큼만 덮을 수도 있어서 믿을 수 없습니다.
    ///
    /// [왜 EventSystem 을 끄지 않고 막을 덮는가]
    ///   EventSystem 을 끄면 나중에 반드시 다시 켜야 하고, 그게 DontDestroyOnLoad 에 있는 경우
    ///   켜는 걸 빠뜨리면 LoginScene 전체가 안 눌립니다. 막은 이 오브젝트와 함께 사라지므로 되돌릴 게 없습니다.
    ///
    /// ScreenSpaceOverlay + sortingOrder 최댓값 → 어떤 UI 보다 위에 그려지고, 터치를 전부 가져갑니다.
    /// (Image 가 raycastTarget 이면 알파와 상관없이 터치를 받습니다)
    /// </summary>
    private static void CreateInputBlocker(GameObject host)
    {
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // 32767 — 캔버스 정렬 순서의 최댓값
        host.AddComponent<GraphicRaycaster>();  // 이게 있어야 이 캔버스가 터치를 "받을" 수 있음

        var blocker = new GameObject("Blocker", typeof(RectTransform));
        blocker.transform.SetParent(host.transform, false);

        // 화면 전체로 늘이기: 앵커를 (0,0)~(1,1), 여백 0
        var rt = (RectTransform)blocker.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = blocker.AddComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, 0.6f);   // 반투명 — 팝업의 '삭제 중...' 이 비쳐 보이게
        img.raycastTarget = true;
    }

    private IEnumerator RestartRoutine(string loginSceneName)
    {
        // 설정 패널이 열려 있으면 timeScale 이 0 입니다.
        // yield return null 은 timeScale 과 무관하게 돌지만, 새 씬이 멈춘 채로 시작하면 안 되므로 여기서 맞춥니다.
        // (SceneLoader / ReturnToLogin 과 같은 규칙: 씬을 떠날 때는 1f 고정)
        Time.timeScale = 1f;

        // ───────── ③ 게임 씬 내리기 (매니저 생존 상태) ─────────
        //
        // 로드된 씬이 하나뿐이면 언로드할 수 없습니다. 그래서 빈 씬을 하나 만들어 그리로 옮긴 뒤 내립니다.
        // CreateScene 으로 만든 씬은 Build Settings 에 없어도 되고, 저장되지도 않습니다.
        Scene empty = SceneManager.CreateScene("AccountReset_Empty");

        var toUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            // ※ DontDestroyOnLoad 영역은 이 목록에 나오지 않습니다. 그건 ④ 에서 따로 다룹니다.
            if (s == empty || !s.isLoaded) continue;
            toUnload.Add(s);
        }

        SceneManager.SetActiveScene(empty);   // 새로 만드는 오브젝트가 이 씬에 생기게

        // 카메라가 하나도 없으면 에디터에 "No cameras rendering" 이 잠깐 뜹니다. 검은 화면용 카메라를 둡니다.
        // 빈 씬에 속하므로 ⑤ 에서 씬과 함께 사라집니다.
        var cam = new GameObject("AccountReset_Camera").AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask     = 0;   // 아무것도 그리지 않음

        var ops = new List<AsyncOperation>();
        foreach (Scene s in toUnload)
        {
            Debug.Log($"[AccountReset] 씬 언로드: {s.name}");
            AsyncOperation op = SceneManager.UnloadSceneAsync(s);
            if (op != null) ops.Add(op);
        }

        foreach (AsyncOperation op in ops)
            while (!op.isDone) yield return null;

        // ───────── ④ DontDestroyOnLoad 매니저 전부 파괴 ─────────
        DestroyPersistentGameObjects();

        // Destroy 는 프레임 끝에 실제로 일어납니다. 한 프레임 쉬어서 옛 매니저들의 OnDestroy
        // (ManagerRoot.Instance = null 등) 가 끝난 뒤에 LoginScene 을 부릅니다. (클래스 주석 참고)
        yield return null;

        // 확인 — 옛 ManagerRoot 가 정말 사라졌는가.
        //   살아 있으면 새 LoginScene 의 루트가 중복으로 자폭하고, 옛 매니저가 옛 값을 들고 계속 일합니다.
        //   잠금은 곧 풀리므로 그 옛 값이 새 세이브에 그대로 기록됩니다 → "삭제했는데 안 지워짐".
        //   에러도 안 나는 종류라 여기서 반드시 잡아 로그를 남기고 강제로 지웁니다.
        if (ManagerRoot.Instance != null)
        {
            Debug.LogError("[AccountReset] 옛 ManagerRoot 가 파괴되지 않아 강제로 지웁니다. " +
                           "DontDestroyOnLoad 루트가 맞는지, 위 '파괴:' 로그 목록을 확인하세요.", ManagerRoot.Instance);
            Destroy(ManagerRoot.Instance.gameObject);
            yield return null;
        }

        // ───────── ⑤ LoginScene 새로 로드 ─────────
        // Single 모드라 임시 빈 씬도 이때 함께 내려갑니다.
        AsyncOperation load = SceneManager.LoadSceneAsync(loginSceneName, LoadSceneMode.Single);
        if (load == null)
        {
            // TryWipe 에서 확인했으므로 정상이라면 여기 올 일이 없습니다.
            Debug.LogError($"[AccountReset] '{loginSceneName}' 로드 실패.");
        }
        else
        {
            while (!load.isDone) yield return null;
        }

        // ★ 안전망. 정상이라면 새 SaveManager.Awake 가 이미 잠금을 풀었습니다.
        //   여기서도 잠겨 있다면 LoginScene 에 SaveManager 가 없다는 뜻인데,
        //   그 상태로 두면 새로 시작한 게임이 "조용히 한 번도 저장되지 않는" 최악의 버그가 됩니다.
        if (SaveManager.IsSaveLocked)
        {
            Debug.LogError("[AccountReset] LoginScene 로드 후에도 저장이 잠겨 있습니다. " +
                           "LoginScene 에 SaveManager 가 있는지 확인하세요. 잠금을 강제로 풉니다.");
            SaveManager.UnlockAfterReset();
        }

        string reason = pendingReason ?? "재시작";   // 로그용
        IsRunning = false;
        pendingLoginScene = null;
        pendingReason = null;

        Debug.Log($"[AccountReset] {reason} 완료 — 매니저를 새로 만들고 LoginScene 에 도착했습니다. (계정: {SaveManager.ActiveAccountLabel})");
        Destroy(gameObject);   // 할 일을 다 했으니 자기 자신도 정리
    }

    /// <summary>
    /// DontDestroyOnLoad 영역의 게임 오브젝트를 파괴합니다. (자기 자신 제외)
    ///
    /// [이름으로 하나하나 지목하지 않는 이유]
    ///   ManagerRoot 외에도 GachaSystem(가챠 씬에서 생김), SceneLoader(Login_Name 이 생성),
    ///   ManagerRoot 밖에 따로 있는 SaveManager 처럼 DontDestroyOnLoad 싱글턴이 여러 곳에 흩어져 있습니다.
    ///   목록을 코드에 적어 두면 나중에 매니저를 하나 추가하는 순간 빠뜨리고,
    ///   빠진 매니저는 옛 값을 들고 살아남아 다음 Save() 때 그 값을 새 세이브에 섞어 넣습니다.
    ///   그래서 "DontDestroyOnLoad 에 있는 것 전부" 를 대상으로 합니다.
    ///
    /// [우리 스크립트가 붙은 것만 지우는 이유]
    ///   DontDestroyOnLoad 에는 엔진·플러그인이 만든 오브젝트도 들어올 수 있습니다
    ///   (URP 디버그 업데이터, DOTween 같은 트윈 라이브러리 등). 그걸 지우면 우리가 모르는 방식으로 고장납니다.
    ///   그래서 "이 프로젝트의 게임 스크립트(ManagerRoot 와 같은 어셈블리)가 하나라도 붙어 있는 루트" 만 지웁니다.
    ///
    ///   ★ 기준이 typeof(AccountReset) 가 아니라 typeof(ManagerRoot) 인 이유
    ///     이 파일을 Plugins 나 Standard Assets 폴더에 넣으면 유니티가 다른 어셈블리
    ///     (Assembly-CSharp-firstpass)로 컴파일합니다. 그러면 "같은 어셈블리" 인 매니저가 하나도 없어서
    ///     아무것도 지우지 않고 넘어가고, 초기화가 조용히 실패합니다.
    ///     "게임 매니저가 사는 곳" 을 기준으로 삼으면 이 파일이 어디 있든 결과가 같습니다.
    ///
    /// ※ DontDestroyOnLoad 영역은 SceneManager.GetSceneAt 목록에 없어서 직접 얻을 수 없습니다.
    ///   대신 "나 자신도 DontDestroyOnLoad 오브젝트" 이므로 gameObject.scene 이 바로 그 영역입니다.
    /// </summary>
    private void DestroyPersistentGameObjects()
    {
        Assembly gameAssembly = typeof(ManagerRoot).Assembly;
        GameObject[] roots = gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root == gameObject) continue;   // 이 코루틴을 돌리는 나 자신은 끝까지 살아 있어야 함

            if (!HasGameScript(root, gameAssembly))
            {
                Debug.Log($"[AccountReset] 유지 (엔진/플러그인 오브젝트): {root.name}");
                continue;
            }

            // ★ ManagerRoot 가 파괴되면 "살아 있던 루트가 파괴되었습니다" 경고가 뜹니다.
            //   평소엔 이상 신호지만, 지금은 의도한 동작입니다.
            Debug.Log($"[AccountReset] 파괴: {root.name}", root);
            Destroy(root);
        }
    }

    private static bool HasGameScript(GameObject root, Assembly gameAssembly)
    {
        // includeInactive: true — 꺼져 있는 자식 매니저도 옛 값을 들고 있을 수 있으므로 포함
        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // 스크립트 파일이 사라진 컴포넌트(Missing Script)는 null 로 나옵니다.
            if (mb != null && mb.GetType().Assembly == gameAssembly)
                return true;
        }
        return false;
    }
}