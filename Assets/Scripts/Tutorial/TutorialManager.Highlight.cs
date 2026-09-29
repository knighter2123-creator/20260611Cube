using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강조 표시 담당 (partial).
///
/// 원리 — "구멍 뚫린 어두운 화면"을 셰이더 없이 만드는 법
///   ┌───────────────┐
///   │      위       │   구멍(강조 영역)을 둘러싸는 검은 Image 4장을 배치한다.
///   ├────┬────┬─────┤   구멍 자리에는 아무것도 없으니 뒤의 게임 화면이 그대로 보인다.
///   │ 왼 │구멍│ 오  │   흰 테두리도 같은 방식으로 얇은 Image 4장.
///   ├────┴────┴─────┤
///   │     아래      │   → 스프라이트/머티리얼 준비 없이 코드만으로 끝난다.
///   └───────────────┘
///
/// 좌표계: 모든 사각형은 "overlayRoot 의 로컬 좌표"로 계산한다.
///   화면 좌표(픽셀) → ScreenPointToLocalPointInRectangle → overlayRoot 로컬
///   → 자식에 넣을 땐 anchoredPosition = 중심 - overlayRoot.rect.center
///   (앵커를 가운데(0.5,0.5)로 고정했기 때문에 이 식이 성립)
/// </summary>
public partial class TutorialManager
{
    [Header("강조 표시")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private Color frameColor = Color.white;
    [SerializeField] private float frameThickness = 10f;
    [Tooltip("단계가 바뀔 때 구멍/박스가 따라가는 속도 (클수록 빠름)")]
    [SerializeField] private float followSpeed = 14f;
    [Tooltip("설명 박스와 구멍, 화면 가장자리 사이 간격")]
    [SerializeField] private float boxMargin = 30f;

    private readonly RectTransform[] dimPieces = new RectTransform[4];
    private readonly Image[] framePieces = new Image[4];
    private readonly Vector3[] worldCorners = new Vector3[4];   // 매 프레임 new 하지 않도록 재사용 (GC 방지)
    private readonly Vector2[] screenCorners = new Vector2[4];

    private Rect currentHole;
    private float frameAlpha;
    private Renderer targetRenderer;
    private Camera targetUICamera;
    private Camera worldCamera;
    private Camera overlayCamera;

    // ─────────────────────────────────────────────
    private void BuildHighlightLayer()
    {
        var layer = new GameObject("HighlightLayer (자동 생성)", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(overlayRoot, false);
        layer.SetAsFirstSibling();                 // 맨 뒤에 그려지도록 (버튼·설명 박스보다 아래)
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;

        for (int i = 0; i < 4; i++) dimPieces[i] = CreatePiece(layer, $"Dim_{i}", dimColor).rectTransform;
        for (int i = 0; i < 4; i++) framePieces[i] = CreatePiece(layer, $"Frame_{i}", frameColor);  // 나중에 만든 게 위에 그려짐

        // 설명 박스도 가운데 앵커로 통일 (위치 계산식이 이것을 전제로 함)
        messageBox.anchorMin = messageBox.anchorMax = messageBox.pivot = new Vector2(0.5f, 0.5f);

        // 튜토리얼 캔버스가 Overlay 면 카메라는 null 을 넘겨야 한다
        // ★ (true) = 꺼져 있는 오브젝트에서도 찾기. overlayRoot 를 꺼진 채로 씬에 저장해 두면
        //   인자 없는 GetComponentInParent 는 null 을 돌려줘서 NullReferenceException 이 납니다.
        Canvas canvas = overlayRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogError("[Tutorial] overlayRoot 가 Canvas 안에 있지 않습니다.", this);
            overlayCamera = null;
            return;
        }
        Canvas root = canvas.rootCanvas;
        overlayCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    private static Image CreatePiece(Transform parent, string name, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;            // 스프라이트가 없으면 흰 사각형 → color 로 칠해짐
        img.raycastTarget = false;    // 클릭은 전부 NextArea 가 받는다
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    private void DisableRaycastInMessageBox()
    {
        foreach (Graphic g in messageBox.GetComponentsInChildren<Graphic>(true))
        {
            // 스킵 버튼을 박스 안에 넣었다면 그건 눌려야 하므로 건드리지 않는다
            if (g.transform.IsChildOf(skipButton.transform)) continue;
            g.raycastTarget = false;
        }
    }

    // ─────────────────────────────────────────────
    // 타겟 → 사각형
    // ─────────────────────────────────────────────

    /// <summary>단계가 바뀔 때 한 번만: GetComponent 류는 매 프레임 부르지 않는다.</summary>
    private void PrepareTarget(TutorialStep step)
    {
        targetRenderer = null;
        targetUICamera = null;

        if (step.targetType == TutorialTargetType.World && step.worldTarget != null)
        {
            if (!step.worldTarget.TryGetComponent(out targetRenderer))
                targetRenderer = step.worldTarget.GetComponentInChildren<Renderer>();
        }
        else if (step.targetType == TutorialTargetType.UI && step.uiTarget != null)
        {
            Canvas c = step.uiTarget.GetComponentInParent<Canvas>();
            if (c != null)
            {
                c = c.rootCanvas;
                targetUICamera = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
            }
        }
    }

    /// <summary>현재 단계 타겟의 사각형(overlayRoot 로컬). 타겟이 없거나 꺼져 있으면 false.</summary>
    private bool TryGetTargetRect(TutorialStep step, out Rect rect)
    {
        rect = default;

        switch (step.targetType)
        {
            case TutorialTargetType.UI:
                if (step.uiTarget == null || !step.uiTarget.gameObject.activeInHierarchy) return false;

                step.uiTarget.GetWorldCorners(worldCorners);
                for (int i = 0; i < 4; i++)
                    screenCorners[i] = RectTransformUtility.WorldToScreenPoint(targetUICamera, worldCorners[i]);
                break;

            case TutorialTargetType.World:
            {
                if (step.worldTarget == null || !step.worldTarget.gameObject.activeInHierarchy) return false;

                Bounds b = (step.useRendererBounds && targetRenderer != null)
                    ? targetRenderer.bounds
                    : new Bounds(step.worldTarget.position, step.worldSize);

                if (!WorldBoundsToScreen(b)) return false;
                break;
            }

            // ★ 추가 — 플레이어 사거리.
            //   Bullet 은 풀에서 런타임에 꺼내는 객체라 인스펙터로 참조할 수 없고,
            //   timeScale 0 인 튜토리얼 중에는 발사되지도 않습니다.
            //   그래서 "총알"이 아니라 "총알이 닿는 범위"를 보여줍니다.
            //   Player.FindTarget 이 쓰는 값과 같은 stat.attackRange 를 읽으므로
            //   강화/증강으로 사거리가 바뀌어도 강조 범위가 자동으로 따라갑니다.
            case TutorialTargetType.PlayerRange:
            {
                Player p = Player.Instance;   // 인스펙터 연결 없이 싱글턴으로 찾는다
                if (p == null || !p.gameObject.activeInHierarchy || p.stat == null) return false;

                // FindTarget 은 "반지름" 비교라 지름 = 반지름 × 2.
                // 구멍은 사각형이라 사거리 원에 딱 맞는 정사각형(외접)으로 그려집니다.
                float diameter = p.stat.attackRange * 2f;
                Bounds b = new Bounds(p.transform.position, new Vector3(diameter, diameter, 0f));

                if (!WorldBoundsToScreen(b)) return false;
                break;
            }

            default:
                return false;
        }

        // 화면 좌표 → overlayRoot 로컬 좌표, 네 점의 최소/최대로 사각형 만들기
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screenCorners[i], overlayCamera, out Vector2 lp);
            min = Vector2.Min(min, lp);
            max = Vector2.Max(max, lp);
        }

        rect = Rect.MinMaxRect(min.x - step.padding, min.y - step.padding,
                               max.x + step.padding, max.y + step.padding);
        return true;
    }

    /// <summary>
    /// 월드 Bounds 의 네 꼭짓점 → 화면 좌표(screenCorners). World / PlayerRange 가 같이 쓴다.
    /// (같은 계산을 두 case 에 복사해 두면 한쪽만 고쳐지는 일이 생기므로 함수로 뺌)
    /// </summary>
    private bool WorldBoundsToScreen(Bounds b)
    {
        if (worldCamera == null) worldCamera = Camera.main;   // Camera.main 은 태그 검색이라 캐시해 둔다
        if (worldCamera == null) return false;

        // 2D 게임이라 z 는 중심값 하나로 충분
        worldCorners[0] = new Vector3(b.min.x, b.min.y, b.center.z);
        worldCorners[1] = new Vector3(b.min.x, b.max.y, b.center.z);
        worldCorners[2] = new Vector3(b.max.x, b.max.y, b.center.z);
        worldCorners[3] = new Vector3(b.max.x, b.min.y, b.center.z);

        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = worldCamera.WorldToScreenPoint(worldCorners[i]);
            if (sp.z < 0f) return false;   // 카메라 뒤쪽
            screenCorners[i] = sp;
        }
        return true;
    }

