using UnityEngine;

/// <summary>증강 시스템 ↔ 게임 코드의 유일한 연결점(재화·경험치 지급, 스테이지 배율) — 게임 쪽 이름이 바뀌어도 카드 대신 여기만 고치게 (static 이라 씬 오브젝트 불필요).</summary>
public static class AugmentBridge
{
    // ── 1) 재화 지급 ──

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
        // Enemy.GrantRewards 와 같은 경로(레벨업 연출 포함) — MonoBehaviour 라 ?. 대신 != null (파괴 검사)
        var lm = LevelUpManager.Instance;
        if (lm != null) lm.AddExp(ToSafeInt(amount));
    }

    // ── 2) 현재 스테이지 배율 ──

    /// <summary>적 체력·보상에 곱하는 누적 난이도 배율(1.5^n) — 보상 카드가 곱해 써서 후반에도 쓸모 있게.</summary>
    public static double GetStageMultiplier()
    {
        var sm = StageManager.Instance;   // 한 번만 읽어 두면 검사와 사용 사이에 값이 달라질 일이 없습니다
        if (sm == null) return 1.0;
        return sm.CurrentStatMult;
    }

    // ── 유틸 — 정수 오버플로 방어 ──

    /// <summary>보유량에 더해도 int 를 넘지 않는 만큼으로 자름 — 배율 1.5^n 이라 55스테이지쯤 int 를 넘어서 (근본 해결은 재화를 long/double 로).</summary>
    private static int ClampToHeadroom(double amount, int current)
    {
        // NaN 방어 — 배율 Infinity × 0 = NaN 은 모든 비교를 빠져나가고 (int)NaN = int.MinValue 라 골드가 0 이 됨
        if (double.IsNaN(amount) || amount <= 0d) return 0;

        int headroom = int.MaxValue - Mathf.Max(0, current);
        if (headroom <= 0) return 0;

        return amount >= headroom ? headroom : (int)amount;
    }

    /// <summary>보유량을 알 수 없는 경우(경험치)의 단순 안전 변환.</summary>
    private static int ToSafeInt(double v)
    {
        if (double.IsNaN(v) || v <= 0d) return 0;   // NaN 이유는 ClampToHeadroom 참고
        if (v >= int.MaxValue) return int.MaxValue;
        return (int)v;
    }
}