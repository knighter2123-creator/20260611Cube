using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 항목 바인딩(블룸 강도, 화면 번쩍임, 진동, 효과음·배경음) — 열릴 때 UI 를 저장값에 맞추고 닫힐 때 리스너 해제만 함 (열기/닫기·timeScale 은 본체 책임).</summary>
public partial class GameSettingManager
{
    [Header("블룸 (레벨업 이펙트)")]
    [Tooltip("이펙트 조절 슬라이더")]
    [SerializeField] private Slider bloomSlider;
    [Tooltip("선택 - 강도를 퍼센트로 보여줄 텍스트")]
    [SerializeField] private TMP_Text bloomValueLabel;

    [Header("화면 번쩍임 (레벨업 플래시)")]
    [Tooltip("레벨업 시 화면 전체가 번쩍이는 연출 on/off")]
    [SerializeField] private Toggle flashToggle;

    [Header("진동")]
    [SerializeField] private Toggle hapticToggle;
    [Tooltip("선택 - 진동 모터가 없는 기기에 보여줄 안내")]
    [SerializeField] private GameObject hapticUnsupportedNotice;

    [Header("소리 (SoundManager)")]
    [Tooltip("효과음 볼륨 슬라이더 (0~1 로 맞춰짐)")]
    [SerializeField] private Slider sfxSlider;
    [Tooltip("선택 - 효과음 볼륨을 퍼센트로 보여줄 텍스트")]
    [SerializeField] private TMP_Text sfxValueLabel;
    [Tooltip("배경음 볼륨 슬라이더 (0~1 로 맞춰짐)")]
    [SerializeField] private Slider bgmSlider;
    [Tooltip("선택 - 배경음 볼륨을 퍼센트로 보여줄 텍스트")]
    [SerializeField] private TMP_Text bgmValueLabel;

    // ───────────────────────── 진입점 ─────────────────────────

    /// <summary>패널을 열 때 GameSettingManager.Open() 이 호출한다.</summary>
    private void BindAll()
    {
        // 이 로그가 없으면 패널이 Open() 을 안 거치고 열린 것 (인스펙터 On Click 의 SetActive 등)
        GameLog.Info("[Setting] BindAll 시작");

        BindBloom();
        BindFlash();
        BindHaptic();
        BindSound();
    }

    /// <summary>패널을 닫을 때 모든 리스너 해제 — BloomController 엔 UnbindSlider 가 없어 public OnSliderChanged 를 직접 뗌 (안 떼면 열 때마다 쌓여 드래그 중 디스크 저장이 반복).</summary>
    private void UnbindAll()
    {
        var bloom = BloomController.Instance;

        if (bloomSlider != null)
        {
            if (bloom != null)
                bloomSlider.onValueChanged.RemoveListener(bloom.OnSliderChanged);   // BindSlider 가 건 것

            bloomSlider.onValueChanged.RemoveListener(OnBloomSliderChanged);        // 내가 건 것
        }

        LevelUpEffect.Instance?.UnbindFlashToggle();
        HapticManager.Instance?.UnbindToggle();

        // 소리 — SoundManager 가 건 리스너를 떼고 볼륨을 저장 + 내가 건 라벨 리스너도 뗌
        SoundManager sound = SoundManager.Instance;
        if (sound != null) sound.UnbindSliders(sfxSlider, bgmSlider);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(UpdateSfxLabel);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(UpdateBgmLabel);
    }

    // ───────────────────────── 블룸 ─────────────────────────

    private void BindBloom()
    {
        var bloom = BloomController.Instance;

        if (bloom == null)
        {
            Debug.LogWarning("[Setting] BloomController.Instance 가 null 입니다. Effect Manager 가 씬에 있는지 확인하세요.");
            if (bloomSlider != null) bloomSlider.interactable = false;
            return;
        }

        if (bloomSlider == null)
            Debug.LogWarning("[Setting] Bloom Slider 가 인스펙터에 연결되지 않았습니다.", this);
        else
        {
            bloomSlider.interactable = true;

            // min/max 설정 + 저장값으로 위치 맞춤 + 리스너 등록을 한 번에
            bloom.BindSlider(bloomSlider);

            // 라벨 갱신과 토글 동기화는 BindSlider 가 모르는 부분이라 따로 붙인다.
            bloomSlider.onValueChanged.AddListener(OnBloomSliderChanged);
        }

        // 블룸 토글이 없어져 userEnabled=false 로 저장돼 있으면 슬라이더를 올려도 안 켜지므로 되살림 (조작 수단이 사라진 죽은 값 정리)
        if (!bloom.UserEnabled && bloom.UserIntensity01 > 0.001f)
        {
            GameLog.Info("[Setting] 블룸 토글이 없는 구성이라 userEnabled 를 되살립니다.", this);
            bloom.SetUserEnabled(true);
        }

        UpdateBloomLabel(bloom.UserIntensity01);
    }

