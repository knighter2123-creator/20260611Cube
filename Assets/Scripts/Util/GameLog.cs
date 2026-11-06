using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>정보성 로그 전용 출구 — [Conditional("GAME_VERBOSE_LOG")] 라 심볼이 없으면 호출과 인자 문자열 생성까지 컴파일에서 사라짐 (Debug.Log 는 몰리면 끊김).</summary>
// 켜기/끄기: Tools → Game Log / ⚠ 경고·에러와 직접 눌러 보는 진단 출력은 Debug.Log* 를 그대로 (항상 보여야 함)
public static class GameLog
{
    public const string VerboseSymbol = "GAME_VERBOSE_LOG";

    [Conditional(VerboseSymbol)]
    public static void Info(object message, Object context = null) => Debug.Log(message, context);
}
