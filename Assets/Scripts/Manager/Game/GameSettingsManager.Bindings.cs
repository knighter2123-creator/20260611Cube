using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameSettingsManager 의 설정 항목 바인딩 전담 partial 파일.
/// 블룸(레벨업 이펙트) 강도·on/off, 진동 on/off.
///
/// 이 파일은 패널을 열거나 닫지 않는다. timeScale 도 건드리지 않는다.
/// 그건 전부 GameSettingsManager.cs 의 책임이고, 여기는 "열렸을 때 UI를 맞추는" 일만 한다.
/// 책임을 한 곳에만 두는 게 SettingsPanel 과 겹쳤던 문제의 근본 해결책이다.
/// </summary>
public partial class GameSettingManager
{
    [Header("블룸 (레벨업 이펙트)")]
    [Tooltip("이펙트 조절 슬라이더")]
    [SerializeField] private Slider bloomSlider;
    [Tooltip("레벨업 이펙트 on/off 체크박스")]
    [SerializeField] private Toggle bloomToggle;
    [Tooltip("선택 - 강도를 퍼센트로 보여줄 텍스트")]
    [SerializeField] private TMP_Text bloomValueLabel;

    [Header("진동")]
    [SerializeField] private Toggle hapticToggle;
    [Tooltip("선택 - 진동 모터가 없는 기기에 보여줄 안내")]
    [SerializeField] private GameObject hapticUnsupportedNotice;

    [Header("바인딩 옵션")]
    [Tooltip("강도 0인 상태에서 체크박스를 켰을 때 되살릴 기본 강도")]
    [Range(0f, 1f)]
    [SerializeField] private float reviveIntensity01 = 0.45f;

    // ───────────────────────── 진입점 ─────────────────────────

    /// <summary>패널을 열 때 GameSettingsManager.Open() 이 호출한다.</summary>
    private void BindAll()
    {
        BindBloom();
        BindHaptic();
    }

    /// <summary>
    /// 패널을 닫을 때 GameSettingsManager.Close() 가 호출한다.
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

        if (bloomToggle != null)
            bloomToggle.onValueChanged.RemoveListener(OnBloomToggleChanged);

        HapticManager.Instance?.UnbindToggle();
    }

    // ───────────────────────── 블룸 ─────────────────────────

    private void BindBloom()
    {
        var bloom = BloomController.Instance;

        if (bloom == null)
        {
            Debug.LogWarning("[Settings] BloomController.Instance 가 null 입니다. Effect Manager 가 씬에 있는지 확인하세요.");
            if (bloomSlider != null) bloomSlider.interactable = false;
            if (bloomToggle != null) bloomToggle.interactable = false;
            return;
        }

        if (bloomSlider != null)
        {
            bloomSlider.interactable = true;

            // min/max 설정 + 저장값으로 위치 맞춤 + 리스너 등록을 한 번에
            bloom.BindSlider(bloomSlider);

            // 라벨 갱신과 토글 동기화는 BindSlider 가 모르는 부분이라 따로 붙인다.
            bloomSlider.onValueChanged.AddListener(OnBloomSliderChanged);
        }

        if (bloomToggle != null)
        {
            bloomToggle.interactable = true;

            // ★ bloom.BindToggle() 을 쓰지 않는 이유
            //   BindToggle 은 내부 필드 userEnabled(= 순수 토글 상태)로 체크를 맞춘다.
            //   화면에는 프로퍼티 UserEnabled(= 토글 ON && 강도 > 0)가 반영되어야
            //   "강도 0인데 체크는 켜져 있는" 모순된 상태가 안 생긴다.
            bloomToggle.SetIsOnWithoutNotify(bloom.UserEnabled);
            bloomToggle.onValueChanged.AddListener(OnBloomToggleChanged);
        }

        UpdateBloomLabel(bloom.UserIntensity01);
    }

    /// <summary>
    /// 슬라이더를 움직였을 때. 저장과 적용은 BindSlider 가 건 리스너가 이미 처리했다.
    /// 여기서는 체크박스와의 동기화만 한다.
    ///
    /// UserEnabled 는 "토글 ON && 강도 > 0.001" 이므로
    /// 슬라이더를 0까지 내리는 것 = 끄는 것이고, 체크박스도 같이 풀려야 말이 된다.
    ///
    /// 리스너 실행 순서는 등록 순서다. BindSlider 를 먼저 불렀으니
    /// 이 함수가 도는 시점에는 BloomController 의 값이 이미 갱신되어 있다.
    /// </summary>
    private void OnBloomSliderChanged(float value01)
    {
        UpdateBloomLabel(value01);

        var bloom = BloomController.Instance;
        if (bloom != null && bloomToggle != null)
            bloomToggle.SetIsOnWithoutNotify(bloom.UserEnabled);
    }

    private void OnBloomToggleChanged(bool on)
    {
        var bloom = BloomController.Instance;
        if (bloom == null) return;

        // 강도 0인 채로 체크만 켜면 화면에 아무 변화가 없다.
        // 유저는 "체크했는데 왜 안 되지?" 하고 버그로 받아들인다.
        if (on && bloom.UserIntensity01 <= 0.001f)
        {
            bloom.SetUserIntensity01(reviveIntensity01);

            if (bloomSlider != null)
                bloomSlider.SetValueWithoutNotify(reviveIntensity01);

            UpdateBloomLabel(reviveIntensity01);
        }

        bloom.SetUserEnabled(on);
    }

    private void UpdateBloomLabel(float value01)
    {
        if (bloomValueLabel != null)
            bloomValueLabel.text = $"{value01 * 100f:0}%";
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
