using System;
using BackEnd;
using UnityEngine;

/// <summary>뒤끝 실시간 알림: 로그인(온라인)돼 있으면 스스로 연결 · 계정이 바뀌면 재연결 · 로그아웃이면 끊고, 알림을 메인 스레드 이벤트로 넘김 (내용이 없어 받는 쪽이 목록을 다시 받음).</summary>
// 알림이 안 와도 ChatManager.Friends/Direct · MailboxSync 가 느린 주기로 다시 받음 (IsConnected 면 주기를 길게)
// ⚠ 뒤끝 콘솔 > 실시간 알림이 켜져 있어야 OnAuthorize 가 성공함
public static class BackendNotify
{
    /// <summary>우편이 옴 — 콘솔에서 우편 발송 · 유저 우편 (메인 스레드) — Mailbox 가 목록을 다시 받음</summary>
    public static event Action MailReceived;

    /// <summary>친구 요청을 받음 · 내 요청이 수락됨 · 거절됨 (메인 스레드)</summary>
    public static event Action FriendsChanged;

    /// <summary>쪽지(개인 메시지)를 받음 (메인 스레드)</summary>
    public static event Action MessageReceived;

    /// <summary>콘솔에서 새 공지사항이 게시됨 (메인 스레드) — NoticeManager 가 목록을 다시 받음</summary>
    public static event Action NoticeCreated;

    /// <summary>길드 가입이 승인·거절됨 · 내 길드에 가입 신청이 옴 (메인 스레드) — 길드 정보를 다시 받음</summary>
    public static event Action GuildChanged;

    /// <summary>실시간 알림이 연결·인증까지 됐는가.</summary>
    public static bool IsConnected => connectedUid != null && authorized;

    // 실패마다 재시도 간격 2배(콘솔에서 꺼져 있을 때 경고가 쌓이지 않게), 인증되면 처음 값으로
    private const float FirstRetrySeconds = 60f;
    private const float MaxRetrySeconds   = 900f;
    private static float RetrySeconds = FirstRetrySeconds;

    private static string connectedUid;   // 연결을 요청한 계정 (null = 끊김)
    private static bool   authorized;
    private static bool   hooked;
    private static float  nextTryTime;
    private static BackendNotifyRunner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        runner          = null;
        MailReceived    = null;
        FriendsChanged  = null;
        MessageReceived = null;
        NoticeCreated   = null;
        GuildChanged    = null;
        connectedUid    = null;
        authorized      = false;
        hooked          = false;
        nextTryTime     = 0f;
        RetrySeconds    = FirstRetrySeconds;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRunner();
    }

    // AccountReset 이 DDOL 을 지우므로 씬마다 다시 만듦
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureRunner();

    private static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("[BackendNotify]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<BackendNotifyRunner>();
    }

    /// <summary>매 프레임 (BackendNotifyRunner) — 로그인·온라인이면 그 계정으로, 아니면 끊음.</summary>
    internal static void AutoTick()
    {
        AuthManager auth = AuthManager.Instance;
        bool online = BackendManager.IsInitialized && auth != null && auth.IsOnline;
        Tick(online ? auth.Uid : null);
    }

    /// <summary>uid = 알림을 받을 계정 (뒤끝 세션이 그 계정일 때만), null = 끊기.</summary>
    private static void Tick(string uid)
    {
        if (uid != null && uid != Backend.UserInDate) uid = null;   // 세션이 다른 계정이면 연결하지 않음

        if (connectedUid != null && connectedUid != uid) Disconnect();
        if (uid == null || connectedUid != null || Time.unscaledTime < nextTryTime) return;

        Hook();
        connectedUid = uid;
        authorized   = false;
        try
        {
            // 끊기면 SDK 가 스스로 다시 연결 (지연 ms, 최대 지연 ms, 시도 횟수)
            Backend.Notification.Connect(true, 3000, 30000, 10);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Notify] 실시간 알림 연결 실패 — {RetrySeconds}초 뒤 다시 시도: {e.Message}");
            connectedUid = null;
            authorized   = false;
            ScheduleRetry();
        }
    }

    private static void Disconnect()
    {
        try { Backend.Notification.DisConnect(); }
        catch (Exception e) { Debug.LogWarning($"[Notify] 실시간 알림 끊기 중 예외 (무시): {e.Message}"); }
        connectedUid = null;
        authorized   = false;
    }

    /// <summary>RetrySeconds 뒤에 다시 시도하고, 다음 간격은 두 배로 (최대 MaxRetrySeconds).</summary>
    private static void ScheduleRetry()
    {
        nextTryTime  = Time.unscaledTime + RetrySeconds;
        RetrySeconds = Mathf.Min(MaxRetrySeconds, RetrySeconds * 2f);
    }

    /// <summary>알림 처리기는 한 번만 붙입니다 (Backend.Notification 은 앱 전체에 하나).</summary>
    private static void Hook()
    {
        if (hooked) return;
        hooked = true;

        Backend.Notification.OnAuthorize = (result, reason) => BackendManager.Post(() =>
        {
            if (connectedUid == null) return;   // 그 사이 끊음
            authorized = result;
            if (result)
            {
                RetrySeconds = FirstRetrySeconds;
                GameLog.Info("[Notify] 실시간 알림 연결됨");
            }
            else
            {
                Debug.LogWarning($"[Notify] 실시간 알림 인증 실패 — {RetrySeconds}초 뒤 다시 시도 (콘솔에서 실시간 알림이 켜져 있는지 확인): {reason}");
                Disconnect();
                ScheduleRetry();
            }
        });

        Backend.Notification.OnReconnectFailed = () => BackendManager.Post(() =>
        {
            if (connectedUid == null) return;
            Debug.LogWarning($"[Notify] 실시간 알림 재연결 실패 — {RetrySeconds}초 뒤 다시 시도");
            Disconnect();
            ScheduleRetry();
        });

        Backend.Notification.OnReceivedFriendRequest = () => BackendManager.Post(() => FriendsChanged?.Invoke());
        Backend.Notification.OnAcceptedFriendRequest = () => BackendManager.Post(() => FriendsChanged?.Invoke());
        Backend.Notification.OnRejectedFriendRequest = () => BackendManager.Post(() => FriendsChanged?.Invoke());
        Backend.Notification.OnReceivedMessage       = () => BackendManager.Post(() => MessageReceived?.Invoke());
        Backend.Notification.OnNewNoticeCreated      = (title, content) => BackendManager.Post(() => NoticeCreated?.Invoke());
        Backend.Notification.OnApprovedGuildJoin      = () => BackendManager.Post(() => GuildChanged?.Invoke());
        Backend.Notification.OnRejectedGuildJoin      = () => BackendManager.Post(() => GuildChanged?.Invoke());
        Backend.Notification.OnReceivedGuildApplicant = () => BackendManager.Post(() => GuildChanged?.Invoke());
        Backend.Notification.OnNewPostCreated         = (type, title, content, author) => BackendManager.Post(() => MailReceived?.Invoke());
        Backend.Notification.OnReceivedUserPost       = () => BackendManager.Post(() => MailReceived?.Invoke());
    }
}

/// <summary>BackendNotify.AutoTick 을 매 프레임 부르는 DDOL 오브젝트 (BackendNotify 가 자동으로 만듦).</summary>
public sealed class BackendNotifyRunner : MonoBehaviour
{
    private void Update() => BackendNotify.AutoTick();
}
