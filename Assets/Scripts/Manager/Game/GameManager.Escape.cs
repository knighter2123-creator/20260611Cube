using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

/// <summary>Escape(안드로이드 뒤로가기) 처리: 감지는 GameManager, 할 일은 각 씬·팝업이 핸들러로 등록 — 나중에 등록된 것(위에 뜬 팝업)부터 처리하는 스택이라 씬 이름 비교가 필요 없음.</summary>
public partial class GameManager
{
    // 핸들러 반환값: true = 처리함(여기서 멈춤), false = 다음 핸들러로
    private readonly List<Func<bool>> escapeHandlers = new List<Func<bool>>();

    /// <summary>씬/팝업이 활성화될 때(OnEnable) 자기 처리 함수를 등록한다.</summary>
    public void RegisterEscapeHandler(Func<bool> handler)
    {
        if (handler == null) return;
        if (escapeHandlers.Contains(handler)) return;   // 중복 등록 방지
        escapeHandlers.Add(handler);
    }

    /// <summary>OnDisable 에서 꼭 해제 (빼먹으면 파괴된 오브젝트를 붙잡아 MissingReferenceException).</summary>
    public void UnregisterEscapeHandler(Func<bool> handler)
    {
        if (handler == null) return;
        escapeHandlers.Remove(handler);
    }

    /// <summary>GameManager.Update 에서 호출 (partial 은 Update 를 두 개 가질 수 없음) — 눌리면 최근 핸들러부터 처리될 때까지 전달.</summary>
    private void UpdateEscape()
    {
        if (!IsEscapePressed()) return;

        // 역순 순회 — 최근 등록부터, 핸들러 안에서 Unregister 해도 안전
        for (int i = escapeHandlers.Count - 1; i >= 0; i--)
        {
            var handler = escapeHandlers[i];

            // 대상 오브젝트가 이미 파괴됐다면 정리하고 넘어간다(안전망).
            if (handler.Target is UnityEngine.Object unityTarget && unityTarget == null)
            {
                escapeHandlers.RemoveAt(i);
                continue;
            }

            if (handler.Invoke())
                return;   // 누군가 처리했으면 아래로 전파하지 않는다
        }

        // 여기까지 왔다 = 이 씬에는 Escape로 할 일이 없다. 그냥 무시.
        GameLog.Info("[Escape] 처리할 핸들러 없음");
    }

    /// <summary>New Input System 으로 Escape 감지 — legacy Input 은 InvalidOperationException, 키보드 없는 기기는 null 이라 ?. 필수.</summary>
    private bool IsEscapePressed()
    {
        return Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
    }
}
