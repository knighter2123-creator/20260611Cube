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

    public void Shake() => Shake(defaultDuration, defaultMagnitude);

    public void Shake(float duration, float magnitude)
    {
        // 흔들리는 중이 아닐 때만 원점 기록 (중첩 호출 시 원점 오염 방지)
        if (shaking == null) originLocalPos = transform.localPosition;
        else StopCoroutine(shaking);

        shaking = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;                    // 일시정지 중에도 흔들리게
            float damper = 1f - Mathf.Clamp01(t / duration); // 갈수록 약해짐
            float x = Random.Range(-1f, 1f) * magnitude * damper;
            float y = Random.Range(-1f, 1f) * magnitude * damper;
            transform.localPosition = originLocalPos + new Vector3(x, y, 0f);
            yield return null;
        }
        transform.localPosition = originLocalPos;
        shaking = null;
    }
}