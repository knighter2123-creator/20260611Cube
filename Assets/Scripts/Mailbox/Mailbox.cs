using System;
using System.Collections.Generic;
using System.Text;
using Manager.currency;
using UnityEngine;

/// <summary>
/// 우편함 — 보상을 우편으로 받아 두었다가 [받기] 로 지급합니다.
///
/// [두 종류의 우편]
///   로컬 우편 — 게임이 Send 로 직접 넣은 우편. SaveData.mailbox 에 저장 (GachaTicket 과 같은 방식 — 매니저 오브젝트 없음).
///   서버 우편 — 뒤끝 우편 (관리자 · 주간 랭킹 보상 · 쿠폰). MailboxSync 가 받아 와 메모리에만 둡니다 (서버가 원본).
///   화면(MailboxPanel)·개수 표시(OpenMailboxButton)는 둘을 합쳐서 봅니다.
///
/// [받기]
///   로컬 — 받음 표시(claimed)를 먼저 하고 지급한 뒤 저장.
///   서버 — 서버에 '받음' 처리를 먼저 하고(IMailService.Claim), 성공한 뒤에 지급·저장합니다.
///          그래서 결과가 다음 프레임 이후에 옵니다. 진행 중에는 IsClaiming, 실패하면 LastError + OnChanged.
///          뒤끝은 받은 우편을 목록에서 빼므로, 받은 기록을 로컬 우편함에 복사해 둡니다 (같은 id 의 서버 우편은 숨김).
///   서버 우편은 로그인 계정 + 온라인 + 지금 세이브의 주인일 때만 받을 수 있습니다 (다른 계정 세이브에 지급하지 않게).
///
/// [받은 우편]  지우지 않고 "받음" 으로 남깁니다. 세이브가 끝없이 커지지 않게 받은 기록은 MAX_CLAIMED_KEPT 개까지만
///   (넘으면 가장 오래전에 받은 것부터 정리). 받은 기록은 이 기기 세이브에 있으므로 클라우드 세이브와 함께 옮겨집니다.
/// </summary>
public static class Mailbox
{
    /// <summary>남겨 두는 받은 우편 수. 넘으면 오래전에 받은 것부터 정리합니다.</summary>
    public const int MAX_CLAIMED_KEPT = 50;

    /// <summary>우편이 들어오거나 받아서 없어질 때, 받기가 실패했을 때. (우편함 창 · 버튼 개수 표시용)</summary>
    public static event Action OnChanged;

    /// <summary>마지막 받기 실패 사유 (화면 표시용). 다음 받기를 시작하면 비워집니다.</summary>
    public static string LastError { get; private set; }

    /// <summary>서버 우편을 받는 중인가 (버튼 연타 방지).</summary>
    public static bool IsClaiming { get; private set; }

    /// <summary>서버 우편 목록을 받아 오는 중인가.</summary>
    public static bool IsRefreshing { get; private set; }

