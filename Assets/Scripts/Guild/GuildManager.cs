using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using BackEnd.Functions;
using UnityEngine;
using GuildApi = BackEnd.Functions.GuildFunctions;

public enum GuildRole  { None, Member, ViceMaster, Master }
public enum GuildState { Unknown, Loading, NoGuild, InGuild, Failed }

public class GuildInfo
{
    public string inDate;
    public string name;
    public string masterInDate;
    public string masterNickname;
    public int    memberCount;
    public string intro = string.Empty;
    public bool   autoApprove;
    public readonly HashSet<string> viceMasters = new HashSet<string>();   // gamer inDate
}

public class GuildMember
{
    public string     gamerInDate;
    public string     nickname;
    public GuildRole  role;
    public long       lastLoginMs;   // UTC ms (모르면 0)
}

public class GuildApplicant
{
    public string gamerInDate;
    public string nickname;
}

/// <summary>뒤끝 길드(Backend.NewFunctions.Guild): 내 길드·멤버·가입 신청·길드 목록을 받아 두고 OnChanged 로 알림. 작업이 끝나면 다시 받아 서버와 맞춤.</summary>
// 길드 '재화'(goodsCount)는 쓰지 않음 — 만들 때 1. 소개는 customGuildInfo 의 "intro" (수정은 구버전 Backend.Guild.ModifyGuildV5)
// ⚠ 응답 코드(404/409/412 등)의 뜻은 뒤끝 문서 기준 — 실서버에서 다르면 각 작업의 특수 문구를 고칠 것
public static class GuildManager
{
    public const int    PAGE_SIZE  = 20;    // 길드 목록 한 번에
    public const int    MEMBER_MAX = 100;   // 멤버·신청 목록 한 번에
    public const string INTRO_KEY  = "intro";

    public static event Action OnChanged;

    public static GuildState State      { get; private set; }
    public static string     LastError  { get; private set; }
    /// <summary>만들기·가입·추방 등 작업 중 (버튼 연타 방지).</summary>
    public static bool       IsBusy     { get; private set; }

    public static GuildInfo  MyGuild    { get; private set; }
    public static GuildRole  MyRole     { get; private set; }
    public static string     MyUid      => accountUid;

    private static readonly List<GuildMember>    members    = new List<GuildMember>();
    private static readonly List<GuildApplicant> applicants = new List<GuildApplicant>();
    private static readonly List<GuildInfo>      browse     = new List<GuildInfo>();
    private static readonly HashSet<string>      appliedTo  = new HashSet<string>();   // 이번 접속에서 가입 신청한 길드

    public static IReadOnlyList<GuildMember>    Members    => members;
    public static IReadOnlyList<GuildApplicant> Applicants => applicants;
    public static IReadOnlyList<GuildInfo>      Browse     => browse;

    public static bool IsBrowseLoading { get; private set; }
    public static bool BrowseHasMore   => !string.IsNullOrEmpty(browseNextKey);
    /// <summary>목록이 이름 검색 결과인가 (false = 전체 목록).</summary>
    public static bool IsSearchResult  { get; private set; }
    public static bool HasApplied(string guildInDate) => appliedTo.Contains(guildInDate);

