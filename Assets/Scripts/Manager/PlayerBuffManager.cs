using UnityEngine;

/// <summary>플레이어 영구 버프(누적 데미지 배율) 관리 — 저장은 SaveData 에 위임, LoginScene 에 두고 DontDestroyOnLoad.</summary>
public class PlayerBuffManager : MonoBehaviour
{
    public static PlayerBuffManager Instance { get; private set; }

    // 1.0 = 버프 없음.  AddPermanentDamageBuff(0.3) → 1.3 (+30%)
    public float DamageMultiplier { get; private set; } = 1f;

    /// <summary>세이브에서 값을 읽어왔는가. CaptureTo 가드용.</summary>
    private bool loaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        // Instance 정리 — SaveManager 의 Instance?.CaptureTo 가 파괴된 객체를 통과시키지 않게
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 모든 Awake 후(SaveManager 로드 완료)에 복원 — 실행 순서에 의존하지 않으려고 Start
        if (SaveManager.Instance != null)
            ApplyFrom(SaveManager.Instance.Current);
        else
            Debug.LogError("[PlayerBuff] SaveManager 가 없어 버프를 복원하지 못했습니다.");
    }

    public void AddPermanentDamageBuff(float percent)
    {
        DamageMultiplier += percent;
        SaveManager.Instance?.Save();   // 즉시 저장 (병합 방식이라 다른 데이터는 보존)
    }

    // ── 세이브 연동 ──────────────────────────────
    public void CaptureTo(SaveData d)
    {
        if (d == null) return;

        // 복원 전(Awake~Start)엔 쓰지 않음 — 그때 저장이 돌면 영구 배율이 1.0 으로 덮임 (병합 방식이라 직전 값 보존)
        if (!loaded) return;

        d.damageMultiplier = DamageMultiplier;
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

        DamageMultiplier = d.damageMultiplier;   // 세이브 없으면 기본 SaveData의 1.0
        loaded = true;
    }
}