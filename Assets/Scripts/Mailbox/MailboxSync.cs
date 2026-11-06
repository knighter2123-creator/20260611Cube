using UnityEngine;

/// <summary>서버 우편을 로그인 직후 한 번, 이후 refreshInterval 마다 받아 오고 로그아웃 시 비움 (RankingManager.Awake 가 자동으로 붙임).</summary>
public class MailboxSync : MonoBehaviour
{
    [Tooltip("서버 우편을 다시 받아 오는 간격(초). 랭킹 보상은 주 1회라 자주 받을 필요가 없습니다.")]
    [SerializeField] private float refreshInterval = 300f;

    private string syncedUid;
    private float  nextRefreshTime;

    private void Update()
    {
        bool ready = ProfileSync.TryGetAccount(out string uid)
                  && AuthManager.Instance != null && AuthManager.Instance.IsOnline
                  && SaveManager.Instance != null && SaveManager.Instance.IsGameplayStarted;

        if (!ready)
        {
            // 로그아웃·계정 전환 — 다른 계정의 우편이 보이지 않게
            if (syncedUid != null && !ProfileSync.TryGetAccount(out _)) { Mailbox.ClearServerMails(); syncedUid = null; }
            return;
        }

        if (uid != syncedUid) { syncedUid = uid; nextRefreshTime = 0f; }
        if (Time.unscaledTime < nextRefreshTime) return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Mailbox.Refresh();
    }
}
