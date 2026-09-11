using System.Text;
using UnityEngine;

/// <summary>
/// HapticManager 진단 전담 partial 파일.
///
/// ★ HapticManager.cs 의 InitVibrator() 안, InitVibrationAttributes() 호출 바로 앞에
///   이 한 줄을 추가하세요:
///
///       useAttributes = useVibrationAttributes;
///
/// [왜 만드는가]
///   진동은 실패해도 아무 흔적을 남기지 않습니다. 예외도 없고 화면 변화도 없습니다.
///   실기기에서는 콘솔도 안 보이니 "안 울린다"는 사실 말고는 단서가 없습니다.
///   그래서 상태를 스스로 보고하게 만듭니다.
/// </summary>
public partial class HapticManager
{
    [Header("진동 진단")]
    [Tooltip("VibrationAttributes(USAGE_TOUCH) 로 용도를 표시할지. " +
             "실기기에서 진동이 안 울릴 때 이걸 꺼보세요. 아래 설명 참고.")]
    [SerializeField] private bool useVibrationAttributes = true;

    /// <summary>
    /// 마지막 진단 결과. 실기기에서는 콘솔을 못 보므로
    /// 설정 화면의 TMP_Text 같은 데 그대로 띄워서 확인할 수 있습니다.
    /// </summary>
    public string LastReport { get; private set; } = "(아직 진단하지 않음)";

    /// <summary>
    /// 진동이 안 될 때 원인을 한 번에 확인합니다.
    /// 인스펙터에서 컴포넌트 우클릭 → "진동 진단" 으로도 실행할 수 있습니다.
    /// </summary>
    [ContextMenu("진동 진단")]
    public string Diagnose()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═════ 진동 진단 ═════");

#if UNITY_ANDROID && !UNITY_EDITOR
        // ① 권한 — 여기가 1순위 용의자입니다.
        //
        //   AndroidManifest 에 VIBRATE 가 없으면 진동은 "조용히" 실패합니다.
        //   예외도, 로그도, 경고도 없습니다. 그래서 눈으로는 절대 구분이 안 됩니다.
        //   checkSelfPermission 으로 직접 물어보면 바로 답이 나옵니다.
        sb.AppendLine($"{Mark(HasVibratePermission())} VIBRATE 권한 : {HasVibratePermission()}");
        if (!HasVibratePermission())
        {
            sb.AppendLine("   → Player Settings → Publishing Settings → Custom Main Manifest 체크 후");
            sb.AppendLine("     Assets/Plugins/Android/AndroidManifest.xml 의 <manifest> 안에 추가:");
            sb.AppendLine("     <uses-permission android:name=\"android.permission.VIBRATE\" />");
            sb.AppendLine("     ※ 추가 후 반드시 다시 빌드해야 합니다.");
        }

        // ② 기기 능력
        sb.AppendLine($"   안드로이드 API        : {apiLevel}");
        sb.AppendLine($"{Mark(vibrator != null)} Vibrator 서비스       : {(vibrator != null ? "확보" : "실패")}");
        sb.AppendLine($"{Mark(IsSupported)} hasVibrator()         : {IsSupported}");
        sb.AppendLine($"   세기 제어 지원        : {hasAmplitudeControl}");
        sb.AppendLine($"   VibrationEffect 클래스: {(vibrationEffectClass != null ? "확보" : "실패")}");
        sb.AppendLine($"   용도 표시(Attributes) : {(touchAttributes != null ? "확보" : "없음")} / 사용 {useAttributes}");
#else
        sb.AppendLine("ℹ️ 에디터 또는 안드로이드가 아닌 플랫폼입니다. 실제 진동은 나지 않습니다.");
        sb.AppendLine($"   IsSupported(UI용)     : {IsSupported}");
#endif

