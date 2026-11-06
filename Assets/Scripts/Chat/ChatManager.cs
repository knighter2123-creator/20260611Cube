using System;
using System.Collections.Generic;
using UnityEngine;


public partial class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    // ★ 값(숫자)이 씬에 저장됩니다 — 새 값은 맨 뒤에 추가하세요. (2 는 지운 옛 Firestore 자리 — 다시 쓰지 마세요)
    public enum Backend { Backnd = 0, Local = 1 }

    /// <summary>개인 메시지(쪽지) 한 통 최대 글자 수. 전체 채팅 길이는 뒤끝챗 콘솔에서 정합니다.</summary>
    public const int DIRECT_MAX_LENGTH = 60;

    private static string AccountPending => GameMessages.Common.accountPending;

    [Tooltip("Backnd = 뒤끝 서버 (전체 채팅 뒤끝챗 · 친구 · 쪽지), Local = 서버 없이 나에게만 보이는 테스트")]
    [SerializeField] private Backend backend = Backend.Backnd;

    [Tooltip("뒤끝챗 콘솔에서 만든 공개 채널 그룹 이름")]
    [SerializeField] private string channelGroup = "global";
    [Tooltip("그 그룹 안의 채널 이름. 비우면 그룹 이름과 같게")]
    [SerializeField] private string channelName = "";

    [Tooltip("보관·표시할 최근 메시지 수 (연결할 때 이만큼 받아 옴)")]
    [SerializeField] private int historyCount = 50;

    [Tooltip("개인 채팅 탭을 열 때 받아 오는 최근 메시지 수")]
    [SerializeField] private int directHistoryCount = 50;

    [Tooltip("개인 메시지 사이 최소 간격(초) — 도배 방지. 전체 채팅 도배는 뒤끝챗 콘솔 설정으로 막습니다.")]
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

        service = CreateService(backend, channelGroup, channelName);
        service.MessagesReceived += OnMessagesReceived;
        service.MessagesRemoved  += OnMessagesRemoved;
        service.Failed           += OnServiceFailed;

        AwakeFriends();
        AwakeDirect();

        usesBackendSocial = friendService is BackendFriendService || directService is BackendDirectService;
        BackendNotify.FriendsChanged  += OnRealtimeFriends;
        BackendNotify.MessageReceived += OnRealtimeMessage;
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

        BackendNotify.FriendsChanged  -= OnRealtimeFriends;
        BackendNotify.MessageReceived -= OnRealtimeMessage;
        if (usesBackendSocial) BackendNotify.Tick(null);   // 실시간 알림 끊기
    }

    // ───────────────────────────── 뒤끝 실시간 알림 ─────────────────────────────

    private bool usesBackendSocial;   // 친구·개인 메시지가 뒤끝이면 실시간 알림을 씀

    /// <summary>친구나 개인 메시지가 연결돼 있는 동안 그 계정으로 실시간 알림을 받음 (BackendNotify 주석).</summary>
    private void UpdateRealtime()
    {
        if (!usesBackendSocial) return;
        string uid = friendsLink.Key ?? directLink.Key;
        BackendNotify.Tick(uid == "local" ? null : uid);
    }

    private void OnRealtimeFriends()
    {
        if (friendsLink.Key != null) RefreshFriends();
    }

    private void OnRealtimeMessage()
    {
        if (directLink.Key != null) RefreshDirect();
    }

    /// <summary>서버 종류를 늘리려면 여기에 한 줄 추가 (IChatService 주석 참고).</summary>
    private static IChatService CreateService(Backend backend, string group, string name)
    {
        switch (backend)
        {
            case Backend.Local: return new LocalChatService();
            default:            return new BackendChatService(group, name);   // 전체 채팅 뒤끝챗 + 친구 뒤끝 친구 + 개인 메시지 뒤끝 쪽지
        }
    }

    /// <summary>서버 콜백을 받을 때 이 매니저가 이미 파괴됐으면 무시하도록 감쌉니다.</summary>
    private Action<string> IfAlive(Action<string> onDone) => error =>
    {
        if (this != null) onDone?.Invoke(error);
    };

    /// <summary>IfAlive 의 인자 두 개짜리 — (uid, 오류) 같은 콜백용.</summary>
    private Action<T1, T2> IfAlive<T1, T2>(Action<T1, T2> onDone) => (a, b) =>
    {
        if (this != null) onDone?.Invoke(a, b);
    };

    // ───────────────────────────── 연결 ─────────────────────────────

    private void Update()
    {
        service.Tick();   // 뒤끝챗 ChatClient.Update — 받은 메시지 콜백이 이 안에서 옴

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
        UpdateRealtime();

        if (reason == null && chatLink.Key == null)
            reason = chatLink.FailReason ?? GameMessages.Chat.connecting;

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
        chatLink.Failed(ChatText.Fill(GameMessages.Chat.reconnecting, "reason", reason), retryDelay);
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

    /// <summary>메시지 전송(toUid 가 있으면 개인 대화방) — 결과는 onDone(오류 문구, 성공이면 null), 검사에 걸리면 서버에 안 보내고 바로 onDone.</summary>
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

        sending = true;   // 한 번에 하나 — 서비스가 보내는 중인 메시지를 하나만 추적함
        if (toUid != null) nextSendTime = Time.unscaledTime + sendCooldown;

        Action<string> done = result =>
        {
            if (this == null) return;
            sending = false;
            onDone?.Invoke(result);
        };

        if (toUid != null) directService.Send(uid, toUid, message, done);
        else               service.Send(message, done);
    }

    /// <summary>보내기 전 검사. 보낼 수 없으면 이유, 보낼 수 있으면 null (uid = 보내는 사람).</summary>
    private string CheckSend(string text, string toUid, out string uid)
    {
        uid = null;
        bool direct = toUid != null;

        if (UnavailableReason != null)                    return UnavailableReason;
        if (direct && CannotSendDirect(toUid, out string directError)) return directError;
        if (text.Length == 0)                             return GameMessages.Chat.emptyMessage;
        if (sending)                                      return GameMessages.Chat.sending;
        // 전체 채팅의 길이·도배는 뒤끝챗 콘솔 설정으로 서버가 막음 — 여기서는 개인 메시지만 검사
        if (direct && text.Length > DIRECT_MAX_LENGTH)    return ChatText.Fill(GameMessages.Chat.directTooLong, "max", DIRECT_MAX_LENGTH.ToString());
        if (direct && Time.unscaledTime < nextSendTime)   return GameMessages.Chat.tooFast;
        if (!TryGetSender(out uid))                       return GameMessages.Chat.accountPendingSend;
        if (direct && toUid == uid)                       return GameMessages.Chat.selfDirect;
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

    /// <summary>리스너 하나의 연결 상태(연결 UID·끊긴 이유·재연결 시각) — 채팅은 신원을, 친구·개인 메시지는 채팅의 Key 를 따라 열고 닫음.</summary>
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
