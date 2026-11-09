using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>공지사항 한 개 (뒤끝 콘솔 > 공지사항 에서 작성한 것).</summary>
public class NoticeItem
{
    public string inDate;
    public string title;
    public string content;
    public string author;
    public string linkUrl;          // 콘솔의 '링크' (없으면 null)
    public string linkButtonName;   // 콘솔의 '링크 버튼 이름' (없으면 null)
    public long   postedAt;         // UTC unix ms (게시일)

    public bool HasLink => !string.IsNullOrEmpty(linkUrl);

    /// <summary>"2026.11.09" (기기 시간대)</summary>
    public string DateText => postedAt <= 0 ? string.Empty
        : DateTimeOffset.FromUnixTimeMilliseconds(postedAt).ToLocalTime().ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
}

/// <summary>뒤끝 공지사항 읽기 전용 — 작성·수정은 뒤끝 콘솔에서만. 목록을 페이지 단위로 받아 두고 OnChanged 로 알림.</summary>
// 새 공지 실시간 알림(BackendNotify.NoticeCreated)이 오면 첫 페이지를 다시 받음. 안 읽은 공지 = 마지막으로 창을 연 뒤 게시된 것 (기기별 PlayerPrefs)
public static class NoticeManager
{
    /// <summary>한 번에 받아 오는 공지 수 (뒤끝 최대 100).</summary>
    public const int PAGE_SIZE = 10;

    /// <summary>창을 열지 않아도 이 간격(초)이 지나면 버튼이 다시 받아 옴 (안 읽은 개수 표시용).</summary>
    public const float STALE_SECONDS = 30f;

    private const string LastSeenKey = "notice.lastSeenMs";

    /// <summary>목록이 바뀌었거나, 받기를 시작/실패했을 때 (메인 스레드).</summary>
    public static event Action OnChanged;

    private static readonly List<NoticeItem> items = new List<NoticeItem>();
    public static IReadOnlyList<NoticeItem> Items => items;

    public static bool   IsLoading  { get; private set; }
    /// <summary>한 번이라도 받아 왔는가 (실패만 했으면 false).</summary>
    public static bool   HasLoaded  { get; private set; }
    /// <summary>다음 페이지가 있는가.</summary>
    public static bool   HasMore    => !string.IsNullOrEmpty(nextOffset);
    /// <summary>마지막 받기 실패 사유 (화면 표시용). 성공하면 null.</summary>
    public static bool   LastFailed { get; private set; }
    public static bool   LastFailedOffline { get; private set; }

