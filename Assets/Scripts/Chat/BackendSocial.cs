using BackEnd;

/// <summary>뒤끝 친구·쪽지 서비스(BackendFriendService / BackendDirectService)가 함께 쓰는 도우미.</summary>
public static class BackendSession
{
    /// <summary>이 계정으로 뒤끝에 물을 수 있는가(되면 null, 안 되면 이유) — 세션이 다른 계정이면 막아 목록이 섞이지 않게.</summary>
    public static string Problem(string uid, string signInPrompt)
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsSignedIn)                       return signInPrompt;
        if (!auth.IsOnline)                                         return GameMessages.Common.offline;
        if (string.IsNullOrEmpty(uid) || uid != Backend.UserInDate) return GameMessages.Common.accountChecking;
        return null;
    }
}

/// <summary>목록 다시 받기를 한 번에 하나만 돌리는 문지기 — 도중 재요청은 끝난 뒤 한 번 더, Reset 마다 세대를 올려 계정이 바뀐 뒤 늦은 응답은 버림.</summary>
public sealed class RefreshGate
{
    public int  Generation { get; private set; }
    public bool LoadedOnce { get; private set; }   // 이 세대에서 한 번이라도 받았는가

    private bool running;
    private bool again;

    public void Reset()
    {
        Generation++;
        LoadedOnce = false;
        running    = false;
        again      = false;
    }

    public bool IsCurrent(int generation) => generation == Generation;

    /// <summary>시작할 수 있으면 true. 이미 받는 중이면 끝난 뒤 한 번 더 돌도록 표시하고 false.</summary>
    public bool TryBegin(out int generation)
    {
        generation = Generation;
        if (running) { again = true; return false; }
        running = true;
        return true;
    }

    /// <summary>받기를 끝냅니다. 그사이 또 요청이 있었으면 true (= 바로 한 번 더 받을 것).</summary>
    public bool End(bool success)
    {
        running = false;
        if (success) LoadedOnce = true;
        if (!again) return false;
        again = false;
        return true;
    }
}
