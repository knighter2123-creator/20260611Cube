using UnityEngine;
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
public class TimedAttachEffect : MonoBehaviour
{
    private string _id;
    private Coroutine _life;

    // GetComponentsInChildren 은 부를 때마다 새 배열을 만듭니다. 결과를 담을 목록을 하나만 두고 재사용합니다.
    // (메인 스레드에서만 불리고, 아래 foreach 안에서 다시 Spawn 을 부르지 않으므로 공유해도 안전)
    private static readonly List<TimedAttachEffect> SearchBuffer = new List<TimedAttachEffect>(4);

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

        GameObject obj = Instantiate(prefab, parent);
        obj.transform.localPosition = Vector3.zero;   // 적 중심에 부착 (오프셋은 프리팹에서 조정)

        // 부모(적) 스케일에 영향받지 않게 이펙트 본래 크기로 고정
        Vector3 baseScale   = prefab.transform.localScale;
        Vector3 parentLossy = parent.lossyScale;
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
