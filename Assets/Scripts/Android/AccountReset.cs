using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;   // 입력 차단막(Image, GraphicRaycaster)

/// <summary>계정 삭제·전환·클라우드 적용: 저장 잠금 → (삭제) → 게임 씬 언로드 → DDOL 매니저 전부 파괴 → LoginScene 재로드로 '막 설치한 상태'처럼 매니저를 새로 만듦 (메모리의 옛 값이 다시 저장되지 않게).</summary>
// 사용: TryWipe(또는 TryBegin…) 성공 → 호출 쪽 UI 정리 → RestartFromLogin / 지우는 것: save.json·증강 스택·옛 이름 PlayerPrefs, 남기는 것: 기기 설정
// ★ 씬 언로드를 매니저 파괴보다 먼저 — 반대면 언로드되는 오브젝트의 OnDisable 이 죽은 매니저를 불러 MissingReferenceException
// ★ 파괴 후 한 프레임 쉬고 LoginScene 로드 — Destroy 는 프레임 끝에 일어나서, 안 쉬면 새 ManagerRoot 가 중복으로 보고 자폭
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

    // ── 1단계 — 지우기 (동기, 실패할 수 있음) ──

    /// <summary>저장을 잠그고 진행 데이터를 지움, 실패하면 잠금을 풀고 false — 실패할 수 있는 일을 되돌릴 수 없는 재시작보다 먼저 끝내려고 분리.</summary>
    public static bool TryWipe(string loginSceneName, out string error)
    {
        // ① 저장 잠금 — 반드시 삭제보다 먼저. 순서가 바뀌면 지운 직후 끼어든 Save() 가 파일을 되살립니다.
        if (!TryLockForRestart(loginSceneName, "계정 삭제", out error)) return false;

        // ② 세이브 파일 삭제 — 에디터의 '세이브 삭제' 와 같은 경로라 랭킹 초기화 표시도 함께 남습니다 (SaveManager.TryDeleteSaveFile)
        if (!SaveManager.TryDeleteSaveFile(out string fileError))
        {
            // 파일이 그대로 남아 있으므로 게임을 그대로 계속하면 됩니다. 잠금만 풀어 줍니다.
            CancelRestart();
            error = GameMessages.Account.dataDeleteFailed;
            Debug.LogError($"[AccountReset] 세이브 파일 삭제 실패로 초기화를 중단합니다: {fileError}");
            return false;
        }

        // ② PlayerPrefs 의 진행 데이터 삭제 (설정값은 건드리지 않음)
        AugmentManager.DeleteAllSavesForReset();   // 증강 카드 스택
        PlayerProfile.DeleteLegacyPrefsForReset(); // 예전 방식 이름 — 안 지우면 LoginScene 에서 옛 이름이 이관돼 되살아남
        PlayerPrefs.Save();                        // PlayerPrefs 는 Save() 전까지 메모리에만 있음 → 바로 앱이 꺼져도 안전하게

        GameLog.Info("[AccountReset] 진행 데이터 삭제 완료. RestartFromLogin() 으로 매니저를 새로 만듭니다.");
        return true;
    }

    /// <summary>계정 전환 1단계: 지우지 않고 저장만 잠금 — 잠금 없이는 매니저가 파괴되며 부르는 Save() 가 이전 계정 값을 새 계정 파일에 씀 (이후 SetActiveAccount → RestartFromLogin).</summary>
    public static bool TryBeginAccountSwitch(string loginSceneName, out string error)
        => TryLockForRestart(loginSceneName, "계정 전환", out error);

    /// <summary>클라우드 세이브 적용 1단계(TryBeginAccountSwitch 와 같음) — 잠근 뒤 파일 쓰기 → RestartFromLogin, 쓰기 실패 시 CancelBegin.</summary>
    public static bool TryBeginCloudRestore(string loginSceneName, out string error)
        => TryLockForRestart(loginSceneName, "클라우드 세이브 적용", out error);

    /// <summary>TryBeginAccountSwitch / TryBeginCloudRestore 뒤, RestartFromLogin() 전에 실패했을 때 되돌립니다.</summary>
    public static void CancelBegin()
    {
        if (IsRunning) CancelRestart();
    }

    /// <summary>재시작 공통 준비(진행 중 검사 → 씬 로드 가능 확인 → 저장 잠금) — 씬을 못 읽으면 지우기 전에 멈춰 빈 화면에 갇히지 않게.</summary>
    private static bool TryLockForRestart(string loginSceneName, string reason, out string error)
    {
        error = string.Empty;

        if (IsRunning)
        {
            error = GameMessages.Account.resetRunning;
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(loginSceneName))
        {
            error = GameMessages.Account.noLoginScene;
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

    // ── 2단계 — 새로 시작 (비동기, 되돌릴 수 없음) ──

    /// <summary>TryWipe 성공 뒤에 호출합니다. 매니저를 전부 새로 만들고 LoginScene 으로 갑니다.</summary>
    public static void RestartFromLogin()
    {
        if (!IsRunning || string.IsNullOrEmpty(pendingLoginScene))
        {
            Debug.LogError("[AccountReset] TryWipe() 가 성공하지 않았는데 RestartFromLogin() 이 불렸습니다. 무시합니다.");
            return;
        }

        // 호출한 설정 패널은 곧 사라져 코루틴이 멈추므로, 전용 DDOL 오브젝트에서 코루틴을 돌림
        var go = new GameObject("[AccountReset]", typeof(RectTransform));
        DontDestroyOnLoad(go);

        // 입력 차단막 — 이 한 줄을 부르는 순간부터 화면 전체의 터치를 막는다.
        CreateInputBlocker(go);

        var runner = go.AddComponent<AccountReset>();
        runner.StartCoroutine(runner.RestartRoutine(pendingLoginScene));
    }

    /// <summary>초기화 동안 최상단(sortingOrder 최대) 반투명 막으로 모든 터치를 막음 — 그 사이 다른 버튼이 씬을 로드하면 매니저 없는 LoginScene 이 되므로 (EventSystem 을 끄면 다시 켜야 해서 막을 씀).</summary>
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
        // 설정 패널이 timeScale 0 으로 멈춰 뒀을 수 있어 새 씬이 멈춘 채 시작하지 않게 1 로 (씬을 떠날 때의 공통 규칙)
        Time.timeScale = 1f;

        // ③ 게임 씬 내리기 (매니저 생존 상태) — 씬이 하나뿐이면 언로드가 안 되므로 임시 빈 씬을 만들어 옮김
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

        // "No cameras rendering" 방지용 검은 카메라 (빈 씬과 함께 ⑤ 에서 사라짐)
        var cam = new GameObject("AccountReset_Camera").AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask     = 0;   // 아무것도 그리지 않음

        var ops = new List<AsyncOperation>();
        foreach (Scene s in toUnload)
        {
            GameLog.Info($"[AccountReset] 씬 언로드: {s.name}");
            AsyncOperation op = SceneManager.UnloadSceneAsync(s);
            if (op != null) ops.Add(op);
        }

        foreach (AsyncOperation op in ops)
            while (!op.isDone) yield return null;

        // ───────── ④ DontDestroyOnLoad 매니저 전부 파괴 ─────────
        DestroyPersistentGameObjects();

        // Destroy 는 프레임 끝에 일어나므로 한 프레임 쉬어 옛 매니저 OnDestroy 가 끝난 뒤 로드 (클래스 주석 ★)
        yield return null;

        // 옛 ManagerRoot 가 남아 있으면 옛 값이 새 세이브에 조용히 기록되므로 강제로 지움
        if (ManagerRoot.Instance != null)
        {
            Debug.LogError("[AccountReset] 옛 ManagerRoot 가 파괴되지 않아 강제로 지웁니다. " +
                           "DontDestroyOnLoad 루트가 맞는지, 위 '파괴:' 로그 목록을 확인하세요.", ManagerRoot.Instance);
            Destroy(ManagerRoot.Instance.gameObject);
            yield return null;
        }

        // ⑤ LoginScene 새로 로드 (Single 모드라 임시 빈 씬도 함께 내려감)
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

        // 안전망: 아직 잠겨 있으면(LoginScene 에 SaveManager 없음) 게임이 조용히 저장 안 되는 버그가 되므로 강제 해제
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

        GameLog.Info($"[AccountReset] {reason} 완료 — 매니저를 새로 만들고 LoginScene 에 도착했습니다. (계정: {SaveManager.ActiveAccountLabel})");
        Destroy(gameObject);   // 할 일을 다 했으니 자기 자신도 정리
    }

    /// <summary>DDOL 영역(내 gameObject.scene)의 루트 중 게임 어셈블리 스크립트가 붙은 것만 전부 파괴 — 이름 목록은 새 매니저를 빠뜨리기 쉽고, 엔진·플러그인 오브젝트는 건드리면 안 되므로.</summary>
    // 기준이 typeof(ManagerRoot) 인 이유: 이 파일이 Plugins 로 옮겨져 다른 어셈블리가 돼도 결과가 같게
    private void DestroyPersistentGameObjects()
    {
        Assembly gameAssembly = typeof(ManagerRoot).Assembly;
        GameObject[] roots = gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root == gameObject) continue;   // 이 코루틴을 돌리는 나 자신은 끝까지 살아 있어야 함

            if (!HasGameScript(root, gameAssembly))
            {
                GameLog.Info($"[AccountReset] 유지 (엔진/플러그인 오브젝트): {root.name}");
                continue;
            }

            // ManagerRoot 파괴 경고가 떠도 여기서는 의도한 동작
            GameLog.Info($"[AccountReset] 파괴: {root.name}", root);
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