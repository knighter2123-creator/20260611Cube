using UnityEngine;

/// <summary>
/// 목표 프레임. Application.targetFrameRate 를 정하지 않으면 안드로이드는 기본 30fps 로 묶여
/// 화면 전체가 뚝뚝 끊겨 보입니다. 씬보다 먼저 한 번 정합니다.
/// (에디터·PC 는 QualitySettings 의 VSync 가 우선이라 영향이 거의 없습니다)
/// </summary>
public static class FrameRateSetup
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Application.targetFrameRate = TargetFps;
    }
}
