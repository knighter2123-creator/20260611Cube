using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using BackndChat;
using UnityEngine;


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
        if (auth == null || !auth.IsReady)  { reason = GameMessages.Chat.serverPreparing; return false; }
        if (!auth.IsSignedIn)               { reason = GameMessages.Chat.loginRequired; return false; }
        if (!auth.IsOnline)                 { reason = GameMessages.Chat.offlineRetrying; return false; }
        if (auth.Uid != Backend.UserInDate) { reason = GameMessages.Chat.accountChecking; return false; }
        if (string.IsNullOrEmpty(auth.DisplayName)) { reason = GameMessages.Chat.nicknamePending; return false; }   // 뒤끝챗은 닉네임이 있어야 접속됨

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
            Failed?.Invoke(GameMessages.Chat.serverUnreachable);
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

        // 연결 오류는 Update 가 끝난 뒤 알림 (콜백 안이면 ChatManager 가 바로 Dispose 해 돌고 있는 client.Update 를 정리하게 됨)
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
        if (client == null || joinedGroup == null) { onDone?.Invoke(GameMessages.Chat.joiningChannel); return; }

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
            FinishPending(GameMessages.Chat.sendFailed);
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
            case ERROR_MESSAGE.MESSAGE_FILTERED:   return GameMessages.Chat.filtered;
            case ERROR_MESSAGE.MESSAGE_SPAM:       return GameMessages.Chat.tooFast;
            case ERROR_MESSAGE.MESSAGE_TOO_LONG:   return GameMessages.Chat.tooLong;
            case ERROR_MESSAGE.MESSAGE_TOO_SHORT:  return GameMessages.Chat.emptyMessage;
            case ERROR_MESSAGE.CHAT_BAN:
                return param is ErrorMessageChatBanParam ban
                    ? ChatText.Fill(GameMessages.Chat.bannedRemain, "remain", FormatSeconds(ban.RemainSeconds))
                    : GameMessages.Chat.banned;
            case ERROR_MESSAGE.NOT_NICKNAME:       return GameMessages.Chat.notNickname;
            case ERROR_MESSAGE.CHAT_SERVER_FULL:   return GameMessages.Chat.serverFull;
            case ERROR_MESSAGE.CHANNEL_FULL:       return GameMessages.Chat.channelFull;
            case ERROR_MESSAGE.DISABLED_SERVICE:   return GameMessages.Chat.disabledService;
            case ERROR_MESSAGE.DISABLED_CHANNEL:   return GameMessages.Chat.disabledChannel;
            case ERROR_MESSAGE.DUPLICATE_CONNECTION: return GameMessages.Chat.duplicateConnection;
            case ERROR_MESSAGE.NOT_AUTHENTICATION: return GameMessages.Chat.authFailed;
            default:                               return ChatText.Fill(GameMessages.Chat.unknownError, "error", error.ToString());
        }
    }

    private static string FormatSeconds(ulong s)
        => s >= 3600 ? ChatText.Fill(GameMessages.Chat.hoursMinutes, "h", (s / 3600).ToString(), "m", (s % 3600 / 60).ToString())
         : s >= 60  ? ChatText.Fill(GameMessages.Chat.minutes, "m", (s / 60).ToString())
                    : ChatText.Fill(GameMessages.Chat.seconds, "s", s.ToString());

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
            // uid 메타데이터가 없으면(다른 앱·콘솔 공지) 닉네임으로 대신 — 내 uid 와 겹치지 않게 접두사 (ChatMessage.HasPlayerUid)
            Uid      = system ? ChatMessage.SystemUid : !string.IsNullOrEmpty(uid) ? uid : ChatMessage.NameOnlyPrefix + m.GamerName,
            Nickname = system ? GameMessages.Chat.systemSender : m.GamerName,
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
