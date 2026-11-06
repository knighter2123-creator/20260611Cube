using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>적 HP 바 관리자(Canvas) — 적 위치를 따라가며 바는 풀링(생성·rebuild·GC 가 스폰마다 프레임을 튀게 해서).</summary>
public class HpBar : MonoBehaviour
{
    [SerializeField] GameObject _monsterHPbar;

    [Tooltip("시작할 때 미리 만들어 둘 HP 바 개수. 동시에 살아 있는 적 수에 맞추면 첫 스폰 렉도 사라집니다.")]
    [SerializeField] int prewarmCount = 12;

    [Tooltip("한 프레임에 미리 만들 개수. UI 라 만들 때마다 Canvas 재구성이 따라오므로 한 번에 몰지 않습니다.")]
    [SerializeField] int prewarmPerFrame = 3;

    readonly List<Transform>     monsterTransforms = new List<Transform>();
    readonly List<GameObject>    activeBars        = new List<GameObject>();
    readonly List<RectTransform> activeBarRects    = new List<RectTransform>();   // 매 프레임 GetComponent 방지
    readonly Stack<GameObject>   freeBars          = new Stack<GameObject>();

    [SerializeField] Camera        mainCamera;
    RectTransform canvasRect;

    // 예열을 프레임마다 나눠 생성 (다른 예열과 겹쳐 시작 직후 멈칫), 그사이 부족분은 RegisterEnemy 가 채움
    IEnumerator Start()
    {
        canvasRect = GetComponent<RectTransform>();

        // HP 바는 누를 일이 없음 — 터치마다 검사하지 않고, 겹친 자리의 아래쪽 클릭도 가로채지 않게
        if (TryGetComponent(out GraphicRaycaster raycaster)) raycaster.enabled = false;

        int perFrame = Mathf.Max(1, prewarmPerFrame);
        while (freeBars.Count + activeBars.Count < prewarmCount)
        {
            yield return null;
            for (int i = 0; i < perFrame && freeBars.Count + activeBars.Count < prewarmCount; i++)
                freeBars.Push(CreateBar());
        }
    }

    GameObject CreateBar()
    {
        GameObject bar = Instantiate(_monsterHPbar, transform);
        foreach (Graphic g in bar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        bar.SetActive(false);
        return bar;
    }

    public void RegisterEnemy(GameObject monster)
    {
        // ✅ 이미 등록된 Enemy면 스킵 (중복 방지)
        if (monsterTransforms.Contains(monster.transform)) return;

        // 살아 있는 것이 나올 때까지 꺼냄 (씬 정리 등으로 파괴된 것은 버림)
        GameObject hpbar = null;
        while (freeBars.Count > 0 && hpbar == null) hpbar = freeBars.Pop();
        if (hpbar == null) hpbar = CreateBar();

        hpbar.SetActive(true);

        monsterTransforms.Add(monster.transform);
        activeBars.Add(hpbar);
        activeBarRects.Add((RectTransform)hpbar.transform);

        monster.GetComponent<Enemy>()?.SetHpBar(hpbar);
    }

    /// <summary>적이 죽었을 때 Enemy.RemoveHpBar 가 호출. 파괴하지 않고 꺼서 풀로 돌려보냅니다.</summary>
    public void Release(GameObject hpbar)
    {
        if (hpbar == null) return;

        int i = activeBars.IndexOf(hpbar);
        if (i >= 0) RemoveAt(i);

        hpbar.SetActive(false);
        freeBars.Push(hpbar);
    }

    void RemoveAt(int i)
    {
        monsterTransforms.RemoveAt(i);
        activeBars.RemoveAt(i);
        activeBarRects.RemoveAt(i);
    }

    void Update()
    {
        for (int i = activeBars.Count - 1; i >= 0; i--)
        {
            if (monsterTransforms[i] == null || activeBars[i] == null)
            {
                RemoveAt(i);
                continue;
            }

            Vector3 screenPos = mainCamera.WorldToScreenPoint(
                monsterTransforms[i].position + new Vector3(0, 0.5f, 0)
            );

            // 값이 바뀔 때만 SetActive — 같은 값이라도 매 프레임 호출하면 비용이 있습니다
            bool visible = screenPos.z > 0;
            if (activeBars[i].activeSelf != visible) activeBars[i].SetActive(visible);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out Vector2 localPoint
            );
            activeBarRects[i].anchoredPosition = localPoint;
        }
    }
}
