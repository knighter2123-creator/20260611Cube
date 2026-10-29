using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 실시간 리스너로 채팅을 주고받습니다.
///
/// [Firestore 구조]
///   chat/{자동 ID}
///     uid       : string    — 보낸 사람 UID (보안 규칙: 본인 UID 로만 쓸 수 있음)
///     nickname  : string    — 보낸 순간의 PlayerProfile.DisplayName
///     progress  : int       — 보낸 순간의 StageRecord.BestProgress
///     text      : string
///     sentAt    : int       — 보낸 시각 (UTC ms, 정렬 기준)
///     createdAt : timestamp — 서버 시각 (관리용)
///     expireAt  : timestamp — 보낸 뒤 EXPIRE_DAYS 일. 콘솔에서 TTL 정책을 켜면 이 시각에 자동 삭제됩니다.
///
/// [정렬을 sentAt(기기 시각)으로 하는 이유]
///   서버 시각(createdAt)은 보내는 순간 내 화면에는 아직 비어 있어서(서버 확인 전) 정렬에 쓸 수 없습니다.
///   기기 시계가 틀린 사람의 메시지는 순서가 조금 어긋날 수 있습니다 (RankingManager.reachedAt 과 같은 타협).
///
/// [리스너 비용]  연결할 때 최근 historyCount 개 읽기 + 이후 새 메시지 1개당 1회 읽기 (접속자 수만큼).
///
/// [★ 게스트 채팅 — 채팅 전용 보조 FirebaseApp]
///   보안 규칙은 로그인한 사람(request.auth)만 읽고 쓰게 합니다. 게스트는 Firebase 에 로그인돼 있지 않으므로,
///   채팅에만 쓰는 두 번째 FirebaseApp("chat-guest") 을 만들어 거기서 '익명 로그인' 을 합니다.
///   기본 앱(AuthManager)에 익명 로그인을 하면 IsSignedIn 이 true 가 되어 계정별 세이브 전환·랭킹·ProfileSync 가
///   게스트를 '계정' 으로 착각하므로, 일부러 앱을 나눴습니다. 기본 앱의 로그인 상태는 전혀 바뀌지 않습니다.
///   - 로그인 유저: 기본 앱 Firestore + 계정 UID (랭킹과 같은 UID)
///   - 게스트     : 보조 앱 Firestore + 익명 UID (기기에 저장돼 앱을 다시 켜도 같음)
///   ⚠ Firebase 콘솔 > Authentication > 로그인 방법 에서 '익명' 을 켜야 합니다.
///
/// [친구·차단 / 개인 메시지]  같은 Firestore 를 쓰는 FirestoreFriendService / FirestoreDirectService 가 맡습니다.
/// </summary>
public class FirestoreChatService : IChatService
{
    public const string COLLECTION = "chat";
    private const int EXPIRE_DAYS = 7;

    private const string GUEST_APP_NAME      = "chat-guest";
    private const float  GUEST_RETRY_SECONDS = 30f;

    public event Action<IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<IReadOnlyList<string>>      MessagesRemoved;
    public event Action<string>                     Failed;

    public IFriendService Friends { get; }
    public IDirectService Direct  { get; }

    public FirestoreChatService()
    {
        Friends = new FirestoreFriendService(CurrentDb);
        Direct  = new FirestoreDirectService(CurrentDb);
    }

    private ListenerRegistration listener;

    // 게스트용 보조 앱 (클래스 주석 ★ 참고)
    private FirebaseApp  guestApp;
    private FirebaseAuth guestAuth;
    private bool         guestSigningIn;
    private float        guestRetryTime;
    private string       guestFailReason;

    // ───────────────────────────── 신원 ─────────────────────────────

    public bool TryGetIdentity(out string uid, out string reason)
    {
        uid    = null;
        reason = null;

        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsReady) { reason = "서버 연결 준비 중입니다."; return false; }

        if (auth.IsSignedIn)
        {
            uid = auth.Uid;
            return true;
        }

