using UnityEngine;
using UnityEngine.UI;

/// <summary>HapticManager 토글 바인딩: 저장값 반영 + 리스너 등록 + 지원 여부를 BindToggle 한 줄로 묶음 — 설정 패널마다 흩어 두면 빠뜨리기 쉬워서.</summary>
// OnDestroy 는 HapticManager.cs 에 있음 (partial 은 한 클래스라 두 곳에 쓰면 컴파일 에러)
public partial class HapticManager
{
    // 현재 연결된 토글. 설정 패널이 열릴 때 붙고 닫힐 때 떨어진다.
    private Toggle boundToggle;

    /// <summary>설정 패널이 열릴 때 토글 초기 상태 + 리스너 등록 — ⚠ 인스펙터 On Value Changed 는 비울 것(두 번 호출됨).</summary>
    public void BindToggle(Toggle toggle)
    {
        if (toggle == null)
        {
            Debug.LogWarning("[Haptic] BindToggle: toggle이 null입니다.");
            return;
        }

        // 이전 바인딩부터 끊음 — 안 하면 열 때마다 리스너가 쌓임
        UnbindToggle();

        boundToggle = toggle;

        // 진동 모터 없는 기기에선 비활성 (에디터는 Editor Treat As Supported 로 열어 둠)
        toggle.interactable = IsSupported;

        // isOn 대입은 onValueChanged 를 쏴서 열기만 해도 저장·피드백 진동이 일어나므로 WithoutNotify
        toggle.SetIsOnWithoutNotify(userEnabled && IsSupported);

        toggle.onValueChanged.AddListener(OnBoundToggleChanged);
    }

    /// <summary>설정 패널이 닫힐 때(및 OnDestroy) 리스너 해제 — BindToggle 과 짝.</summary>
    public void UnbindToggle()
    {
        if (boundToggle == null) return;

        boundToggle.onValueChanged.RemoveListener(OnBoundToggleChanged);
        boundToggle = null;
    }

    /// <summary>코드에서 SetUserEnabled 를 불렀을 때 토글도 따라오게 — WithoutNotify 라 무한 루프 없음.</summary>
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