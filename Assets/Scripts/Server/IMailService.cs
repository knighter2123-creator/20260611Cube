using System;
using System.Collections.Generic;

/// <summary>
/// 서버 우편 — 받을 우편 목록과 받기. 지급(재화 추가·저장)은 부르는 쪽(Mailbox)이 합니다.
/// 모든 콜백은 메인 스레드. 구현: BackendMailService (뒤끝 우편). 고르는 곳: ServerServices.Mail
/// </summary>
public interface IMailService
{
    /// <summary>받을 수 있는 서버 우편 전체. onDone(목록, 실패 문구 — 성공이면 null)</summary>
    void Fetch(Action<List<MailItem>, string> onDone);

    /// <summary>서버에서 이 우편을 '받음' 으로 처리합니다. onDone(실패 문구 — 성공이면 null). 성공한 뒤에 지급하세요.</summary>
    void Claim(MailItem mail, Action<string> onDone);
}
