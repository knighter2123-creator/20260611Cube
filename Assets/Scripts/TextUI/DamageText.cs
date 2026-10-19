using System.Collections;
using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro tmp;

    private DamageTextData data;
    private Transform cameraTransform;

    private void Awake()
    {
        if (tmp == null)
            tmp = GetComponentInChildren<TextMeshPro>();
    }

    /// <summary>
    /// 데미지 텍스트 초기화 및 애니메이션 시작
    /// </summary>
    public void Initialize(int amount, DamageTextType type, DamageTextData textData, Transform cam)
    {
        data = textData;
        cameraTransform = cam;

        ApplyStyle(amount, type);

        // 좌우 뒤집기
        transform.localScale = new Vector3(-1f, 1f, 1f);

        StopAllCoroutines();
        StartCoroutine(AnimateAndReturn());
    }

    // ★ [프레임 드랍 수정] tmp.text = amount.ToString() 대신 tmp.SetText("{0}", amount) 를 씁니다.
    //   ToString / $"..." 는 맞을 때마다 새 문자열을 만듭니다. 각성으로 발사체가 늘면 초당 수십 개라 GC 가 자주 돕니다.
    //   SetText 의 {0} 서식은 TMP 가 숫자를 내부 버퍼에 직접 써서 문자열을 만들지 않습니다.
    //   (서식 문자열 자체는 리터럴이라 할당이 없습니다)
    private void ApplyStyle(int amount, DamageTextType type)
    {
        switch (type)
        {
            case DamageTextType.Normal:
                SetAmount("{0}", amount);
                tmp.color = data.normalColor;
                tmp.fontSize = data.normalFontSize;
                break;

            case DamageTextType.Critical:
                SetAmount("<b>{0}!</b>", amount);
                tmp.color = data.criticalColor;
                tmp.fontSize = data.criticalFontSize;
                break;

            case DamageTextType.Heal:
                SetAmount("+{0}", amount);
                tmp.color = data.healColor;
                tmp.fontSize = data.normalFontSize;
                break;

            case DamageTextType.DoT:
                SetAmount("{0}", amount);
                tmp.color = data.dotColor;
                tmp.fontSize = data.normalFontSize * 0.85f;
                break;
        }
    }

    /// <summary>
    /// float 가 정수를 정확히 담을 수 있는 한계 (2^24 = 16,777,216).
    /// TMP 의 SetText 는 인자를 float 로 받아서, 이보다 큰 데미지는 끝자리가 틀려집니다 (123,456,789 → 123,456,792).
    /// </summary>
    private const int ExactFloatLimit = 16_777_216;

    /// <summary>
    /// 데미지 숫자를 서식에 넣어 표시합니다.
    /// 평소에는 할당 없는 SetText, float 로 정확히 표현할 수 없는 큰 값만 문자열 서식으로 처리합니다.
    /// (서식은 SetText 와 string.Format 이 같은 "{0}" 규칙을 써서 그대로 넘길 수 있습니다)
    /// </summary>
    private void SetAmount(string format, int amount)
    {
        if (amount > -ExactFloatLimit && amount < ExactFloatLimit)
            tmp.SetText(format, amount);
        else
            tmp.text = string.Format(format, amount);
    }

    private IEnumerator AnimateAndReturn()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < data.lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / data.lifetime;

            // 위로 떠오르기
            float rise = data.riseCurve.Evaluate(t) * data.riseHeight;
            transform.position = startPos + Vector3.up * rise;

            // 페이드 아웃
            Color c = tmp.color;
            c.a = data.fadeCurve.Evaluate(t);
            tmp.color = c;

            // 카메라 방향으로 빌보드
            if (cameraTransform != null)
                transform.forward = cameraTransform.position - transform.position;

            yield return null;
        }

        // 오브젝트 풀로 반환
        DamageTextPool.Instance.Return(this);
    }
}

public enum DamageTextType
{
    Normal,
    Critical,
    Heal,
    DoT
}
