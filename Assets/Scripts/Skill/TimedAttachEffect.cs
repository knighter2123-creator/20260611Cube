using UnityEngine;
using UnityEngine.Rendering;   // SortingGroup
using System.Collections;
using System.Collections.Generic;

// 적에게 일정 시간 붙는 디버프 시각 효과 — 적의 자식으로 생성해 끝나면 꺼서 재사용(GC 방지), 도중에 꺼지면(적 풀 반환) 스스로 정리, 위치는 적 그림 범위의 가운데(크기별 고정 오프셋 불가), 적 SortingGroup 안 가장 앞 순서(밖이면 다른 적 몸 위에도 그려짐)
public class TimedAttachEffect : MonoBehaviour
{
    private string _id;
    private Coroutine _life;

    // GetComponentsInChildren 결과 목록 재사용 (매번 배열 할당 방지, 메인 스레드·재진입 없음이라 공유 안전)
    private static readonly List<TimedAttachEffect> SearchBuffer = new List<TimedAttachEffect>(4);
    private static readonly List<SpriteRenderer>    RendererBuffer = new List<SpriteRenderer>(32);

    // 부모에 효과를 부착. 같은 id가 이미 있으면 재사용해서 시간만 갱신(중복 스프라이트 방지).
    public static void Spawn(GameObject prefab, Transform parent, float duration, string id)
    {
        if (prefab == null || parent == null) return;

        // 꺼진 부모(풀에 들어간 적)에 새로 만들면 코루틴 없이 켜진 채 남아, 다음 스폰 때 그대로 보입니다.
        if (!parent.gameObject.activeInHierarchy) return;

        // 재적용 시 중복 부착 방지 — 같은 id 효과가 있으면(꺼져 있어도) 다시 켜고 시간만 리셋
        parent.GetComponentsInChildren(true, SearchBuffer);
        foreach (var fx in SearchBuffer)
        {
            if (fx._id == id)
            {
                SearchBuffer.Clear();
                fx.Play(duration);
                return;
            }
        }
        SearchBuffer.Clear();

        // 적 몸 가운데 + 프리팹 오프셋 — 처음 만들 때 한 번, 생성 '전에' 계산 (뒤면 이펙트 자신이 범위에 포함)
        Vector3 center = BodyCenter(parent, out bool found);

        // 적 SortingGroup 안에 넣음 — 이 적보다는 앞, 앞에 선 다른 적에겐 함께 가려짐
        SortingGroup group  = parent.GetComponentInChildren<SortingGroup>();
        Transform    holder = group != null ? group.transform : parent;

        GameObject obj = Instantiate(prefab, holder);
        obj.transform.position = found
            ? new Vector3(center.x, center.y, parent.position.z) + prefab.transform.localPosition
            : parent.position + prefab.transform.localPosition;

        DrawAbove(obj, holder);

        // 부모(적 모델) 스케일에 영향받지 않게 이펙트 본래 크기로 고정 (모델이 좌우 반전(-1)돼 있어도 나누면 다시 바로 섬)
        Vector3 baseScale   = prefab.transform.localScale;
        Vector3 parentLossy = holder.lossyScale;
        obj.transform.localScale = new Vector3(
            Unscale(baseScale.x, parentLossy.x),
            Unscale(baseScale.y, parentLossy.y),
            Unscale(baseScale.z, parentLossy.z));

        var effect = obj.GetComponent<TimedAttachEffect>();
        if (effect == null) effect = obj.AddComponent<TimedAttachEffect>();

        effect._id = id;
        effect.Play(duration);
    }

    // 부모 스케일로 나눠 원래 크기를 되찾습니다. 0 이면 나눌 수 없으니 그대로 둡니다.
    private static float Unscale(float value, float parentScale)
        => parentScale != 0f ? value / parentScale : value;

    /// <summary>적 그림 가운데(월드) — 스프라이트 있는 렌더러 범위를 합침 (빈 렌더러·붙은 이펙트는 제외, 넣으면 원점 쪽으로 끌림).</summary>
    private static Vector3 BodyCenter(Transform target, out bool found)
    {
        target.GetComponentsInChildren(false, RendererBuffer);

        Bounds bounds = default;
        found = false;
        foreach (SpriteRenderer sr in RendererBuffer)
        {
            if (sr.sprite == null || !sr.enabled || sr.GetComponentInParent<TimedAttachEffect>(true) != null) continue;

            if (found) bounds.Encapsulate(sr.bounds);
            else       { bounds = sr.bounds; found = true; }
        }
        RendererBuffer.Clear();
        return bounds.center;
    }

    /// <summary>이펙트 렌더러 순서를 적 그림 중 가장 앞보다 한 칸 앞으로 (SortingGroup 안에서만 비교, 이펙트 내부 간격은 유지).</summary>
    private static void DrawAbove(GameObject effect, Transform holder)
    {
        int layerId = 0, layerValue = int.MinValue, order = int.MinValue;
        holder.GetComponentsInChildren(false, RendererBuffer);
        foreach (SpriteRenderer sr in RendererBuffer)
        {
            if (sr.sprite == null || sr.transform.IsChildOf(effect.transform)) continue;

            // 정렬은 레이어가 먼저, 같은 레이어 안에서 순서 — 둘 다 따져 가장 앞의 것을 찾습니다.
            int value = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
            if (value > layerValue || (value == layerValue && sr.sortingOrder > order))
            {
                layerValue = value;
                layerId    = sr.sortingLayerID;
                order      = sr.sortingOrder;
            }
        }
        RendererBuffer.Clear();
        if (order == int.MinValue) return;   // 기준 삼을 그림이 없음 — 프리팹 값 그대로

        effect.GetComponentsInChildren(true, RendererBuffer);
        int lowest = int.MaxValue;
        foreach (SpriteRenderer sr in RendererBuffer) lowest = Mathf.Min(lowest, sr.sortingOrder);
        foreach (SpriteRenderer sr in RendererBuffer)
        {
            sr.sortingLayerID = layerId;
            sr.sortingOrder   = order + 1 + (sr.sortingOrder - lowest);
        }
        RendererBuffer.Clear();
    }

    public void Play(float duration)
    {
        // 부모가 꺼져 있으면 코루틴 시작 불가 — 외부에서 Play 를 직접 부르는 경우 대비
        if (!transform.parent || !transform.parent.gameObject.activeInHierarchy) return;

        gameObject.SetActive(true);
        if (_life != null) StopCoroutine(_life);
        _life = StartCoroutine(LifeRoutine(duration));
    }

    private IEnumerator LifeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        // 정상 종료 — 꺼서 재사용, _life 를 먼저 비워 OnDisable 이 '도중 꺼짐'으로 오해하지 않게
        _life = null;
        gameObject.SetActive(false);
    }

    // 도중에 꺼짐(적 풀 반환·씬 언로드) → Destroy 로 정리 (부모 비활성화 중엔 SetActive 불가, 드문 경우라 재사용 포기)
    private void OnDisable()
    {
        if (_life == null) return;

        _life = null;
        Destroy(gameObject);
    }
}