        // 게스트 — 보조 앱의 익명 로그인이 끝나면 그 UID
        if (EnsureGuestSignedIn())
        {
            uid = guestAuth.CurrentUser.UserId;
            return true;
        }
        reason = guestFailReason ?? "채팅 서버에 연결하는 중입니다...";
        return false;
    }

    /// <summary>게스트 익명 로그인이 돼 있으면 true. 안 돼 있으면 로그인을 시작하고(한 번에 하나만) false.</summary>
    private bool EnsureGuestSignedIn()
    {
        if (guestAuth == null)
        {
            if (Time.unscaledTime < guestRetryTime) return false;   // 생성 실패 뒤 매 프레임 다시 시도하지 않게
            try
            {
                // 도메인 리로드를 끈 에디터에서는 이전 플레이의 앱이 남아 있을 수 있음 → 있으면 재사용
                try { guestApp = FirebaseApp.GetInstance(GUEST_APP_NAME); } catch (Exception) { guestApp = null; }
                if (guestApp == null) guestApp = FirebaseApp.Create(FirebaseApp.DefaultInstance.Options, GUEST_APP_NAME);
                guestAuth = FirebaseAuth.GetAuth(guestApp);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Chat] 게스트용 Firebase 앱 생성 실패: {e}");
                guestFailReason = "게스트 채팅을 사용할 수 없습니다.";
                guestRetryTime  = Time.unscaledTime + GUEST_RETRY_SECONDS;
                return false;
            }
        }

        if (guestAuth.CurrentUser != null) return true;   // 이전 실행의 익명 로그인이 기기에 남아 있으면 바로 사용
        if (guestSigningIn || Time.unscaledTime < guestRetryTime) return false;

        guestSigningIn = true;
        try
        {
            guestAuth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                guestSigningIn = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    // 콘솔에서 '익명' 로그인을 안 켰으면 여기로 옴
                    Debug.LogWarning($"[Chat] 게스트 익명 로그인 실패 — {GUEST_RETRY_SECONDS}초 뒤 재시도: {task.Exception}");
                    guestFailReason = "게스트 채팅에 연결하지 못했습니다. 잠시 후 다시 시도합니다.";
                    guestRetryTime  = Time.unscaledTime + GUEST_RETRY_SECONDS;
                    return;
                }
                guestFailReason = null;
                GameLog.Info($"[Chat] 게스트 익명 로그인 완료: {guestAuth.CurrentUser?.UserId}");
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Chat] 게스트 익명 로그인 호출 실패: {e}");
            guestSigningIn  = false;
            guestFailReason = "게스트 채팅에 연결하지 못했습니다. 잠시 후 다시 시도합니다.";
            guestRetryTime  = Time.unscaledTime + GUEST_RETRY_SECONDS;
        }
        return false;
    }

    /// <summary>지금 신원에 맞는 Firestore — 로그인 유저는 기본 앱, 게스트는 보조 앱.</summary>
    private FirebaseFirestore CurrentDb()
    {
        bool signedIn = AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;
        return signedIn || guestApp == null ? FirebaseFirestore.DefaultInstance : FirebaseFirestore.GetInstance(guestApp);
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    public void Connect(int historyCount)
    {
        if (listener != null) return;

        try
        {
            // 최신순으로 N개 → 새 메시지가 오면 Added, 밀려난 옛 메시지는 Removed
            Query query = CurrentDb().Collection(COLLECTION)
                .OrderByDescending("sentAt")
                .Limit(historyCount);

            ListenerRegistration reg = query.Listen(OnSnapshot);   // 콜백은 메인 스레드에서 옴
            listener = reg;

            // 리스너가 오류로 끝나면(권한 거부 등) 알려 줌. Disconnect 로 멈춘 것은 무시.
            reg.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (listener != reg) return;
                listener = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Chat] 리스너 끊김: {task.Exception}");
                    Failed?.Invoke("채팅 서버와 연결이 끊겼습니다.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Chat] 리스너 시작 실패: {e}");
            listener = null;
            Failed?.Invoke("채팅 서버에 연결하지 못했습니다.");
        }
    }

    public void Disconnect()
    {
        ListenerRegistration reg = listener;
        listener = null;   // 먼저 비워서 ListenerTask 완료를 '오류' 로 보지 않게 함
        reg?.Stop();
    }

    private void OnSnapshot(QuerySnapshot snap)
    {
        var added   = new List<ChatMessage>();
        var removed = new List<string>();
        foreach (DocumentChange change in snap.GetChanges())
        {
            switch (change.ChangeType)
            {
                case DocumentChange.Type.Added:
                    if (TryRead(change.Document, out ChatMessage m)) added.Add(m);
                    break;

                // 내 메시지는 서버 확인 전에 Added 로 먼저 오고, 서버가 거절하면(보안 규칙 등) Removed 로 되돌려집니다.
                // 최근 N개 밖으로 밀려난 옛 메시지도 Removed 로 옵니다.
                case DocumentChange.Type.Removed:
                    removed.Add(change.Document.Id);
                    break;

                // Modified = 서버 시각(createdAt)이 채워진 것뿐 → 화면엔 영향 없음
            }
        }
        if (added.Count   > 0) MessagesReceived?.Invoke(added);
        if (removed.Count > 0) MessagesRemoved?.Invoke(removed);
    }

    /// <summary>문서 → 메시지. 개인 메시지(FirestoreDirectService)도 같은 필드라 함께 씁니다.</summary>
    public static bool TryRead(DocumentSnapshot doc, out ChatMessage m)
    {
        doc.TryGetValue("uid",      out string uid);
        doc.TryGetValue("nickname", out string nickname);
        doc.TryGetValue("progress", out long   progress);   // Firestore 정수는 64비트로 옴
        doc.TryGetValue("text",     out string text);
        doc.TryGetValue("sentAt",   out long   sentAt);

        m = new ChatMessage
        {
            Id       = doc.Id,
            Uid      = uid,
            Nickname = string.IsNullOrEmpty(nickname) ? PlayerProfile.ANONYMOUS_PREFIX : nickname,
            Progress = (int)progress,
            Text     = text,
            SentAt   = sentAt,
        };
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>메시지 → 문서. 개인 메시지도 같은 필드.</summary>
    public static Dictionary<string, object> ToData(ChatMessage m) => new Dictionary<string, object>
    {
        { "uid",       m.Uid },
        { "nickname",  m.Nickname },
        { "progress",  m.Progress },
        { "text",      m.Text },
        { "sentAt",    m.SentAt },
        { "createdAt", FieldValue.ServerTimestamp },
        { "expireAt",  Timestamp.FromDateTime(DateTime.UtcNow.AddDays(EXPIRE_DAYS)) },
    };

    // ───────────────────────────── 보내기 ─────────────────────────────

    public void Send(ChatMessage m, Action<string> onDone)
    {
        var data = ToData(m);

        try
        {
            // 내 화면에는 리스너가 서버 확인 전에 바로 보여 줍니다. 이 콜백은 서버가 받아 준 뒤에 옵니다.
            CurrentDb().Collection(COLLECTION).AddAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Chat] 전송 실패: {task.Exception}");
                    onDone?.Invoke("메시지를 보내지 못했습니다.");
                    return;
                }
                onDone?.Invoke(null);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Chat] 전송 호출 실패: {e}");
            onDone?.Invoke("메시지를 보내지 못했습니다.");
        }
    }
}
