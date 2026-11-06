public partial class CurrencyManager
{
    /// <summary>세이브에서 값을 읽었는가 — Awake~Start 사이에 저장이 돌면 gold=0 이 기록되는데, int 0 은 '미준비'와 '진짜 0원'을 구분 못 해 플래그를 둠.</summary>
    private bool loaded;

    /// <summary>세이브에서 값을 읽어온 상태인가. (HUD가 성급히 0을 표시하지 않도록 판단용)</summary>
    public bool IsLoaded => loaded;

    /// <summary>현재 재화를 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        if (d == null) return;

        // 불러오기 전이면 안 씀 — Save 는 Current 를 재사용하는 병합 방식이라 직전 값이 보존됨
        if (!loaded) return;

        d.gold = gold;
        d.gem  = gem;
    }

    /// <summary>세이브 값을 현재 재화에 반영.</summary>
    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

        gold = d.gold;
        gem  = d.gem;

        // 첫 실행의 빈 SaveData 도 '진짜 0원'이므로 loaded 를 세움
        loaded = true;

        // UI 직접 호출 제거 — 이벤트만 발행하면 CurrencyHUD가 갱신함
        OnGoldChanged?.Invoke(gold);
        OnGemChanged?.Invoke(gem);
    }
}