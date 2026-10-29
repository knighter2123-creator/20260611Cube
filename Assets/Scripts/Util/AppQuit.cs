using UnityEngine;

/// <summary>
/// 앱 종료(에디터에서는 플레이 모드 종료) 중인지. 이때는 모든 오브젝트가 파괴되므로
/// OnDestroy 의 "살아 있어야 할 매니저가 죽었다" 경고를 건너뛸 때 씁니다.
/// </summary>
public static class AppQuit
{
    public static bool IsQuitting { get; private set; }

    // 도메인 리로드를 끈 에디터 설정에서도 플레이할 때마다 다시 false 로 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        IsQuitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    private static void OnQuitting() => IsQuitting = true;
}