    // ─────────────────────────────────────────────
    // 매 프레임 갱신
    // ─────────────────────────────────────────────

    private void SnapHighlight()
    {
        bool has = GetTargetOrCenter(out Rect target);
        currentHole = target;
        frameAlpha = has ? 1f : 0f;
        messageBox.anchoredPosition = ComputeBoxPosition(target, has);
        ApplyHole(currentHole);
    }

    private void UpdateHighlight(float dt)
    {
        bool has = GetTargetOrCenter(out Rect target);

        // 지수 감쇠 보간: 프레임레이트가 달라도 같은 속도로 따라간다
        float k = 1f - Mathf.Exp(-followSpeed * dt);

        currentHole = LerpRect(currentHole, target, k);
        frameAlpha = Mathf.MoveTowards(frameAlpha, has ? 1f : 0f, dt * 6f);
        messageBox.anchoredPosition = Vector2.Lerp(messageBox.anchoredPosition, ComputeBoxPosition(target, has), k);

        ApplyHole(currentHole);
    }

    /// <summary>타겟이 없으면 화면 가운데 크기 0 구멍 = 화면 전체가 어두워짐</summary>
    private bool GetTargetOrCenter(out Rect target)
    {
        if (TryGetTargetRect(steps[index], out target))
        {
            // ★ 수정 — 화면 가장자리 버튼(미션·상점)에서 아래 테두리가 잘리던 문제.
            //   padding 을 더한 구멍 + 테두리 두께가 화면 밖으로 나가면, 밖으로 나간 테두리는 그려지지 않습니다.
            //   그래서 구멍을 "안전 영역 안쪽으로 테두리 두께만큼 들어온 사각형" 안으로 잘라 넣습니다.
            //   밀어서 옮기는 게 아니라 넘친 쪽의 여백만 줄이는 것이라, 구멍이 버튼에서 벗어나지 않습니다.
            Rect s = GetSafeLocalRect();
            float t = frameThickness;
            target = ClampInside(target, Rect.MinMaxRect(s.xMin + t, s.yMin + t, s.xMax - t, s.yMax - t));
            return true;
        }

        target = new Rect(overlayRoot.rect.center, Vector2.zero);
        return false;
    }

