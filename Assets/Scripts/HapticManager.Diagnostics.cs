using System.Collections;
using UnityEngine;

/// <summary>
/// 실기기에서 "어느 진동 경로가 살아 있는지" 직접 재보는 진단 파일.
///
/// 진동이 안 올 때 원인 후보가 넷인데 서로 증상이 똑같습니다.
///   ① VIBRATE 권한 없음
///   ② VibrationAttributes(USAGE_TOUCH) 를 시스템이 억제
///   ③ JNI 호출 자체가 실패
///   ④ 사실은 울리고 있는데 너무 약해서 못 느낌
///
/// 네 가지를 구분하려고, 서로 다른 경로로 400ms 짜리 강한 진동을
/// 1.2초 간격으로 차례로 쏘고 각 단계를 로그로 남깁니다.
/// 몇 번째에서 느껴졌는지만 알면 원인이 확정됩니다.
///
/// ★ 이 파일에 Handheld.Vibrate() 가 들어 있는 것 자체가 하나의 장치입니다.
///   유니티는 코드에 Handheld.Vibrate() 가 있으면 빌드할 때
///   uses-permission VIBRATE 를 매니페스트에 자동으로 넣어줍니다.
///   즉 이 파일을 추가하는 것만으로 ① 권한이 변수에서 빠집니다.
///
/// 원인을 찾은 뒤에는 이 파일을 지워도 됩니다.
/// (단, 지우면 권한 자동 추가도 같이 사라지니 매니페스트를 직접 확인할 것)
/// </summary>
public partial class HapticManager
{
    private Coroutine diagnostics;

    /// <summary>
    /// 진단 시퀀스 시작. 화면의 'VIB' 버튼이나 임시 버튼에 연결해서 쓰세요.
    /// 유저 설정(userEnabled)과 쿨다운을 모두 무시합니다 — 지금은 기기가 되는지만 봅니다.
    /// </summary>
    public void RunDiagnosticSequence()
    {
        if (diagnostics != null) StopCoroutine(diagnostics);
        diagnostics = StartCoroutine(DiagnosticRoutine());
    }

    private IEnumerator DiagnosticRoutine()
    {
        Debug.Log("[Haptic] ===== 진동 진단 시작 — 4단계, 각 400ms =====");

        // WaitForSecondsRealtime 을 쓰는 이유 — 설정 패널에서 실행하면
        // Time.timeScale 이 0이라 WaitForSeconds 는 영원히 끝나지 않습니다.
        var gap = new WaitForSecondsRealtime(1.2f);

        // ── ① 유니티 기본 ──
        // 가장 단순한 경로. 이것만 되면 우리 JNI 코드 쪽에 문제가 있다는 뜻입니다.
        Debug.Log("[Haptic] ① Handheld.Vibrate() — 유니티 기본 (약 500ms)");
        Handheld.Vibrate();
        yield return gap;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator == null || vibrationEffectClass == null)
        {
            Debug.LogError("[Haptic] Vibrator 를 못 잡아 ②~④ 를 건너뜁니다. 초기화 로그를 확인하세요.");
            diagnostics = null;
            yield break;
        }

        // ── ② 속성 없이 ──
        // VibrationAttributes 를 넣기 전의 동작입니다.
        // ③이 안 되고 이게 되면 USAGE_TOUCH 억제가 원인으로 확정됩니다.
        Debug.Log("[Haptic] ② createOneShot + 속성 없음");
        TryVibrate(() =>
        {
            using (var e = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", 400L, 255))
                vibrator.Call("vibrate", e);
        }, "②");
        yield return gap;

        // ── ③ USAGE_TOUCH 속성 ──
        Debug.Log("[Haptic] ③ createOneShot + VibrationAttributes(USAGE_TOUCH)");
        if (touchAttributes == null)
        {
            Debug.LogWarning("[Haptic] ③ 건너뜀 — touchAttributes 가 null 입니다.");
        }
        else
        {
            TryVibrate(() =>
            {
                using (var e = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", 400L, 255))
                    vibrator.Call("vibrate", e, touchAttributes);
            }, "③");
        }
        yield return gap;

        // ── ④ 구형 API ──
        // deprecated 지만 아직 동작합니다. 위가 다 안 되는데 이게 되는 기기도 있습니다.
        Debug.Log("[Haptic] ④ vibrate(long) — 구형 경로");
        TryVibrate(() => vibrator.Call("vibrate", 400L), "④");
        yield return gap;
#else
        Debug.Log("[Haptic] (에디터) ②~④ 는 실기기에서만 동작합니다.");
        yield return gap;
#endif

        Debug.Log("[Haptic] ===== 진단 끝. 몇 번에서 느껴졌는지 확인하세요 =====");
        diagnostics = null;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 한 단계를 실행하고 예외를 붙잡아 남긴다.
    /// 예외가 나도 멈추지 않고 다음 단계로 넘어가야
    /// "어디까지 되고 어디부터 안 되는지"를 한 번에 알 수 있다.
    /// </summary>
    private static void TryVibrate(System.Action action, string label)
    {
        try
        {
            action();
            Debug.Log($"[Haptic] {label} 호출 성공 (예외 없음)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Haptic] {label} 실패: {e.Message}");
        }
    }
#endif
}