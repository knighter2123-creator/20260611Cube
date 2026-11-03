using UnityEngine;

/// <summary>
/// 서버 기능을 고르는 단 한 곳 — 게임 코드는 구체 클래스(뒤끝 등)가 아니라 여기의 인터페이스만 씁니다.
///
/// [왜]  서버를 바꾸거나(Firebase → 뒤끝), 서버 없이 시험할 때(Local) 이 파일의 한 줄만 바꾸면 되게.
///       채팅의 IChatService(ChatManager.CreateService)와 같은 생각입니다.
///
/// [지금 있는 것]
///   Nickname  — INicknameService  (뒤끝: BackendNicknameService / 시험: LocalNicknameService)
///   Mail      — IMailService      (뒤끝 우편: BackendMailService)
///   (랭킹은 RankingManager 가 뒤끝 유저 리더보드를 바로 씀)
/// [다음 단계에서 추가]  클라우드 세이브 · 친구/쪽지 · 가챠 확률 · 채팅
///
/// 테스트 코드에서 바꿔 끼울 수 있게 setter 를 열어 둡니다. 플레이 시작 때마다 기본값으로 돌아갑니다.
/// </summary>
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
