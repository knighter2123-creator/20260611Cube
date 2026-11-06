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

        // IsDeleting 을 먼저 켜 새 업로드를 막고(TryGetAccount 가 false), 이미 올리는 중인 클라우드 세이브는 끝날 때까지 기다린 뒤 탈퇴
        // — 안 기다리면 탈퇴와 엇갈린 업로드가 지운 기록을 되살리거나, 탈퇴 직후 실패로 재시도 대상에 남을 수 있음
        IsDeleting = true;

        CloudSave cloud = CloudSave.Instance;
        if (cloud != null && cloud.IsUploading)
        {
            GameLog.Info("[ServerAccountData] 클라우드 세이브 올리기가 끝나길 기다린 뒤 계정을 삭제합니다.");
            cloud.WhenUploadIdle(() => Withdraw(owner, onDone));
        }
        else
        {
            Withdraw(owner, onDone);
        }
    }

    private static void Withdraw(string owner, Action<bool, string> onDone)
    {
        // 기다리는 사이 로그아웃·계정 전환됐으면 지우지 않음
        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsSignedIn || auth.Uid != owner)
        {
            IsDeleting = false;
            onDone?.Invoke(false, GameMessages.Account.serverDataMismatch);
            return;
        }

        try
        {
            auth.DeleteAccount((ok, error) =>
            {
                // 성공이면 호출한 쪽이 같은 프레임에 TryWipe 로 저장을 잠그므로, 그 사이 업로드가 끼어들 틈이 없습니다.
                IsDeleting = false;
                onDone?.Invoke(ok, error);
            });
        }
        catch (Exception e)
        {
            // 동기 예외로 콜백이 영영 안 오면 IsDeleting 이 남아 업로드가 막히므로 여기서 풂
            Debug.LogError($"[ServerAccountData] 계정 삭제 요청 실패: {e}");
            IsDeleting = false;
            onDone?.Invoke(false, GameMessages.Account.deleteFailed);
        }
    }
}