        // ③ 유저 설정 — 블룸에서 겪은 것과 같은 함정입니다.
        //   저장된 값이 꺼짐이면 코드가 아무리 맞아도 안 울립니다.
        sb.AppendLine($"{Mark(userEnabled)} 유저 설정(userEnabled): {userEnabled}");
        if (!userEnabled)
            sb.AppendLine("   → 설정에서 꺼져 있습니다. PlayerPrefs 'haptic_enabled' 가 0입니다.");

        sb.AppendLine($"   쿨다운 남은 시간      : {Mathf.Max(0f, minInterval - (Time.unscaledTime - lastVibrateTime)):0.00}s");

        // ④ 결론
        sb.AppendLine("───────────────────");
        if (!userEnabled)
            sb.AppendLine("결론: 유저 설정이 꺼져 있습니다. 설정에서 켜고 다시 시도하세요.");
        else if (!IsSupported)
            sb.AppendLine("결론: 기기가 진동을 지원하지 않거나 초기화에 실패했습니다.");
#if UNITY_ANDROID && !UNITY_EDITOR
        else if (!HasVibratePermission())
            sb.AppendLine("결론: VIBRATE 권한이 없습니다. 매니페스트를 고치고 다시 빌드하세요.");
#endif
        else
            sb.AppendLine("결론: 코드 쪽 조건은 모두 정상입니다. 아래 '시스템 설정' 항목을 확인하세요.");

        sb.AppendLine();
        sb.AppendLine("[시스템 설정도 확인하세요]");
        sb.AppendLine(" · 기기의 '소리와 진동 → 진동 세기 / 터치 피드백' 이 꺼져 있으면");
        sb.AppendLine("   앱의 진동도 함께 억제될 수 있습니다. (특히 USAGE_TOUCH 로 표시할 때)");
        sb.AppendLine(" · 방해 금지 모드도 영향을 줄 수 있습니다.");

        LastReport = sb.ToString();
        Debug.Log(LastReport, this);
        return LastReport;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// VIBRATE 권한이 실제로 부여됐는지 확인합니다.
    ///
    /// VIBRATE 는 런타임 권한이 아니라 '일반 권한' 이라 매니페스트에만 있으면
    /// 설치 시 자동으로 부여됩니다. 뒤집어 말하면, 매니페스트에 없으면
    /// 영원히 거부 상태이고 사용자가 고칠 방법도 없습니다.
    ///
    /// Context.checkSelfPermission 은 API 23+ 입니다. 이 프로젝트는 최소 30 이라 항상 쓸 수 있습니다.
    /// PackageManager.PERMISSION_GRANTED 는 0 입니다.
    /// </summary>
    private bool HasVibratePermission()
    {
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                int result = activity.Call<int>("checkSelfPermission", "android.permission.VIBRATE");
                return result == 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] 권한 확인 실패: {e.Message}");
            return false;   // 확인 못 하면 없는 것으로 취급해 눈에 띄게 한다
        }
    }
#endif

    /// <summary>
    /// 설정 화면의 '진동 테스트' 버튼 OnClick 에 연결하세요.
    ///
    /// 쿨다운과 유저 설정을 모두 무시하고 최대 세기로 한 번 울립니다.
    /// "진동 자체가 되는가" 와 "레벨업 경로가 진동을 부르는가" 를 분리해서 확인하기 위함입니다.
    /// 이 버튼이 울리는데 레벨업에서 안 울린다면, 문제는 진동이 아니라 호출부에 있습니다.
    /// </summary>
    public void TestVibrate()
    {
        Debug.Log("[Haptic] 테스트 진동 요청", this);

        bool saved = userEnabled;
        userEnabled = true;                        // 설정을 잠깐 무시
        Vibrate(120, 255, ignoreCooldown: true);   // 확실히 느껴지게 길고 강하게
        userEnabled = saved;

        Diagnose();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static string Mark(bool ok) => ok ? "OK " : "!! ";
#else
    private static string Mark(bool ok) => ok ? "OK " : "-- ";
#endif
}
