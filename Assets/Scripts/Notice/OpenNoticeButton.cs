using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공지사항 열기 버튼 — 안 읽은 공지 수를 글자 옆에 표시.</summary>
[RequireComponent(typeof(Button))]
public class OpenNoticeButton : MonoBehaviour
{
    [SerializeField] private NoticePanel noticePanel;
    [Tooltip("버튼 글자 (비워 두면 개수 표시 안 함)")]
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("버튼 글자. 안 읽은 공지가 있으면 뒤에 개수가 붙습니다.")]
    [SerializeField] private string text = "공지";
    [Tooltip("안 읽은 공지 개수 색")]
    [SerializeField] private Color countColor = ChatText.UnreadColor;

    [Tooltip("로그인됐는지 · 오래됐는지 확인하는 간격(초, 실제 시간). 다시 받는 간격은 NoticeManager.STALE_SECONDS")]
    [SerializeField] private float checkInterval = 2f;

    private int   shownCount = -1;
    private bool  dirty = true;
    private float nextCheckTime;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (noticePanel != null) noticePanel.Open();
        });
    }

    private void OnEnable()
    {
        NoticeManager.OnChanged += MarkDirty;
        dirty = true;
    }

    private void OnDisable() => NoticeManager.OnChanged -= MarkDirty;

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (Time.unscaledTime >= nextCheckTime)
        {
            nextCheckTime = Time.unscaledTime + Mathf.Max(0.1f, checkInterval);
            NoticeManager.RefreshIfStale();   // 로그인 전이면 아무것도 안 함
        }

        if (label == null || !dirty) return;
        dirty = false;

        int count = NoticeManager.UnreadCount;
        if (count == shownCount) return;

        shownCount = count;
        label.text = count > 0 ? $"{text} {ChatText.Colored(countColor, count.ToString())}" : text;
    }
}
