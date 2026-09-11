using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실기기에서 콘솔 로그를 화면에 직접 띄우는 디버그 오버레이.
/// adb 나 케이블 없이 폰에서 바로 로그를 읽을 수 있다.
///
/// [붙이는 위치]
///   LoginScene 의 ManagerRoot 하위에 빈 오브젝트를 만들어 붙인다.
///   (루트가 DontDestroyOnLoad 라 씬을 넘어가도 계속 보인다)
///
/// [쓰는 법]
///   화면 오른쪽 위의 작은 'LOG' 버튼을 누르면 켜고 끌 수 있다.
///   Filter 에 "[Haptic]" 을 넣으면 그 태그가 들어간 줄만 보여준다.
///
/// [출시 전에]
///   Enabled 체크를 해제하거나 오브젝트를 지울 것.
///   아래 #if 로 개발 빌드에서만 동작하게 해둘 수도 있다.
/// </summary>
public class OnScreenLog : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("끄면 오버레이 자체가 동작하지 않습니다. 출시 빌드에서는 해제하세요.")]
    [SerializeField] private bool enabledOverlay = true;
    [SerializeField] private bool showOnStart = true;
    [Tooltip("화면에 유지할 최대 줄 수")]
    [SerializeField] private int maxLines = 18;
    [Tooltip("이 문자열이 들어간 로그만 표시. 비우면 전부 표시")]
    [SerializeField] private string filter = "[Haptic]";
    [Tooltip("화면 높이 기준 글자 크기 비율. 폰에서 작으면 키우세요")]
    [Range(0.012f, 0.05f)]
    [SerializeField] private float fontScale = 0.022f;

    private readonly List<string> lines = new List<string>();
    private bool visible;
    private Vector2 scroll;
    private GUIStyle boxStyle, textStyle, buttonStyle;

    private void Awake()
    {
        if (!enabledOverlay)
        {
            // 컴포넌트를 꺼두면 OnGUI 도 돌지 않는다. 비용이 0이 된다.
            enabled = false;
            return;
        }

        visible = showOnStart;

        // ★ logMessageReceived 는 Debug.Log 가 호출될 때마다 불린다.
        //   Unity 가 이미 잡아둔 것을 넘겨받는 것이라, 기존 코드를 하나도 안 고쳐도
        //   지금까지 심어둔 모든 로그가 그대로 화면에 뜬다.
        Application.logMessageReceived += Handle;
    }

    private void OnDestroy()
    {
        // 등록과 해제는 반드시 짝으로. 씬을 넘나들며 중복 등록되면 같은 줄이 여러 번 쌓인다.
        Application.logMessageReceived -= Handle;
    }

    private void Handle(string message, string stackTrace, LogType type)
    {
        if (!string.IsNullOrEmpty(filter) && !message.Contains(filter)) return;

        // 경고/에러는 색을 입혀 눈에 띄게 한다. 작은 화면에서는 색 구분이 크게 도움이 된다.
        string colored = type switch
        {
            LogType.Warning => $"<color=#FFD54F>{message}</color>",
            LogType.Error or LogType.Exception or LogType.Assert => $"<color=#FF6E6E>{message}</color>",
            _ => message
        };

        lines.Add($"{Time.frameCount}  {colored}");

        // 오래된 줄부터 버린다. 안 버리면 방치형처럼 오래 켜두는 게임에서 메모리가 계속 는다.
        while (lines.Count > Mathf.Max(1, maxLines))
            lines.RemoveAt(0);

        scroll.y = float.MaxValue;   // 항상 맨 아래로
    }

    private void OnGUI()
    {
        EnsureStyles();

        float w = Screen.width;
        float h = Screen.height;
        float btnW = w * 0.16f;
        float btnH = h * 0.05f;

        // 토글 버튼 — 오른쪽 위 모서리
        if (GUI.Button(new Rect(w - btnW - 8f, 8f, btnW, btnH), visible ? "LOG ✕" : "LOG", buttonStyle))
            visible = !visible;

        if (!visible) return;

        Rect area = new Rect(8f, btnH + 16f, w - 16f, h * 0.45f);
        GUI.Box(area, GUIContent.none, boxStyle);

        GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
        scroll = GUILayout.BeginScrollView(scroll);

        for (int i = 0; i < lines.Count; i++)
            GUILayout.Label(lines[i], textStyle);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (textStyle != null) return;

        int size = Mathf.Max(10, Mathf.RoundToInt(Screen.height * fontScale));

        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            richText = true,   // <color> 태그를 쓰려면 필요
            wordWrap = true
        };
        textStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size };

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(new Color(0f, 0f, 0f, 0.75f));
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
