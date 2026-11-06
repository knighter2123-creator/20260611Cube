using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>플레이어 이름의 유일한 창구: 읽기는 누구나(Name·DisplayName), 쓰기는 최초 등록(무료)·유료 개명(보석)·서버 경유 요청으로만 — 캐시 없이 매번 SaveData.playerName 을 읽어 세이브를 지우면 이름도 사라짐.</summary>
public static class PlayerProfile
{
    // ───────── 규칙 (UI 도 이 상수를 참조 → 숫자가 한 곳에만 존재) ─────────
    public const int MIN_LENGTH = 2;
    public const int MAX_LENGTH = 6;   // 기존 PlayerName.cs 의 제한(6자)을 그대로 유지

    // 개명 1회 보석 비용 (UI 안내도 이 상수를 읽어 자동으로 맞음)
    public const int CHANGE_NAME_GEM_COST = 3000;

    // 예전 PlayerName.cs 가 쓰던 PlayerPrefs 키 (이관용으로만 사용)
    private const string LEGACY_PREFS_KEY = "PlayerName";

    // 허용 문자: 완성형 한글(가~힣), 영문, 숫자. 공백·특수문자·자음 단독(ㄱㄴ) 불가
    private static readonly Regex AllowedPattern = new Regex("^[가-힣a-zA-Z0-9]+$");

    /// <summary>이름이 처음 정해진 순간 한 번 호출됩니다. (UI 갱신용)</summary>
    public static event System.Action<string> OnNameRegistered;

    /// <summary>이름이 "변경"된 순간 호출됩니다 (TryChangeName 성공 시). UI 갱신용.</summary>
    public static event System.Action<string> OnNameChanged;

