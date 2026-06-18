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

    private void ApplyStyle(int amount, DamageTextType type)
    {
        switch (type)
        {
            case DamageTextType.Normal:
                tmp.text = amount.ToString();
                tmp.color = data.normalColor;
                tmp.fontSize = data.normalFontSize;
                break;

            case DamageTextType.Critical:
                tmp.text = $"<b>{amount}!</b>";
                tmp.color = data.criticalColor;
                tmp.fontSize = data.criticalFontSize;
                break;

            case DamageTextType.Heal:
                tmp.text = $"+{amount}";
                tmp.color = data.healColor;
                tmp.fontSize = data.normalFontSize;
                break;

            case DamageTextType.DoT:
                tmp.text = amount.ToString();
                tmp.color = data.dotColor;
                tmp.fontSize = data.normalFontSize * 0.85f;
                break;
        }
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
