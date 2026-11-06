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

/// <summary>우편 한 통(id 로 찾음) — 받으면 지우지 않고 claimed 표시, postType 이 비면 로컬 우편·있으면 뒤끝 서버 우편.</summary>
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
