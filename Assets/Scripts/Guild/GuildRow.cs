using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>길드 창 목록 한 줄 (길드 목록 · 멤버 · 가입 신청 공용) — 제목 / 설명 + 오른쪽 버튼 최대 3개. 쓰지 않는 버튼은 숨김.</summary>
public class GuildRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;
    [Tooltip("오른쪽부터 차례로 채움 (0 = 맨 오른쪽)")]
    [SerializeField] private Button[] buttons = new Button[0];

    public struct RowAction
    {
        public string label;
        public Action onClick;
        public bool   enabled;
        public RowAction(string label, Action onClick, bool enabled = true) { this.label = label; this.onClick = onClick; this.enabled = enabled; }
    }

    public void Set(string title, string sub, params RowAction[] actions)
    {
        if (titleText != null) titleText.text = title;
        if (subText   != null)
        {
            subText.text = sub ?? string.Empty;
            subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            bool used = actions != null && i < actions.Length;
            button.gameObject.SetActive(used);
            if (!used) continue;

            RowAction a = actions[i];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => a.onClick?.Invoke());
            button.interactable = a.enabled;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = a.label;
        }
    }
}
