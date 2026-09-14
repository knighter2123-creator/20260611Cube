using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelUpEffect 의 유저 설정 전담 partial 파일.
/// 지금은 "화면 번쩍임(플래시)" 하나만 다룬다.
///
/// ★ LevelUpEffect.cs 의 선언을 한 단어만 고칠 것:
///     public class LevelUpEffect        →  public partial class LevelUpEffect
///
/// ★ 그리고 Awake() 맨 끝에 한 줄 추가:
///     LoadFlashSetting();
///
/// [왜 기존 로직을 안 건드리는가]
///   useScreenFlash 가 이미 public 이고, UpdateFlash() 와 SetupFlashQuad() 가
///   그 값을 보고 있습니다. 그래서 이 파일은 그 필드를 대신 읽고 쓰기만 합니다.
///   연출 코드는 한 줄도 고칠 필요가 없습니다.
///
///   인스펙터의 Use Screen Flash 는 이제 "첫 실행 기본값"이 됩니다.
///   두 번째 실행부터는 PlayerPrefs 저장값이 이깁니다.
///   (HapticManager 의 defaultEnabled, BloomController 의 defaultIntensity01 과 같은 역할)
/// </summary>
public partial class LevelUpEffect
{
    private const string PrefsFlashKey = "Settings_ScreenFlash";

    /// <summary>화면 번쩍임이 켜져 있는가. 설정 UI 가 물어볼 수 있게 공개.</summary>
    public bool FlashEnabled => useScreenFlash;

    private Toggle boundFlashToggle;

    /// <summary>
    /// 저장된 설정을 읽어 useScreenFlash 에 반영한다.
    /// Awake() 맨 끝에서 호출할 것.
    /// </summary>
    public void LoadFlashSetting()
    {
        // 저장값이 없으면 인스펙터 값을 그대로 기본값으로 쓴다.
        useScreenFlash = PlayerPrefs.GetInt(PrefsFlashKey, useScreenFlash ? 1 : 0) == 1;

        if (logBinding)
            Debug.Log($"[LevelUpEffect] 화면 번쩍임 설정 로드 — {useScreenFlash}", this);
    }

    /// <summary>화면 번쩍임 on/off. 즉시 저장된다.</summary>
    public void SetFlashEnabled(bool on)
    {
        useScreenFlash = on;

        PlayerPrefs.SetInt(PrefsFlashKey, on ? 1 : 0);
        PlayerPrefs.Save();

        // ★ 지금 재생 중인 연출의 플래시도 즉시 꺼준다.
        //   안 하면 "껐는데 이번 것은 계속 번쩍이는" 어정쩡한 순간이 생긴다.
        //   flash 는 SetupFlashQuad 에서 카메라 밑으로 옮겨지므로
        //   root 를 꺼도 같이 꺼지지 않는다. 직접 건드려야 한다.
        if (!on && flash != null)
            flash.enabled = false;

        RefreshFlashToggle();

        if (logBinding)
            Debug.Log($"[LevelUpEffect] 화면 번쩍임 — {on}", this);
    }

    // ───────────────────────── UI 바인딩 ─────────────────────────

    /// <summary>
    /// 설정 패널이 열릴 때 호출. 상태 맞춤 + 리스너 등록을 한 번에.
    /// BloomController.BindSlider / HapticManager.BindToggle 과 같은 형태다.
    /// </summary>
    public void BindFlashToggle(Toggle toggle)
    {
        if (toggle == null) return;

        UnbindFlashToggle();   // 이전 바인딩이 남아 있으면 먼저 끊는다 (리스너 누수 방지)

        boundFlashToggle = toggle;

        // SetIsOnWithoutNotify — 저장값을 화면에 반영하는 건 유저 조작이 아니므로
        // onValueChanged 가 되쏘아지면 안 된다. (되쏘면 열기만 해도 저장 로직이 돈다)
        toggle.SetIsOnWithoutNotify(useScreenFlash);
        toggle.onValueChanged.AddListener(SetFlashEnabled);
    }

    /// <summary>설정 패널이 닫힐 때 호출. 등록과 해제는 반드시 짝으로.</summary>
    public void UnbindFlashToggle()
    {
        if (boundFlashToggle == null) return;

        boundFlashToggle.onValueChanged.RemoveListener(SetFlashEnabled);
        boundFlashToggle = null;
    }

    private void RefreshFlashToggle()
    {
        if (boundFlashToggle == null) return;
        boundFlashToggle.SetIsOnWithoutNotify(useScreenFlash);
    }
}
