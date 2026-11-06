using System;

/// <summary>닉네임 서버 요청의 결과.</summary>
public enum NicknameResultKind
{
    Ok,
    Duplicate,   // 다른 유저가 쓰는 닉네임
    Invalid,     // 서버 규칙에 맞지 않음 (길이·금칙어 등)
    Offline,     // 서버에 닿지 못함 — 나중에 다시
    Session,     // 로그인 세션 문제 — 다시 로그인 필요
    Other,
}

public readonly struct NicknameResult
{
    public readonly NicknameResultKind Kind;
    /// <summary>유저에게 보여 줄 한국어 문구 (성공이면 빈 문자열)</summary>
    public readonly string Message;

    public NicknameResult(NicknameResultKind kind, string message) { Kind = kind; Message = message ?? string.Empty; }

    public bool Ok => Kind == NicknameResultKind.Ok;
    public static readonly NicknameResult Success = new NicknameResult(NicknameResultKind.Ok, string.Empty);
}

/// <summary>로그인 계정 닉네임 읽기·정하기 인터페이스 — 콜백은 메인 스레드, 구현(뒤끝/Local)은 ServerServices 에서 고름.</summary>
public interface INicknameService
{
    /// <summary>서버에 저장된 내 닉네임. onDone(닉네임 — 없으면 "", 실패 문구 — 성공이면 null)</summary>
    void FetchMine(Action<string, string> onDone);

    /// <summary>내 닉네임을 정함(hasServerName 으로 등록/변경 구분) — 길이·글자 검사는 PlayerProfile.Validate 가 먼저, 서버는 중복·금칙어만.</summary>
    void Set(string nickname, bool hasServerName, Action<NicknameResult> onDone);
}
