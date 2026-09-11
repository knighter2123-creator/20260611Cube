public partial class CurrencyManager
{
    /// <summary>
    /// ApplyFrom 을 한 번이라도 받았는가 (= 세이브에서 값을 읽어왔는가).
    ///
    /// [왜 필요한가]
    ///   SaveManager.Save() 는 살아 있는 매니저의 CaptureTo 를 불러 Current 를 갱신합니다.
    ///   그런데 이 매니저는 Awake 에서 만들어지고 값은 Start 에서야 채워집니다.
    ///   그 사이에 저장이 돌면 gold=0, gem=0 이 그대로 디스크에 기록됩니다.
    ///
    ///   자동 저장이 OnApplicationPause / OnApplicationQuit 에 걸려 있어서,
    ///   앱 실행 직후 바로 백그라운드로 보내면 재현될 수 있는 경로입니다.
    ///
    ///   LevelUpManager 는 CaptureTo 에 'if (stat == null) return;' 가드가 있어 이 문제가 없습니다.
    ///   그쪽은 stat 참조 자체가 준비 신호 역할을 하지만,
    ///   여기는 int 필드라 0이 "미준비"인지 "진짜 0원"인지 구분되지 않습니다.
    ///   그래서 플래그를 따로 둡니다.
    /// </summary>
    private bool loaded;

    /// <summary>세이브에서 값을 읽어온 상태인가. (HUD가 성급히 0을 표시하지 않도록 판단용)</summary>
    public bool IsLoaded => loaded;

    /// <summary>현재 재화를 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        if (d == null) return;

        // ★ 아직 불러오기 전이면 아무것도 쓰지 않습니다.
        //   Save() 는 Current 를 재사용하는 병합 방식이라,
        //   여기서 그냥 return 하면 직전 저장값이 그대로 보존됩니다.
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

        // 첫 실행이라 세이브 파일이 없어도 SaveManager 는 빈 SaveData 를 넘겨줍니다.
        // 그 경우의 0 은 "진짜 0원"이므로 여기서 loaded 를 세우는 게 맞습니다.
        loaded = true;

        // UI 직접 호출 제거 — 이벤트만 발행하면 CurrencyHUD가 갱신함
        OnGoldChanged?.Invoke(gold);
        OnGemChanged?.Invoke(gem);
    }
}