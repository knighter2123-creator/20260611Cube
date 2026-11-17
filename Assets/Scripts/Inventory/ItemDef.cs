using System;

/// <summary>차트 itemType 값.</summary>
public static class ItemTypes
{
    public const string Fragment    = "fragment";      // targetId = CompanionData.id
    public const string GachaTicket = "gachaTicket";
}

/// <summary>아이템 id 규칙 — 조각은 "fragment_" + 동료 id.</summary>
public static class ItemIds
{
    public const string GachaTicket    = "gacha_ticket";
    public const string FragmentPrefix = "fragment_";

    public static string Fragment(string companionId) => FragmentPrefix + companionId;

    public static bool TryGetFragmentTarget(string itemId, out string companionId)
    {
        companionId = null;
        if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith(FragmentPrefix, StringComparison.Ordinal)) return false;
        companionId = itemId.Substring(FragmentPrefix.Length);
        return companionId.Length > 0;
    }
}

/// <summary>아이템 정의 1행 (뒤끝 차트).</summary>
[Serializable]
public class ItemDef
{
    public string itemId;
    public string itemType;
    public string name;
    public string iconKey;
    public string targetId;
    public string description;
    public int    sortOrder;   // 작을수록 앞

    public bool IsFragment => itemType == ItemTypes.Fragment;
}
