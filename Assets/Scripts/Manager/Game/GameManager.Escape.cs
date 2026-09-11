using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

/// <summary>
/// GameManager의 Escape(안드로이드 뒤로가기) 처리 전담 partial 파일.
/// 이 프로젝트의 관례(기능별 partial 분리)에 맞춰 GameManager.Escape.cs 로 둔다.
///
/// [설계 의도]
///   "언제 눌렸는가"는 전역(GameManager)이 판단하고,
///   "그래서 뭘 할 것인가"는 각 씬이 스스로 등록한다.
///
///   이렇게 하면 씬 이름 문자열 비교(if (name == "LoginScene"))가 통째로 사라진다.
///   씬을 새로 추가해도 GameManager는 건드릴 일이 없다.
///
/// [핸들러 우선순위]
///   나중에 등록된 것이 먼저 처리한다(= 스택). 화면 위에 나중에 뜬 팝업이
///   뒤로가기를 먼저 먹는 게 사용자가 기대하는 동작이기 때문이다.
///   예) 로그인 씬에서 옵션 팝업이 열려 있으면
///       Escape → 옵션 팝업만 닫힘 (종료 패널은 안 뜸)
/// </summary>
public partial class GameManager
{
    // 핸들러는 bool을 반환한다: true = "내가 처리했다, 여기서 멈춰라"
    // false = "나는 지금 처리할 상황이 아니다, 다음 핸들러로 넘겨라"
    private readonly List<Func<bool>> escapeHandlers = new List<Func<bool>>();

    /// <summary>씬/팝업이 활성화될 때(OnEnable) 자기 처리 함수를 등록한다.</summary>
    public void RegisterEscapeHandler(Func<bool> handler)
    {
        if (handler == null) return;
        if (escapeHandlers.Contains(handler)) return;   // 중복 등록 방지
        escapeHandlers.Add(handler);
    }

    /// <summary>
    /// 씬/팝업이 사라질 때(OnDisable) 반드시 해제한다.
    /// 해제를 빼먹으면 이미 파괴된 오브젝트의 메서드를 붙잡고 있어서
    /// MissingReferenceException이 터진다. 등록/해제는 항상 짝으로.
    /// </summary>
    public void UnregisterEscapeHandler(Func<bool> handler)
    {
        if (handler == null) return;
        escapeHandlers.Remove(handler);
    }

    /// <summary>
    /// ★ GameManager 본체의 기존 Update() 안에서 이 함수를 호출할 것.
    ///   partial 클래스는 Update()를 두 개 가질 수 없기 때문에
    ///   여기서 Update()를 새로 만들면 컴파일 에러가 난다.
    ///
    ///   void Update()
    ///   {
    ///       // ...기존 코드...
    ///       UpdateEscape();
    ///   }
    /// </summary>
    private void UpdateEscape()
    {
        if (!IsEscapePressed()) return;

        // 뒤에서부터(나중에 등록된 것부터) 순회.
        // 핸들러 안에서 Unregister가 호출돼도 안전하도록 역순 for문을 쓴다.
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
        Debug.Log("[Escape] 처리할 핸들러 없음");
    }

    /// <summary>
    /// New Input System 기준 Escape 감지.
    ///
    /// ※ 이 프로젝트는 이미 New Input System으로 이전했기 때문에
    ///   Input.GetKeyDown(KeyCode.Escape)를 쓰면 안 된다.
    ///   Active Input Handling이 "Input System Package (New)"일 때
    ///   legacy Input 접근은 InvalidOperationException을 던진다.
    ///
    /// ※ Keyboard.current는 키보드가 없는 기기에서 null이므로 ?. 필수.
    /// </summary>
    private bool IsEscapePressed()
    {
        return Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
    }
}
