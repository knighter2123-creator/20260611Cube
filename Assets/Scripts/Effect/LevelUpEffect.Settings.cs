using UnityEngine;
using UnityEngine.UI;

/// <summary>레벨업 화면 번쩍임(플래시) 유저 설정 — useScreenFlash 를 PlayerPrefs 와 주고받기만 해서 연출 코드는 그대로 (인스펙터 값은 첫 실행 기본값).</summary>
public partial class LevelUpEffect
{
    private const string PrefsFlashKey = "Settings_ScreenFlash";

    /// <summary>화면 번쩍임이 켜져 있는가. 설정 UI 가 물어볼 수 있게 공개.</summary>
    public bool FlashEnabled => useScreenFlash;

    private Toggle boundFlashToggle;

    /// <summary>저장된 설정을 useScreenFlash 에 반영 (Awake 끝에서 호출).</summary>
    public void LoadFlashSetting()
    {
        // 저장값이 없으면 인스펙터 값을 그대로 기본값으로 쓴다.
        useScreenFlash = PlayerPrefs.GetInt(PrefsFlashKey, useScreenFlash ? 1 : 0) == 1;

        if (logBinding)
            GameLog.Info($"[LevelUpEffect] 화면 번쩍임 설정 로드 — {useScreenFlash}", this);
    }

    /// <summary>화면 번쩍임 on/off. 즉시 저장된다.</summary>
    public void SetFlashEnabled(bool on)
    {
        useScreenFlash = on;

        PlayerPrefs.SetInt(PrefsFlashKey, on ? 1 : 0);
        PlayerPrefs.Save();

        // 재생 중인 플래시도 즉시 끔 (flash 는 카메라 밑에 있어 root 를 꺼도 안 꺼짐)
        if (!on && flash != null)
            flash.enabled = false;

        RefreshFlashToggle();

        if (logBinding)
            GameLog.Info($"[LevelUpEffect] 화면 번쩍임 — {on}", this);
    }

    // ───────────────────────── UI 바인딩 ─────────────────────────

    /// <summary>설정 패널이 열릴 때 상태 맞춤 + 리스너 등록 (BindToggle 계열과 같은 형태).</summary>
    public void BindFlashToggle(Toggle toggle)
    {
        if (toggle == null) return;

        UnbindFlashToggle();   // 이전 바인딩이 남아 있으면 먼저 끊는다 (리스너 누수 방지)

        boundFlashToggle = toggle;

        // WithoutNotify — 저장값 반영은 유저 조작이 아니라 이벤트를 쏘면 안 됨
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
