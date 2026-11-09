using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공지 목록 한 줄 — [NEW] 표시 / 제목 / 날짜, 누르면 내용 보기. Tools/UI 굽기/공지사항 이 칸을 채웁니다.</summary>
[RequireComponent(typeof(Button))]
public class NoticeRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateText;
    [Tooltip("안 읽은 공지일 때만 켜지는 표시 (비워도 됨)")]
    [SerializeField] private GameObject      newMark;

    public void Set(NoticeItem notice, bool isNew, Action onClick)
    {
        if (titleText != null) titleText.text = notice.title;
        if (dateText  != null) dateText.text  = notice.DateText;
        if (newMark   != null) newMark.SetActive(isNew);

        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