    /// <summary>
    /// ★ 추가 — 기기의 안전 영역(Screen.safeArea)을 overlayRoot 로컬 좌표로.
    ///   Galaxy Note10 처럼 펀치홀 카메라·둥근 모서리가 있는 기기는
    ///   화면 끝 몇 픽셀이 가려집니다. 설명 박스와 테두리는 이 안에만 둡니다.
    ///   (시뮬레이터 상단 'Safe Area' 버튼으로 영역을 눈으로 확인할 수 있습니다)
    ///   Rect 몇 개 계산이라 매 프레임 불러도 성능 영향은 무시할 수준입니다.
    /// </summary>
    private Rect GetSafeLocalRect()
    {
        Rect sa = Screen.safeArea;   // 픽셀 단위, 좌하단 (0,0)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, sa.min, overlayCamera, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, sa.max, overlayCamera, out Vector2 b);

        // 캔버스가 화면보다 작게 잡혀 있어도 캔버스 밖으로는 나가지 않게 교집합
        return ClampInside(Rect.MinMaxRect(a.x, a.y, b.x, b.y), overlayRoot.rect);
    }

    /// <summary>r 을 bounds 안으로 잘라 넣는다. 넘친 변만 안쪽으로 당겨지고, 크기가 음수가 되지 않게 보정.</summary>
    private static Rect ClampInside(Rect r, Rect bounds)
    {
        float xMin = Mathf.Max(r.xMin, bounds.xMin);
        float yMin = Mathf.Max(r.yMin, bounds.yMin);
        float xMax = Mathf.Max(Mathf.Min(r.xMax, bounds.xMax), xMin);
        float yMax = Mathf.Max(Mathf.Min(r.yMax, bounds.yMax), yMin);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void ApplyHole(Rect h)
    {
        Rect r = overlayRoot.rect;

        // 구멍이 화면 밖으로 나간 부분은 잘라낸다 (조각 크기가 음수가 되는 것 방지)
        float xMin = Mathf.Clamp(h.xMin, r.xMin, r.xMax);
        float xMax = Mathf.Clamp(h.xMax, xMin,   r.xMax);
        float yMin = Mathf.Clamp(h.yMin, r.yMin, r.yMax);
        float yMax = Mathf.Clamp(h.yMax, yMin,   r.yMax);

        SetRect(dimPieces[0], Rect.MinMaxRect(r.xMin, yMax,   r.xMax, r.yMax));  // 위
        SetRect(dimPieces[1], Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, yMin));    // 아래
        SetRect(dimPieces[2], Rect.MinMaxRect(r.xMin, yMin,   xMin,   yMax));    // 왼쪽
        SetRect(dimPieces[3], Rect.MinMaxRect(xMax,   yMin,   r.xMax, yMax));    // 오른쪽

        float t = frameThickness;
        SetRect(framePieces[0].rectTransform, Rect.MinMaxRect(xMin - t, yMax,     xMax + t, yMax + t));
        SetRect(framePieces[1].rectTransform, Rect.MinMaxRect(xMin - t, yMin - t, xMax + t, yMin));
        SetRect(framePieces[2].rectTransform, Rect.MinMaxRect(xMin - t, yMin,     xMin,     yMax));
        SetRect(framePieces[3].rectTransform, Rect.MinMaxRect(xMax,     yMin,     xMax + t, yMax));

        // 테두리가 은은하게 깜빡이도록 (unscaledTime: timeScale 0 에서도 흐름)
        float pulse = Mathf.Lerp(0.6f, 1f, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);
        Color c = frameColor;
        c.a *= frameAlpha * pulse;
        for (int i = 0; i < 4; i++) framePieces[i].color = c;
    }

