using System;
using UnityEngine;

/// <summary>소환권(보석 대신 쓰는 소환 재화, 상점 구매·보스 처치로 획득) — 매니저 없이 SaveManager.Current 칸을 직접 읽고 써서 씬 오브젝트가 필요 없고 계정 전환도 자동으로 따라감, 저장은 부른 쪽이 함 (CurrencyType.GachaTicket 경로도 여기로).</summary>
public static class GachaTicket
{
    /// <summary>보유 수가 바뀔 때마다 새 보유 수를 알립니다. (소환권 표시 UI 용)</summary>
    public static event Action<int> OnChanged;

    /// <summary>플레이 시작 직전 static 이벤트 초기화 (Reload Domain 꺼짐 시 파괴된 UI 구독이 남아서).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    // SaveManager 가 없거나(에디터에서 게임 씬을 바로 실행) 아직 로드 전이면 null
    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    /// <summary>현재 보유 수. 세이브를 읽을 수 없으면 0.</summary>
    public static int Count => Data != null ? Data.gachaTicket : 0;

    /// <summary>소환권을 더합니다. 0 이하는 무시합니다. 결과는 int 최댓값에서 멈춥니다(넘침 방지).</summary>
    public static void Add(int amount)
    {
        if (amount <= 0) return;

        SaveData data = Data;
        if (data == null)
        {
            Debug.LogWarning($"[GachaTicket] SaveManager 가 없어 소환권 {amount}장을 지급하지 못했습니다.");
            return;
        }

        long next = (long)data.gachaTicket + amount;   // CurrencyManager.AddGold 와 같은 포화 덧셈
        data.gachaTicket = next > int.MaxValue ? int.MaxValue : (int)next;

        GameLog.Info($"[GachaTicket] 소환권 +{amount} | 현재: {data.gachaTicket}");
        OnChanged?.Invoke(data.gachaTicket);
    }

    /// <summary>소환권이 amount 장 이상 있으면 차감하고 true. 모자라면 아무것도 바꾸지 않고 false.</summary>
    public static bool TrySpend(int amount)
    {
        SaveData data = Data;
        if (amount <= 0 || data == null || data.gachaTicket < amount) return false;

        data.gachaTicket -= amount;

        GameLog.Info($"[GachaTicket] 소환권 -{amount} | 현재: {data.gachaTicket}");
        OnChanged?.Invoke(data.gachaTicket);
        return true;
    }
}
