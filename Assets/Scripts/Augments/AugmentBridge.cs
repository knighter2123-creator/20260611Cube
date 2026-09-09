using UnityEngine;

/// <summary>
/// ★ 증강 시스템 ↔ 기존 게임 코드를 잇는 유일한 지점입니다.
///
/// [왜 이렇게 분리하나 — 어댑터 패턴]
/// 증강 카드가 CurrencyManager.Instance.AddGold(...) 를 직접 부르게 만들면,
/// 나중에 재화 시스템 이름이 바뀔 때 카드 스크립트를 전부 뒤져야 합니다.
/// 중간에 이 파일 하나를 두면 바꿀 곳이 여기 한 군데뿐입니다.
///
/// static 클래스라 씬에 오브젝트를 만들 필요가 없습니다.
/// </summary>
public static class AugmentBridge
{
    // ─────────────────────────────────────────────────────────
    //  1) 재화 지급
    // ─────────────────────────────────────────────────────────

    public static void AddGold(double amount)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;

        // ★ 현재 보유량을 기준으로 "더 담을 수 있는 만큼"만 넘깁니다. 이유는 아래 주석 참고.
        cm.AddGold(ClampToHeadroom(amount, cm.Gold));
    }

    public static void AddGem(double amount)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;

        cm.AddGem(ClampToHeadroom(amount, cm.Gem));
    }

    public static void AddExp(double amount)
    {
        // Enemy.GrantRewards() 가 쓰는 경로와 똑같이 맞췄습니다.
        // LevelUpManager 안에서 레벨업 연출(LevelUpEffect)까지 처리될 겁니다.
        LevelUpManager.Instance?.AddExp(ToSafeInt(amount));
    }

    // ─────────────────────────────────────────────────────────
    //  2) 현재 스테이지 배율
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// StageManager 의 누적 난이도 배율(1.5^n). 적 체력·보상에 곱해지는 그 값입니다.
    /// 보상 카드가 이걸 곱해 쓰기 때문에, 100스테이지에서도 "골드 지급" 카드가 쓸모 있습니다.
    /// </summary>
    public static double GetStageMultiplier()
    {
        if (StageManager.Instance == null) return 1.0;
        return StageManager.Instance.CurrentStatMult;
    }

    // ─────────────────────────────────────────────────────────
    //  유틸 — 정수 오버플로 방어
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// "지금 보유량에 더해도 int 를 넘지 않는 최대치"로 잘라냅니다.
    ///
    /// 【왜 단순 Clamp 로는 부족한가】
    /// CurrencyManager 의 골드·보석은 int 이고, 내부 계산은 이렇습니다.
    ///
    ///     gold = Mathf.Max(0, gold + amount);
    ///
    /// int + int 는 최대값(약 21억)을 넘으면 조용히 음수로 뒤집힙니다(오버플로).
    /// 예) gold = 20억, amount = 20억  →  합계가 음수  →  Mathf.Max(0, 음수) = 0
    ///     즉 골드를 얻었는데 전 재산이 0이 됩니다. 보석은 Max 도 없어서 음수로 남고요.
    ///
    /// 이 게임은 배율이 1.5^n 으로 자라기 때문에 도달 가능한 범위입니다.
    /// (55스테이지쯤 배율이 이미 int 한계를 넘습니다)
    /// 그래서 amount 만 자르는 게 아니라 "현재 보유량까지 고려해서" 잘라야 안전합니다.
    ///
    /// ※ 근본 해결은 재화 타입을 long 이나 double 로 바꾸는 것입니다.
    ///   Enemy.GrantRewards() 도 Mathf.RoundToInt 로 같은 한계를 갖고 있으니,
    ///   후반 밸런싱을 할 때 한 번에 정리하는 걸 권합니다.
    ///   그때까지는 이 함수가 "최소한 손해는 안 보게" 막아줍니다.
    /// </summary>
    private static int ClampToHeadroom(double amount, int current)
    {
        if (amount <= 0d) return 0;

        int headroom = int.MaxValue - Mathf.Max(0, current);
        if (headroom <= 0) return 0;

        return amount >= headroom ? headroom : (int)amount;
    }

    /// <summary>보유량을 알 수 없는 경우(경험치)의 단순 안전 변환.</summary>
    private static int ToSafeInt(double v)
    {
        if (v <= 0d) return 0;
        if (v >= int.MaxValue) return int.MaxValue;
        return (int)v;
    }
}