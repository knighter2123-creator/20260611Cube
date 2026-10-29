using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 HP 바 관리자 (Canvas 위). 적 위치를 따라 HP 바를 옮깁니다.
///
/// ★ HP 바도 풀링합니다.
///   예전에는 스폰마다 Instantiate, 사망마다 Destroy 를 해서
///   ① UI 오브젝트 생성 비용 ② Canvas 재구성(rebuild) ③ Destroy 가 남긴 GC 가
///   스폰 순간마다 겹쳐 프레임이 튀었습니다. 적은 ObjectPoolManager 로 풀링하면서
///   HP 바만 빠져 있었던 것 — 이제 꺼서 보관했다가 다시 씁니다.
/// </summary>
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

    // ★ [프레임 드랍 수정] 예전에는 씬이 켜지는 프레임에 12개를 한꺼번에 만들었습니다.
    //   총알·데미지 텍스트 예열과 같은 프레임에 겹쳐 시작 직후 크게 멈칫했으므로 프레임마다 나눠 만듭니다.
    //   그사이 적이 나오면 RegisterEnemy 가 부족분을 하나씩 만들어 줍니다.
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
