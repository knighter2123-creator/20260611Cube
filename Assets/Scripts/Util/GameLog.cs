using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// 진행 상황을 알려 주는 "정보성" 로그 전용 출구.
///
/// [왜 Debug.Log 를 직접 쓰지 않는가]
///   에디터에서 Debug.Log 한 번은 스택 트레이스 추출 + 콘솔 갱신 + Editor.log 파일 쓰기까지 하므로
///   생각보다 비쌉니다. 가챠 100연차(조각 로그 100줄), 씬 진입(동료 회수 로그 수십 줄)처럼
///   한 프레임에 몰리면 눈에 띄는 끊김이 됩니다. 안드로이드 빌드에도 그대로 들어가 logcat 으로 나갑니다.
///
/// [Conditional 의 효과]
///   GAME_VERBOSE_LOG 심볼이 없으면 컴파일러가 GameLog.Info(...) "호출 자체" 를 지웁니다.
///   인자로 넘긴 $"..." 문자열 보간도 실행되지 않으므로 문자열 할당(GC)까지 사라집니다.
///   (런타임 bool 검사로는 인자 문자열이 이미 만들어진 뒤라 이 이득을 얻을 수 없습니다.)
///
/// [켜고 끄기]
///   메뉴 Tools → Game Log → 상세 로그(Info) 켜기/끄기. (현재 빌드 타깃의 Scripting Define 을 바꿉니다)
///
/// ★ 경고/에러는 이 클래스를 거치지 말고 Debug.LogWarning / Debug.LogError 를 그대로 쓰세요.
///   문제 상황은 항상 보여야 합니다.
/// ★ ContextMenu 진단·치트처럼 "직접 눌러서 보는" 출력도 Debug.Log 그대로 둡니다.
/// </summary>
public static class GameLog
{
    public const string VerboseSymbol = "GAME_VERBOSE_LOG";

    [Conditional(VerboseSymbol)]
    public static void Info(object message, Object context = null) => Debug.Log(message, context);
}
