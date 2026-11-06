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

    /// <summary>데미지 텍스트 초기화 및 애니메이션 시작.</summary>
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

    // SetText("{0}", amount) — ToString 은 타격마다 문자열을 만들어 GC, SetText 는 내부 버퍼에 직접 씀
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

    /// <summary>float 가 정수를 정확히 담는 한계 2^24 — SetText 는 float 인자라 이보다 크면 끝자리가 틀어짐.</summary>
    private const int ExactFloatLimit = 16_777_216;

    /// <summary>데미지 표시 — 평소엔 할당 없는 SetText, 2^24 초과만 string.Format (같은 "{0}" 서식 공유).</summary>
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
