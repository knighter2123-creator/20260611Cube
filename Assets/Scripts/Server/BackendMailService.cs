using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using Manager.currency;
using UnityEngine;

/// <summary>뒤끝 우편(관리자·랭킹 보상·쿠폰)을 받아 MailItem 으로 변환 — 차트 줄의 itemType 으로 재화를 알아봄.</summary>
// [아이템 차트] itemType: gold/gem/gachaTicket(골드/보석/소환권도 됨), amount(선택, 기본 1) → 받는 양 = itemCount × amount
// ⚠ itemType 이 안 맞는 우편은 받기를 막음 (받으면 서버에서 사라지는데 지급할 방법이 없어서)
public class BackendMailService : IMailService
{
    // 이 순서로 받아 와 하나로 합침 (유저 간 우편 PostType.User 는 쓰지 않음)
    private static readonly PostType[] Types = { PostType.Admin, PostType.Rank, PostType.Coupon };

    private bool loggedRawOnce;   // 형식을 모르는 우편이 오면 원본 JSON 을 한 번만 로그 (문의·디버그용)

    public void Fetch(Action<List<MailItem>, string> onDone)
    {
        var all = new List<MailItem>();
        FetchFrom(0, all, 0, onDone);
    }

    private void FetchFrom(int index, List<MailItem> all, int failures, Action<List<MailItem>, string> onDone)
    {
        if (index >= Types.Length)
        {
            // 일부만 실패했으면 받은 만큼은 보여 줌. 전부 실패해야 실패
            onDone?.Invoke(failures == Types.Length ? null : all,
                           failures == Types.Length ? GameMessages.Mail.fetchFailed : null);
            return;
        }

        PostType type = Types[index];
        BackendManager.Call(cb => Backend.UPost.GetPostList(type, cb), bro =>
        {
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Mail] {type} 우편 목록 실패: {BackendManager.Describe(bro)}");
                FetchFrom(index + 1, all, failures + 1, onDone);
                return;
            }

            try
            {
                JsonData list = BackendJson.Get(bro.GetReturnValuetoJSON(), "postList");
                if (list != null && list.IsArray)
                    foreach (JsonData post in list) all.Add(ToMail(type, post));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Mail] {type} 우편 읽기 실패: {e.Message}\n{bro.GetReturnValue()}");
            }
            FetchFrom(index + 1, all, failures, onDone);
        });
    }

    public void Claim(MailItem mail, Action<string> onDone)
    {
        if (!Enum.TryParse(mail.postType, out PostType type))
        {
            onDone?.Invoke(GameMessages.Mail.unknownPost);
            return;
        }

        BackendManager.Call(cb => Backend.UPost.ReceivePostItem(type, mail.postInDate, cb), bro =>
        {
            if (bro != null && bro.IsSuccess()) { onDone?.Invoke(null); return; }

            Debug.LogWarning($"[Mail] 우편 받기 실패 ({mail.title}): {BackendManager.Describe(bro)}");
            onDone?.Invoke(BackendManager.IsNetworkError(bro)
                ? GameMessages.Common.offlineCheck
                : bro.GetStatusCode() == "404" ? GameMessages.Mail.gone : GameMessages.Mail.claimFailed);
        });
    }

    // ───────── 변환 ─────────

    private MailItem ToMail(PostType type, JsonData post)
    {
        string inDate = BackendJson.Str(post, "inDate");
        var mail = new MailItem
        {
            id         = $"{type}:{inDate}",
            title      = BackendJson.Str(post, "title") ?? GameMessages.Mail.defaultTitle,
            body       = BackendJson.Str(post, "content") ?? string.Empty,
            sentAt     = BackendJson.DateMs(post, "sentDate", BackendJson.DateMs(post, "inDate")),
            expiresAt  = BackendJson.DateMs(post, "expirationDate"),
            postType   = type.ToString(),
            postInDate = inDate,
        };

        JsonData items = BackendJson.Get(post, "items");
        if (items != null && items.IsArray)
        {
            foreach (JsonData entry in items)
            {
                long count    = BackendJson.Long(entry, "itemCount", 1);
                JsonData item = BackendJson.Get(entry, "item");

                if (TryReadCurrency(item, out CurrencyType currency, out long perItem))
                {
                    long amount = count * perItem;
                    mail.rewards.Add(new MailReward(currency, amount > int.MaxValue ? int.MaxValue : (int)amount));
                }
                else
                {
                    mail.hasUnknownReward = true;
                    if (!loggedRawOnce)
                    {
                        loggedRawOnce = true;
                        Debug.LogWarning($"[Mail] 아이템 차트에서 재화를 알아보지 못한 우편이 있습니다 (itemType 컬럼 확인 — BackendMailService 주석). 원본: {post.ToJson()}");
                    }
                }
            }
        }
        return mail;
    }

    private static bool TryReadCurrency(JsonData item, out CurrencyType type, out long perItem)
    {
        type    = CurrencyType.Gold;
        perItem = Math.Max(1, BackendJson.Long(item, "amount", 1));

        string key = (BackendJson.Str(item, "itemType") ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "gold": case "골드":                                  type = CurrencyType.Gold;        return true;
            case "gem": case "보석":                                   type = CurrencyType.Gem;         return true;
            case "gachaticket": case "ticket": case "소환권":          type = CurrencyType.GachaTicket; return true;
            default:                                                   return false;
        }
    }
}
