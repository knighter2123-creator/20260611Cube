using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 기존 UI 스크립트를 고치지 않고 탭에 끼워 넣기 위한 범용 어댑터.
///
/// [언제 쓰는가]
///   PlayerStatusUI 처럼 이미 `Open()` / `Close()` / `Refresh()` 가 public 으로 열려 있고,
///   자기 패널을 스스로 켜고 끄는 스크립트가 있을 때.
///   그 스크립트를 상속하거나 수정하지 않고, 인스펙터에서 함수만 연결하면 됩니다.
///
/// [셋업]
///   1) 탭의 contentRoot 에 이 컴포넌트를 붙입니다.
///   2) TabWindow 의 해당 탭 Page 칸에 이 컴포넌트를 넣습니다.
///   3) On Show 에 PlayerStatusUI.Open, On Hide 에 PlayerStatusUI.Close 를 연결합니다.
///
/// ★ 이 스크립트는 '스탯창'이라는 도메인을 전혀 모릅니다.
///   그래서 나중에 상점 탭, 동료 탭 어디에나 그대로 재사용됩니다.
///   (AugmentUIFactory 를 밖으로 뺀 것과 같은 기준입니다)
/// </summary>
public class UnityEventTabPage : MonoBehaviour, ITabPage
{
    [Tooltip("이 탭이 선택될 때 호출 — 보통 대상 UI 의 Open() 또는 Refresh()")]
    [SerializeField] private UnityEvent onShow;

    [Tooltip("이 탭이 가려질 때 호출 — 보통 대상 UI 의 Close()")]
    [SerializeField] private UnityEvent onHide;

    public void OnTabShow() => onShow?.Invoke();
    public void OnTabHide() => onHide?.Invoke();
}
