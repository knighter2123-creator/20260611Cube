using UnityEngine;

/// <summary>
/// 플레이어 영구 버프(누적 데미지 배율) 관리.
/// 영속성은 SaveManager(SaveData)에 위임 — 자체 PlayerPrefs 저장/로드는 제거함.
/// LoginScene에 두고 DontDestroyOnLoad로 세션 내내 유지하세요.
/// </summary>
public class PlayerBuffManager : MonoBehaviour
{
    public static PlayerBuffManager Instance { get; private set; }

    // 1.0 = 버프 없음.  AddPermanentDamageBuff(0.3) → 1.3 (+30%)
    public float DamageMultiplier { get; private set; } = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 모든 Awake 완료 후(= SaveManager가 파일 로드를 끝낸 시점)에 세이브에서 복원.
        // 실행 순서에 의존하지 않으려고 Awake가 아닌 Start에서 가져온다.
        if (SaveManager.Instance != null)
            ApplyFrom(SaveManager.Instance.Current);
    }

    public void AddPermanentDamageBuff(float percent)
    {
        DamageMultiplier += percent;
        SaveManager.Instance?.Save();   // 즉시 저장 (병합 방식이라 다른 데이터는 보존)
    }

    // ── 세이브 연동 ──────────────────────────────
    public void CaptureTo(SaveData d)
    {
        d.damageMultiplier = DamageMultiplier;
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;
        DamageMultiplier = d.damageMultiplier;   // 세이브 없으면 기본 SaveData의 1.0
    }
}