    private static string nextOffset;
    private static int    requestId;       // Refresh 가 진행 중인 LoadMore 응답을 버리게
    private static float  lastFetchTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged         = null;
        items.Clear();
        IsLoading         = false;
        HasLoaded         = false;
        LastFailed        = false;
        LastFailedOffline = false;
        nextOffset        = null;
        requestId         = 0;
        lastFetchTime     = float.NegativeInfinity;
    }

    // BackendNotify.ResetStatics(SubsystemRegistration) 뒤에 붙여야 이벤트가 지워지지 않음
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookRealtime() => BackendNotify.NoticeCreated += () => Refresh();

    /// <summary>뒤끝에 로그인돼 있어 공지를 받을 수 있는가.</summary>
    public static bool CanFetch => BackendManager.IsInitialized
                                && AuthManager.Instance != null && AuthManager.Instance.IsOnline;

    /// <summary>첫 페이지부터 다시 받아 옴 (창 열기 · 새 공지 알림).</summary>
    public static void Refresh()
    {
        if (!CanFetch) return;
        Fetch(null);
    }

    /// <summary>다음 페이지를 이어서 받아 옴 ([더 보기]).</summary>
    public static void LoadMore()
    {
        if (IsLoading || !HasMore || !CanFetch) return;
        Fetch(nextOffset);
    }

    /// <summary>마지막으로 받은 지 STALE_SECONDS 가 지났으면 다시 받음 (열기 버튼이 부름).</summary>
    public static void RefreshIfStale()
    {
        if (IsLoading || Time.unscaledTime - lastFetchTime < STALE_SECONDS) return;
        Refresh();
    }

    private static void Fetch(string offset)
    {
        int id = ++requestId;
        IsLoading     = true;
        lastFetchTime = Time.unscaledTime;
        OnChanged?.Invoke();

        BackendManager.Call(cb =>
        {
            if (offset == null) Backend.Notice.NoticeList(PAGE_SIZE, cb);
            else                Backend.Notice.NoticeList(PAGE_SIZE, offset, cb);
        }, bro =>
        {
            if (id != requestId) return;   // 그 사이 Refresh 가 새로 시작됨
            IsLoading = false;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Notice] 공지 목록 실패: {BackendManager.Describe(bro)}");
                LastFailed        = true;
                LastFailedOffline = BackendManager.IsNetworkError(bro);
                OnChanged?.Invoke();
                return;
            }

            var page = new List<NoticeItem>();
            try
            {
                JsonData rows = BackendJson.Get(bro.GetReturnValuetoJSON(), "rows");
                if (rows != null && rows.IsArray)
                    foreach (JsonData row in rows)
                    {
                        // 콘솔에서 '비공개'로 둔 공지는 보이지 않게 (목록에 섞여 와도)
                        string isPublic = BackendJson.Str(row, "isPublic");
                        if (isPublic != null && isPublic.Equals("n", StringComparison.OrdinalIgnoreCase)) continue;
                        page.Add(ToNotice(row));
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Notice] 공지 읽기 실패: {e.Message}\n{bro.GetReturnValue()}");
            }

            if (offset == null) items.Clear();
            foreach (NoticeItem n in page)
                if (!items.Exists(x => x.inDate == n.inDate)) items.Add(n);

            nextOffset        = bro.LastEvaluatedKeyString();
            HasLoaded         = true;
            LastFailed        = false;
            LastFailedOffline = false;
            OnChanged?.Invoke();
        });
    }
    
    
    

    private static NoticeItem ToNotice(JsonData row)
    {
        string inDate = BackendJson.Str(row, "inDate");
        return new NoticeItem
        {
            inDate         = inDate,
            title          = BackendJson.Str(row, "title") ?? string.Empty,
            content        = BackendJson.Str(row, "content") ?? string.Empty,
            author         = BackendJson.Str(row, "author"),
            linkUrl        = NullIfEmpty(BackendJson.Str(row, "linkUrl")),
            linkButtonName = NullIfEmpty(BackendJson.Str(row, "linkButtonName")),
            postedAt       = BackendJson.DateMs(row, "postingDate", BackendJson.DateMs(row, "inDate")),
        };
    }

    private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) || s == "null" ? null : s.Trim();

    // ───────────────────────────── 안 읽은 공지 ─────────────────────────────

    /// <summary>마지막으로 공지 창에서 본 가장 새 공지의 게시 시각 (UTC ms, 기기별).</summary>
    public static long LastSeenMs
    {
        get => long.TryParse(PlayerPrefs.GetString(LastSeenKey, "0"), out long v) ? v : 0;
        private set { PlayerPrefs.SetString(LastSeenKey, value.ToString(CultureInfo.InvariantCulture)); PlayerPrefs.Save(); }
    }

    /// <summary>받아 둔 공지 중 LastSeenMs 이후에 게시된 수.</summary>
    public static int UnreadCount
    {
        get
        {
            long seen = LastSeenMs;
            int count = 0;
            foreach (NoticeItem n in items) if (n.postedAt > seen) count++;
            return count;
        }
    }

    /// <summary>받아 둔 공지를 모두 읽음으로 (공지 창이 목록을 그릴 때).</summary>
    public static void MarkAllSeen()
    {
        long newest = LastSeenMs;
        foreach (NoticeItem n in items) if (n.postedAt > newest) newest = n.postedAt;
        if (newest == LastSeenMs) return;
        LastSeenMs = newest;
        OnChanged?.Invoke();
    }
}
