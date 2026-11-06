using UnityEngine;

/// <summary>PC 빌드용 하드웨어 커서(모바일은 아무것도 안 함) — 스프라이트 영역만 GPU 로 잘라 Point 필터로 확대한 텍스처를 씀 (sprite.texture 는 시트 전체가 커서가 되어서).</summary>
// [붙이는 위치] 로그인 씬 ManagerRoot 하위 — 포커스가 돌아올 때 다시 적용하려면 살아 있어야 함
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("커서")]
    [SerializeField] private Sprite cursorSprite;

    [Tooltip("클릭 기준점 (스프라이트 왼쪽 위 기준 픽셀, 원본 크기 기준). 화살표 커서면 보통 (0, 0)")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    [Tooltip("확대 배율 (정수). 26px 같은 작은 도트 커서를 화면에 맞게 키울 때")]
    [Range(1, 8)]
    [SerializeField] private int scale = 2;

    [Tooltip("Auto = 하드웨어 커서 (지연 없음, 권장). ForceSoftware = 유니티가 직접 그림")]
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private Texture2D cursorTexture;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (Application.isMobilePlatform) return;
        Apply();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (cursorTexture != null) Destroy(cursorTexture);
    }

    // 다른 창에 갔다가 돌아오면 Windows 가 기본 화살표로 되돌리는 경우가 있어 다시 적용합니다.
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !Application.isMobilePlatform) Apply();
    }

    /// <summary>커서를 표시하고 스프라이트를 적용합니다. 스프라이트가 없으면 OS 기본 커서.</summary>
    public void Apply()
    {
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (cursorSprite == null)
        {
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
            return;
        }

        if (cursorTexture == null) cursorTexture = BuildTexture(cursorSprite, scale);

        Cursor.SetCursor(cursorTexture, hotspot * scale, cursorMode);
    }


    /// <summary>스프라이트 영역만 잘라 scale 배로 키운 RGBA32 텍스처를 만듭니다.</summary>
    private static Texture2D BuildTexture(Sprite sprite, int scale)
    {
        Texture2D src  = sprite.texture;
        Rect      rect = sprite.textureRect;

        int w = Mathf.RoundToInt(rect.width)  * scale;
        int h = Mathf.RoundToInt(rect.height) * scale;

        // 원본 텍스처 전체에서 스프라이트 영역만 UV 로 지정해 확대 복사
        Vector2 uvScale  = new Vector2(rect.width / src.width, rect.height / src.height);
        Vector2 uvOffset = new Vector2(rect.x / src.width, rect.y / src.height);

        RenderTexture rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        FilterMode    prev = src.filterMode;
        src.filterMode = FilterMode.Point;   // 도트가 번지지 않게

        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        Graphics.Blit(src, rt, uvScale, uvOffset);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        src.filterMode = prev;

        return tex;
    }
}
