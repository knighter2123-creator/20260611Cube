using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaUIManager : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject gachaPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("뽑기 버튼")]
    [SerializeField] private Button draw1Button;
    [SerializeField] private Button draw10Button;
    [SerializeField] private Button draw100Button;

    [Header("이동 버튼")]
    [SerializeField] private Button closeResultButton;

    [Header("뽑기 결과")]
    [SerializeField] private Transform  resultContent;
    [SerializeField] private GameObject resultItemPrefab;

    [Header("등장 연출")]
    [Tooltip("아이템 간 등장 간격(초)")]
    [SerializeField] private float perItemDelay = 0.08f;
    [Tooltip("전체 등장에 쓸 최대 시간(초). 100연차처럼 많으면 간격을 자동 축소")]
    [SerializeField] private float maxTotalRevealTime = 2.5f;
    [Tooltip("등장 이펙트 Overlay 호환 스프라이트 애니메이션")]
    [SerializeField] private UISpriteAnimation effectPrefab;
    [Tooltip("이펙트를 담을 UI 부모. ★반드시 씬(Hierarchy)의 오브젝트를 넣을 것. 프리팹 애셋 X")]
    [SerializeField] private RectTransform effectLayer;
    [Tooltip("이펙트가 자동으로 사라지지 않을 때를 대비한 강제 정리 시간(0이면 정리 안 함)")]
    [SerializeField] private float effectLifetime = 1.5f;

    [Header("화면 흔들림")]
    [SerializeField] private ScreenShake screenShake;
    [Tooltip("이 등급 이상일 때만 화면 흔들림 (enum 선언 순서 기준)")]
    [SerializeField] private CompanionGrade shakeThreshold = CompanionGrade.Epic;

    private Coroutine revealing;
    private bool effectLayerValid;

    void Start()
    {
        ValidateEffectLayer();

        draw1Button.onClick.AddListener(()   => OnDraw(GachaSystem.Instance.DrawOne()));
        draw10Button.onClick.AddListener(()  => OnDraw(GachaSystem.Instance.DrawTen()));
        draw100Button.onClick.AddListener(() => OnDraw(GachaSystem.Instance.DrawHundred()));

        closeResultButton.onClick.AddListener(() => ShowGachaPanel());

        ShowGachaPanel();
    }

    /// <summary>
    /// effectLayer가 씬 오브젝트인지 검사.
    /// 프리팹 애셋을 인스펙터에 넣으면 Instantiate(prefab, parent)가
    /// "Cannot instantiate objects with a parent which is persistent" 경고를 내고
    /// 부모 없이 씬 루트에 생성됩니다.
    /// </summary>
    private void ValidateEffectLayer()
    {
        effectLayerValid = false;

        if (effectLayer == null)
        {
            Debug.LogWarning("[GachaUIManager] effectLayer가 비어 있어 등장 이펙트를 건너뜁니다.", this);
            return;
        }

        // 프리팹 애셋은 씬에 속해 있지 않음 → scene.IsValid()가 false
        if (!effectLayer.gameObject.scene.IsValid())
        {
            Debug.LogError(
                $"[GachaUIManager] effectLayer('{effectLayer.name}')에 프리팹 애셋이 들어가 있습니다. " +
                "Project 창이 아니라 Hierarchy(씬)에 있는 Canvas 하위 오브젝트를 드래그해 넣으세요.", this);
            return;
        }

        effectLayerValid = true;
    }

    private void ShowGachaPanel()
    {
        // 연출 도중 닫으면 코루틴 정리
        if (revealing != null) { StopCoroutine(revealing); revealing = null; }
        ClearEffects();
        gachaPanel.SetActive(true);
        resultPanel.SetActive(false);
    }

    private void ShowResultPanel()
    {
        gachaPanel.SetActive(false);
        resultPanel.SetActive(true);
    }

    private void OnDraw(List<GachaSystem.GachaResult> results)
    {
        if (results.Count == 0) return;   // 보석 부족 등
        if (revealing != null)  return;   // 연출 중 중복 실행 방지

        // Destroy는 프레임 끝에 처리되므로, 새 아이템이 붙기 전에 계층에서 즉시 떼어냄
        foreach (Transform child in resultContent)
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        ClearEffects();

        ShowResultPanel();
        revealing = StartCoroutine(RevealRoutine(results));
    }

    // ✅ 아이템을 하나씩 순서대로 공개
    private IEnumerator RevealRoutine(List<GachaSystem.GachaResult> results)
    {
        // 개수가 많으면 간격을 줄여 전체 시간이 maxTotalRevealTime을 넘지 않게
        float delay = perItemDelay;
        if (results.Count * delay > maxTotalRevealTime)
            delay = maxTotalRevealTime / results.Count;

        foreach (GachaSystem.GachaResult result in results)
        {
            GameObject item = Instantiate(resultItemPrefab, resultContent);
            GachaResultItem ui = item.GetComponent<GachaResultItem>();
            if (ui == null) continue;

            ui.Setup(result);

            SpawnEffect(item.transform);   // ✅ 파티클 대신 스프라이트 애니메이션
            ui.PlayAppear();

            if (screenShake != null && result.data.grade >= shakeThreshold)
                screenShake.Shake();

            yield return new WaitForSecondsRealtime(delay);
        }

        revealing = null;
    }

    private void SpawnEffect(Transform itemTransform)
    {
        if (effectPrefab == null || !effectLayerValid) return;

        // Overlay 캔버스에서는 월드 position이 곧 스크린 픽셀 좌표 → 아이템 위치에 바로 배치 가능
        UISpriteAnimation fx = Instantiate(effectPrefab, effectLayer);
        fx.transform.position = itemTransform.position;
        fx.transform.localScale = Vector3.one;
        // playOnEnable이 true면 자동 재생. 아니면 fx.Play();

        if (effectLifetime > 0f)
            Destroy(fx.gameObject, effectLifetime);   // 100연차에서 이펙트가 쌓이는 것 방지
    }

    private void ClearEffects()
    {
        if (!effectLayerValid) return;

        for (int i = effectLayer.childCount - 1; i >= 0; i--)
            Destroy(effectLayer.GetChild(i).gameObject);
    }
}