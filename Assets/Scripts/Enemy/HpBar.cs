using System.Collections.Generic;
using UnityEngine;

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

    readonly List<Transform>     monsterTransforms = new List<Transform>();
    readonly List<GameObject>    activeBars        = new List<GameObject>();
    readonly List<RectTransform> activeBarRects    = new List<RectTransform>();   // 매 프레임 GetComponent 방지
    readonly Stack<GameObject>   freeBars          = new Stack<GameObject>();

    Camera        mainCamera;
    RectTransform canvasRect;

    void Start()
    {
        mainCamera = Camera.main;
        canvasRect = GetComponent<RectTransform>();

        for (int i = freeBars.Count; i < prewarmCount; i++)
            freeBars.Push(CreateBar());
    }

    GameObject CreateBar()
    {
        GameObject bar = Instantiate(_monsterHPbar, transform);
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
