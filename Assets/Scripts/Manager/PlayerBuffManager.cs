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

    /// <summary>세이브에서 값을 읽어왔는가. CaptureTo 가드용.</summary>
    private bool loaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡지 않게 정리.
        // SaveManager.Save() 의 'Instance?.CaptureTo(d)' 에서 ?. 가 제대로 건너뛰게 하려면 필요하다.
        // (C#의 ?. 는 유니티의 == 오버로딩을 타지 않아 파괴된 객체도 통과시킨다)
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 모든 Awake 완료 후(= SaveManager가 파일 로드를 끝낸 시점)에 세이브에서 복원.
        // 실행 순서에 의존하지 않으려고 Awake가 아닌 Start에서 가져온다. ← 좋은 선택이다
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

        // ★ 추가 — Awake ~ Start 사이에는 DamageMultiplier 가 아직 초기값 1f 입니다.
        //   그 구간에 저장이 돌면(앱을 곧바로 백그라운드로 보내는 등)
        //   누적해둔 배율이 1.0 으로 덮어써집니다.
        //
        //   창은 한 프레임 남짓으로 좁지만, 날아가는 것이 영구 버프라 대가가 큽니다.
        //   여기서 return 하면 Save() 가 Current 를 재사용하는 병합 방식이라
        //   직전 저장값이 그대로 보존됩니다.
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