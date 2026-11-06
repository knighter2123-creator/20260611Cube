using UnityEngine;

/// <summary>씬보다 먼저 targetFrameRate 설정 — 안 하면 안드로이드가 기본 30fps 로 묶여 끊겨 보임 (에디터·PC 는 VSync 우선).</summary>
public static class FrameRateSetup
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Application.targetFrameRate = TargetFps;
    }
}
