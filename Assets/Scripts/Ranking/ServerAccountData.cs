using System;
using UnityEngine;

/// <summary>
/// 계정 삭제 시 서버에 있는 이 계정의 데이터를 지웁니다 — 뒤끝 계정 탈퇴(AuthManager.DeleteAccount).
///   탈퇴하면 그 뒤끝 계정(gamer inDate)에 묶인 랭킹 · 닉네임 · 클라우드 세이브 · 친구 · 쪽지에 다시 닿을 수 없습니다
///   (같은 구글/이메일로 다시 로그인해도 새 계정이 만들어짐).
///
/// [호출 순서]  GameSettingManager.AccountReset 의 [삭제] →  ① 여기(서버)  →  ② AccountReset.TryWipe(로컬)
///   서버 삭제는 네트워크 때문에 실패할 수 있습니다. "실패할 수 있는 일을 먼저" 라는 AccountReset 의 원칙대로
///   서버를 먼저 지우고, 실패하면 로컬도 지우지 않습니다 (서버 기록만 남은 계정이 생기지 않게).
///
/// [어느 계정을 지우나]  지금 세이브의 주인 (SaveManager.ActiveAccountId)
///   - 게스트 세이브      → 서버에 올린 적이 없으므로 지울 것 없음 (바로 성공)
///   - 계정 세이브        → 그 계정으로 로그인돼 있어야 지울 수 있음
///
/// [지우는 동안 업로드 막기]  IsDeleting 동안 RankingManager / ProfileSync 가 올리지 않습니다 (ProfileSync.TryGetAccount).
///   성공하면 곧바로 TryWipe 가 저장을 잠가서 계속 막힙니다.
/// </summary>
public static class ServerAccountData
{
    public static bool IsDeleting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsDeleting = false;
    }

    /// <summary>
    /// 지금 세이브 주인의 서버 데이터를 지웁니다. 결과는 메인 스레드에서 onDone(성공, 실패 문구) 로 옵니다.
    /// </summary>
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
