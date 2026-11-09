using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>길드 채팅 — GuildManager 의 내 길드를 따라 뒤끝챗 길드 채널에 들어가고(IChatService.SetGuild), 받은 메시지를 따로 보관합니다.</summary>
// 길드 정보는 계정마다 한 번 받고(길드 창을 열지 않아도 길드 탭이 보이게), 그 뒤엔 길드 창 · 실시간 알림(가입 승인 등) · 길드 채널에서 내보내짐 때 다시 받음
// 안 읽은 수 = '여기까지 읽음' 시각(기기별 PlayerPrefs, 계정·길드마다) 뒤에 온 다른 사람의 메시지 (차단 제외)
public partial class ChatManager
{
    private const float  GuildRetrySeconds = 60f;   // 길드 정보 받기에 실패했을 때 다시 받기까지
    private const string GuildReadPrefix   = "chat.guildRead.";

    /// <summary>보낸 시각 순 최근 길드 채팅 메시지.</summary>
    public IReadOnlyList<ChatMessage> GuildMessages => guildMessages.Items;

    /// <summary>길드에 들어가 있는가 (길드 탭을 보여 줄지).</summary>
    public bool HasGuild => guildKey != null;

    /// <summary>내 길드 이름 (길드가 없으면 null).</summary>
    public string GuildName { get; private set; }

    /// <summary>지금 길드 채팅을 쓸 수 없는 이유. null 이면 사용 가능.</summary>
    public string GuildUnavailableReason { get; private set; }

    /// <summary>안 읽은 길드 메시지 수 (받아 둔 최근 메시지 안에서).</summary>
    public int GuildUnread { get; private set; }

    private readonly MessageLog guildMessages = new MessageLog();

    private string guildKey;          // 들어가 있어야 할 길드 inDate (null = 길드 없음)
    private string guildAccount;      // guildKey 가 어느 계정의 길드인지
    private string guildLoadedFor;    // 길드 정보를 받아 오기 시작한 계정
    private float  nextGuildLoadTime;
    private bool   guildWasReady;

    private bool   guildWatched;        // 채팅 창에서 길드 탭을 보고 있음 (ChatPanel 이 정함)
    private long   guildReadAt = -1;    // 이 시각(UTC ms)까지 읽음. -1 = 아직 모름 (처음 들어간 길드)
    private string guildReadKey;        // PlayerPrefs 키

    private void AwakeGuild()   => BackendNotify.GuildChanged += OnRealtimeGuild;
    private void DestroyGuild() => BackendNotify.GuildChanged -= OnRealtimeGuild;

    private void OnRealtimeGuild()
    {
        if (chatLink.Key != null) GuildManager.Refresh(loadBrowse: false);
    }

    /// <summary>길드 탭을 보고 있는 동안 true (ChatPanel) — 보는 동안 온 메시지는 바로 읽음 처리.</summary>
    public void WatchGuild(bool on) => guildWatched = on;

    private void ClearGuild()
    {
        guildMessages.Clear();
        guildWasReady = false;
        GuildUnread   = 0;
    }