    /// <summary>Reload Domain 을 끈 에디터에서 지난 플레이의 파괴된 UI 구독이 남지 않게 비움.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnNameRegistered = null;
        OnNameChanged = null;
        IsNameRequestPending = false;
    }

    // ───────── 읽기 ─────────

    // SaveManager 가 없거나(에디터에서 MainScene 을 바로 실행) 아직 로드 전이면 null
    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    /// <summary>현재 이름. 없으면 빈 문자열. (setter 가 없으므로 외부에서 대입 불가)</summary>
    public static string Name => Data?.playerName ?? string.Empty;

    public static bool HasName => !string.IsNullOrEmpty(Name);

    // ───────── 익명 이름 (이름을 정하기 전 표시용) ─────────

    public const string ANONYMOUS_PREFIX = "익명";

    /// <summary>이름 전 표시용 "익명1234"(처음 읽을 때 한 번 만들어 저장) — playerName 에 넣으면 무료 최초 등록이 막혀서 별도 필드, 다른 유저와 겹쳐도 허용.</summary>
    public static string AnonymousName
    {
        get
        {
            SaveData data = Data;
            if (data == null) return ANONYMOUS_PREFIX;

            if (string.IsNullOrEmpty(data.anonymousName))
            {
                data.anonymousName = ANONYMOUS_PREFIX + Random.Range(1000, 10000);
                Persist();   // 기존 세이브가 있으면 바로 기록, 첫 실행이면 첫 Save() 때 함께 기록 (Persist 주석 참고)
                GameLog.Info($"[PlayerProfile] 익명 이름 생성: {data.anonymousName}");
            }
            return data.anonymousName;
        }
    }

    /// <summary>화면·랭킹에 보여 줄 이름. 정한 이름이 없으면 익명 이름.</summary>
    public static string DisplayName => HasName ? Name : AnonymousName;

    /// <summary>쓰기 창구 공통 — 저장 시스템이 준비됐는지 확인합니다.</summary>
    private static bool TryGetData(out SaveData data, out string error)
    {
        data  = Data;
        error = string.Empty;
        if (data != null) return true;

        error = "저장 시스템을 찾을 수 없습니다.";
        Debug.LogError("[PlayerProfile] SaveManager 가 없습니다. LoginScene 부터 실행하세요.");
        return false;
    }

    // ───────── 검사 ─────────

    /// <summary>입력값을 다듬고(앞뒤 공백 제거) 규칙(길이·한글/영문/숫자) 검사만 함.</summary>
    public static bool Validate(string raw, out string cleaned, out string error)
    {
        cleaned = (raw ?? string.Empty).Trim();
        error = string.Empty;

        if (cleaned.Length == 0)
            error = "이름을 입력해 주세요.";
        else if (cleaned.Length < MIN_LENGTH || cleaned.Length > MAX_LENGTH)
            error = $"이름은 {MIN_LENGTH}~{MAX_LENGTH}자로 입력해 주세요.";
        else if (!AllowedPattern.IsMatch(cleaned))
            error = "한글, 영문, 숫자만 사용할 수 있습니다.";

        if (error.Length > 0)
        {
            cleaned = string.Empty;
            return false;
        }
        return true;
    }

    // ───────── 쓰기 (딱 한 번만 성공) ─────────

    /// <summary>이름 최초 1회 등록(이미 있으면 실패 — 개명은 TryChangeName) — ⚠ 이 기기에만 저장(게스트용), 로그인 계정은 RequestRegister.</summary>
    public static bool TryRegister(string raw, out string error)
    {
        if (!CheckRegister(raw, out SaveData data, out string cleaned, out error))
            return false;

        CommitRegister(data, cleaned);
        return true;
    }

    /// <summary>등록 전 검사 (이름 없음 + 규칙). 통과하면 다듬은 이름.</summary>
    private static bool CheckRegister(string raw, out SaveData data, out string cleaned, out string error)
    {
        cleaned = string.Empty;
        if (!TryGetData(out data, out error))
            return false;

        if (HasName)
        {
            error = "이미 이름이 정해져 있어 변경할 수 없습니다.";
            Debug.LogWarning($"[PlayerProfile] 이름 변경 시도 거절 (현재: {data.playerName}, 요청: {raw})");
            return false;
        }

        return Validate(raw, out cleaned, out error);
    }

    private static void CommitRegister(SaveData data, string cleaned)
    {
        data.playerName    = cleaned;
        data.nameUpdatedAt = NowMs();

        RegisterSave();

        GameLog.Info($"[PlayerProfile] 이름 등록 완료: {cleaned}");
        OnNameRegistered?.Invoke(cleaned);
    }

    // ───────── 쓰기 (MainScene, 보석 소모 개명) ─────────

    /// <summary>유료 개명(이미 이름이 있어야 성공) — 전체 Save() 로 저장해 방금 줄어든 보석까지 함께 기록 / ⚠ 이 기기에만 저장(게스트용), 로그인 계정은 RequestChangeName.</summary>
    public static bool TryChangeName(string raw, out string error)
    {
        if (!CheckChange(raw, out SaveData data, out string cleaned, out bool same, out error))
            return false;
        if (same) return true;

        // ★ 검사를 모두 통과한 직후, 이름을 바꾸기 직전에 차감 (먼저 차감하면 실패 시 보석만 사라짐)
        if (!CurrencyManager.Instance.SpendGem(CHANGE_NAME_GEM_COST))
        {
            error = $"보석이 부족합니다. (필요: {CHANGE_NAME_GEM_COST})";
            return false;
        }

        CommitChange(data, cleaned);
        return true;
    }

    /// <summary>개명 전 검사(이름 있음·규칙·보석 보유 확인만) — same = 지금 이름과 같음(보석 없이 성공).</summary>
    private static bool CheckChange(string raw, out SaveData data, out string cleaned, out bool same, out string error)
    {
        cleaned = string.Empty;
        same    = false;
        if (!TryGetData(out data, out error))
            return false;

        if (!HasName)
        {
            // 정상 흐름에선 안 옴(이름이 없으면 패널이 최초 설정 모드) — 잘못된 호출 방어
            error = "등록된 이름이 없습니다. 먼저 이름을 설정해 주세요.";
            return false;
        }

        if (!Validate(raw, out cleaned, out error))
            return false;

        // 같은 이름이면 보석 없이 성공 (안 그러면 같은 이름을 다시 입력한 유저의 보석이 사라짐)
        if (cleaned == data.playerName)
        {
            same = true;
            return true;
        }

        if (CurrencyManager.Instance == null)
        {
            error = "재화 정보를 찾을 수 없습니다.";
            Debug.LogError("[PlayerProfile] CurrencyManager 가 없습니다.");
            return false;
        }
        if (CurrencyManager.Instance.Gem < CHANGE_NAME_GEM_COST)
        {
            error = $"보석이 부족합니다. (필요: {CHANGE_NAME_GEM_COST})";
            return false;
        }
        return true;
    }

    private static void CommitChange(SaveData data, string cleaned)
    {
        data.playerName    = cleaned;
        data.nameUpdatedAt = NowMs();
        SaveManager.Instance.Save();   // 위 설명대로 TryRegister 의 Persist() 가 아니라 전체 Save() 사용 (줄어든 보석도 함께 저장)

        GameLog.Info($"[PlayerProfile] 이름 변경 완료: {cleaned} (보석 -{CHANGE_NAME_GEM_COST})");
        OnNameChanged?.Invoke(cleaned);
    }

    // ───────── 쓰기 (서버를 거치는 창구 — NicknameChangePanel 이 씀) ─────────

    /// <summary>서버에 이름을 묻는 중인가 (확인 버튼 연타 방지).</summary>
    public static bool IsNameRequestPending { get; private set; }

    /// <summary>최초 등록(무료) — 로그인 계정은 뒤끝에 먼저 등록(중복 검사) 후 기기에 적용, 게스트는 TryRegister.</summary>
    public static void RequestRegister(string raw, System.Action<bool, string> onDone)
    {
        if (!ProfileSync.TryGetAccount(out string uid))
        {
            bool ok = TryRegister(raw, out string localError);
            onDone?.Invoke(ok, localError);
            return;
        }

        if (!CheckRegister(raw, out _, out string cleaned, out string error) || !CanAskServer(uid, out error))
        {
            onDone?.Invoke(false, error);
            return;
        }

        AskServer(uid, cleaned, onDone, () =>
        {
            if (!TryGetData(out SaveData data, out string e)) return e;
            if (HasName) return "이미 이름이 정해져 있습니다.";   // 기다리는 사이 다른 경로로 정해짐 (서버 이름은 ProfileSync 가 다시 맞춤)
            CommitRegister(data, cleaned);
            return null;
        });
    }

    /// <summary>유료 개명 — 로그인 계정은 뒤끝에서 바꾼 '뒤에' 보석 차감(거절되면 보석 그대로), 게스트는 TryChangeName.</summary>
    public static void RequestChangeName(string raw, System.Action<bool, string> onDone)
    {
        if (!ProfileSync.TryGetAccount(out string uid))
        {
            bool ok = TryChangeName(raw, out string localError);
            onDone?.Invoke(ok, localError);
            return;
        }

        if (!CheckChange(raw, out _, out string cleaned, out bool same, out string error))
        {
            onDone?.Invoke(false, error);
            return;
        }
        if (same)
        {
            onDone?.Invoke(true, string.Empty);
            return;
        }
        if (!CanAskServer(uid, out error))
        {
            onDone?.Invoke(false, error);
            return;
        }

        AskServer(uid, cleaned, onDone, () =>
        {
            if (!TryGetData(out SaveData data, out string e)) return e;

            // 서버 이름이 이미 바뀌어 반드시 맞춤 — 그 사이 보석이 모자라면 차감 없이 적용 (이름이 갈라지는 것보다 나음)
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGem(CHANGE_NAME_GEM_COST))
                Debug.LogWarning("[PlayerProfile] 서버 개명 후 보석이 모자라 차감하지 못했습니다 — 이름만 적용합니다.");

            CommitChange(data, cleaned);
            return null;
        });
    }

    private static bool CanAskServer(string uid, out string error)
    {
        error = null;
        if (IsNameRequestPending)
            error = "처리 중입니다. 잠시만 기다려 주세요.";
        else if (ProfileSync.Instance == null)
            error = "닉네임 서버를 찾을 수 없습니다.";
        else
            ProfileSync.Instance.CanSetName(uid, out error);
        return error == null;
    }

    /// <summary>서버에 이름을 정하고, 성공하면 apply 로 이 기기에 적용합니다. apply 는 실패 문구(성공이면 null)를 돌려줍니다.</summary>
    private static void AskServer(string uid, string cleaned, System.Action<bool, string> onDone, System.Func<string> apply)
    {
        IsNameRequestPending = true;
        ProfileSync.Instance.SetNameOnServer(uid, cleaned, result =>
        {
            IsNameRequestPending = false;

            if (!result.Ok)
            {
                onDone?.Invoke(false, result.Message);
                return;
            }

            // 기다리는 사이 계정이 바뀌었으면 이 기기(다른 계정 세이브)에 적용하지 않음
            if (!ProfileSync.TryGetAccount(out string nowUid) || nowUid != uid)
            {
                onDone?.Invoke(false, "계정이 바뀌어 적용하지 못했습니다.");
                return;
            }

            string error = apply();
            onDone?.Invoke(error == null, error ?? string.Empty);
        });
    }

    /// <summary>게스트 때 이름이 서버에서 이미 쓰이고 있을 때(ProfileSync 전용) — 이름을 지워 무료 최초 설정을 다시 하게.</summary>
    public static void ClearNameAfterServerConflict()
    {
        if (!TryGetData(out SaveData data, out _) || !HasName) return;

        string old = data.playerName;
        data.playerName    = string.Empty;
        data.nameUpdatedAt = NowMs();
        RegisterSave();

        GameLog.Info($"[PlayerProfile] 서버에서 쓸 수 없는 이름 '{old}' 을(를) 지웠습니다 — 다시 정해야 합니다.");
        OnNameChanged?.Invoke(AnonymousName);
    }

    // ───────── 서버 이름 적용 (ProfileSync 전용) ─────────

    /// <summary>이름을 마지막으로 정한 시각 (UTC ms). 서버 값과 어느 쪽이 최신인지 비교할 때 씁니다.</summary>
    public static long NameUpdatedAt => Data?.nameUpdatedAt ?? 0;

    /// <summary>다른 기기에서 정한 서버 이름을 적용(보석 없음 — 바꾼 기기에서 이미 냄), 규칙에 안 맞는 값은 무시.</summary>
    public static bool ApplyFromServer(string raw, long updatedAt)
    {
        if (!TryGetData(out SaveData data, out _)) return false;

        if (!Validate(raw, out string cleaned, out string error))
        {
            Debug.LogWarning($"[PlayerProfile] 서버 이름 '{raw}' 이(가) 규칙에 맞지 않아 무시합니다: {error}");
            return false;
        }

        bool hadName = HasName;
        data.playerName    = cleaned;
        data.nameUpdatedAt = updatedAt;

        RegisterSave();   // 게임 씬 진입 전(LoginScene)에 불릴 수 있으므로 등록과 같은 저장 경로

        GameLog.Info($"[PlayerProfile] 서버 이름 적용: {cleaned}");
        if (hadName) OnNameChanged?.Invoke(cleaned);
        else         OnNameRegistered?.Invoke(cleaned);
        return true;
    }

    private static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ───────── 파일 기록 ─────────

    /// <summary>이름 등록 저장 — 게임 씬 진입 후면 전체 Save(), 전이면 Persist() (Save 는 매니저 초기값으로 덮거나 무시되므로); 판단은 SaveManager.IsGameplayStarted 한 곳 기준.</summary>
    private static void RegisterSave()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (sm.IsGameplayStarted)
            sm.Save();
        else
            Persist();
    }

    /// <summary>Current 의 이름만 파일에 확정(LoginScene 전용, CaptureTo 없이 — Save 는 매니저 기본값으로 덮음) — ⚠ 첫 실행이면 파일을 만들지 않음 (만들면 기본값 baseDamage 0 이 적용돼 공격력 0 버그, 이름은 첫 Save 때 함께 기록).</summary>
    private static void Persist()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (sm.HasSave())
            sm.WriteCurrentToDisk();   // 기존 유저: 디스크 내용 + 이름만 바뀐 상태로 기록
        else
            GameLog.Info("[PlayerProfile] 첫 실행 — 이름은 첫 저장 때 함께 기록됩니다.");
    }

    // ───────── 예전 PlayerPrefs 이름 이관 (1회성) ─────────

    /// <summary>옛 PlayerPrefs 이름을 SaveData 로 1회 이관(LoginScene 시작 시) — 규칙에 맞으면 가져오고, 키는 지움 (안 지우면 세이브 삭제 후 옛 이름이 부활).</summary>
    public static void MigrateLegacyIfNeeded()
    {
        if (!PlayerPrefs.HasKey(LEGACY_PREFS_KEY)) return;
        if (Data == null) return;   // SaveManager 가 준비될 때까지 키를 보존

        string legacy = PlayerPrefs.GetString(LEGACY_PREFS_KEY, string.Empty);

        if (!HasName && Validate(legacy, out string cleaned, out _))
        {
            Data.playerName = cleaned;
            Persist();
            GameLog.Info($"[PlayerProfile] 예전 이름 이관: {cleaned}");

            // 세이브 파일이 없으면 이름이 아직 메모리에만 있으므로 파일에 확정될 때까지 키를 남김 (다음 실행에 else 로 지워짐)
            if (!SaveManager.Instance.HasSave()) return;
        }
        else
        {
            GameLog.Info($"[PlayerProfile] 예전 이름 이관 안 함 (값: '{legacy}', 이미 이름 있음={HasName})");
        }

        PlayerPrefs.DeleteKey(LEGACY_PREFS_KEY);
        PlayerPrefs.Save();
    }

    // ───────── ★ [계정 삭제] ─────────

    /// <summary>계정 삭제 전용: 남아 있을 수 있는 옛 이름 키 삭제 — 안 지우면 이관 코드가 옛 이름을 다시 가져옴 (PlayerPrefs.Save 는 AccountReset 이 한 번에).</summary>
    public static void DeleteLegacyPrefsForReset()
    {
        PlayerPrefs.DeleteKey(LEGACY_PREFS_KEY);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>테스트 전용: 이름만 지움(다른 진행도 유지, 릴리스 빌드엔 없음).</summary>
    public static void DebugClear()
    {
        SaveData data = Data;
        if (data == null) return;
        data.playerName = string.Empty;
        Persist();
        Debug.Log("[PlayerProfile] (디버그) 이름 초기화");
    }
#endif
}