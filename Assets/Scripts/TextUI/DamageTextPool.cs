using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데미지 텍스트 오브젝트 풀 싱글톤.
/// 씬에 하나만 배치하고 DamageTextData와 Prefab을 Inspector에서 연결하세요.
/// </summary>
public class DamageTextPool : MonoBehaviour
{
    public static DamageTextPool Instance { get; private set; }

    [Header("참조")]
    [SerializeField] private DamageText prefab;
    [SerializeField] private DamageTextData data;

    [Header("풀 설정")]
    [SerializeField] private int initialPoolSize = 20;

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
        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            DamageText obj = Instantiate(prefab, transform);
            obj.gameObject.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    // ───────────────────────────────────────────
    // Public API
    // ───────────────────────────────────────────

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

    /// <summary>힐</summary>
    public void ShowHeal(Vector3 worldPos, int amount)
    {
        Spawn(worldPos, amount, DamageTextType.Heal);
    }

    // ───────────────────────────────────────────
    // Internal
    // ───────────────────────────────────────────

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
