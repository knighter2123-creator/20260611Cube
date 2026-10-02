using System;
using UnityEngine;

/// <summary>
/// 소환권 — 동료 소환에 보석 대신 쓰는 재화.
///
/// [얻는 법]
///   · 상점에서 보석으로 구매 (상점 상품 에셋: 비용 Gem 300 → 보상 GachaTicket 1)
///   · 일반 스테이지 보스 처치 시 1장 (BossMonster.GrantRewards)
/// [쓰는 법]
///   · GachaSystem 이 소환할 때 소환권을 먼저 쓰고, 모자라면 보석으로 결제합니다.
///
/// [저장 — 매니저 오브젝트가 없는 이유]
///   값은 SaveData.gachaTicket 한 칸뿐이고, SaveManager.Current 에 직접 읽고 씁니다.
///   SaveManager.Save() 는 Current 를 바탕으로 매니저 값을 덧씌우므로 이 칸은 그대로 파일까지 갑니다
///   (PlayerProfile 의 이름과 같은 방식).
///     → 씬에 오브젝트를 둘 필요가 없고(StageScene 에서 지급, ShopScene 에서 사용),
///     → 계정 삭제·전환으로 세이브가 바뀌면 소환권도 자연스럽게 따라 바뀝니다 (따로 들고 있는 사본이 없음).
///   저장 시점은 부른 쪽이 정합니다 — 보스 처치는 스테이지 클리어 저장, 구매는 ShopManager, 소환은 GachaSystem 이 저장합니다.
///
/// [다른 재화와 함께 쓰기]
///   CurrencyType.GachaTicket 으로 CurrencyManager.AddCurrency / TrySpendCurrency 를 거쳐도 여기로 옵니다.
///   그래서 상점 상품·가이드 퀘스트 보상처럼 '재화 종류' 로 지급하는 기존 시스템이 그대로 동작합니다.
/// </summary>
public static class GachaTicket
{
    /// <summary>보유 수가 바뀔 때마다 새 보유 수를 알립니다. (소환권 표시 UI 용)</summary>
    public static event Action<int> OnChanged;

    /// <summary>
    /// Enter Play Mode Options 에서 "Reload Domain" 을 끄면 static 이벤트에
    /// 이전 플레이의 (이미 파괴된) UI 가 구독된 채 남습니다. 플레이 시작 직전에 비웁니다.
    /// </summary>
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

        Debug.Log($"[GachaTicket] 소환권 +{amount} | 현재: {data.gachaTicket}");
        OnChanged?.Invoke(data.gachaTicket);
    }

    /// <summary>소환권이 amount 장 이상 있으면 차감하고 true. 모자라면 아무것도 바꾸지 않고 false.</summary>
    public static bool TrySpend(int amount)
    {
        SaveData data = Data;
        if (amount <= 0 || data == null || data.gachaTicket < amount) return false;

        data.gachaTicket -= amount;

        Debug.Log($"[GachaTicket] 소환권 -{amount} | 현재: {data.gachaTicket}");
        OnChanged?.Invoke(data.gachaTicket);
        return true;
    }
}
