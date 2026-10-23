using System;
using System.Threading.Tasks;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// 계정 삭제 시 서버(Firestore)에 있는 이 계정의 데이터 — 랭킹(rankings/{UID}) + 닉네임(profiles/{UID})
/// + 클라우드 세이브(saves/{UID}) — 를 지웁니다.
///
/// [호출 순서]  GameSettingManager.AccountReset 의 [삭제] →  ① 여기(서버)  →  ② AccountReset.TryWipe(로컬)
///   서버 삭제는 네트워크 때문에 실패할 수 있습니다. "실패할 수 있는 일을 먼저" 라는 AccountReset 의 원칙대로
///   서버를 먼저 지우고, 실패하면 로컬도 지우지 않습니다 (서버 기록만 남은 계정이 생기지 않게).
///
/// [어느 계정을 지우나]  지금 세이브의 주인 (SaveManager.ActiveAccountId)
///   - 게스트 세이브      → 서버에 올린 적이 없으므로 지울 것 없음 (바로 성공)
///   - 계정 세이브        → 그 계정으로 로그인돼 있어야 지울 수 있음 (보안 규칙: 본인 문서만)
///
/// [지우는 동안 업로드 막기]  IsDeleting 동안 RankingManager / ProfileSync 가 올리지 않습니다 (ProfileSync.TryGetAccount).
///   안 막으면 지운 직후 매니저가 문서를 다시 만들 수 있습니다. 성공하면 곧바로 TryWipe 가 저장을 잠가서 계속 막힙니다.
/// </summary>
public static class ServerAccountData
{
    /// <summary>서버 삭제를 기다리는 최대 시간. 오프라인이면 Firestore 는 응답 없이 계속 기다리므로 끊어 줍니다.</summary>
    private const float TIMEOUT_SECONDS = 10f;

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
            onDone?.Invoke(false, "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");
            return;
        }
        if (!auth.IsSignedIn || auth.Uid != owner)
        {
            // 보안 규칙상 본인으로 로그인해야만 지울 수 있음
            onDone?.Invoke(false, "로그인 정보가 맞지 않아 서버 기록을 지울 수 없습니다. 다시 로그인해 주세요.");
            return;
        }

        // 클라우드 세이브를 올리는 중이면 기다림 — 삭제 뒤에 그 트랜잭션이 끝나면 지운 세이브가 되살아남
        //   (IsDeleting 은 "새로" 올리는 것만 막고, 이미 시작한 올리기는 막지 못함)
        if (CloudSave.Instance != null && CloudSave.Instance.IsUploading)
        {
            onDone?.Invoke(false, "데이터를 저장하는 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }

        IsDeleting = true;

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference ranking = db.Collection(RankingManager.COLLECTION).Document(owner);
        DocumentReference profile = db.Collection(ProfileSync.COLLECTION).Document(owner);
        DocumentReference save    = db.Collection(CloudSave.COLLECTION).Document(owner);   // 클라우드 세이브도 함께 — 남으면 다음 로그인 때 되살아남

        try
        {
            // ① 서버에 닿는지 먼저 확인 (Source.Server — 오프라인이면 바로 실패)
            //   확인 없이 바로 지우면, 오프라인에서는 삭제가 기기에 '예약' 만 된 채 시간 초과로 실패 처리되고,
            //   나중에 온라인이 되는 순간 계속 플레이 중인 계정의 서버 기록이 지워집니다.
            profile.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(check =>
            {
                if (check.IsFaulted || check.IsCanceled)
                {
                    Debug.LogWarning($"[ServerAccountData] 서버 연결 확인 실패: {check.Exception}");
                    Finish(onDone, false, "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");
                    return;
                }

                // ② 세 문서를 한 번에 삭제 (배치 — 모두 지워지거나 하나도 안 지워짐)
                WriteBatch batch = db.StartBatch();
                batch.Delete(ranking);
                batch.Delete(profile);
                batch.Delete(save);
                Task commit = batch.CommitAsync();

                Task.WhenAny(commit, Task.Delay(TimeSpan.FromSeconds(TIMEOUT_SECONDS)))
                    .ContinueWithOnMainThread(done =>
                    {
                        if (done.Result != commit || commit.IsFaulted || commit.IsCanceled)
                        {
                            Debug.LogWarning($"[ServerAccountData] 서버 기록 삭제 실패/시간 초과: {commit.Exception}");

                            // 시간 초과면 삭제가 기기에 예약된 채 남아, 나중에 서버에 반영될 수 있습니다.
                            // 로컬은 지우지 않고 계속 플레이하므로 다시 올리도록 표시해 둡니다
                            // (Firestore 는 한 기기의 쓰기를 순서대로 반영 → 예약된 삭제 뒤에 다시 올라감).
                            RankingManager.Instance?.RequestUpload();
                            ProfileSync.Instance?.RequestPush();

                            Finish(onDone, false, "서버 기록을 지우지 못했습니다. 잠시 후 다시 시도해 주세요.");
                            return;
                        }

                        GameLog.Info($"[ServerAccountData] 서버 기록 삭제 완료 (랭킹 + 닉네임 + 클라우드 세이브): {SaveManager.ActiveAccountLabel}");
                        Finish(onDone, true, null);
                    });
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerAccountData] 삭제 호출 실패: {e}");
            Finish(onDone, false, "서버 기록을 지우지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }
    }

    private static void Finish(Action<bool, string> onDone, bool ok, string error)
    {
        // 성공이면 호출한 쪽이 같은 프레임에 TryWipe 로 저장을 잠그므로, 그 사이 업로드가 끼어들 틈이 없습니다.
        IsDeleting = false;
        onDone?.Invoke(ok, error);
    }
}
