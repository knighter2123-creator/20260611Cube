using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>채팅 한 줄 탭 처리(ChatPanel 이 줄마다 붙이고 Message 를 넣어 둠) — 클릭만 받아 드래그는 ScrollRect 가 받게 (드래그 기준을 넘으면 입력 모듈이 클릭 취소).</summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class ChatLinePress : MonoBehaviour, IPointerClickHandler
{
    /// <summary>이 줄이 보여 주는 메시지.</summary>
    public ChatMessage Message;

    public event Action<ChatLinePress, Vector2> Tapped;   // 누른 화면 좌표

    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return;   // 스크롤하다 뗀 것
        Tapped?.Invoke(this, e.pressPosition);
    }
}