    /// <summary>
    /// 구멍 옆(좌/우)에 자리가 있으면 옆에, 없으면(가로로 긴 상단 바 등) 위/아래에 박스를 둔다.
    /// 사진처럼 "타워 왼쪽 → 설명은 오른쪽"이 기본.
    /// </summary>
    private Vector2 ComputeBoxPosition(Rect hole, bool hasTarget)
    {
        Rect root = overlayRoot.rect;
        Rect r = GetSafeLocalRect();   // ★ 수정 — 화면 전체가 아니라 안전 영역 기준으로 배치/클램프
        Vector2 half = messageBox.rect.size * 0.5f;
        Vector2 pos;

        if (!hasTarget)
        {
            pos = r.center;
        }
        else
        {
            float frame = frameThickness + boxMargin;
            float rightX = hole.xMax + frame + half.x;
            float leftX  = hole.xMin - frame - half.x;
            bool fitsRight = rightX + half.x <= r.xMax - boxMargin;
            bool fitsLeft  = leftX  - half.x >= r.xMin + boxMargin;

            if (fitsRight && (hole.center.x <= r.center.x || !fitsLeft))
                pos = new Vector2(rightX, hole.center.y);
            else if (fitsLeft)
                pos = new Vector2(leftX, hole.center.y);
            else
            {
                float belowY = hole.yMin - frame - half.y;
                float aboveY = hole.yMax + frame + half.y;
                bool fitsBelow = belowY - half.y >= r.yMin + boxMargin;
                bool fitsAbove = aboveY + half.y <= r.yMax - boxMargin;

                // 화면 위쪽 타겟이면 아래에, 아래쪽 타겟이면 위에
                bool preferBelow = hole.center.y >= r.center.y;
                float y = (preferBelow && fitsBelow) || !fitsAbove ? belowY : aboveY;
                pos = new Vector2(hole.center.x, y);
            }

            // 마지막으로 화면 안으로 밀어 넣기
            pos.x = Mathf.Clamp(pos.x, r.xMin + boxMargin + half.x, r.xMax - boxMargin - half.x);
            pos.y = Mathf.Clamp(pos.y, r.yMin + boxMargin + half.y, r.yMax - boxMargin - half.y);
        }

        // overlayRoot 로컬 → 가운데 앵커 기준 anchoredPosition.
        // ★ 안전 영역(r)의 중심이 아니라 overlayRoot 전체(root)의 중심을 빼야 합니다.
        //   앵커 기준점은 부모(overlayRoot) 사각형의 중심이기 때문입니다.
        return pos - root.center;
    }

    private void SetRect(RectTransform rt, Rect local)
    {
        rt.anchoredPosition = local.center - overlayRoot.rect.center;
        rt.sizeDelta = local.size;
    }

    private static Rect LerpRect(Rect a, Rect b, float t)
    {
        return Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
                               Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));
    }
}