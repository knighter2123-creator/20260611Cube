using System;
using System.Collections.Concurrent;
using BackEnd;
using UnityEngine;

/// <summary>뒤끝 공용 도우미: 초기화를 한 번만 하고, 다른 스레드에서 오는 비동기 응답을 큐에 넣어 다음 Update 에 메인 스레드로 넘김 (Unity API 를 안전하게 쓰려고).</summary>
// [붙이는 위치] 붙이지 않음 — 첫 씬 로드 전 스스로 [BackendManager] 를 만들어 DontDestroyOnLoad / 키는 TheBackendSettings(뒤끝 메뉴 > Edit Settings)
// ⚠ 호출 중 예외가 나면 bro = null 로 옴 → 받는 쪽은 null 도 실패로 처리
public class BackendManager : MonoBehaviour
{
    private static BackendManager instance;
    private static readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();

    public static bool   IsInitialized  { get; private set; }
    public static bool   IsInitializing { get; private set; }
    /// <summary>마지막 초기화 실패 사유 (로그·디버그용). 성공하면 null.</summary>
    public static string InitError      { get; private set; }

    private static Action<bool> pendingInit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance       = null;
        IsInitialized  = false;
        IsInitializing = false;
        InitError      = null;
        pendingInit    = null;
        while (mainQueue.TryDequeue(out _)) { }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[BackendManager]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BackendManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
    // ⚠ 테스트 로그인 코드를 Start 에 두지 말 것 — 매번 자동 생성되므로 실제 게임에서도 세션을 바꾸고 초기화가 AuthManager 와 겹침

    private void Update()
    {
        // 한 프레임에 몰려도 전부 처리 (응답 수는 많지 않음)
        while (mainQueue.TryDequeue(out Action action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>아무 스레드에서나 불러도 됨 — 다음 Update 에서 메인 스레드로 실행.</summary>
    public static void Post(Action action)
    {
        if (action != null) mainQueue.Enqueue(action);
    }
    
   

    // ───────────────────────────── 초기화 ─────────────────────────────

    /// <summary>뒤끝 초기화. 결과는 메인 스레드에서 onDone(성공) 으로 옵니다. 실패하면 다시 불러 재시도할 수 있습니다.</summary>
    public static void Initialize(Action<bool> onDone)
    {
        if (IsInitialized) { onDone?.Invoke(true); return; }

        pendingInit += onDone;
        if (IsInitializing) return;
        IsInitializing = true;

        try
        {
            Backend.InitializeAsync(bro => Post(() => FinishInit(bro != null && bro.IsSuccess(), bro?.ToString())));
        }
        catch (Exception e)
        {
            FinishInit(false, e.Message);
        }
    }

    private static void FinishInit(bool ok, string detail)
    {
        IsInitializing = false;
        IsInitialized  = ok;
        InitError      = ok ? null : detail;

        if (ok) GameLog.Info("[Backend] 뒤끝 초기화 성공");
        else    Debug.LogError($"[Backend] 뒤끝 초기화 실패 — TheBackendSettings 의 Client App ID / Signature Key 를 확인하세요: {detail}");

        Action<bool> callbacks = pendingInit;
        pendingInit = null;
        callbacks?.Invoke(ok);
    }

    // ───────────────────────────── 비동기 호출 ─────────────────────────────

    /// <summary>뒤끝 비동기 함수를 부르고, 응답을 메인 스레드에서 onDone 으로 받습니다. (클래스 주석 [비동기 호출])</summary>
    public static void Call(Action<Backend.BackendCallback> request, Action<BackendReturnObject> onDone)
    {
        try
        {
            request(bro => Post(() => onDone?.Invoke(bro)));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Backend] 호출 실패: {e}");
            Post(() => onDone?.Invoke(null));
        }
    }

    /// <summary>결과가 BackendReturnObject 하위 타입인 호출용(예: 리더보드) — 람다로는 추론이 안 되니 Call&lt;T&gt; 로 타입을 적을 것.</summary>
    public static void Call<T>(Action<Backend.BackendCallback<T>> request, Action<T> onDone) where T : BackendReturnObject
    {
        try
        {
            request(bro => Post(() => onDone?.Invoke(bro)));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Backend] 호출 실패: {e}");
            Post(() => onDone?.Invoke(null));
        }
    }

    /// <summary>서버에 닿지 못한 실패인가(오프라인·서버 오류·점검·요청 과다) — true 면 계정 문제가 아니라 나중에 재시도할 일.</summary>
    public static bool IsNetworkError(BackendReturnObject bro)
    {
        return bro == null
            || bro.IsClientRequestFailError()
            || bro.IsServerError()
            || bro.IsMaintenanceError()
            || bro.IsTooManyRequestError()
            || bro.IsTooManyRequestByLocalError();
    }

    /// <summary>로그용 한 줄 — "401 BadUnauthorizedException: bad customPassword"</summary>
    public static string Describe(BackendReturnObject bro)
        => bro == null ? "(응답 없음)" : $"{bro.GetStatusCode()} {bro.GetErrorCode()}: {bro.GetMessage()}";
}
