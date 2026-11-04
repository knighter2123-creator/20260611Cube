using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using BackndChat;
using UnityEngine;

/// <summary>
/// 전체 채팅 — 뒤끝챗(BackndChat SDK)의 공개 채널 하나에서 모두가 이야기합니다.
/// 친구는 BackendFriendService(뒤끝 친구), 개인 메시지는 BackendDirectService(뒤끝 쪽지)가 맡습니다.
///
/// [뒤끝 콘솔 준비]
///   1) 뒤끝챗 > 채널 관리에서 공개(일반) 채널 그룹을 만듭니다 — 이름을 ChatManager 인스펙터 channelGroup 에 (기본 "global").
///   2) 유니티 메뉴의 뒤끝챗 설정 창에 뒤끝챗 UUID 를 넣습니다 (BackndChat SDK 가 Backend.GetBackndChatSettings 로 읽음).
///   3) 뒤끝 닉네임이 있어야 합니다 (없으면 NOT_NICKNAME) — ProfileSync 가 정한 뒤 연결합니다 (TryGetIdentity).
///
/// [보내는 사람 정보]  뒤끝챗 메시지에는 닉네임(GamerName)과 '보낸 사람 메타데이터' 가 실립니다.
///   메타데이터에 uid(뒤끝 gamer inDate) · progress(StageRecord.BestProgress) 를 넣어 두고, 받을 때 꺼내
///   ChatMessage.Uid / Progress 로 씁니다 (내 메시지 강조 · 차단 · 친구 요청이 uid 로 동작하도록).
///
/// [연결 흐름]  Connect → ChatClient 생성 → 채널 입장 요청(SendJoinOpenChannel) → OnJoinChannel 에서 최근 메시지 전달.
///   입장 응답이 joinTimeout 안에 없으면 다시 요청합니다. 연결 수준 오류(인증·중복 접속·서비스 꺼짐)는 Failed.
///
/// [보내기 결과]  뒤끝챗은 보낸 결과를 따로 주지 않아서, 내 메시지가 채널로 돌아오면 성공 · 그 전에 OnError 가 오면 실패로 봅니다.
///   sendTimeout 안에 둘 다 없으면 성공으로 칩니다 (채팅 입력이 영영 잠기지 않게).
///
/// [약속]  IChatService 와 같음 — 모든 이벤트·콜백은 메인 스레드. ChatClient 콜백은 Tick(=ChatClient.Update) 안에서 옵니다.
/// </summary>
public class BackendChatService : IChatService, IChatClientListener
{
    public event Action<IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<IReadOnlyList<string>>      MessagesRemoved;
    public event Action<string>                     Failed;

    public IFriendService Friends { get; } = new BackendFriendService();
    public IDirectService Direct  { get; } = new BackendDirectService();

    private const string MetaUid      = "uid";
    private const string MetaProgress = "progress";
    private const float  JoinTimeout  = 8f;
    private const float  SendTimeout  = 5f;

    private readonly string channelGroup;
    private readonly string channelName;

    private ChatClient client;
    private string     myUid;
    private int        historyCount = 50;

    // 입장한 채널 (null = 아직)
    private string joinedGroup;
    private string joinedName;
    private ulong  joinedNumber;
    private float  nextJoinTime;

    // 내 메타데이터로 보낸 마지막 값 — 바뀌면 UpdateMetadata / UpdateNickname
    private int    sentProgress = -1;
    private string sentName;

    private string failReason;   // OnError 에서 받은 연결 오류 — Tick 이 Update 뒤에 Failed 로 보냄

    // 보내는 중인 메시지 (한 번에 하나 — ChatManager 가 sending 으로 막음)
    private Action<string> pendingSend;
    private string         pendingText;
    private float          pendingUntil;

    /// <param name="channelGroup">뒤끝챗 콘솔의 공개 채널 그룹 이름</param>
    /// <param name="channelName">그 그룹 안의 채널 이름 — 비우면 그룹 이름과 같게</param>
    public BackendChatService(string channelGroup, string channelName)
    {
        this.channelGroup = string.IsNullOrWhiteSpace(channelGroup) ? "global" : channelGroup.Trim();
        this.channelName  = string.IsNullOrWhiteSpace(channelName) ? this.channelGroup : channelName.Trim();
    }

    // ───────────────────────────── 신원 ─────────────────────────────

