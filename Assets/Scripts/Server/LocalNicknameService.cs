using System;

/// <summary>
/// 서버 없는 시험용 닉네임 — 이 실행 동안만 메모리에 들고 있고, 중복 검사 없이 항상 성공합니다.
/// 쓰려면 ServerServices.Nickname = new LocalNicknameService(); (예: 에디터 테스트 코드에서)
/// </summary>
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
