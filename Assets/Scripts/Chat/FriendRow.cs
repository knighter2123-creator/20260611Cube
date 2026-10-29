using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>친구 창 목록 한 줄 — 닉네임 + 오른쪽 버튼 두 개 (수락/거절, 메시지/삭제 등). 칸은 UIBaker 가 채웁니다.</summary>
public class FriendRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TextMeshProUGUI primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TextMeshProUGUI secondaryLabel;

    /// <summary>버튼 글자가 null 이면 그 버튼을 숨깁니다.</summary>
    public void Set(string name, string primary, Action onPrimary, string secondary, Action onSecondary)
    {
        nameText.text = name;
        SetButton(primaryButton,   primaryLabel,   primary,   onPrimary);
        SetButton(secondaryButton, secondaryLabel, secondary, onSecondary);
    }

    private static void SetButton(Button button, TextMeshProUGUI label, string text, Action onClick)
    {
        if (button == null) return;
        button.gameObject.SetActive(text != null);
        button.onClick.RemoveAllListeners();
        if (text == null) return;

        if (label != null) label.text = text;
        if (onClick != null) button.onClick.AddListener(() => onClick());
    }
}
