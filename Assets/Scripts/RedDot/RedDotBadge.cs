using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 등에 붙이면 고른 알림이 하나라도 있을 때 빨간 점을 켬.
/// 에디터에서 이 컴포넌트를 붙이면 점(자식 오브젝트)이 자동으로 만들어지니 위치·크기·색은 그 오브젝트에서 바꾸세요.
/// </summary>
[DisallowMultipleComponent]
public class RedDotBadge : MonoBehaviour
{
    [Tooltip("이 중 하나라도 있으면 점을 켬")]
    [SerializeField] private RedDotKey[] keys = { RedDotKey.Mail };

    [Tooltip("켜고 끌 점 오브젝트 (컴포넌트를 붙이면 자동으로 만들어짐 · 우클릭 메뉴 '레드닷 점 만들기')")]
    [SerializeField] private GameObject dot;

    [Tooltip("(선택) 점 안에 개수를 쓸 글자. 비우면 점만 표시")]
    [SerializeField] private TMP_Text countText;
    [Tooltip("이보다 많으면 '{max}+' 로 표시")]
    [SerializeField] private int maxCount = 99;

    [Header("깜빡임 (선택)")]
    [Tooltip("점이 켜진 동안 살짝 커졌다 작아짐")]
    [SerializeField] private bool  pulse = false;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseScale = 0.15f;

    private int     shownCount = -1;
    private Vector3 dotBaseScale = Vector3.one;

    private void Awake()
    {
        if (dot != null) dotBaseScale = dot.transform.localScale;
    }

    private void OnEnable()
    {
        RedDot.Changed += Refresh;
        shownCount = -1;
        Refresh();
    }

    private void OnDisable() => RedDot.Changed -= Refresh;

    /// <summary>코드에서 알림 종류를 바꿀 때 (예: 재사용 목록 칸).</summary>
    public void SetKeys(params RedDotKey[] newKeys)
    {
        keys = newKeys ?? new RedDotKey[0];
        shownCount = -1;
        Refresh();
    }

    private void Refresh()
    {
        int count = 0;
        if (keys != null)
            foreach (RedDotKey k in keys) count += RedDot.Count(k);
        if (count == shownCount) return;
        shownCount = count;

        if (dot != null) dot.SetActive(count > 0);
        if (countText != null)
            countText.text = count > maxCount ? maxCount + "+" : count.ToString();
    }

    private void Update()
    {
        if (!pulse || dot == null || !dot.activeSelf) return;
        float s = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseSpeed)) * pulseScale;
        dot.transform.localScale = dotBaseScale * s;
    }

#if UNITY_EDITOR
    // 컴포넌트를 처음 붙일 때 점을 만들어 둠
    private void Reset()
    {
        // 인스펙터 'Reset' 을 눌러도 점이 두 개 생기지 않게 기존 것을 다시 연결
        Transform existing = transform.Find("RedDot");
        if (existing != null) dot = existing.gameObject;
        if (dot == null) CreateDotInEditor();
    }

    [ContextMenu("레드닷 점 만들기")]
    private void CreateDotInEditor()
    {
        if (!(transform is RectTransform)) { Debug.LogWarning("[RedDotBadge] UI(RectTransform) 오브젝트에 붙여 주세요.", this); return; }
        UnityEditor.Undo.RecordObject(this, "레드닷 점 만들기");
        dot = CreateDot((RectTransform)transform);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>오른쪽 위 모서리에 빨간 원(Image)을 만듦. 에디터 도구(RedDotSetup)도 씀.</summary>
    public static GameObject CreateDot(RectTransform parent, float size = 24f)
    {
        var go = new GameObject("RedDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "레드닷 점 만들기");
        go.layer = parent.gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(-size * 0.35f, -size * 0.35f);
        rt.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite        = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color         = new Color(1f, 0.231f, 0.188f);   // #FF3B30
        img.raycastTarget = false;                            // 버튼 클릭을 가로막지 않게

        go.GetComponent<LayoutElement>().ignoreLayout = true; // 버튼에 레이아웃 그룹이 있어도 위치 유지
        go.SetActive(false);
        return go;
    }

    /// <summary>에디터 도구용 — 연결된 점이 없을 때만 만들고 keys 를 정함.</summary>
    public void SetupInEditor(RedDotKey[] newKeys)
    {
        keys = newKeys;
        if (dot == null) CreateDotInEditor();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public GameObject DotInEditor => dot;
#endif
}
