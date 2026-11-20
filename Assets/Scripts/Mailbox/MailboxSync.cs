using UnityEngine;

/// <summary>서버 우편을 로그인 직후 한 번, 뒤끝 실시간 알림(새 우편)이 오면 바로, 그 밖엔 refreshInterval 마다 받아 오고 로그아웃 시 비움 (RankingManager.Awake 가 자동으로 붙임).</summary>
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

        // 뒤끝 실시간 알림(새 우편)이 왔으면 주기를 기다리지 않고 받음 (받는 중·받기 중이면 끝난 뒤)
        if (Mailbox.RefreshPending && !Mailbox.IsRefreshing && !Mailbox.IsClaiming)
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            Mailbox.Refresh();
            return;
        }

        if (Time.unscaledTime < nextRefreshTime) return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Mailbox.Refresh();
    }
}
