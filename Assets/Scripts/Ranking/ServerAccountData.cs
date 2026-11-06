using System;
using UnityEngine;

/// <summary>계정 삭제 시 지금 세이브 주인의 서버 데이터를 뒤끝 탈퇴로 지움 (게스트는 올린 적 없어 바로 성공, 계정은 본인 로그인 필요).</summary>
// 순서: ① 여기(서버, 실패 가능) → ② AccountReset.TryWipe(로컬) — 실패할 수 있는 일을 먼저 해서 서버 기록만 남는 계정이 안 생기게
// IsDeleting 동안 RankingManager·ProfileSync 업로드를 막음 (지운 데이터가 되살아나지 않게)
public static class ServerAccountData
{
    public static bool IsDeleting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsDeleting = false;
    }

    /// <summary>지금 세이브 주인의 서버 데이터를 지우고 메인 스레드에서 onDone(성공, 실패 문구).</summary>
    public static void DeleteForCurrentAccount(Action<bool, string> onDone)
    {
        if (IsDeleting) return;

        string owner = SaveManager.ActiveAccountId;
        if (SaveManager.IsGuestAccount(owner))
        {
            onDone?.Invoke(true, null);   // 게스트 — 서버에 올린 적 없음
            return;
        }

        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsReady)
        {
            onDone?.Invoke(false, GameMessages.Common.offlineCheck);
            return;
        }
        if (!auth.IsSignedIn || auth.Uid != owner)
        {
            // 본인으로 로그인해야만 지울 수 있음
            onDone?.Invoke(false, GameMessages.Account.serverDataMismatch);
            return;
        }

        IsDeleting = true;
        auth.DeleteAccount((ok, error) =>
        {
            // 성공이면 호출한 쪽이 같은 프레임에 TryWipe 로 저장을 잠그므로, 그 사이 업로드가 끼어들 틈이 없습니다.
            IsDeleting = false;
            onDone?.Invoke(ok, error);
        });
    }
}
