using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameSettingManager 의 설정 항목 바인딩 전담 partial 파일.
/// 블룸(레벨업 이펙트) 강도·on/off, 진동 on/off.
///
/// 이 파일은 패널을 열거나 닫지 않는다. timeScale 도 건드리지 않는다.
/// 그건 전부 GameSettingManager.cs 의 책임이고, 여기는 "열렸을 때 UI를 맞추는" 일만 한다.
/// 책임을 한 곳에만 두는 게 SettingsPanel 과 겹쳤던 문제의 근본 해결책이다.
/// </summary>
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

    // ───────────────────────── 진입점 ─────────────────────────

    /// <summary>패널을 열 때 GameSettingManager.Open() 이 호출한다.</summary>
    private void BindAll()
    {
        // ★ 이 줄이 콘솔에 안 찍히면, 설정 패널이 Open() 을 거치지 않고 열린 것입니다.
        //   (버튼의 인스펙터 On Click 에 SetActive(true) 가 직접 걸려 있다든지)
        //   그 경우 바인딩 코드를 어느 파일로 옮겨도 똑같이 안 됩니다.
        Debug.Log("[Setting] BindAll 시작");

        BindBloom();
        BindFlash();
        BindHaptic();
    }

    /// <summary>
    /// 패널을 닫을 때 GameSettingManager.Close() 가 호출한다.
    ///
    /// ★ BloomController.BindSlider() 는 내부에서 AddListener 를 하는데
    ///   짝이 되는 UnbindSlider() 가 BloomController 쪽에 없다.
    ///   해제하지 않으면 패널을 열 때마다 리스너가 하나씩 쌓이고,
    ///   슬라이더를 드래그할 때 PlayerPrefs.Save()(디스크 I/O)가 그만큼 반복돼 끊긴다.
    ///
    ///   OnSliderChanged 가 public 이라 밖에서 정확히 떼어낼 수 있다.
    ///   UnityEvent 는 델리게이트 인스턴스가 아니라 (대상 오브젝트 + 메서드)로 비교하므로
    ///   등록할 때와 다른 델리게이트를 넘겨도 제대로 제거된다.
    /// </summary>
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

        // ★ 토글을 없앴으므로 슬라이더가 블룸의 유일한 조작 수단이다.
        //
        //   BloomController.UserEnabled 는 (userEnabled && userScale > 0.001) 이다.
        //   userEnabled 는 원래 토글이 바꾸던 값인데, 토글이 사라지면
        //   그 값을 true 로 되돌릴 UI 가 하나도 남지 않는다.
        //   저장된 값이 false 인 채로 있으면 슬라이더를 끝까지 올려도 블룸이 안 켜지고,
        //   "슬라이더가 고장났다"로 보이게 된다. (지금 저장된 값이 실제로 false 다)
        //
        //   그래서 토글이 없는 구성에서는 여기서 한 번 되살린다.
        //   유저 설정을 무시하는 게 아니라, 조작 수단이 없어진 죽은 값을 정리하는 것이다.
        if (!bloom.UserEnabled && bloom.UserIntensity01 > 0.001f)
        {
            Debug.Log("[Setting] 블룸 토글이 없는 구성이라 userEnabled 를 되살립니다.", this);
            bloom.SetUserEnabled(true);
        }

        UpdateBloomLabel(bloom.UserIntensity01);
    }

    /// <summary>
    /// 슬라이더를 움직였을 때. 저장과 적용은 BindSlider 가 건 리스너가 이미 처리했다.
    /// 여기서는 퍼센트 라벨만 갱신한다.
    /// </summary>
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

    /// <summary>
    /// 레벨업 플래시 on/off.
    ///
    /// ★ 블룸 토글과 헷갈리지 말 것.
    ///   블룸 = 밝은 것 주변에 번지는 빛무리 (BloomController 소유)
    ///   플래시 = 화면 전체를 덮는 흰 섬광 (LevelUpEffect 소유)
    ///   둘은 다른 물건이고, 블룸을 꺼도 플래시는 그대로 터진다.
    /// </summary>
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

        Debug.Log($"[Setting] 화면 번쩍임 토글 바인딩 — 표시 {effect.FlashEnabled}", flashToggle);
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
}