    public bool TryGetIdentity(out string uid, out string reason)
    {
        uid    = null;
        reason = null;

        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsReady)  { reason = "서버 연결 준비 중입니다."; return false; }
        if (!auth.IsSignedIn)               { reason = "로그인하면 채팅을 할 수 있습니다."; return false; }
        if (!auth.IsOnline)                 { reason = "서버에 연결할 수 없습니다. 잠시 후 다시 시도합니다."; return false; }
        if (auth.Uid != Backend.UserInDate) { reason = "계정 정보를 확인하는 중입니다..."; return false; }
        if (string.IsNullOrEmpty(auth.DisplayName)) { reason = "닉네임을 정하는 중입니다..."; return false; }   // 뒤끝챗은 닉네임이 있어야 접속됨

        uid = auth.Uid;
        return true;
    }

    // ───────────────────────────── 연결 ─────────────────────────────

    public void Connect(int count)
    {
        Disconnect();

        AuthManager auth = AuthManager.Instance;
        myUid        = auth != null ? auth.Uid : null;
        historyCount = Math.Max(1, count);
        sentProgress = StageRecord.BestProgress;
        sentName     = auth != null ? auth.DisplayName : null;

        try
        {
            client = new ChatClient(this, new ChatClientArguments
            {
                Avatar   = "default",
                Metadata = new Dictionary<string, string>
                {
                    { MetaUid,      myUid ?? string.Empty },
                    { MetaProgress, sentProgress.ToString(CultureInfo.InvariantCulture) },
                },
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Chat] 뒤끝챗 연결 실패 — 뒤끝챗 설정(UUID)을 확인하세요: {e}");
            client = null;
            Failed?.Invoke("채팅 서버에 연결할 수 없습니다.");
            return;
        }

        RequestJoin();
    }

    public void Disconnect()
    {
        FinishPending(null);

        if (client != null)
        {
            try { client.Dispose(); }
            catch (Exception e) { Debug.LogWarning($"[Chat] 뒤끝챗 연결 정리 중 예외 (무시): {e.Message}"); }
        }
        client      = null;
        joinedGroup = null;
        joinedName  = null;
        myUid       = null;
        failReason  = null;
    }

    public void Tick()
    {
        if (client == null) return;

        try { client.Update(); }   // 이 안에서 IChatClientListener 콜백이 옴
        catch (Exception e) { Debug.LogException(e); }

        // 연결 오류는 Update 가 끝난 뒤에 알림 — 콜백 안에서 알리면 ChatManager 가 바로 Disconnect(=Dispose) 해서
        // 아직 돌고 있는 client.Update 안에서 자기 자신을 정리하게 됨
        if (failReason != null)
        {
            string reason = failReason;
            failReason = null;
            Failed?.Invoke(reason);
            return;
        }

        if (joinedGroup == null && Time.unscaledTime >= nextJoinTime) RequestJoin();
        if (pendingSend != null && Time.unscaledTime >= pendingUntil) FinishPending(null);
    }

    private void RequestJoin()
    {
        nextJoinTime = Time.unscaledTime + JoinTimeout;
        try { client.SendJoinOpenChannel(channelGroup, channelName); }
        catch (Exception e) { Debug.LogWarning($"[Chat] 채널 입장 요청 실패 — 잠시 뒤 다시: {e.Message}"); }
    }

    // ───────────────────────────── 보내기 ─────────────────────────────

    public void Send(ChatMessage message, Action<string> onDone)
    {
        if (client == null || joinedGroup == null) { onDone?.Invoke("채팅 채널에 들어가는 중입니다. 잠시 후 다시 보내 주세요."); return; }

        try
        {
            // 진행도·닉네임이 바뀌었으면 먼저 알림 — 이 메시지부터 새 값으로 보이게
            if (message.Progress != sentProgress)
            {
                sentProgress = message.Progress;
                client.UpdateMetadata(new Dictionary<string, string>
                {
                    { MetaUid,      myUid ?? string.Empty },
                    { MetaProgress, sentProgress.ToString(CultureInfo.InvariantCulture) },
                });
            }
            if (!string.IsNullOrEmpty(message.Nickname) && message.Nickname != sentName)
            {
                sentName = message.Nickname;
                client.UpdateNickname(sentName);
            }

            FinishPending(null);   // 혹시 남은 것 정리 (ChatManager 가 막으므로 보통 없음)
            pendingSend  = onDone;
            pendingText  = message.Text;
            pendingUntil = Time.unscaledTime + SendTimeout;

            client.SendChatMessage(joinedGroup, joinedName, joinedNumber, message.Text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Chat] 보내기 실패: {e.Message}");
            FinishPending("메시지를 보내지 못했습니다.");
        }
    }

    private void FinishPending(string error)
    {
        if (pendingSend == null) return;
        Action<string> done = pendingSend;
        pendingSend = null;
        pendingText = null;
        done(error);
    }

    // ───────────────────────────── 받기 (IChatClientListener) ─────────────────────────────

    public void OnJoinChannel(ChannelInfo channelInfo)
    {
        if (channelInfo == null || channelInfo.ChannelGroup != channelGroup) return;

        joinedGroup  = channelInfo.ChannelGroup;
        joinedName   = channelInfo.ChannelName;
        joinedNumber = channelInfo.ChannelNumber;
        GameLog.Info($"[Chat] 뒤끝챗 채널 입장: {joinedGroup}/{joinedName}#{joinedNumber}");

        var history = new List<ChatMessage>();
        if (channelInfo.Messages != null)
        {
            int start = Math.Max(0, channelInfo.Messages.Count - historyCount);
            for (int i = start; i < channelInfo.Messages.Count; i++)
                if (TryConvert(channelInfo.Messages[i], out ChatMessage m)) history.Add(m);
        }
        if (history.Count > 0) MessagesReceived?.Invoke(history);
    }

    public void OnLeaveChannel(ChannelInfo channelInfo)
    {
        if (channelInfo == null || channelInfo.ChannelGroup != joinedGroup) return;
        joinedGroup = null;   // Tick 이 다시 입장 요청
        nextJoinTime = Time.unscaledTime + 1f;
    }

    public void OnChatMessage(MessageInfo messageInfo)
    {
        if (!IsMyChannel(messageInfo) || !TryConvert(messageInfo, out ChatMessage m)) return;

        // 내가 보낸 메시지가 돌아옴 → 보내기 성공
        if (pendingSend != null && m.Uid == myUid && m.Text == pendingText) FinishPending(null);

        MessagesReceived?.Invoke(new[] { m });
    }

    public void OnHideMessage(MessageInfo messageInfo)   => Remove(messageInfo);
    public void OnDeleteMessage(MessageInfo messageInfo) => Remove(messageInfo);

    private void Remove(MessageInfo messageInfo)
    {
        if (!IsMyChannel(messageInfo)) return;
        MessagesRemoved?.Invoke(new[] { IdOf(messageInfo) });
    }

    public void OnError(ERROR_MESSAGE error, object param)
    {
        Debug.LogWarning($"[Chat] 뒤끝챗 오류: {error} {param}");

        switch (error)
        {
            // 연결 자체가 안 되는 오류 → 끊고 ChatManager 가 잠시 뒤 다시 연결
            case ERROR_MESSAGE.NOT_AUTHENTICATION:
            case ERROR_MESSAGE.DUPLICATE_CONNECTION:
            case ERROR_MESSAGE.DISABLED_SERVICE:
            case ERROR_MESSAGE.CHAT_SERVER_FULL:
            case ERROR_MESSAGE.NOT_NICKNAME:
                FinishPending(ToMessage(error, param));
                failReason = ToMessage(error, param);   // Tick 에서 client.Update 가 끝난 뒤 Failed
                return;

            // 채널 문제 → 다시 입장 시도
            case ERROR_MESSAGE.DISABLED_CHANNEL:
            case ERROR_MESSAGE.CHANNEL_FULL:
            case ERROR_MESSAGE.NOT_JOIN_CHANNEL:
                joinedGroup = null;
                FinishPending(ToMessage(error, param));
                return;

            case ERROR_MESSAGE.ALREADY_JOIN_CHANNEL:
                return;   // 이미 들어가 있음 (OnJoinChannel 이 이미 왔거나 곧 옴)

            default:
                FinishPending(ToMessage(error, param));   // 보내기 관련 (금칙어·도배·길이·채팅 금지 등)
                return;
        }
    }

    private static string ToMessage(ERROR_MESSAGE error, object param)
    {
        switch (error)
        {
            case ERROR_MESSAGE.MESSAGE_FILTERED:   return "사용할 수 없는 단어가 들어 있습니다.";
            case ERROR_MESSAGE.MESSAGE_SPAM:       return "너무 빨리 보내고 있습니다. 잠시 후 다시 보내 주세요.";
            case ERROR_MESSAGE.MESSAGE_TOO_LONG:   return "메시지가 너무 깁니다.";
            case ERROR_MESSAGE.MESSAGE_TOO_SHORT:  return "메시지를 입력해 주세요.";
            case ERROR_MESSAGE.CHAT_BAN:
                return param is ErrorMessageChatBanParam ban
                    ? $"채팅이 제한되었습니다. ({FormatSeconds(ban.RemainSeconds)} 남음)"
                    : "채팅이 제한되었습니다.";
            case ERROR_MESSAGE.NOT_NICKNAME:       return "닉네임을 정한 뒤 채팅할 수 있습니다.";
            case ERROR_MESSAGE.CHAT_SERVER_FULL:   return "채팅 서버가 가득 찼습니다.";
            case ERROR_MESSAGE.CHANNEL_FULL:       return "채팅 채널이 가득 찼습니다.";
            case ERROR_MESSAGE.DISABLED_SERVICE:   return "채팅 서비스가 꺼져 있습니다.";
            case ERROR_MESSAGE.DISABLED_CHANNEL:   return "채팅 채널을 쓸 수 없습니다.";
            case ERROR_MESSAGE.DUPLICATE_CONNECTION: return "다른 곳에서 같은 계정으로 채팅에 접속했습니다.";
            case ERROR_MESSAGE.NOT_AUTHENTICATION: return "채팅 서버 인증에 실패했습니다.";
            default:                               return $"채팅 오류 ({error})";
        }
    }

    private static string FormatSeconds(ulong s)
        => s >= 3600 ? $"{s / 3600}시간 {s % 3600 / 60}분" : s >= 60 ? $"{s / 60}분" : $"{s}초";

    // 이 게임에서 쓰지 않는 콜백 — 입장·퇴장한 사람 목록, 번역, 귓속말(개인 메시지는 쪽지로)
    public void OnJoinChannelPlayer(string channelGroup, string channelName, ulong channelNumber, PlayerInfo player) { }
    public void OnLeaveChannelPlayer(string channelGroup, string channelName, ulong channelNumber, PlayerInfo player) { }
    public void OnUpdatePlayerInfo(string channelGroup, string channelName, ulong channelNumber, PlayerInfo player) { }
    public void OnChangeGamerName(string oldGamerName, string newGamerName) { }
    public void OnWhisperMessage(WhisperMessageInfo messageInfo) { }
    public void OnTranslateMessage(Dictionary<string, MessageInfo> messages) { }
    public void OnSuccess(SUCCESS_MESSAGE success, object param) { }

    // ───────────────────────────── 변환 ─────────────────────────────

    private bool IsMyChannel(MessageInfo m)
        => m != null && joinedGroup != null && m.ChannelGroup == joinedGroup && m.ChannelName == joinedName && m.ChannelNumber == joinedNumber;

    private static string IdOf(MessageInfo m) => $"{m.ChannelGroup}/{m.ChannelName}/{m.ChannelNumber}/{m.Index}";

    private static bool TryConvert(MessageInfo m, out ChatMessage message)
    {
        message = default;
        if (m == null) return false;

        bool system = m.MessageType == MESSAGE_TYPE.SYSTEM_MESSAGE;
        string uid = null, progress = null;
        if (m.Metadata != null)
        {
            m.Metadata.TryGetValue(MetaUid, out uid);
            m.Metadata.TryGetValue(MetaProgress, out progress);
        }

        message = new ChatMessage
        {
            Id       = IdOf(m),
            // uid 메타데이터가 없으면(다른 앱·콘솔 공지) 닉네임으로 대신 — 내 uid 와 겹치지 않게 접두사
            Uid      = system ? "system" : !string.IsNullOrEmpty(uid) ? uid : "name:" + m.GamerName,
            Nickname = system ? "알림" : m.GamerName,
            Progress = int.TryParse(progress, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 0,
            Text     = m.Message ?? string.Empty,
            SentAt   = ParseTime(m.Time),
        };
        return true;
    }

    /// <summary>뒤끝챗 시각 문자열 → UTC ms. 숫자(초 또는 ms)와 날짜 문자열을 모두 받습니다. 읽지 못하면 지금.</summary>
    private static long ParseTime(string s)
    {
        if (!string.IsNullOrEmpty(s))
        {
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
                return n < 100000000000L ? n * 1000 : n;   // 초 단위면 ms 로
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset d))
                return d.ToUnixTimeMilliseconds();
        }
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
