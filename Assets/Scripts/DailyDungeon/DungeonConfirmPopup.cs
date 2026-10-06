using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 던전 입장 확인 팝업. "이 동료 배치로 시작할까요?" 를 묻고, 확인을 누르면 콜백을 실행합니다.
///
/// [붙이는 위치]
///   StageScene Canvas 아래 "항상 켜져 있는" 오브젝트에 붙이고, panel 에는 실제 팝업 오브젝트를 연결합니다.
///   (QuitConfirmController 와 같은 이유 — 팝업 자신에 붙이면 꺼져 있는 동안 뒤로가기 핸들러가 등록되지 않습니다)
///   ★ 탭 창보다 '위에' 그려지는 곳에 두세요.
/// </summary>
public class DungeonConfirmPopup : MonoBehaviour
{
    [SerializeField] private GameObject      panel;
    [SerializeField] private TextMeshProUGUI titleText;     // (선택)
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button          confirmButton;
    [SerializeField] private Button          cancelButton;

    private Action onConfirm;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        else Debug.LogError("[DungeonConfirmPopup] panel 이 연결되지 않았습니다.", this);

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Hide);
    }

    void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton  != null) cancelButton.onClick.RemoveListener(Hide);
    }

    void OnEnable()  => GameManager.Instance?.RegisterEscapeHandler(OnEscape);
    void OnDisable() => GameManager.Instance?.UnregisterEscapeHandler(OnEscape);

    public void Show(string title, string message, Action confirmAction)
    {
        onConfirm = confirmAction;

        if (titleText != null)
        {
            titleText.text = title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
        if (messageText != null) messageText.text = message;
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        onConfirm = null;
        if (panel != null) panel.SetActive(false);
    }

    private void Confirm()
    {
        // 콜백이 씬을 넘기므로 먼저 닫고 비운 뒤 실행 (연타로 두 번 입장하는 것 방지)
        Action action = onConfirm;
        Hide();
        action?.Invoke();
    }

    /// <summary>뒤로가기 — 팝업이 열려 있을 때만 닫고 소비합니다.</summary>
    private bool OnEscape()
    {
        if (!IsOpen) return false;
        Hide();
        return true;
    }
}
