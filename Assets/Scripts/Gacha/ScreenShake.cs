using System.Collections;
using UnityEngine;

// 흔들 대상 Transform에 붙입니다.
// UI라면 resultPanel 루트(또는 흔들기 전용 컨테이너), 카메라 연출이면 Main Camera에.
public class ScreenShake : MonoBehaviour
{
    [SerializeField] private float defaultDuration  = 0.25f;
    [SerializeField] private float defaultMagnitude = 18f;  // UI 픽셀 기준. 카메라면 0.2 정도

    private Vector3 originLocalPos;
    private Coroutine shaking;
    private bool hasOrigin;

    public void Shake() => Shake(defaultDuration, defaultMagnitude);

    public void Shake(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f) return;

        // 비활성 상태에서는 코루틴을 시작할 수 없습니다
        if (!isActiveAndEnabled) return;

        // 흔들리는 중이 아닐 때만 원점 기록 (중첩 호출 시 원점 오염 방지)
        if (shaking == null)
        {
            originLocalPos = transform.localPosition;
            hasOrigin = true;
        }
        else
        {
            StopCoroutine(shaking);
        }

        shaking = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    /// <summary>흔들림을 즉시 멈추고 원위치로 되돌립니다.</summary>
    public void StopShake()
    {
        if (shaking != null)
        {
            StopCoroutine(shaking);
            shaking = null;
        }

        if (hasOrigin)
        {
            transform.localPosition = originLocalPos;
            hasOrigin = false;
        }
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;                     // 일시정지 중에도 흔들리게
            float damper = 1f - Mathf.Clamp01(t / duration); // 갈수록 약해짐
            float x = Random.Range(-1f, 1f) * magnitude * damper;
            float y = Random.Range(-1f, 1f) * magnitude * damper;
            transform.localPosition = originLocalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        transform.localPosition = originLocalPos;
        hasOrigin = false;
        shaking   = null;
    }

    // ★ 흔들리는 도중에 패널이 닫히면(SetActive(false)) 코루틴이 중단되면서
    //   오브젝트가 어긋난 위치에 영구히 남습니다. 여기서 원위치로 되돌립니다.
    private void OnDisable()
    {
        shaking = null;

        if (hasOrigin)
        {
            transform.localPosition = originLocalPos;
            hasOrigin = false;
        }
    }
}