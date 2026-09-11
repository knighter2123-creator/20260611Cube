using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HapticManager의 UI 바인딩 전담 partial 파일.
/// 이 프로젝트 관례(기능별 partial 분리)에 맞춰 HapticManager.Binding.cs 로 둔다.
///
/// ★ OnDestroy 는 여기 두지 않는다.
///   partial 클래스는 결국 하나의 클래스라 OnDestroy 를 두 파일에 쓰면 컴파일 에러가 난다.
///   Awake 가 Instance 를 세우는 쪽(HapticManager.cs)에 OnDestroy 를 두고,
///   거기서 UnbindToggle() 을 부르게 했다. 생명주기 한 쌍은 같은 파일에 붙어 있는 게 좋다.
///
/// [왜 매니저가 토글을 들고 있는가]
///   BloomController.BindSlider() 와 같은 발상이다.
///   "저장값으로 위치 맞추기 + 리스너 등록 + 지원 여부 반영"은 항상 같이 가야 하는 3종 세트인데,
///   이걸 설정 패널 쪽에 흩어놓으면 패널을 새로 만들 때마다 한두 개씩 빼먹는다.
///   매니저가 한 함수로 묶어서 제공하면 호출부는 한 줄로 끝난다.
/// </summary>
public partial class HapticManager
{
    // 현재 연결된 토글. 설정 패널이 열릴 때 붙고 닫힐 때 떨어진다.
    private Toggle boundToggle;

    /// <summary>
    /// 설정 패널이 열릴 때 호출. 토글 초기 상태 맞춤 + 리스너 등록을 한 번에 처리한다.
    ///
    /// ★ 이 함수를 쓰면 토글의 인스펙터 On Value Changed 는 반드시 비워두세요.
    ///   거기에 OnToggleChanged 를 연결해두면 코드 등록분과 겹쳐서
    ///   한 번 누를 때 SetUserEnabled 가 두 번 호출됩니다.
    /// </summary>
    public void BindToggle(Toggle toggle)
    {
        if (toggle == null)
        {
            Debug.LogWarning("[Haptic] BindToggle: toggle이 null입니다.");
            return;
        }

        // 이전 바인딩이 남아 있으면 먼저 끊는다.
        // 이걸 빼먹으면 패널을 열고 닫을 때마다 리스너가 하나씩 쌓여서
        // 토글 한 번 누를 때 SetUserEnabled가 여러 번 호출된다. (전형적인 리스너 누수)
        UnbindToggle();

        boundToggle = toggle;

        // 진동 모터가 없는 기기에서는 만질 수 없게 한다.
        // 눌러도 아무 일 없는 토글을 열어두는 건 유저를 속이는 것에 가깝다.
        // (에디터에서는 HapticManager 의 Editor Treat As Supported 옵션으로 열어둔다)
        toggle.interactable = IsSupported;

        // ★ SetIsOnWithoutNotify 를 쓰는 이유
        //   그냥 toggle.isOn = ... 로 대입하면 onValueChanged 가 되쏘아진다.
        //   그러면 패널을 열기만 해도 SetUserEnabled → PlayerPrefs.Save() 가 돌고,
        //   진동이 켜져 있으면 "적용 피드백" 진동까지 울린다.
        //   저장값을 화면에 반영하는 것은 유저의 조작이 아니므로 알림이 나가면 안 된다.
        toggle.SetIsOnWithoutNotify(userEnabled && IsSupported);

        toggle.onValueChanged.AddListener(OnBoundToggleChanged);
    }

    /// <summary>
    /// 설정 패널이 닫힐 때 호출. 등록과 해제는 반드시 짝으로.
    /// HapticManager.cs 의 OnDestroy 에서도 불러 준다.
    /// </summary>
    public void UnbindToggle()
    {
        if (boundToggle == null) return;

        boundToggle.onValueChanged.RemoveListener(OnBoundToggleChanged);
        boundToggle = null;
    }

    /// <summary>
    /// 코드에서 SetUserEnabled()를 직접 불렀을 때(예: 저사양 모드 일괄 적용)
    /// 화면의 토글도 같이 따라오게 만드는 용도.
    ///
    /// SetIsOnWithoutNotify 를 쓰기 때문에 무한 루프가 생기지 않는다.
    /// (토글 조작 → SetUserEnabled → Refresh → 알림 없음 → 끝)
    /// isOn 대입을 썼다면 서로를 계속 호출하며 스택이 터진다.
    /// </summary>
    public void RefreshBoundToggle()
    {
        if (boundToggle == null) return;

        boundToggle.interactable = IsSupported;
        boundToggle.SetIsOnWithoutNotify(userEnabled && IsSupported);
    }

    private void OnBoundToggleChanged(bool on)
    {
        // 유저가 직접 조작한 경로. 여기서만 저장과 피드백 진동이 일어난다.
        SetUserEnabled(on);
    }
}