using UnityEngine;
using UnityEngine.Rendering;   // SortingGroup
using System.Collections;
using System.Collections.Generic;

// 적에게 일정 시간 부착되는 스킬/디버프 시각 효과.
// 부모(적)의 자식으로 생성되며, 지속시간이 끝나면 꺼져서 다음 부착 때 재사용된다.
//
// ★ [버그 수정] 적이 풀로 돌아갈 때 이펙트가 남던 문제
//   예전에는 지속시간이 끝나면 Destroy 하는 코루틴만 있었습니다. 그런데 지속시간 도중 적이 죽어
//   풀로 반환(SetActive(false))되면 자식인 이 오브젝트의 코루틴도 같이 멈춰 Destroy 에 도달하지 못합니다.
//   그 적이 다시 스폰되면 꺼질 줄 모르는 방어력 감소 표시를 붙인 채로 나왔습니다.
//   → 지속시간 도중 꺼지면(OnDisable) 스스로 정리합니다.
//
// ★ [프레임 드랍 수정] 매 부착마다 Instantiate / Destroy 하던 것을 재사용으로 바꿨습니다.
//   방어력 감소는 쿨다운마다 들어오므로 생성·파괴 비용과 GC 가 계속 쌓였습니다.
//
// ★ [위치·순서] 적 몸 가운데에, 적보다 앞에 그립니다.
//   예전에는 적의 원점(transform 위치)에 붙였는데, SPUM 모델은 원점보다 아래로 내려가 있어서
//   원점이 머리 근처였습니다 — 이펙트가 적 위쪽에 떠 보였습니다. 적 종류마다 크기(0.3 / 0.5 / 1)가 달라
//   고정 오프셋으로는 맞출 수 없으므로, 적 그림(SpriteRenderer 들)의 실제 범위로 가운데를 계산합니다.
//   가운데에 두면 몸과 겹치므로 그 적의 SortingGroup '안' 에 넣고, 그 안에서 가장 앞 순서로 그립니다.
//   그룹 밖에서 순서만 올리면 모든 적보다 앞이 되어, 적끼리 겹칠 때 뒤쪽 적의 표시가 앞쪽 적 몸 위에 보였습니다.
public class TimedAttachEffect : MonoBehaviour
{
    private string _id;
    private Coroutine _life;

    // GetComponentsInChildren 은 부를 때마다 새 배열을 만듭니다. 결과를 담을 목록을 하나만 두고 재사용합니다.
    // (메인 스레드에서만 불리고, 아래 foreach 안에서 다시 Spawn 을 부르지 않으므로 공유해도 안전)
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

        // 적 몸 가운데 + 프리팹 위치(월드 단위 추가 오프셋 — 0 이면 정가운데).
        // 풀에서 다시 나와도 같은 적 인스턴스·같은 모양이므로 처음 만들 때 한 번만 계산합니다 (재사용 땐 위 Play 경로).
        // 만들기 '전에' 재야 합니다 — 만든 뒤에는 이 이펙트 자신의 그림까지 범위에 들어갑니다.
        Vector3 center = BodyCenter(parent, out bool found);

        // 적 모델의 SortingGroup 안에 넣습니다 — 그룹 밖이면 '모든 적보다 앞' 이 되어 겹친 다른 적 몸 위에도 보입니다.
        // 그룹 안에서는 그 적 그림들끼리만 앞뒤를 정하므로, 이 적보다는 앞이고 앞에 선 다른 적에게는 함께 가려집니다.
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

    /// <summary>
    /// 적 그림의 가운데 (월드 좌표). 스프라이트가 있는 SpriteRenderer 들의 범위를 합칩니다.
    /// 그림 없는 렌더러(루트의 빈 SpriteRenderer)와 이미 붙어 있는 이펙트는 뺍니다 — 넣으면 가운데가 원점 쪽으로 끌려갑니다.
    /// </summary>
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

    /// <summary>
    /// 이펙트의 SpriteRenderer 를 holder 아래 적 그림들 중 가장 앞의 것보다 한 칸 앞 순서로 맞춥니다.
    /// holder 가 SortingGroup 이면 이 순서는 그룹 안에서만 비교됩니다 (SPUM 부위들이 0~수십 순서를 씀).
    /// (이펙트 안에 렌더러가 여럿이면 서로의 앞뒤 간격은 프리팹 그대로 유지)
    /// </summary>
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
        // 부모(적)가 꺼져 있으면 코루틴을 시작할 수 없습니다 (StartCoroutine 이 에러).
        // Spawn 이 이미 걸러 주지만, 외부에서 Play 를 직접 부르는 경우를 대비한 방어입니다.
        if (!transform.parent || !transform.parent.gameObject.activeInHierarchy) return;

        gameObject.SetActive(true);
        if (_life != null) StopCoroutine(_life);
        _life = StartCoroutine(LifeRoutine(duration));
    }

    private IEnumerator LifeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        // 정상 종료 — 파괴하지 않고 꺼 두었다가 다음 부착 때 재사용합니다.
        // _life 를 먼저 비워야 아래 SetActive(false) 가 부르는 OnDisable 이 '도중에 꺼짐' 으로 오해하지 않습니다.
        _life = null;
        gameObject.SetActive(false);
    }

    // 지속시간 '도중에' 꺼졌다 = 부모(적)가 풀로 돌아갔거나 씬이 내려가는 중.
    // 코루틴은 이미 멈췄으므로 여기서 정리합니다.
    //
    // ★ SetActive(false) 가 아니라 Destroy 인 이유
    //   부모가 비활성화되는 '도중' 에는 자식의 활성 상태를 바꿀 수 없습니다
    //   ("Cannot change GameObject hierarchy while activating or deactivating the parent" — EnemyDeathEffect 주석 참고).
    //   Destroy 는 프레임 끝에 처리되므로 이 시점에도 안전합니다. 드문 경우라 재사용을 포기해도 비용은 작습니다.
    private void OnDisable()
    {
        if (_life == null) return;

        _life = null;
        Destroy(gameObject);
    }
}
