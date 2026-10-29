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
    public IReadOnlyList<ChatMessage> Messages => messages;

    /// <summary>지금 채팅을 쓸 수 없는 이유. null 이면 사용 가능.</summary>
    public string UnavailableReason { get; private set; }

    private IChatService service;
    private readonly List<ChatMessage> messages = new List<ChatMessage>();
    private readonly HashSet<string>   ids      = new HashSet<string>();

    private string connectedKey;     // 지금 연결된 계정 (null = 끊김)
    private string messagesKey;      // messages 가 어느 계정으로 받은 것인지
    private string failReason;       // 연결이 끊긴 이유 (다시 연결될 때까지 표시)
    private float  nextConnectTime;
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

    // ───────────────────────────── 연결 ─────────────────────────────

    private void Update()
    {
        string key = DesiredKey(out string reason);

        if (key != connectedKey)
        {
            if (connectedKey != null)
            {
                service.Disconnect();   // 로그아웃·계정 전환
                connectedKey = null;
            }

            // 다른 계정(또는 로그아웃)이 되면 이전 대화를 비움. 같은 계정의 재연결이면 남겨 둠.
            if (key != messagesKey)
            {
                messages.Clear();
                ids.Clear();
                messagesKey = key;
                ResetAccountState();   // 개인 메시지·친구·차단 목록도 비움 (새 계정 것은 다시 연결되면 받음)
                changed     = true;
            }

            if (key != null && Time.unscaledTime >= nextConnectTime)
            {
                connectedKey = key;
                failReason   = null;
                service.Connect(historyCount);   // 실패하면 이 안에서 바로 OnServiceFailed 가 불릴 수 있음
            }
        }

        UpdateFriendsConnection();
        UpdateDirectConnection();

        if (reason == null && connectedKey == null)
            reason = failReason ?? "채팅 서버에 연결하는 중입니다...";

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

    /// <summary>지금 연결해야 할 ID. null 이면 연결하면 안 되고 reason 에 이유가 담깁니다.</summary>
    private string DesiredKey(out string reason)
        => service.TryGetIdentity(out string uid, out reason) ? uid : null;

    private void OnServiceFailed(string reason)
    {
        // 목록은 남겨 둠 — 다시 연결되면 Id 로 겹치는 것만 걸러서 이어 붙임
        service.Disconnect();
        connectedKey    = null;
        failReason      = $"{reason} 잠시 후 다시 연결합니다.";
        nextConnectTime = Time.unscaledTime + retryDelay;
        changed         = true;
    }

    private void OnMessagesReceived(IReadOnlyList<ChatMessage> received)
    {
        if (AddMessages(messages, ids, received, historyCount)) changed = true;
    }

    private void OnMessagesRemoved(IReadOnlyList<string> removed)
    {
        if (RemoveMessages(messages, ids, removed)) changed = true;
    }

    /// <summary>Id 로 겹치는 것을 거르고 보낸 시각 순으로 붙인 뒤, cap 개를 넘는 옛 메시지를 버립니다. 바뀌었으면 true. (개인 메시지도 같이 씀)</summary>
    private static bool AddMessages(List<ChatMessage> list, HashSet<string> idSet, IReadOnlyList<ChatMessage> received, int cap)
    {
        bool any = false;
        foreach (ChatMessage m in received)
        {
            if (string.IsNullOrEmpty(m.Id) || !idSet.Add(m.Id)) continue;
            list.Add(m);
            any = true;
        }
        if (!any) return false;

        list.Sort((a, b) 
            => a.SentAt != b.SentAt ? a.SentAt.CompareTo(b.SentAt) : string.CompareOrdinal(a.Id, b.Id));

        int overflow = list.Count - cap;
        if (overflow > 0)
        {
            for (int i = 0; i < overflow; i++) idSet.Remove(list[i].Id);
            list.RemoveRange(0, overflow);
        }
        return true;
    }

    private static bool RemoveMessages(List<ChatMessage> list, HashSet<string> idSet, IReadOnlyList<string> removed)
    {
        bool any = false;
        foreach (string id in removed)
        {
            if (!idSet.Remove(id)) continue;
            list.RemoveAll(m => m.Id == id);
            any = true;
        }
        return any;
    }

    // ───────────────────────────── 보내기 ─────────────────────────────

    /// <summary>지금 연결된 내 ID — 내 메시지 강조용. (게스트면 채팅 전용 익명 UID)</summary>
    public string MyUid => connectedKey;

    /// <summary>
    /// 메시지를 보냅니다. toUid 가 있으면 그 사람과의 대화방으로 (개인 메시지).
    /// 결과는 메인 스레드에서 onDone(오류 문구) 로 옵니다 — 성공이면 null.
    /// 검사에 걸리면 서버에 보내지 않고 바로 onDone 을 부릅니다.
    /// </summary>
    public void Send(string raw, Action<string> onDone, string toUid = null)
    {
        string text   = (raw ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        bool   direct = toUid != null;

        if (UnavailableReason != null)               { onDone?.Invoke(UnavailableReason); return; }
        if (direct && CannotSendDirect(toUid, out string directError)) { onDone?.Invoke(directError); return; }
        if (text.Length == 0)                        { onDone?.Invoke("메시지를 입력해 주세요."); return; }
        if (text.Length > MAX_LENGTH)                { onDone?.Invoke($"메시지는 {MAX_LENGTH}자까지 보낼 수 있습니다."); return; }
        if (sending || Time.unscaledTime < nextSendTime) { onDone?.Invoke("너무 빨리 보내고 있습니다. 잠시 후 다시 보내 주세요."); return; }
        if (!TryGetSender(out string uid))           { onDone?.Invoke("계정 정보를 확인하는 중입니다. 잠시 후 다시 보내 주세요."); return; }
        if (direct && toUid == uid)                  { onDone?.Invoke("나에게는 개인 메시지를 보낼 수 없습니다."); return; }

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

        Action<string> done = error =>
        {
            if (this == null) return;
            sending = false;
            onDone?.Invoke(error);
        };

        if (direct) SendDirect(uid, toUid, message, done);
        else        service.Send(message, done);
    }

    private bool TryGetSender(out string uid)
    {
        uid = connectedKey;
        if (uid == null) return false;

        // 게스트(또는 Local) — 지금 세이브가 곧 게스트 세이브이므로 바로 보냄
        if (backend == Backend.Local || AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn) return true;

        // 로그인 계정 = 지금 세이브의 주인 + 서버 닉네임을 받아 온 뒤 (RankingManager.CanUpload 와 같은 조건)
        if (!ProfileSync.TryGetAccount(out string account) || account != uid) return false;
        return ProfileSync.Instance == null || ProfileSync.Instance.IsPulledFor(uid);
    }
}
