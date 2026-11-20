using System;
using UnityEngine;

/// <summary>레드닷 알림 종류. ★ 값(숫자)이 씬에 저장됩니다 — 새 값은 맨 뒤에 추가하세요.</summary>
public enum RedDotKey
{
    Mail           = 0,   // 안 받은 우편
    FriendRequest  = 1,   // 받은 친구 요청
    Chat           = 2,   // 안 읽은 개인 메시지 + 길드 채팅
    Inventory      = 3,   // 새로 들어온 아이템 (인벤토리 창을 닫으면 사라짐)
    Notice         = 4,   // 안 읽은 공지
    GuildApplicant = 5,   // 길드 가입 신청 (길드장·부길드장)
}

/// <summary>
/// 레드닷 상태를 한곳에서 셈 — 각 기능의 이벤트가 오면 바로, 이벤트가 없는 것(채팅·친구)은 checkInterval 마다 다시 셈.
/// 개수가 바뀐 프레임에만 Changed 를 부르므로 RedDotBadge 는 매 프레임 일하지 않음.
/// </summary>
public static class RedDot
{
    /// <summary>어떤 키든 개수가 바뀌면 (프레임당 최대 1회).</summary>
    public static event Action Changed;

    /// <summary>이벤트가 없어도 다시 세는 간격(초, 실제 시간).</summary>
    public const float CHECK_INTERVAL = 0.25f;

    private static readonly int KeyCount = Enum.GetValues(typeof(RedDotKey)).Length;
    private static int[] counts = new int[KeyCount];

    private static bool  dirty = true;
    private static float nextCheckTime;
    private static RedDotRunner runner;

    public static int  Count(RedDotKey key) => (int)key >= 0 && (int)key < counts.Length ? counts[(int)key] : 0;
    public static bool Has(RedDotKey key)   => Count(key) > 0;

    /// <summary>다음 프레임에 바로 다시 셈 (새 알림 원인을 추가했는데 그쪽에 이벤트가 없을 때 부르세요).</summary>
    public static void Recheck() => dirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed       = null;
        counts        = new int[KeyCount];
        dirty         = true;
        nextCheckTime = 0f;
        runner        = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // 각 매니저가 SubsystemRegistration 에서 자기 이벤트를 비우므로 그 뒤(BeforeSceneLoad)에 구독
        Mailbox.OnChanged           -= Recheck; Mailbox.OnChanged           += Recheck;
        Inventory.OnChanged         -= Recheck; Inventory.OnChanged         += Recheck;
        InventoryNewItems.OnChanged -= Recheck; InventoryNewItems.OnChanged += Recheck;
        NoticeManager.OnChanged     -= Recheck; NoticeManager.OnChanged     += Recheck;
        GuildManager.OnChanged      -= Recheck; GuildManager.OnChanged      += Recheck;

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRunner();
    }

    // AccountReset 이 DDOL 을 지우므로 씬마다 다시 만듦
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        EnsureRunner();
        dirty = true;
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("[RedDot]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<RedDotRunner>();
    }

    internal static void Tick()
    {
        float now = Time.unscaledTime;
        if (!dirty && now < nextCheckTime) return;
        dirty         = false;
        nextCheckTime = now + CHECK_INTERVAL;

        bool any = false;
        for (int i = 0; i < KeyCount; i++)
        {
            int c = Evaluate((RedDotKey)i);
            if (c == counts[i]) continue;
            counts[i] = c;
            any = true;
        }
        if (any) Changed?.Invoke();
    }

    /// <summary>알림 종류를 늘리려면 enum 에 값을 추가하고 여기에 한 줄 추가.</summary>
    private static int Evaluate(RedDotKey key)
    {
        try
        {
            ChatManager chat = ChatManager.Instance;
            switch (key)
            {
                case RedDotKey.Mail:           return Mailbox.Count;
                case RedDotKey.FriendRequest:  return chat != null ? chat.CountFriends(FriendState.Incoming) : 0;
                case RedDotKey.Chat:           return chat != null ? chat.TotalUnread + chat.GuildUnread : 0;
                case RedDotKey.Inventory:      return InventoryNewItems.Count;
                case RedDotKey.Notice:         return NoticeManager.UnreadCount;
                case RedDotKey.GuildApplicant: return GuildManager.MyRole >= GuildRole.ViceMaster ? GuildManager.Applicants.Count : 0;
            }
        }
        catch (Exception e)
        {
            // 한 기능이 깨져도 다른 레드닷은 계속 동작하게
            Debug.LogWarning($"[RedDot] {key} 개수를 세지 못했습니다: {e.Message}");
        }
        return 0;
    }
}

/// <summary>RedDot.Tick 을 매 프레임 부르는 DDOL 오브젝트 (RedDot 이 자동으로 만듦).</summary>
public sealed class RedDotRunner : MonoBehaviour
{
    private void Update() => RedDot.Tick();
}
