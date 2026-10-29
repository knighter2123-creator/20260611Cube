using System;
using System.Collections.Generic;

/// <summary>
/// 채팅 서버와 실제로 주고받는 부분. ChatManager 는 이 인터페이스만 알고, 어느 서버인지는 모릅니다.
///
/// [구현체]
///   FirestoreChatService — Firestore 실시간 리스너 (기본)
///   LocalChatService     — 서버 없이 내 메시지만 보이는 테스트용
///   → Realtime Database 등으로 바꾸려면 이 인터페이스를 구현한 클래스를 추가하고 ChatManager.CreateService 에 한 줄 추가하면 됩니다.
///
/// [약속]  모든 이벤트·콜백은 메인 스레드에서 호출합니다 (Unity API 를 바로 써도 되게).
/// </summary>
public interface IChatService
{
    /// <summary>새로 받은 전체 채팅 메시지. 처음 연결하면 최근 기록이 한 번에 옵니다. 같은 메시지가 다시 올 수 있습니다 (받는 쪽이 Id 로 거름).</summary>
    event Action<IReadOnlyList<ChatMessage>> MessagesReceived;

    /// <summary>목록에서 빠진 메시지 Id (서버가 거절한 내 메시지, 최근 N개 밖으로 밀려난 메시지).</summary>
    event Action<IReadOnlyList<string>> MessagesRemoved;

    /// <summary>연결이 끊겼을 때 (권한 오류 등). 받은 뒤엔 연결이 끊긴 상태입니다.</summary>
    event Action<string> Failed;

    /// <summary>같은 서버·같은 신원으로 쓰는 친구·차단 기능.</summary>
    IFriendService Friends { get; }

    /// <summary>같은 서버·같은 신원으로 쓰는 1:1 개인 메시지.</summary>
    IDirectService Direct { get; }

    /// <summary>
    /// 이 서버에서 "나" 를 나타내는 ID (보낸 사람 uid, 내 메시지 강조에 씀). 매 프레임 불러도 됩니다.
    /// 아직 준비가 안 됐으면 false 와 함께 reason 에 이유를 담습니다 (필요하면 이 안에서 준비를 시작).
    /// </summary>
    bool TryGetIdentity(out string uid, out string reason);

    /// <summary>최근 historyCount 개를 받아 오고, 이후 새 메시지를 계속 받습니다.</summary>
    void Connect(int historyCount);

    void Disconnect();

    /// <summary>보내기. 성공이면 onDone(null), 실패면 onDone(오류 문구).</summary>
    void Send(ChatMessage message, Action<string> onDone);
}