    private static string accountUid;
    private static int    generation;      // 계정이 바뀌거나 새로 받기 시작하면 올려 늦은 응답을 버림
    private static int    browseGeneration;
    private static string browseNextKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged = null;
        accountUid = null;
        generation = browseGeneration = 0;
        ClearData();
    }

    private static void ClearData()
    {
        State = GuildState.Unknown;
        LastError = null;
        IsBusy = false;
        MyGuild = null;
        MyRole = GuildRole.None;
        members.Clear();
        applicants.Clear();
        browse.Clear();
        appliedTo.Clear();
        IsBrowseLoading = false;
        IsSearchResult = false;
        browseNextKey = null;
    }

    private static void Changed() => OnChanged?.Invoke();

    /// <summary>뒤끝에 이 계정으로 물을 수 있으면 uid. 계정이 바뀌었으면 받아 둔 것을 비움.</summary>
    private static bool TryAccount(out string uid)
    {
        uid = AuthManager.Instance != null ? AuthManager.Instance.Uid : null;
        string problem = BackendSession.Problem(uid, GameMessages.Guild.loginRequired);
        if (uid != accountUid)
        {
            accountUid = uid;
            generation++;
            browseGeneration++;
            ClearData();
        }
        if (problem == null) return true;

        // 잠깐 오프라인 등 — 이미 받은 내 길드는 그대로 둠 (길드 채팅이 끊기거나 길드 창이 목록 화면으로 바뀌지 않게)
        if (MyGuild == null) State = GuildState.Failed;
        LastError = problem;
        Changed();
        return false;
    }

    // ───────────────────────────── 내 길드 ─────────────────────────────

    /// <summary>내 길드 → (있으면) 멤버·신청 목록, (없으면 · loadBrowse) 길드 목록을 다시 받음.</summary>
    /// <param name="loadBrowse">false = 길드가 없어도 길드 목록은 받지 않음 (길드 창을 열지 않은 채 길드 여부만 알고 싶을 때 — 뒤끝 호출 절약)</param>
    public static void Refresh(bool loadBrowse = true)
    {
        if (!TryAccount(out _)) return;

        int gen = ++generation;
        if (MyGuild == null) State = GuildState.Loading;
        Changed();

        BackendManager.Call<GuildApi.BackendGuildReturnObject>(cb => Backend.NewFunctions.Guild.GetMyGuildInfo(cb), bro =>
        {
            if (gen != generation) return;

            if (bro != null && (bro.GetStatusCode() == "404" || bro.GetStatusCode() == "412"))   // 길드에 없음
            {
                MyGuild = null;
                MyRole  = GuildRole.None;
                members.Clear();
                applicants.Clear();
                State = GuildState.NoGuild;
                LastError = null;
                Changed();
                if (loadBrowse && (browse.Count == 0 || IsSearchResult)) LoadGuildList();
                return;
            }
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Guild] 내 길드 조회 실패: {BackendManager.Describe(bro)}");
                if (MyGuild == null) State = GuildState.Failed;
                LastError = BackendManager.IsNetworkError(bro) ? GameMessages.Common.offline : GameMessages.Guild.loadFailed;
                Changed();
                return;
            }

            MyGuild = ToGuild(bro.GetReturnValueByGuildItem());
            MyRole  = RoleOf(MyGuild, accountUid);
            State   = GuildState.InGuild;
            LastError = null;
            Changed();
            LoadMembers(gen);
            if (MyRole >= GuildRole.ViceMaster) LoadApplicants(gen);
            else applicants.Clear();
        });
    }

    private static void LoadMembers(int gen)
    {
        string guild = MyGuild?.inDate;
        if (guild == null) return;

        BackendManager.Call<GuildApi.BackendGuildMemberListReturnObject>(cb => Backend.NewFunctions.Guild.GetGuildMemberList(guild, MEMBER_MAX, cb), bro =>
        {
            if (gen != generation) return;
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Guild] 멤버 목록 실패: {BackendManager.Describe(bro)}");
                LastError = BackendManager.IsNetworkError(bro) ? GameMessages.Common.offline : GameMessages.Guild.loadFailed;
                Changed();
                return;
            }

            members.Clear();
            List<GuildApi.BackendGuildMemberItem> list = bro.GetReturnValueByGuildMemberItemList();
            if (list != null)
                foreach (GuildApi.BackendGuildMemberItem m in list)
                    members.Add(new GuildMember
                    {
                        gamerInDate = m.gamerInDate,
                        nickname    = string.IsNullOrEmpty(m.nickname) ? GameMessages.Friends.noName : m.nickname,
                        role        = ParseRole(m.position, m.gamerInDate, MyGuild),
                        lastLoginMs = ParseMs(m.lastLogin),
                    });

            // 길드장 → 부길드장 → 멤버, 같은 직책은 최근 접속 순
            members.Sort((a, b) => a.role != b.role ? b.role.CompareTo(a.role) : b.lastLoginMs.CompareTo(a.lastLoginMs));

            GuildMember me = members.Find(x => x.gamerInDate == accountUid);
            if (me != null && me.role != MyRole)
            {
                MyRole = me.role;
                if (MyRole >= GuildRole.ViceMaster) LoadApplicants(gen);
                else applicants.Clear();   // 부길드장에서 내려옴 — 볼 수 없는 신청 목록이 버튼에 남지 않게
            }
            Changed();
        });
    }

    private static void LoadApplicants(int gen)
    {
        BackendManager.Call<GuildApi.BackendGuildJoinRequestReturnObject>(cb => Backend.NewFunctions.Guild.GetGuildJoinRequestList(MEMBER_MAX, cb), bro =>
        {
            if (gen != generation) return;
            applicants.Clear();
            if (bro != null && bro.GetStatusCode() == "404") { Changed(); return; }   // 신청 없음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Guild] 가입 신청 목록 실패: {BackendManager.Describe(bro)}");
                Changed();
                return;
            }

            List<GuildApi.BackendGuildJoinRequestItem> list = bro.GetReturnValueByGuildList();
            if (list != null)
                foreach (GuildApi.BackendGuildJoinRequestItem a in list)
                    applicants.Add(new GuildApplicant
                    {
                        gamerInDate = a.inDate,
                        nickname    = string.IsNullOrEmpty(a.nickname) ? GameMessages.Friends.noName : a.nickname,
                    });
            Changed();
        });
    }

    // ───────────────────────────── 길드 목록 · 검색 ─────────────────────────────

    /// <summary>전체 길드 목록 첫 페이지 (more = true 면 다음 페이지를 이어 붙임).</summary>
    public static void LoadGuildList(bool more = false)
    {
        if (!TryAccount(out _)) return;
        if (more && (!BrowseHasMore || IsBrowseLoading || IsSearchResult)) return;

        int gen = ++browseGeneration;
        string key = more ? browseNextKey : null;
        IsBrowseLoading = true;
        Changed();

        BackendManager.Call<GuildApi.BackendGuildListReturnObject>(cb =>
        {
            if (key == null) Backend.NewFunctions.Guild.GetAllGuildList(PAGE_SIZE, cb);
            else             Backend.NewFunctions.Guild.GetAllGuildList(PAGE_SIZE, new FirstKey(key), cb);
        }, bro =>
        {
            if (gen != browseGeneration) return;
            IsBrowseLoading = false;

            if (!more) { browse.Clear(); IsSearchResult = false; browseNextKey = null; }

            if (bro != null && bro.GetStatusCode() == "404") { browseNextKey = null; Changed(); return; }   // 길드가 하나도 없음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Guild] 길드 목록 실패: {BackendManager.Describe(bro)}");
                LastError = BackendManager.IsNetworkError(bro) ? GameMessages.Common.offline : GameMessages.Guild.listFailed;
                Changed();
                return;
            }

            List<GuildApi.BackendGuildItem> list = bro.GetReturnValueByGuildList();
            if (list != null)
                foreach (GuildApi.BackendGuildItem g in list)
                {
                    GuildInfo info = ToGuild(g);
                    if (info.inDate != null && !browse.Exists(x => x.inDate == info.inDate)) browse.Add(info);
                }
            browseNextKey = bro.HasFirstKey() ? bro.FirstKeystring() : null;
            LastError = null;
            Changed();
        });
    }

    /// <summary>길드 이름으로 찾기 (정확히 같은 이름). 빈 이름이면 전체 목록.</summary>
    public static void Search(string guildName)
    {
        guildName = guildName?.Trim();
        if (string.IsNullOrEmpty(guildName)) { LoadGuildList(); return; }
        if (!TryAccount(out _)) return;

        int gen = ++browseGeneration;
        IsBrowseLoading = true;
        Changed();

        BackendManager.Call<GuildApi.BackendGuildInDateReturnObject>(cb => Backend.NewFunctions.Guild.GetGuildInDateByGuildName(guildName, false, cb), bro =>
        {
            if (gen != browseGeneration) return;

            string inDate = bro != null && bro.IsSuccess() ? bro.GetGuildInDate() : null;
            if (string.IsNullOrEmpty(inDate))
            {
                if (bro != null && !bro.IsSuccess() && bro.GetStatusCode() != "404")
                    Debug.LogWarning($"[Guild] 길드 이름 검색 실패: {BackendManager.Describe(bro)}");
                ShowSearch(null, BackendManager.IsNetworkError(bro) ? GameMessages.Common.offline : GameMessages.Guild.guildNotFound);
                return;
            }

            BackendManager.Call<GuildApi.BackendGuildReturnObject>(cb => Backend.NewFunctions.Guild.GetGuildInfo(inDate, cb), info =>
            {
                if (gen != browseGeneration) return;
                if (info == null || !info.IsSuccess())
                {
                    Debug.LogWarning($"[Guild] 길드 정보 실패: {BackendManager.Describe(info)}");
                    ShowSearch(null, GameMessages.Guild.guildNotFound);
                    return;
                }
                ShowSearch(ToGuild(info.GetReturnValueByGuildItem()), null);
            });
        });
    }

    private static void ShowSearch(GuildInfo found, string error)
    {
        IsBrowseLoading = false;
        IsSearchResult  = true;
        browseNextKey   = null;
        browse.Clear();
        if (found != null) browse.Add(found);
        LastError = error;
        Changed();
    }

    // ───────────────────────────── 작업 ─────────────────────────────

    /// <summary>길드 만들기 — 보석 gemCost 를 먼저 내고, 실패하면 돌려줌.</summary>
    public static void Create(string guildName, string intro, int gemCost, Action<string> onDone)
    {
        guildName = guildName?.Trim();
        if (string.IsNullOrEmpty(guildName)) { onDone?.Invoke(GameMessages.Guild.enterName); return; }
        if (IsBusy) { onDone?.Invoke(GameMessages.Guild.busy); return; }

        CurrencyManager wallet = CurrencyManager.Instance;
        if (gemCost > 0 && (wallet == null || !wallet.SpendGem(gemCost)))
        {
            onDone?.Invoke(ChatText.Fill(GameMessages.Guild.notEnoughGem, "cost", gemCost.ToString("N0")));
            return;
        }

        var param = new Param();
        param.Add(INTRO_KEY, intro?.Trim() ?? string.Empty);

        Act(cb => Backend.NewFunctions.Guild.CreateGuild(guildName, 1, param, cb), GameMessages.Guild.actionCreate, error =>
        {
            if (error != null && gemCost > 0)
            {
                // 같은 계정일 때만 돌려줌 (그 사이 계정이 바뀌었으면 다른 세이브에 넣지 않게)
                if (CurrencyManager.Instance != null && AuthManager.Instance != null && AuthManager.Instance.Uid == accountUid)
                    CurrencyManager.Instance.AddGem(gemCost);
                else
                    Debug.LogError($"[Guild] 길드 만들기 실패 후 계정이 바뀌어 보석 {gemCost} 을 돌려주지 못했습니다.");
            }
            onDone?.Invoke(error);
        }, (bro, action) => bro.GetStatusCode() switch
        {
            "409" => GameMessages.Guild.nameTaken,
            "400" => GameMessages.Guild.badName,
            "412" => GameMessages.Guild.alreadyInGuild,
            _     => null,
        });
    }

    public static void Join(string guildInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.RequestGuildJoin(guildInDate, cb), GameMessages.Guild.actionJoin, error =>
        {
            if (error == null) appliedTo.Add(guildInDate);
            onDone?.Invoke(error);
        }, (bro, action) => bro.GetStatusCode() switch
        {
            "409" => GameMessages.Guild.alreadyApplied,
            "412" => GameMessages.Guild.cannotJoin,
            "404" => GameMessages.Guild.guildNotFound,
            _     => null,
        });

    public static void Leave(Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.WithdrawGuild(cb), GameMessages.Guild.actionLeave, onDone,
               (bro, action) => MyRole == GuildRole.Master && (bro.GetStatusCode() == "403" || bro.GetStatusCode() == "412")
                   ? GameMessages.Guild.masterCannotLeave : null);

    public static void Kick(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.KickGuildMember(gamerInDate, cb), GameMessages.Guild.actionKick, onDone);

    public static void Approve(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.ApproveGuildJoinRequest(gamerInDate, cb), GameMessages.Guild.actionApprove, onDone,
               (bro, action) => bro.GetStatusCode() switch
               {
                   "412" => GameMessages.Guild.approveFailed,
                   "404" => GameMessages.Guild.applicantGone,
                   _     => null,
               });

    public static void Decline(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.DeclineGuildJoinRequest(gamerInDate, cb), GameMessages.Guild.actionDecline, onDone,
               (bro, action) => bro.GetStatusCode() == "404" ? GameMessages.Guild.applicantGone : null);

    public static void MakeMaster(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.ChangeGuildMaster(gamerInDate, cb), GameMessages.Guild.actionMaster, onDone);

    public static void MakeVice(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.ChangeViceGuildMaster(gamerInDate, cb), GameMessages.Guild.actionVice, onDone);

    public static void RemoveVice(string gamerInDate, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.RemoveViceGuildMaster(gamerInDate, cb), GameMessages.Guild.actionUnvice, onDone);

    public static void SetAutoApprove(bool on, Action<string> onDone)
        => Act(cb => Backend.NewFunctions.Guild.EnableAutoApproveGuildJoinRequest(on, cb), GameMessages.Guild.actionAuto, onDone);

    /// <summary>길드 소개 수정 (customGuildInfo 의 intro) — 구버전 API ModifyGuildV5 (V3·V4 는 SDK 에서 obsolete).</summary>
    public static void UpdateIntro(string intro, Action<string> onDone)
    {
        var param = new Param();
        param.Add(INTRO_KEY, intro?.Trim() ?? string.Empty);
        if (!TryBegin(onDone, out string uid)) return;

        BackendManager.Call(cb => Backend.Guild.ModifyGuildV5(param, cb), bro => Finish(uid, bro, GameMessages.Guild.actionIntro, onDone, null));
    }

    // 작업 공통: 계정 확인 → 한 번에 하나 → 결과 문구 → 성공·실패 모두 다시 받아 서버와 맞춤
    private static void Act(Action<Backend.BackendCallback<BackendReturnObject>> request, string action, Action<string> onDone,
                            Func<BackendReturnObject, string, string> special = null)
    {
        if (!TryBegin(onDone, out string uid)) return;
        BackendManager.Call<BackendReturnObject>(request, bro => Finish(uid, bro, action, onDone, special));
    }

    private static bool TryBegin(Action<string> onDone, out string uid)
    {
        uid = null;
        if (IsBusy) { onDone?.Invoke(GameMessages.Guild.busy); return false; }
        if (!TryAccount(out uid)) { onDone?.Invoke(LastError); return false; }
        IsBusy = true;
        Changed();
        return true;
    }

    private static void Finish(string uid, BackendReturnObject bro, string action, Action<string> onDone,
                               Func<BackendReturnObject, string, string> special)
    {
        IsBusy = false;
        AuthManager auth = AuthManager.Instance;
        if (auth == null || auth.Uid != uid) { onDone?.Invoke(GameMessages.Common.accountChanged); Changed(); return; }

        string error = null;
        if (bro == null || !bro.IsSuccess())
        {
            Debug.LogWarning($"[Guild] {action} 실패: {BackendManager.Describe(bro)}");
            error = BackendManager.IsNetworkError(bro) ? GameMessages.Common.offlineRetry
                  : special?.Invoke(bro, action)
                  ?? ChatText.Fill(GameMessages.Guild.actionFailed, "action", action, "code", bro.GetStatusCode());
        }
        onDone?.Invoke(error);
        Refresh();
    }

    // ───────────────────────────── 변환 ─────────────────────────────

    private static GuildInfo ToGuild(GuildApi.BackendGuildItem g)
    {
        var info = new GuildInfo();
        if (g == null) return info;

        info.inDate         = g.inDate;
        info.name           = g.guildName;
        info.masterInDate   = g.masterInDate;
        info.masterNickname = g.masterNickname;
        info.memberCount    = g.memberCount;
        info.autoApprove    = g._immediateRegistration;
        if (g.customGuildInfo != null && g.customGuildInfo.TryGetValue(INTRO_KEY, out string intro) && intro != null)
            info.intro = intro;
        // 부길드장 목록이 {inDate: 닉네임} 인지 {닉네임: inDate} 인지 문서에 없어 양쪽 다 넣음 (inDate 와 닉네임이 겹칠 일은 없음)
        if (g.viceMasterList != null)
            foreach (KeyValuePair<string, string> pair in g.viceMasterList)
            {
                if (!string.IsNullOrEmpty(pair.Key))   info.viceMasters.Add(pair.Key);
                if (!string.IsNullOrEmpty(pair.Value)) info.viceMasters.Add(pair.Value);
            }
        return info;
    }

    private static GuildRole RoleOf(GuildInfo guild, string uid)
    {
        if (guild == null || string.IsNullOrEmpty(uid)) return GuildRole.None;
        if (guild.masterInDate == uid)          return GuildRole.Master;
        if (guild.viceMasters.Contains(uid))    return GuildRole.ViceMaster;
        return GuildRole.Member;
    }

    private static GuildRole ParseRole(string position, string uid, GuildInfo guild)
    {
        switch ((position ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "master":     return GuildRole.Master;
            case "vicemaster": return GuildRole.ViceMaster;
            case "member":     return GuildRole.Member;
            default:           return RoleOf(guild, uid);   // 모르는 값이면 길드 정보로 판단
        }
    }

    private static long ParseMs(string date)
        => DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset d)
            ? d.ToUnixTimeMilliseconds() : 0;
}
