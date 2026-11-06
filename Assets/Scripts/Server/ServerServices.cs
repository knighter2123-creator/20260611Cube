using UnityEngine;

/// <summary>서버 구현(Nickname·Mail)을 고르는 단 한 곳 — 게임 코드는 인터페이스만 써서 서버 교체·Local 시험 때 여기 한 줄만 바꾸면 됨 (setter 는 테스트용, 플레이마다 기본값 복구).</summary>
public static class ServerServices
{
    public static INicknameService Nickname { get; set; } = new BackendNicknameService();
    public static IMailService     Mail     { get; set; } = new BackendMailService();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Nickname = new BackendNicknameService();
        Mail     = new BackendMailService();
    }
}