    /// <summary>슬라이더 변경 시 퍼센트 라벨만 갱신 (저장·적용은 BindSlider 의 리스너가 함).</summary>
    private void OnBloomSliderChanged(float value01)
    {
        UpdateBloomLabel(value01);
    }

    private void UpdateBloomLabel(float value01)
    {
        if (bloomValueLabel != null)
            bloomValueLabel.text = $"{value01 * 100f:0}%";
    }

    // ───────────────────────── 화면 번쩍임 ─────────────────────────

    /// <summary>레벨업 화면 번쩍임(LevelUpEffect 의 흰 섬광) on/off — 블룸(빛무리, BloomController)과는 별개.</summary>
    private void BindFlash()
    {
        if (flashToggle == null) return;   // 선택 항목이므로 조용히 넘어간다

        var effect = LevelUpEffect.Instance;

        if (effect == null)
        {
            Debug.LogWarning("[Setting] LevelUpEffect.Instance 가 없습니다. " +
                             "Effect Manager 가 씬에 있는지 확인하세요.", this);
            flashToggle.interactable = false;
            return;
        }

        flashToggle.interactable = true;
        effect.BindFlashToggle(flashToggle);

        GameLog.Info($"[Setting] 화면 번쩍임 토글 바인딩 — 표시 {effect.FlashEnabled}", flashToggle);
    }

    // ───────────────────────── 진동 ─────────────────────────

    private void BindHaptic()
    {
        if (hapticToggle == null) return;

        var haptic = HapticManager.Instance;

        if (haptic == null)
        {
            hapticToggle.interactable = false;
            return;
        }

        // 상태 맞춤 + interactable + 리스너 등록이 이 한 줄에 다 들어 있다.
        haptic.BindToggle(hapticToggle);

        // 토글이 회색으로 죽어 있는 이유를 알려주지 않으면 버그로 오해받는다.
        if (hapticUnsupportedNotice != null)
            hapticUnsupportedNotice.SetActive(!haptic.IsSupported);
    }

    // ───────────────────────── 소리 ─────────────────────────

    /// <summary>효과음 · 배경음 볼륨 슬라이더. 둘 다 선택 항목 — 비어 있으면 조용히 넘어간다.</summary>
    private void BindSound()
    {
        if (sfxSlider == null && bgmSlider == null) return;

        SoundManager sound = SoundManager.Instance;
        if (sound == null)
        {
            Debug.LogWarning("[Setting] SoundManager.Instance 가 없습니다. ManagerRoot 에 SoundManager 가 있는지 확인하세요.", this);
            if (sfxSlider != null) sfxSlider.interactable = false;
            if (bgmSlider != null) bgmSlider.interactable = false;
            return;
        }

        // min/max + 저장값으로 위치 맞춤 + 적용 리스너는 SoundManager 가, 퍼센트 라벨은 여기서
        if (sfxSlider != null)
        {
            sfxSlider.interactable = true;
            sound.BindSfxSlider(sfxSlider);
            sfxSlider.onValueChanged.RemoveListener(UpdateSfxLabel);
            sfxSlider.onValueChanged.AddListener(UpdateSfxLabel);
        }
        if (bgmSlider != null)
        {
            bgmSlider.interactable = true;
            sound.BindBgmSlider(bgmSlider);
            bgmSlider.onValueChanged.RemoveListener(UpdateBgmLabel);
            bgmSlider.onValueChanged.AddListener(UpdateBgmLabel);
        }

        UpdateSfxLabel(sound.SfxVolume01);
        UpdateBgmLabel(sound.BgmVolume01);
    }

    private void UpdateSfxLabel(float value01) { if (sfxValueLabel != null) sfxValueLabel.text = $"{value01 * 100f:0}%"; }
    private void UpdateBgmLabel(float value01) { if (bgmValueLabel != null) bgmValueLabel.text = $"{value01 * 100f:0}%"; }
}