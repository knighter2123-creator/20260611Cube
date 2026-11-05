using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우편함을 여는 버튼 (StageScene 의 랭킹·친구 버튼 옆). 받지 않은 우편이 있으면 글자 옆에 개수를 붙입니다.
/// Tools/UI 굽기/우편함 이 옆 버튼을 복제해 만들고 칸을 채웁니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenMailboxButton : MonoBehaviour
{
    [SerializeField] private MailboxPanel mailboxPanel;
    [Tooltip("버튼 글자 (비워 두면 개수 표시 안 함)")]
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("버튼 글자. 받지 않은 우편이 있으면 뒤에 개수가 붙습니다.")]
    [SerializeField] private string text = "우편";
    [Tooltip("받지 않은 우편 개수 색")]
    [SerializeField] private Color countColor = ChatText.UnreadColor;

    private int shownCount = -1;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (mailboxPanel != null) mailboxPanel.Open();
        });
    }

    private void Update()
    {
        if (label == null) return;

        int count = Mailbox.Count;
        if (count == shownCount) return;

        shownCount = count;
        label.text = count > 0 ? $"{text} {ChatText.Colored(countColor, count.ToString())}" : text;
    }
}