    private static readonly List<MailItem> serverMails = new List<MailItem>();
    private static string serverUid;   // serverMails 가 어느 계정 것인가

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged    = null;
        LastError    = null;
        IsClaiming   = false;
        IsRefreshing = false;
        serverMails.Clear();
        serverUid = null;
    }

    /// <summary>지금 세이브의 로컬 우편함. 세이브를 읽을 수 없으면 null.</summary>
    public static MailboxSaveData Data
    {
        get
        {
            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Current : null;
            if (d == null) return null;
            if (d.mailbox == null) d.mailbox = new MailboxSaveData();
            if (d.mailbox.mails == null) d.mailbox.mails = new List<MailItem>();
            return d.mailbox;
        }
    }

    /// <summary>
    /// 지금 계정의 아직 안 받은 서버 우편 (계정이 바뀌었으면 빈 목록).
    /// 로컬 우편함에 이미 받은 기록이 있는 id 는 뺍니다 — 서버가 받은 우편을 한동안 더 돌려줘도 두 번 보이지 않게.
    /// </summary>
    private static List<MailItem> CurrentServerMails()
    {
        if (!ProfileSync.TryGetAccount(out string uid) || uid != serverUid) return new List<MailItem>();
        MailboxSaveData box = Data;
        return box == null ? new List<MailItem>(serverMails)
                           : serverMails.FindAll(s => !box.mails.Exists(m => m.id == s.id));
    }

    /// <summary>아직 받지 않은 우편 수 (버튼 개수 표시).</summary>
    public static int Count
    {
        get
        {
            MailboxSaveData box = Data;
            int local = box != null ? box.mails.FindAll(m => !m.claimed).Count : 0;
            return local + CurrentServerMails().Count;
        }
    }

    /// <summary>로컬 + 서버 우편 — 안 받은 것 먼저, 그 안에서 최신 순. 목록을 고치지 마세요.</summary>
    public static List<MailItem> NewestFirst()
    {
        var list = new List<MailItem>();
        MailboxSaveData box = Data;
        if (box != null) list.AddRange(box.mails);
        list.AddRange(CurrentServerMails());
        list.Sort((a, b) => a.claimed != b.claimed ? a.claimed.CompareTo(b.claimed) : b.sentAt.CompareTo(a.sentAt));
        return list;
    }

    // ───────────────────────────── 로컬 우편 넣기 ─────────────────────────────

    /// <summary>로컬 우편을 넣습니다. 같은 id 가 이미 있으면 false. 저장은 부른 쪽이 합니다.</summary>
    public static bool Send(MailItem mail)
    {
        MailboxSaveData box = Data;
        if (box == null || mail == null || string.IsNullOrEmpty(mail.id)) return false;
        if (box.mails.Exists(m => m.id == mail.id)) return false;

        mail.postType = null;   // 로컬 우편
        box.mails.Add(mail);
        GameLog.Info($"[Mailbox] 우편 도착: {mail.title} ({RewardText(mail.rewards)})");
        OnChanged?.Invoke();
        return true;
    }

    // ───────────────────────────── 서버 우편 받아 오기 ─────────────────────────────

    /// <summary>서버 우편 목록을 다시 받아 옵니다 (MailboxSync · 우편함 창 열기). 조건이 안 되면 아무것도 안 함.</summary>
    public static void Refresh()
    {
        if (IsRefreshing || IsClaiming) return;
        if (!CanUseServer(out string uid)) return;

        IsRefreshing = true;
        ServerServices.Mail.Fetch((mails, error) =>
        {
            IsRefreshing = false;
            if (error != null)
            {
                Debug.LogWarning($"[Mailbox] 서버 우편 받아 오기 실패: {error}");
                return;
            }
            if (!ProfileSync.TryGetAccount(out string nowUid) || nowUid != uid) return;   // 계정이 바뀜

            serverUid = uid;
            serverMails.Clear();
            serverMails.AddRange(mails);
            OnChanged?.Invoke();
        });
    }

    /// <summary>로그아웃·계정 전환 때 — 다른 계정의 서버 우편을 보여 주지 않게 비웁니다.</summary>
    public static void ClearServerMails()
    {
        if (serverMails.Count == 0 && serverUid == null) return;
        serverMails.Clear();
        serverUid = null;
        OnChanged?.Invoke();
    }

    private static bool CanUseServer(out string uid)
    {
        uid = null;
        return ProfileSync.TryGetAccount(out uid)
            && AuthManager.Instance != null && AuthManager.Instance.IsOnline
            && SaveManager.Instance != null && SaveManager.Instance.IsGameplayStarted;
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    /// <summary>
    /// 우편 하나를 받습니다. 로컬 우편은 바로 지급, 서버 우편은 서버 확인 뒤 지급 (결과는 OnChanged / LastError).
    /// 시작했으면 true.
    /// </summary>
    public static bool TryClaim(string mailId)
    {
        if (IsClaiming || !CanGrant()) return false;
        LastError = null;

        MailboxSaveData box = Data;
        if (box.mails.Exists(m => m.id == mailId))
        {
            if (!ClaimLocal(mailId)) return false;   // 이미 받음
            TrimClaimed();
            SaveManager.Instance.Save();
            OnChanged?.Invoke();
            return true;
        }

        MailItem server = CurrentServerMails().Find(m => m.id == mailId);
        if (server == null) return false;

        ClaimServer(new Queue<MailItem>(new[] { server }));
        return true;
    }

    /// <summary>모든 우편을 받습니다. 로컬은 바로, 서버 우편은 하나씩 차례로 (게임이 모르는 보상이 든 우편은 건너뜀).</summary>
    public static void ClaimAll()
    {
        if (IsClaiming || !CanGrant()) return;
        LastError = null;

        int local = 0;
        foreach (MailItem m in Data.mails.ToArray())
            if (ClaimLocal(m.id)) local++;
        if (local > 0)
        {
            TrimClaimed();
            SaveManager.Instance.Save();
            OnChanged?.Invoke();
        }

        var queue = new Queue<MailItem>();
        foreach (MailItem m in CurrentServerMails())
            if (!m.hasUnknownReward) queue.Enqueue(m);
        if (queue.Count > 0) ClaimServer(queue);
    }

    private static bool CanGrant()
    {
        if (Data == null) return false;
        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[Mailbox] CurrencyManager 없음 — 우편 받기 취소");
            return false;
        }
        return true;
    }

    private static bool ClaimLocal(string mailId)
    {
        MailItem mail = Data.mails.Find(m => m.id == mailId);
        if (mail == null || mail.claimed) return false;

        MarkClaimed(mail);   // 표시하고 나서 지급 — 지급 중 예외가 나도 두 번 받지 않게
        Grant(mail);
        return true;
    }

    private static void MarkClaimed(MailItem mail)
    {
        mail.claimed   = true;
        mail.claimedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>받은 기록이 MAX_CLAIMED_KEPT 를 넘으면 가장 오래전에 받은 것부터 정리 (안 받은 우편은 건드리지 않음).</summary>
    private static void TrimClaimed()
    {
        List<MailItem> mails = Data.mails;
        List<MailItem> claimed = mails.FindAll(m => m.claimed);
        if (claimed.Count <= MAX_CLAIMED_KEPT) return;

        claimed.Sort((a, b) => a.claimedAt.CompareTo(b.claimedAt));
        for (int i = 0; i < claimed.Count - MAX_CLAIMED_KEPT; i++) mails.Remove(claimed[i]);
    }

    /// <summary>서버 우편을 하나씩: 서버 '받음' 처리 → 성공하면 지급·저장 → 다음. 실패하면 거기서 멈춤.</summary>
    private static void ClaimServer(Queue<MailItem> queue)
    {
        if (queue.Count == 0) { IsClaiming = false; return; }

        MailItem mail = queue.Dequeue();
        if (mail.hasUnknownReward)
        {
            Fail(GameMessages.Mail.needUpdate);
            return;
        }
        if (!CanUseServer(out string uid) || uid != serverUid)
        {
            Fail(GameMessages.Mail.needOnline);
            return;
        }

        IsClaiming = true;
        OnChanged?.Invoke();   // 버튼 잠금 표시

        ServerServices.Mail.Claim(mail, error =>
        {
            if (error != null) { Fail(error); return; }

            // 받는 사이 계정이 바뀌었으면 지급하지 않음 (서버에서는 이미 받음 처리 — 드문 경우라 로그만)
            if (!ProfileSync.TryGetAccount(out string nowUid) || nowUid != uid || SaveManager.Instance == null)
            {
                Debug.LogError($"[Mailbox] 우편을 받는 사이 계정이 바뀌어 지급하지 못했습니다: {mail.title}");
                Fail(GameMessages.Mail.accountChanged);
                return;
            }

            // 뒤끝은 받은 우편을 목록에서 빼므로 받은 기록을 로컬 우편함에 남김 (화면에서 "받음" 으로 보이게)
            serverMails.Remove(mail);
            MarkClaimed(mail);
            if (!Data.mails.Exists(m => m.id == mail.id)) Data.mails.Add(mail);
            Grant(mail);
            TrimClaimed();
            SaveManager.Instance.Save();
            OnChanged?.Invoke();

            ClaimServer(queue);
        });
    }

    private static void Fail(string message)
    {
        IsClaiming = false;
        LastError  = message;
        OnChanged?.Invoke();
    }

    private static void Grant(MailItem mail)
    {
        if (mail.rewards != null && CurrencyManager.Instance != null)
            foreach (MailReward r in mail.rewards)
                if (r != null && r.amount > 0)
                    CurrencyManager.Instance.AddCurrency(r.type, r.amount);

        GameLog.Info($"[Mailbox] 우편 받기: {mail.title} ({RewardText(mail.rewards)})");
    }

    // ───────── 표시 ─────────

    public static string CurrencyName(CurrencyType type)
    {
        switch (type)
        {
            case CurrencyType.Gold:        return "골드";
            case CurrencyType.Gem:         return "보석";
            case CurrencyType.GachaTicket: return "소환권";
            default:                       return type.ToString();
        }
    }

    /// <summary>"보석 3,000  +  소환권 10". 가운뎃점(·)은 BMJUA 폰트에 없어 쓰지 않습니다.</summary>
    public static string RewardText(List<MailReward> rewards)
    {
        if (rewards == null || rewards.Count == 0) return "-";
        var sb = new StringBuilder();
        foreach (MailReward r in rewards)
        {
            if (r == null || r.amount <= 0) continue;
            if (sb.Length > 0) sb.Append("  +  ");
            sb.Append(CurrencyName(r.type)).Append(' ').Append(r.amount.ToString("N0"));
        }
        return sb.Length > 0 ? sb.ToString() : "-";
    }
}
