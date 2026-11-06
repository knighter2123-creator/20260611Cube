using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>데미지 텍스트 풀 싱글톤 — Awake 에서 동적 폰트 아틀라스에 쓸 글자를 미리 넣고(첫 타격마다 아틀라스 재업로드로 끊김), 예열은 프레임마다 나눠 생성.</summary>
// [붙이는 위치] 씬에 하나, DamageTextData·Prefab 연결
public class DamageTextPool : MonoBehaviour
{
    public static DamageTextPool Instance { get; private set; }

    [Header("참조")]
    [SerializeField] private DamageText prefab;
    [SerializeField] private DamageTextData data;

    [Header("풀 설정")]
    [SerializeField] private int initialPoolSize = 20;

    [Tooltip("한 프레임에 미리 만들 개수 (시작 직후 프레임 드랍 방지)")]
    [SerializeField] private int prewarmPerFrame = 4;

    [Tooltip("글꼴이 동적 아틀라스면 시작할 때 이 글자들을 미리 넣어 둡니다. DamageText 서식(+, !)과 숫자.")]
    [SerializeField] private string warmCharacters = "0123456789+-!";

    private readonly Queue<DamageText> pool = new();
    private Camera mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        mainCam = Camera.main;
        WarmFontAtlas();
        StartCoroutine(PrewarmRoutine());
    }

    /// <summary>동적 글꼴이면 쓸 글자를 미리 아틀라스에 넣습니다 (클래스 주석 ① 참고). 정적 글꼴이면 할 일 없음.</summary>
    private void WarmFontAtlas()
    {
        if (prefab == null || string.IsNullOrEmpty(warmCharacters)) return;

        TextMeshPro text = prefab.GetComponentInChildren<TextMeshPro>(true);
        TMP_FontAsset font = text != null ? text.font : null;
        if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) return;

        if (!font.TryAddCharacters(warmCharacters, out string missing))
            Debug.LogWarning($"[DamageTextPool] 글꼴 '{font.name}' 에 없는 글자: '{missing}'");
    }

    private IEnumerator PrewarmRoutine()
    {
        int perFrame = Mathf.Max(1, prewarmPerFrame);
        int made = 0;
        while (made < initialPoolSize)
        {
            yield return null;   // 씬이 켜지는 프레임은 건너뜀 (그사이 부족하면 Spawn 이 하나씩 만듦)
            for (int i = 0; i < perFrame && made < initialPoolSize; i++, made++)
            {
                DamageText obj = Instantiate(prefab, transform);
                obj.gameObject.SetActive(false);
                pool.Enqueue(obj);
            }
        }
    }

    // ── Public API ──

    /// <summary>일반 데미지</summary>
    public void ShowDamage(Vector3 worldPos, int amount, bool isCritical = false)
    {
        Spawn(worldPos, amount, isCritical ? DamageTextType.Critical : DamageTextType.Normal);
    }

    /// <summary>도트 데미지(독, 화상 등)</summary>
    public void ShowDoT(Vector3 worldPos, int amount)
    {
        Spawn(worldPos, amount, DamageTextType.DoT);
    }

    // ── Internal ──

    private void Spawn(Vector3 worldPos, int amount, DamageTextType type)
    {
        DamageText obj = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab, transform);

        // 머리 위 + 살짝 랜덤 X 오프셋
        float xOffset = Random.Range(-data.randomXRange, data.randomXRange);
        obj.transform.position = worldPos + data.spawnOffset + new Vector3(xOffset, 0f, 0f);

        obj.gameObject.SetActive(true);
        obj.Initialize(amount, type, data, mainCam.transform);
    }

    public void Return(DamageText obj)
    {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}
