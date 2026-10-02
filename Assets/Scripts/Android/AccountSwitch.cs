using UnityEngine;

/// <summary>
/// 게임 시작 직전에 "지금 로그인한 계정" 과 "지금 읽고 있는 세이브의 주인" 을 맞춥니다.
/// Login_Name.StartGame() 이 씬 이동 직전에 PrepareForStart() 를 부릅니다.
/// (게스트 시작 / 구글 로그인 성공 / 이메일 로그인 성공 — 세 경로 모두 Login_Name.StartGame 으로 모이므로 한 곳에서 처리)
///
/// [세 가지 경우]
///   ① 같은 계정 (평소 — 앱을 다시 켜서 같은 계정으로 자동 로그인)
///        → 아무것도 안 함. SaveManager 가 시작할 때 이미 이 계정 파일을 읽었음 (마지막 계정을 기억하므로)
///
///   ② 게스트 → 이 기기에서 처음 쓰는 계정  ★ 방식 A: 게스트 진행을 계정으로 옮기기
///        → save.json 을 save_<UID>.json 으로 "이름만 바꿈" (+ 증강 PlayerPrefs 키도 이동)
///        → 메모리의 진행(매니저 값)과 파일 내용이 똑같으므로 재시작 없이 그대로 시작
///        → 게스트 파일은 사라지므로, 다른 계정이 같은 진행을 또 가져갈 수 없음
///
///   ③ 그 밖의 전환 (A 계정 → B 계정, 계정 → 게스트(로그아웃 후 시작), 이미 이 기기에 세이브가 있는 계정)
///        → 지금 계정 진행 저장 → 저장 잠금 → 사용 계정 변경 → 매니저 전부 새로 만들고 LoginScene 다시 로드
///          (계정 삭제의 RestartFromLogin 을 그대로 재사용) → 새 LoginScene 에서 자동으로 게임 시작
///        → 매니저들이 이전 계정의 레벨·골드를 메모리에 들고 있기 때문에, 파일만 바꿔 읽으면 안 됩니다.
///
/// [계정을 아직 모르면]
///   Firebase 가 준비 전이거나(앱을 켜자마자 시작) 로그인 처리 중이면 NotReady 를 돌려줍니다.
///   Login_Name 이 잠깐(기본 3초) 기다렸다가 다시 묻고, 그래도 모르면(오프라인·Firebase 오류) "지금 계정 그대로" 시작합니다.
///   (바로 "지금 계정 그대로" 시작하면, A 가 로그아웃하고 앱을 끈 기기에서 다음 사람이
///    켜자마자 시작을 누를 때 A 의 진행으로 들어가 A 파일에 저장되는 틈이 생깁니다)
/// </summary>
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

    /// <summary>
    /// 게임 시작 직전에 부릅니다. 결과에 따라 호출한 쪽이 시작 / 대기 / 중단을 정합니다 (StartResult 참고).
    /// </summary>
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

        Debug.Log($"[AccountSwitch] 계정 변경 감지: {Label(current)} → {Label(target)}");

        // 지금 계정의 진행을 먼저 파일에 확정 (로그인 화면으로 돌아온 경우 마지막 플레이분)
        // 게임 씬에 들어간 적이 없으면 Save() 가 알아서 무시합니다 (매니저 ApplyFrom 전 — '게임 시작 전 저장 차단')
        if (sm.IsGameplayStarted) sm.Save();

        // ② 게스트 → 이 기기에 세이브가 없는 계정: 게스트 진행을 옮기고 그대로 시작
        if (SaveManager.IsGuestAccount(current) && !SaveManager.SaveFileExists(target))
        {
            if (SaveManager.TryMoveSaveFile(current, target, out string moveError))
            {
                AugmentManager.MoveSavesBetweenAccounts(current, target);
                SaveManager.SetActiveAccount(target);
                Debug.Log("[AccountSwitch] 게스트 진행을 이 계정으로 옮겼습니다 (재시작 없음 — 메모리와 파일 내용이 같음).");
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

    /// <summary>
    /// 새 LoginScene 의 Login_Name.Start 가 부릅니다. true 면 바로 StartGame() 을 이어서 부르면 됩니다.
    /// (유저는 이미 '시작' 을 눌렀으므로 한 번 더 누르게 하지 않음)
    /// </summary>
    public static bool ConsumeAutoStartAfterReload()
    {
        if (!autoStartAfterReload) return false;
        autoStartAfterReload = false;
        skipCheckOnce = true;   // 이어지는 StartGame 의 PrepareForStart 는 검사 없이 통과
        return true;
    }

    /// <summary>
    /// 지금 로그인 상태로 판단한 "써야 할 계정". 판단할 수 없으면 false.
    ///   로그인됨 → UID / 로그인 안 됨 → "" (게스트)
    ///   ⚠ 로그인돼 있으면 '게스트로 시작하기' 버튼으로 시작해도 그 계정으로 시작합니다 (로그인 상태가 기준).
    /// </summary>
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