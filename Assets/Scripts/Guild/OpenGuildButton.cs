using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>길드 창 열기 버튼 — 길드장·부길드장이면 받은 가입 신청 수를 글자 옆에 표시.</summary>
[RequireComponent(typeof(Button))]
public class OpenGuildButton : MonoBehaviour
{
    [SerializeField] private GuildPanel guildPanel;
    [Tooltip("버튼 글자 (비워 두면 개수 표시 안 함)")]
    [SerializeField] private TextMeshProUGUI label;

    [SerializeField] private string text = "길드";
    [Tooltip("가입 신청 개수 색")]
    [SerializeField] private Color countColor = ChatText.UnreadColor;

    private int  shownCount = -1;
    private bool dirty = true;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (guildPanel != null) guildPanel.Open();
        });
    }

    private void OnEnable()
    {
        GuildManager.OnChanged += MarkDirty;
        dirty = true;
    }

    private void OnDisable() => GuildManager.OnChanged -= MarkDirty;

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (label == null || !dirty) return;
        dirty = false;

        int count = GuildManager.MyRole >= GuildRole.ViceMaster ? GuildManager.Applicants.Count : 0;
        if (count == shownCount) return;

        shownCount = count;
        label.text = count > 0 ? $"{text} {ChatText.Colored(countColor, count.ToString())}" : text;
    }
}
