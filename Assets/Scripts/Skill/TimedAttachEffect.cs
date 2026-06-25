using UnityEngine;
using System.Collections;

// 적에게 일정 시간 부착되는 스킬/디버프 시각 효과.
// 부모(적)의 자식으로 생성되며, 지속시간이 끝나면 스스로 제거된다.
public class TimedAttachEffect : MonoBehaviour
{
    private string _id;
    private Coroutine _life;

    // 부모에 효과를 부착. 같은 id가 이미 있으면 재사용해서 시간만 갱신(중복 스프라이트 방지).
    public static void Spawn(GameObject prefab, Transform parent, float duration, string id)
    {
        if (prefab == null || parent == null) return;

        // 재적용 시 중복 부착 방지 — 같은 id 효과가 있으면 시간만 리셋
        foreach (var fx in parent.GetComponentsInChildren<TimedAttachEffect>(true))
        {
            if (fx._id == id) { fx.Play(duration); return; }
        }

        GameObject obj = Instantiate(prefab, parent);
        obj.transform.localPosition = Vector3.zero;   // 적 중심에 부착 (오프셋은 프리팹에서 조정)
        
        // 부모(적) 스케일에 영향받지 않게 이펙트 본래 크기로 고정
        Vector3 baseScale = prefab.transform.localScale;
        Vector3 parentLossy = parent.lossyScale;
        obj.transform.localScale = new Vector3(
            parentLossy.x != 0 ? baseScale.x / parentLossy.x : baseScale.x,
            parentLossy.y != 0 ? baseScale.y / parentLossy.y : baseScale.y,
            parentLossy.z != 0 ? baseScale.z / parentLossy.z : baseScale.z);
        
        var effect = obj.GetComponent<TimedAttachEffect>();
        if (effect == null) effect = obj.AddComponent<TimedAttachEffect>();

        effect._id = id;
        effect.Play(duration);
    }

    public void Play(float duration)
    {
        gameObject.SetActive(true);
        if (_life != null) StopCoroutine(_life);
        _life = StartCoroutine(LifeRoutine(duration));
    }

    private IEnumerator LifeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}