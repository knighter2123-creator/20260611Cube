using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 채팅 — 접속한 모두가 같은 채널에서 이야기합니다.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. RankingManager 와 같은 DontDestroyOnLoad 싱글턴이다.
///
/// [하는 일]  연결 관리 / 최근 메시지 보관 / 보내기 전 검사(길이·도배).
/// [안 하는 일]  서버 통신 — IChatService 구현체가 함.  UI — ChatPanel 이 Changed 를 받아 그림.
///
/// [언제 연결하는가]  서비스가 "나" 의 ID 를 줄 수 있게 되면 (IChatService.TryGetIdentity).
///   Firestore 는 로그인 유저면 계정 UID, 게스트면 채팅 전용 익명 UID (FirestoreChatService 주석 ★ 참고).
///   ID 가 바뀌면(로그인·로그아웃·계정 전환) 끊고 목록을 비운 뒤 다시 연결합니다.
///   패널이 닫혀 있어도 연결을 유지합니다 → 미리보기 한 줄이 계속 갱신되고, 열자마자 대화가 보입니다.
///
/// [보내는 값]  닉네임 = PlayerProfile.DisplayName, 진행도 = StageRecord.BestProgress (랭킹과 같은 값)
///   로그인 유저는 ProfileSync.TryGetAccount 가 통과할 때만 보냄 — 계정 전환 중 다른(게스트) 세이브의 이름·기록으로 말하지 않게.
///   게스트는 게스트 세이브의 이름(없으면 "익명1234")으로 바로 보냅니다.
///
/// ⚠ 길이·간격 검사는 클라이언트 검사입니다. 서버 쪽은 Firestore 보안 규칙으로 막아야 합니다 (uid 본인 확인, 글자 수).
///
/// [나눠 둔 파일]
///   ChatManager.Direct.cs  — 1:1 개인 메시지 (대화방, 나가기, 안 읽은 수)
///   ChatManager.Friends.cs — 친구 요청·수락·삭제
///   ChatManager.Block.cs   — 채팅 차단 (서버에 저장 — 같은 계정이면 어느 기기에서나 같음)
///   모두 같은 신원(MyUid)·같은 Changed 이벤트를 씁니다. 채팅이 연결되면 함께 연결하고, 계정이 바뀌면 함께 비웁니다 (ResetAccountState).
/// </summary>
public partial class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    public enum Backend { Firestore, Local }

    /// <summary>한 메시지 최대 글자 수. 입력창 제한·보안 규칙과 맞춰 주세요.</summary>
    public const int MAX_LENGTH = 60;

    private const string AccountPending = "계정 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요.";

    [Tooltip("Firestore = 실제 서버, Local = 서버 없이 나에게만 보이는 테스트")]
    [SerializeField] private Backend backend = Backend.Firestore;

    [Tooltip("보관·표시할 최근 메시지 수 (연결할 때 이만큼 받아 옴)")]
    [SerializeField] private int historyCount = 50;

    [Tooltip("개인 채팅 탭을 열 때 받아 오는 최근 메시지 수")]
    [SerializeField] private int directHistoryCount = 50;

    [Tooltip("메시지 사이 최소 간격(초) — 도배 방지")]
    [SerializeField] private float sendCooldown = 1.5f;

    [Tooltip("연결이 끊겼을 때 다시 연결하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 10f;

    /// <summary>메시지 목록이나 UnavailableReason 이 바뀌면 호출됩니다 (프레임당 최대 1회).</summary>
    public event Action Changed;

    /// <summary>보낸 시각 순 최근 메시지.</summary>
    public IReadOnlyList<ChatMessage> Messages => messages.Items;

    /// <summary>지금 채팅을 쓸 수 없는 이유. null 이면 사용 가능.</summary>
    public string UnavailableReason { get; private set; }

    /// <summary>지금 연결된 내 ID — 내 메시지 강조용. (게스트면 채팅 전용 익명 UID)</summary>
    public string MyUid => chatLink.Key;

    private IChatService service;
    private readonly MessageLog messages = new MessageLog();
    private readonly Connection chatLink = new Connection();

    private string messagesKey;      // messages 가 어느 계정으로 받은 것인지
    private float  nextSendTime;
    private bool   sending;
    private bool   changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        service = CreateService(backend);
        service.MessagesReceived += OnMessagesReceived;
        service.MessagesRemoved  += OnMessagesRemoved;
        service.Failed           += OnServiceFailed;

        AwakeFriends();
        AwakeDirect();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        service.MessagesReceived -= OnMessagesReceived;
        service.MessagesRemoved  -= OnMessagesRemoved;
        service.Failed           -= OnServiceFailed;
        service.Disconnect();

        DestroyFriends();
        DestroyDirect();
    }

    /// <summary>서버 종류를 늘리려면 여기에 한 줄 추가 (IChatService 주석 참고).</summary>
    private static IChatService CreateService(Backend backend)
    {
        switch (backend)
        {
            case Backend.Local: return new LocalChatService();
            default:            return new FirestoreChatService();
        }
    }

    /// <summary>서버 콜백을 받을 때 이 매니저가 이미 파괴됐으면 무시하도록 감쌉니다.</summary>
    private Action<string> IfAlive(Action<string> onDone) => error =>
    {
        if (this != null) onDone?.Invoke(error);
    };

    // ───────────────────────────── 연결 ─────────────────────────────

    private void Update()
    {
        string key = service.TryGetIdentity(out string uid, out string reason) ? uid : null;

        if (chatLink.ShouldDisconnect(key))
        {
            service.Disconnect();   // 로그아웃·계정 전환
            chatLink.Disconnected();
        }

        // 다른 계정(또는 로그아웃)이 되면 이전 대화를 비움. 같은 계정의 재연결이면 남겨 둠.
        if (key != messagesKey)
        {
            messagesKey = key;
            messages.Clear();
            ResetAccountState();
            changed = true;
        }

        if (chatLink.ShouldConnect(key))
        {
            chatLink.Connected(key);
            service.Connect(historyCount);   // 실패하면 이 안에서 바로 OnServiceFailed 가 불릴 수 있음
        }

        UpdateFriendsConnection();
        UpdateDirectConnection();

        if (reason == null && chatLink.Key == null)
            reason = chatLink.FailReason ?? "채팅 서버에 연결하는 중입니다...";

        if (reason != UnavailableReason)
        {
            UnavailableReason = reason;
            changed = true;
        }

        if (changed)
        {
            changed = false;
            Changed?.Invoke();
        }
    }

    /// <summary>계정이 바뀔 때 — 이전 계정의 개인 메시지·친구·차단을 비움. 새 계정 것은 다시 연결되면 서버에서 받음.</summary>
    private void ResetAccountState()
    {
        ClearDirect();
        ClearFriends();
    }

    private void OnServiceFailed(string reason)
    {
        // 목록은 남겨 둠 — 다시 연결되면 Id 로 겹치는 것만 걸러서 이어 붙임
        service.Disconnect();
        chatLink.Failed($"{reason} 잠시 후 다시 연결합니다.", retryDelay);
        changed = true;
    }

    private void OnMessagesReceived(IReadOnlyList<ChatMessage> received)
    {
        if (messages.Add(received, historyCount)) changed = true;
    }

    private void OnMessagesRemoved(IReadOnlyList<string> removed)
    {
        if (messages.Remove(removed)) changed = true;
    }

    // ───────────────────────────── 보내기 ─────────────────────────────

    /// <summary>
    /// 메시지를 보냅니다. toUid 가 있으면 그 사람과의 대화방으로 (개인 메시지).
    /// 결과는 메인 스레드에서 onDone(오류 문구) 로 옵니다 — 성공이면 null.
    /// 검사에 걸리면 서버에 보내지 않고 바로 onDone 을 부릅니다.
    /// </summary>
    public void Send(string raw, Action<string> onDone, string toUid = null)
    {
        string text  = (raw ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        string error = CheckSend(text, toUid, out string uid);
        if (error != null) { onDone?.Invoke(error); return; }

        var message = new ChatMessage
        {
            Uid      = uid,
            Nickname = PlayerProfile.DisplayName,
            Progress = StageRecord.BestProgress,
            Text     = text,
            SentAt   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        sending      = true;
        nextSendTime = Time.unscaledTime + sendCooldown;

        Action<string> done = result =>
        {
            if (this == null) return;
            sending = false;
            onDone?.Invoke(result);
        };

        if (toUid != null) SendDirect(uid, toUid, message, done);
        else               service.Send(message, done);
    }

    /// <summary>보내기 전 검사. 보낼 수 없으면 이유, 보낼 수 있으면 null (uid = 보내는 사람).</summary>
    private string CheckSend(string text, string toUid, out string uid)
    {
        uid = null;
        bool direct = toUid != null;

        if (UnavailableReason != null)                    return UnavailableReason;
        if (direct && CannotSendDirect(toUid, out string directError)) return directError;
        if (text.Length == 0)                             return "메시지를 입력해 주세요.";
        if (text.Length > MAX_LENGTH)                     return $"메시지는 {MAX_LENGTH}자까지 보낼 수 있습니다.";
        if (sending || Time.unscaledTime < nextSendTime)  return "너무 빨리 보내고 있습니다. 잠시 후 다시 보내 주세요.";
        if (!TryGetSender(out uid))                       return "계정 정보를 확인하는 중입니다. 잠시 후 다시 보내 주세요.";
        if (direct && toUid == uid)                       return "나에게는 개인 메시지를 보낼 수 없습니다.";
        return null;
    }

    private bool TryGetSender(out string uid)
    {
        uid = chatLink.Key;
        if (uid == null) return false;

        // 게스트(또는 Local) — 지금 세이브가 곧 게스트 세이브이므로 바로 보냄
        if (backend == Backend.Local || AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn) return true;

        // 로그인 계정 = 지금 세이브의 주인 + 서버 닉네임을 받아 온 뒤 (RankingManager.CanUpload 와 같은 조건)
        if (!ProfileSync.TryGetAccount(out string account) || account != uid) return false;
        return ProfileSync.Instance == null || ProfileSync.Instance.IsPulledFor(uid);
    }

    // ───────────────────────────── 도우미 타입 ─────────────────────────────

    /// <summary>
    /// 리스너 하나의 연결 상태 — 연결된 UID, 끊긴 이유, 다시 연결할 시각.
    /// 채팅은 신원(TryGetIdentity)을, 친구·개인 메시지는 채팅의 Key 를 따라 열고 닫습니다.
    /// </summary>
    private sealed class Connection
    {
        public string Key        { get; private set; }   // 연결된 UID (null = 끊김)
        public string FailReason { get; private set; }   // 끊긴 이유 (다시 연결될 때까지 표시)
        private float nextConnectTime;

        /// <summary>연결돼 있는데 따라가야 할 UID 가 바뀜 (로그아웃·계정 전환·채팅 끊김).</summary>
        public bool ShouldDisconnect(string target) => Key != null && Key != target;

        /// <summary>끊긴 상태이고 재시도 대기 시간이 지남.</summary>
        public bool ShouldConnect(string target)
            => target != null && Key == null && Time.unscaledTime >= nextConnectTime;

        public void Connected(string key) { Key = key; FailReason = null; }
        public void Disconnected()        { Key = null; }

        public void Failed(string reason, float retryDelay)
        {
            Key             = null;
            FailReason      = reason;
            nextConnectTime = Time.unscaledTime + retryDelay;
        }

        public void Reset()
        {
            Key             = null;
            FailReason      = null;
            nextConnectTime = 0f;
        }
    }

    /// <summary>Id 로 겹치는 것을 거른, 보낸 시각 순 최근 메시지 목록. 전체 채팅과 개인 메시지가 함께 씁니다.</summary>
    private sealed class MessageLog
    {
        private readonly List<ChatMessage> items = new List<ChatMessage>();
        private readonly HashSet<string>   ids   = new HashSet<string>();

        public IReadOnlyList<ChatMessage> Items => items;

        /// <summary>새 메시지만 붙여 보낸 시각 순으로 정렬하고, cap 개를 넘는 옛 메시지를 버립니다. 바뀌었으면 true.</summary>
        public bool Add(IReadOnlyList<ChatMessage> received, int cap)
        {
            bool any = false;
            foreach (ChatMessage m in received)
            {
                if (string.IsNullOrEmpty(m.Id) || !ids.Add(m.Id)) continue;
                items.Add(m);
                any = true;
            }
            if (!any) return false;

            items.Sort((a, b)
                => a.SentAt != b.SentAt ? a.SentAt.CompareTo(b.SentAt) : string.CompareOrdinal(a.Id, b.Id));

            int overflow = items.Count - cap;
            if (overflow > 0)
            {
                for (int i = 0; i < overflow; i++) ids.Remove(items[i].Id);
                items.RemoveRange(0, overflow);
            }
            return true;
        }

        /// <summary>바뀌었으면 true.</summary>
        public bool Remove(IReadOnlyList<string> removed)
        {
            bool any = false;
            foreach (string id in removed)
                if (ids.Remove(id)) any = true;
            if (any) items.RemoveAll(m => !ids.Contains(m.Id));
            return any;
        }

        public void Clear()
        {
            items.Clear();
            ids.Clear();
        }
    }
}
