using UnityEngine;

/// <summary>게임 시작 직전(Login_Name.StartGame) 로그인 계정과 세이브 주인을 맞춤: 같으면 그대로, 게스트→새 계정이면 세이브 파일 이름만 바꿔 이어서, 그 밖은 AccountReset 재시작으로 매니저를 새로 만듦.</summary>
// 로그인 상태를 아직 모르면 NotReady → Login_Name 이 잠깐 기다렸다 다시 물음 (바로 시작하면 이전 사람 계정 파일에 저장되는 틈이 생김)
public static class AccountSwitch
{
    /// <summary>PrepareForStart 의 결과</summary>
    public enum StartResult
    {
        Proceed,    // 그대로 게임 시작
        Rebooting,  // 계정 전환 재시작을 시작함 — 씬 이동 금지 (재시작 후 자동 시작)
        NotReady,   // 로그인 상태를 아직 모름 — 잠깐 뒤 다시 물어볼 것 (부작용 없음, 몇 번 불러도 안전)
    }

    // 재시작 뒤 새 LoginScene 에서 자동으로 게임을 시작할지 (Login_Name.Start 가 확인)
    private static bool autoStartAfterReload;

    // 자동 시작 때 한 번은 전환 검사를 건너뜀 — 혹시라도 다시 전환이 걸려 재시작이 반복되는 일을 막는 안전장치
    private static bool skipCheckOnce;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        autoStartAfterReload = false;
        skipCheckOnce = false;
    }

    /// <summary>게임 시작 직전에 호출 — 결과(StartResult)에 따라 호출 쪽이 시작/대기/중단을 정함.</summary>
    public static StartResult PrepareForStart()
    {
        if (skipCheckOnce)
        {
            skipCheckOnce = false;
            return StartResult.Proceed;
        }

        SaveManager sm = SaveManager.Instance;
        if (sm == null) return StartResult.Proceed;   // 에디터에서 게임 씬 바로 실행 등 — 판단할 세이브가 없음

        // 로그인 시스템 자체가 없으면(씬에 AuthManager 없음) 기다릴 이유가 없음 → 게스트 규칙 없이 그대로
        if (AuthManager.Instance == null) return StartResult.Proceed;

        if (!TryGetSignedInAccount(out string target))
            return StartResult.NotReady;              // 아직 모름 → 호출한 쪽이 잠깐 기다렸다 다시 물음

        string current = SaveManager.ActiveAccountId;
        if (target == current) return StartResult.Proceed;   // ① 같은 계정

        GameLog.Info($"[AccountSwitch] 계정 변경 감지: {Label(current)} → {Label(target)}");

        // 지금 계정 진행을 먼저 파일에 확정 (게임 씬에 들어간 적 없으면 저장하지 않음 — '게임 시작 전 저장 차단')
        if (sm.IsGameplayStarted) sm.Save();

        // ② 게스트 → 이 기기에 세이브가 없는 계정: 게스트 진행을 옮기고 그대로 시작
        if (SaveManager.IsGuestAccount(current) && !SaveManager.SaveFileExists(target))
        {
            if (SaveManager.TryMoveSaveFile(current, target, out string moveError))
            {
                AugmentManager.MoveSavesBetweenAccounts(current, target);
                SaveManager.SetActiveAccount(target);
                GameLog.Info("[AccountSwitch] 게스트 진행을 이 계정으로 옮겼습니다 (재시작 없음 — 메모리와 파일 내용이 같음).");
                return StartResult.Proceed;
            }

            // 옮기기 실패(저장소 오류) → 계정을 바꾸지 않고 게스트로 계속. 진행은 그대로 안전하게 남음.
            Debug.LogError($"[AccountSwitch] 게스트 진행 이동 실패 — 이번에는 게스트 데이터로 시작합니다: {moveError}");
            return StartResult.Proceed;
        }

        // ③ 그 밖의 전환: 매니저를 새로 만들어야 함
        if (!AccountReset.TryBeginAccountSwitch(SceneLoader.LOGIN_SCENE, out string error))
        {
            // 재시작을 못 하면 계정을 바꾸지 않는 편이 안전 (바꾸면 이전 계정 값이 새 계정 파일에 저장됨)
            Debug.LogError($"[AccountSwitch] 계정 전환 재시작 실패 — 이전 계정 데이터로 시작합니다: {error}");
            return StartResult.Proceed;
        }

        // 순서 중요: 잠금(위) → 계정 변경 → 재시작. 잠금 전에 계정을 바꾸면 그 사이 Save() 가 섞일 수 있음
        SaveManager.SetActiveAccount(target);
        autoStartAfterReload = true;
        AccountReset.RestartFromLogin();
        return StartResult.Rebooting;
    }

    /// <summary>재시작 뒤 새 LoginScene 에서 게임을 자동으로 이어서 시작 (CloudSave 재시작도 쓰려고 공개).</summary>
    public static void RequestAutoStartAfterReload() => autoStartAfterReload = true;

    /// <summary>Login_Name.Start 가 호출 — true 면 바로 StartGame (유저가 이미 시작을 눌렀으므로 다시 누르게 하지 않음).</summary>
    public static bool ConsumeAutoStartAfterReload()
    {
        if (!autoStartAfterReload) return false;
        autoStartAfterReload = false;
        skipCheckOnce = true;   // 이어지는 StartGame 의 PrepareForStart 는 검사 없이 통과
        return true;
    }

    /// <summary>써야 할 계정(로그인=UID, 아니면 "" 게스트), 판단 불가면 false — ⚠ 로그인돼 있으면 '게스트로 시작'을 눌러도 그 계정.</summary>
    private static bool TryGetSignedInAccount(out string accountId)
    {
        accountId = string.Empty;

        AuthManager auth = AuthManager.Instance;   // Unity 오브젝트 → == null 로 검사
        if (auth == null || !auth.IsReady || auth.IsSigningIn)
            return false;

        accountId = auth.IsSignedIn ? (auth.Uid ?? string.Empty) : string.Empty;
        return true;
    }

    private static string Label(string accountId) =>
        SaveManager.IsGuestAccount(accountId) ? "게스트" : accountId.Substring(0, Mathf.Min(6, accountId.Length)) + "…";
}