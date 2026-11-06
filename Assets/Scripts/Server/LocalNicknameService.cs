using System;

/// <summary>서버 없는 시험용 닉네임 — 실행 중 메모리에만 두고 항상 성공 (ServerServices.Nickname = new LocalNicknameService() 로 사용).</summary>
public class LocalNicknameService : INicknameService
{
    private string nickname = string.Empty;

    public void FetchMine(Action<string, string> onDone)
        => BackendManager.Post(() => onDone?.Invoke(nickname, null));

    public void Set(string value, bool hasServerName, Action<NicknameResult> onDone)
    {
        nickname = value ?? string.Empty;
        BackendManager.Post(() => onDone?.Invoke(NicknameResult.Success));   // 실제 서버처럼 다음 프레임에 응답
    }
}
