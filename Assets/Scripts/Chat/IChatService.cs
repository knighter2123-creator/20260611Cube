using System;
using System.Collections.Generic;

/// <summary>채팅 서버 인터페이스(ChatManager 는 어느 서버인지 모름) — 구현: BackendChatService(뒤끝챗, 기본) / LocalChatService(테스트), 모든 이벤트·콜백은 메인 스레드.</summary>
// 서버를 바꾸려면 구현 클래스를 추가하고 ChatManager.CreateService 에 한 줄 추가
public interface IChatService
{
    /// <summary>새로 받은 전체·길드 채팅 메시지 (ChatMessage.Guild 로 구분). 채널에 들어가면 최근 기록이 한 번에 옵니다. 같은 메시지가 다시 올 수 있습니다 (받는 쪽이 Id 로 거름).</summary>
    event Action<IReadOnlyList<ChatMessage>> MessagesReceived;

    /// <summary>목록에서 빠진 메시지 Id (운영자가 가리거나 지운 메시지 — 뒤끝챗 OnHideMessage / OnDeleteMessage).</summary>
    event Action<IReadOnlyList<string>> MessagesRemoved;

    /// <summary>연결이 끊겼을 때 (권한 오류 등). 받은 뒤엔 연결이 끊긴 상태입니다.</summary>
    event Action<string> Failed;

    /// <summary>같은 서버·같은 신원으로 쓰는 친구·차단 기능.</summary>
    IFriendService Friends { get; }

    /// <summary>같은 서버·같은 신원으로 쓰는 1:1 개인 메시지.</summary>
    IDirectService Direct { get; }

    /// <summary>이 서버에서 나의 ID(매 프레임 호출 가능) — 준비 전이면 false + reason (필요하면 여기서 준비 시작).</summary>
    bool TryGetIdentity(out string uid, out string reason);

    /// <summary>최근 historyCount 개를 받아 오고, 이후 새 메시지를 계속 받습니다.</summary>
    void Connect(int historyCount);

    void Disconnect();

    /// <summary>매 프레임 (ChatManager.Update). 직접 돌려 줘야 하는 SDK(뒤끝챗 ChatClient.Update)용 — 할 일 없으면 비워 둠.</summary>
    void Tick();

    /// <summary>보내기 (message.Guild 면 길드 채널로). 성공이면 onDone(null), 실패면 onDone(오류 문구).</summary>
    void Send(ChatMessage message, Action<string> onDone);

    /// <summary>들어가 있어야 할 길드 (뒤끝 길드 inDate, null = 길드 없음) — 매 프레임 불러도 됨, 바뀔 때만 길드 채널을 나가고 들어감.</summary>
    void SetGuild(string guildKey);

    /// <summary>길드 채널에 들어가 있어 보낼 수 있는가.</summary>
    bool GuildReady { get; }

    /// <summary>길드 채널에 못 들어간 마지막 이유 (null = 없음·들어가는 중).</summary>
    string GuildFailReason { get; }
}
