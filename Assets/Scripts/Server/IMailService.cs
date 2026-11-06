using System;
using System.Collections.Generic;

/// <summary>서버 우편 인터페이스(목록·받기 처리만, 지급은 Mailbox 가) — 콜백은 메인 스레드, 구현은 ServerServices.Mail 에서 고름.</summary>
public interface IMailService
{
    /// <summary>받을 수 있는 서버 우편 전체. onDone(목록, 실패 문구 — 성공이면 null)</summary>
    void Fetch(Action<List<MailItem>, string> onDone);

    /// <summary>서버에서 이 우편을 '받음' 으로 처리합니다. onDone(실패 문구 — 성공이면 null). 성공한 뒤에 지급하세요.</summary>
    void Claim(MailItem mail, Action<string> onDone);
}
