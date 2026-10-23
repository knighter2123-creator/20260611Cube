using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenChatButton : MonoBehaviour
{
    [SerializeField] private ChatPanel chatPanel;

    private void Awake()
    {
        // 미리보기 한 줄을 이 버튼 위쪽에 붙임 (ChatPanel.AttachPreviewTo 참고)
        if (chatPanel != null) chatPanel.AttachPreviewTo((RectTransform)transform);

        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (chatPanel != null) chatPanel.Open();
        });
    }
}
