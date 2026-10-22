using System;
using System.Collections.Generic;

/// <summary>
/// 서버 없이 채팅 UI 를 확인하는 테스트용. 보낸 메시지가 나에게만 바로 돌아옵니다.
/// (ChatManager 의 Backend 를 Local 로 두면 로그인 없이도 에디터에서 써 볼 수 있습니다)
/// </summary>
public class LocalChatService : IChatService
{
    public event Action<IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<IReadOnlyList<string>> MessagesRemoved { add { } remove { } }   // 지울 일이 없음
    public event Action<string> Failed { add { } remove { } }                           // 끊길 일이 없음

    public bool TryGetIdentity(out string uid, out string reason)
    {
        uid    = "local";
        reason = null;
        return true;
    }

    public void Connect(int historyCount) { }
    public void Disconnect() { }

    public void Send(ChatMessage message, Action<string> onDone)
    {
        message.Id = Guid.NewGuid().ToString("N");
        MessagesReceived?.Invoke(new[] { message });
        onDone?.Invoke(null);
    }
}
