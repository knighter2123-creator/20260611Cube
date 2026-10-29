using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 채팅 한 줄 누르기(탭). ChatPanel 이 줄을 만들 때 붙이고, 줄이 보여 주는 메시지를 Message 에 넣어 둡니다.
///
/// [스크롤과 구분]  클릭 이벤트만 받습니다 — 드래그 이벤트를 받으면 ScrollRect 가 드래그를 못 받아 목록이 스크롤되지 않음.
///   드래그 기준(EventSystem.pixelDragThreshold)보다 움직이면 ScrollRect 가 드래그를 시작하고, 그때 입력 모듈이 클릭을 취소합니다 (eligibleForClick).
/// </summary>
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
