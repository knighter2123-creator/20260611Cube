using UnityEngine;

/// <summary>
/// PC(스탠드얼론) 빌드에서만 마우스 커서를 커스텀 이미지로 바꿔줍니다.
/// 모바일 빌드에서는 아무 일도 하지 않습니다.
///
/// 씬에 빈 게임오브젝트를 하나 만들어(이름 예: "CursorManager") 이 스크립트를 붙이고,
/// 인스펙터에 커서 텍스처를 꽂으면 끝입니다.
///
/// ─── Cursor.SetCursor는 무엇을 하는가? (학습 포인트) ────────────────────
/// 이건 게임 화면 위에 그림을 그리는 게 아니라, **운영체제에게 "이 앱 위에서는
/// 이 그림을 커서로 써라"라고 부탁하는 것**입니다. 그래서:
///
///   · 매 프레임 위치를 따라가는 코드가 필요 없습니다 (OS가 알아서 그림)
///   · 게임이 렉이 걸려도 커서는 부드럽게 움직입니다 (프레임과 무관)
///   · UI 위든 3D 오브젝트 위든 항상 맨 위에 보입니다 (정렬 순서 고민 없음)
///
/// 이런 방식을 하드웨어 커서라고 부릅니다. 유니티 UI 이미지를 마우스 좌표로
/// 옮기는 방식(소프트웨어 커서)보다 훨씬 가볍고 자연스러워요.
/// 대신 애니메이션이나 아주 큰 커서는 못 쓴다는 제약이 있습니다.
/// ────────────────────────────────────────────────────────────────────
///
/// ─── 클래스 전체가 아니라 "함수 안"만 #if로 감싼 이유 (중요) ─────────────
/// 클래스 전체를 #if UNITY_STANDALONE 으로 감싸면, 모바일 빌드에서 이 클래스가
/// 아예 사라집니다. 그러면 씬에 붙여둔 컴포넌트가 "Missing (Mono Script)"가 되고,
/// 프리팹/씬 파일이 더러워집니다. 심하면 다른 참조까지 끊어져요.
///
/// 그래서 클래스와 필드는 항상 컴파일되게 두고, 실제로 동작하는 코드만
/// 조건부로 감쌉니다. 모바일에서는 그냥 아무것도 안 하는 빈 컴포넌트가 됩니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public class CustomCursor : MonoBehaviour
{
    [Header("커서 이미지")]
    [Tooltip("Texture Type을 'Cursor'로 설정한 텍스처를 넣으세요. (스프라이트가 아니라 텍스처입니다)\n" +
             "권장 크기: 32x32 또는 64x64")]
    [SerializeField] private Texture2D cursorTexture;

    [Tooltip("실제 '클릭 지점'이 이미지의 어느 픽셀인지. 이미지 왼쪽 위가 (0, 0)입니다.\n" +
             "· 화살표 모양이면 뾰족한 끝 → 보통 (0, 0)\n" +
             "· 십자/조준점이면 한가운데 → 32x32 이미지 기준 (16, 16)")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    [Header("옵션")]
    [Tooltip("씬을 넘어가도 이 오브젝트를 유지합니다. (진화 스테이지 씬 전환 대비)")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("알트탭 등으로 창을 벗어났다 돌아왔을 때 커서를 다시 적용합니다.\n" +
             "일부 환경에서 포커스가 바뀌면 OS 기본 커서로 되돌아가는 것을 막아줍니다.")]
    [SerializeField] private bool reapplyOnFocus = true;

    // 중복 방지용 — 커서는 앱 전체에 하나뿐인 전역 설정이므로 인스턴스도 하나면 충분합니다.
    private static CustomCursor instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (dontDestroyOnLoad)
        {
            // DontDestroyOnLoad는 최상위(부모 없는) 오브젝트에만 적용됩니다.
            // 다른 오브젝트의 자식으로 두면 경고가 뜨니 주의하세요.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        Apply();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && reapplyOnFocus) Apply();
    }

    /// <summary>
    /// 커서를 커스텀 이미지로 적용합니다.
    /// 런타임에 커서를 바꾸고 싶어지면 SetCursorTexture()를 쓰세요.
    /// </summary>
    public void Apply()
    {
        // ─── 여기부터가 PC 전용 ────────────────────────────────────────
        // UNITY_STANDALONE : Windows / macOS / Linux 빌드
        // UNITY_EDITOR     : 에디터에서 테스트할 때. 빌드 타겟이 Android로 잡혀 있으면
        //                    에디터에서도 UNITY_STANDALONE이 정의되지 않으므로 함께 넣습니다.
        //                    (이게 없으면 "에디터에선 안 되는데요?" 하는 상황이 생깁니다)
#if UNITY_STANDALONE || UNITY_EDITOR

        if (cursorTexture == null)
        {
            Debug.LogWarning("[CustomCursor] cursorTexture가 비어 있습니다. 기본 커서를 사용합니다.");
            return;
        }

        // 혹시 다른 코드가 커서를 숨기거나 잠갔을 수 있으므로 복구
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        // CursorMode.Auto : 가능하면 하드웨어 커서로, 불가능하면 자동으로 소프트웨어 커서로.
        //                   대부분의 경우 이걸 쓰면 됩니다.
        // CursorMode.ForceSoftware : 항상 유니티가 직접 그림. 커서가 한 프레임 늦게 따라옵니다.
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);

        Debug.Log($"[CustomCursor] 커서 적용 — {cursorTexture.name} (hotspot {hotspot})");
#endif
    }

    /// <summary>런타임에 커서 이미지를 교체하고 싶을 때 사용합니다. (지금은 쓸 일 없음)</summary>
    public void SetCursorTexture(Texture2D texture, Vector2 newHotspot)
    {
        cursorTexture = texture;
        hotspot       = newHotspot;
        Apply();
    }

    /// <summary>OS 기본 커서로 되돌립니다.</summary>
    public void ResetToDefault()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        // 텍스처에 null을 넘기면 시스템 기본 커서로 돌아갑니다.
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
#endif
    }
}
