using System;
using System.Collections.Generic;
using Manager.currency;

/// <summary>우편 한 통에 담긴 재화 한 종류.</summary>
[Serializable]
public class MailReward
{
    public CurrencyType type;
    public int          amount;

    public MailReward() { }
    public MailReward(CurrencyType type, int amount) { this.type = type; this.amount = amount; }
}

/// <summary>
/// 우편 한 통. 받으면(Mailbox.TryClaim) 보상을 지급하고 claimed 로 표시합니다 — 목록에서 지우지 않습니다.
///   로컬 우편  — 게임이 직접 넣은 우편 (SaveData 에 저장). postType 이 비어 있음.
///   서버 우편  — 뒤끝 우편 (관리자·랭킹 보상·쿠폰). 저장하지 않고 MailboxSync 가 서버에서 받아 와 메모리에만 둠.
///                받으면 뒤끝이 목록에서 빼므로, 받은 기록은 로컬 우편함(SaveData)에 복사해 둡니다.
/// id 는 목록 안에서 우편을 찾는 열쇠입니다 (서버 우편은 "Rank:2026-11-09T...").
/// </summary>
[Serializable]
public class MailItem
{
    public string id;
    public string title;
    public string body;
    public long   sentAt;      // UTC unix ms
    public long   expiresAt;   // UTC unix ms, 0 = 없음 (서버 우편만)
    public List<MailReward> rewards = new List<MailReward>();

    // ── 서버 우편 (뒤끝) ──
    public string postType;    // "Admin" / "Rank" / "Coupon" — 비어 있으면 로컬 우편
    public string postInDate;  // 뒤끝 우편 inDate (받기 요청에 씀)
    /// <summary>게임이 모르는 보상이 들어 있음 (아이템 차트 itemType 이 맞지 않음) — 받으면 사라지므로 받기를 막습니다.</summary>
    public bool   hasUnknownReward;

    // ── 받음 표시 ──
    public bool   claimed;     // 받았음 — 목록에 남아 "받음" 으로 보임
    public long   claimedAt;   // UTC unix ms (받은 기록이 MAX_CLAIMED_KEPT 를 넘으면 오래된 것부터 정리)

    public bool IsServerMail => !string.IsNullOrEmpty(postType);
}

/// <summary>SaveData.mailbox — 로컬 우편함.</summary>
[Serializable]
public class MailboxSaveData
{
    public List<MailItem> mails = new List<MailItem>();
}