    /// <summary>매 프레임 (Update, UnavailableReason 을 정한 뒤) — 길드 정보 받기 · 길드 채널 입장 · 안 읽은 수 · 쓸 수 없는 이유.</summary>
    private void UpdateGuild()
    {
        string account = chatLink.Key;
        bool   mine    = account != null && GuildManager.MyUid == account;

        LoadGuildIfNeeded(account, mine);

        GuildInfo guild = mine ? GuildManager.MyGuild : null;   // 받기에 잠깐 실패해도 받아 둔 길드는 유지됨 (GuildManager)
        string key = guild?.inDate;

        if (key != guildKey || (key != null && account != guildAccount))
        {
            guildKey     = key;
            guildAccount = key != null ? account : null;
            ClearGuild();   // 길드를 나갔거나 옮김 — 이전 길드 대화를 비움 (guildWasReady 도 꺼져 아래에서 다시 받지 않음)
            LoadGuildReadAt();
            changed = true;
        }
        if (guild?.name != GuildName)
        {
            GuildName = guild?.name;
            changed = true;
        }

        service.SetGuild(key);   // 바뀔 때만 실제로 나가고 들어감

        // 연결은 그대로인데 길드 채널에서만 내보내짐 → 추방됐을 수 있으니 길드 정보를 다시 받음
        bool ready = service.GuildReady;
        if (guildWasReady && !ready && key != null && account != null) GuildManager.Refresh(loadBrowse: false);
        guildWasReady = ready;

        // 처음 들어간 길드 — 입장할 때 받은 기록(OnGuildMessagesReceived)이 없었으면 지금부터 온 것만 안 읽음으로
        if (ready && guildReadAt < 0) SetGuildReadAt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (guildWatched && key != null) SetGuildReadAt(NewestGuildMessage());
        CountGuildUnread();   // 차단·계정이 바뀌어도 맞게 매 프레임 (최대 historyCount 개)

        string reason = UnavailableReason;
        if (reason == null)
        {
            if (key == null)
                reason = !mine || GuildManager.State == GuildState.Unknown || GuildManager.State == GuildState.Loading
                       ? GameMessages.Chat.guildLoading : GameMessages.Chat.guildNone;
            else if (!ready)
                reason = service.GuildFailReason != null
                       ? ChatText.Fill(GameMessages.Chat.guildJoinFailed, "reason", service.GuildFailReason)
                       : GameMessages.Chat.guildJoining;
        }

        if (reason != GuildUnavailableReason)
        {
            GuildUnavailableReason = reason;
            changed = true;
        }
    }

    /// <summary>계정마다 한 번 (실패하면 GuildRetrySeconds 마다) 내 길드를 받아 옴. Local 테스트 서버면 받지 않음 (길드 창을 열면 그때 받음).</summary>
    private void LoadGuildIfNeeded(string account, bool mine)
    {
        if (account == null || backend != Backend.Backnd) return;

        bool firstTime = guildLoadedFor != account;
        bool retry     = mine && GuildManager.State == GuildState.Failed && Time.unscaledTime >= nextGuildLoadTime;
        if (!firstTime && !retry) return;

        guildLoadedFor    = account;
        nextGuildLoadTime = Time.unscaledTime + GuildRetrySeconds;
        GuildManager.Refresh(loadBrowse: false);   // 길드 여부만 — 길드 목록은 길드 창을 열 때
    }

    private void OnGuildMessagesReceived(IReadOnlyList<ChatMessage> received)
    {
        if (guildKey == null || !guildMessages.Add(received, historyCount)) return;
        changed = true;

        // 처음 들어간 길드의 입장 기록은 읽은 것으로 (기록 50개가 한꺼번에 안 읽음으로 뜨지 않게)
        if (guildReadAt < 0) SetGuildReadAt(NewestGuildMessage());
    }

    // ───────────────────────────── 안 읽은 수 ─────────────────────────────

    private void LoadGuildReadAt()
    {
        guildReadKey = guildKey != null ? GuildReadPrefix + guildAccount + "." + guildKey : null;
        guildReadAt  = guildReadKey != null
                    && long.TryParse(PlayerPrefs.GetString(guildReadKey, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms)
                     ? ms : -1;
    }

    private void SetGuildReadAt(long ms)
    {
        if (guildReadKey == null || ms <= guildReadAt) return;
        guildReadAt = ms;
        PlayerPrefs.SetString(guildReadKey, ms.ToString(CultureInfo.InvariantCulture));   // 디스크 저장은 Unity 가 앱을 멈추거나 끌 때
    }

    private long NewestGuildMessage()
    {
        IReadOnlyList<ChatMessage> list = guildMessages.Items;
        return list.Count > 0 ? list[list.Count - 1].SentAt : guildReadAt;   // 보낸 시각 순 정렬
    }

    private void CountGuildUnread()
    {
        int count = 0;
        if (guildReadAt >= 0 && !guildWatched)
        {
            string me = MyUid;
            foreach (ChatMessage m in guildMessages.Items)
                if (m.SentAt > guildReadAt && m.Uid != me && !IsBlocked(m.Uid)) count++;
        }

        if (count != GuildUnread)
        {
            GuildUnread = count;
            changed = true;
        }
    }
}
