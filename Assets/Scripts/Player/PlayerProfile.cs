using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 플레이어 이름을 읽고 "최초 1회" 등록하는 단 하나의 창구.
///
/// ★ 핵심 설계: "읽기는 누구나, 쓰기는 딱 한 번" — 단, 이제 예외가 하나 생겼습니다.
///   - Name / HasName  : 어느 씬에서든 읽을 수 있음
///   - TryRegister()   : 이름이 아직 없을 때만 성공 → 이후 호출은 전부 거절 (최초 등록, 무료)
///   - TryChangeName() : ★ 신규. 이미 이름이 있을 때만 성공 → 보석을 소모하는 "유료 개명"
///     (MainScene 네임플레이트 클릭 전용. LoginScene 의 최초 등록 흐름과는 별개의 창구입니다)
///
/// ★ 저장 위치: SaveData.playerName (save.json)
///   - SaveManager 는 LoginScene 의 Awake 에서 파일을 Current 로 읽어 두므로,
///     LoginScene 의 Start 시점에는 이미 이름 유무를 알 수 있습니다.
///   - 이 클래스는 값을 따로 들고 있지 않고(캐시 없음) 매번 SaveManager.Current 를 읽습니다.
///     → SaveManager.DeleteSave() 로 세이브를 지우면 이름도 자연스럽게 함께 사라집니다.
///       (이전 버전의 PlayerPrefs 방식은 세이브를 지워도 이름만 남는 문제가 있었음)
///
/// ★ [계정 삭제] 이번 변경: 파일 맨 아래 DeleteLegacyPrefsForReset() 하나만 추가. 기존 코드는 그대로입니다.
/// </summary>
public static class PlayerProfile
{
    // ───────── 규칙 (UI 도 이 상수를 참조 → 숫자가 한 곳에만 존재) ─────────
    public const int MIN_LENGTH = 2;
    public const int MAX_LENGTH = 6;   // 기존 PlayerName.cs 의 제한(6자)을 그대로 유지

    // ★ 신규: 개명(닉네임 변경) 1회당 소모되는 보석. 기획 수치가 정해지면 이 한 줄만 바꾸면 됩니다.
    //   (NicknameChangePanel 의 비용 안내 텍스트도 이 상수를 그대로 읽으므로 UI 표기가 자동으로 맞습니다)
    public const int CHANGE_NAME_GEM_COST = 3000;

    // 예전 PlayerName.cs 가 쓰던 PlayerPrefs 키 (이관용으로만 사용)
    private const string LEGACY_PREFS_KEY = "PlayerName";

    // 허용 문자: 완성형 한글(가~힣), 영문, 숫자. 공백·특수문자·자음 단독(ㄱㄴ) 불가
    private static readonly Regex AllowedPattern = new Regex("^[가-힣a-zA-Z0-9]+$");

    /// <summary>이름이 처음 정해진 순간 한 번 호출됩니다. (UI 갱신용)</summary>
    public static event System.Action<string> OnNameRegistered;

    /// <summary>★ 신규: 이름이 "변경"된 순간 호출됩니다 (TryChangeName 성공 시). UI 갱신용.</summary>
    public static event System.Action<string> OnNameChanged;

    /// <summary>
    /// Enter Play Mode Options 에서 "Reload Domain" 을 끄면 static 이벤트에
    /// 이전 플레이의 (이미 파괴된) UI 가 구독된 채 남습니다. 플레이 시작 직전에 비웁니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnNameRegistered = null;
        OnNameChanged = null;   // ★ 신규 이벤트도 같은 이유로 비워야 합니다
    }

    // ───────── 읽기 ─────────

    // SaveManager 가 없거나(에디터에서 MainScene 을 바로 실행) 아직 로드 전이면 null
    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    /// <summary>현재 이름. 없으면 빈 문자열. (setter 가 없으므로 외부에서 대입 불가)</summary>
    public static string Name => Data?.playerName ?? string.Empty;

    public static bool HasName => !string.IsNullOrEmpty(Name);

    // ───────── 검사 ─────────

    /// <summary>
    /// 입력값을 다듬고(앞뒤 공백 제거) 규칙에 맞는지 검사합니다. 등록은 하지 않습니다.
    /// </summary>
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

    /// <summary>
    /// 이름을 최초 1회 등록합니다. (파일 기록 시점은 아래 Persist() 참고)
    /// 이미 이름이 있으면 무조건 실패 → "MainScene 에서 임의로 못 바꾸게" 하는 장치.
    /// (MainScene 에서 이름을 바꾸는 새 창구는 아래 TryChangeName 입니다)
    /// </summary>
    public static bool TryRegister(string raw, out string error)
    {
        SaveData data = Data;
        if (data == null)
        {
            error = "저장 시스템을 찾을 수 없습니다.";
            Debug.LogError("[PlayerProfile] SaveManager 가 없습니다. LoginScene 부터 실행하세요.");
            return false;
        }

        if (HasName)
        {
            error = "이미 이름이 정해져 있어 변경할 수 없습니다.";
            Debug.LogWarning($"[PlayerProfile] 이름 변경 시도 거절 (현재: {data.playerName}, 요청: {raw})");
            return false;
        }

        if (!Validate(raw, out string cleaned, out error))
            return false;

        data.playerName = cleaned;

        Persist();

        Debug.Log($"[PlayerProfile] 이름 등록 완료: {cleaned}");
        OnNameRegistered?.Invoke(cleaned);
        return true;
    }

    // ───────── 쓰기 (MainScene, 보석 소모 개명) ─────────

    /// <summary>
    /// ★ 신규. MainScene 네임플레이트 클릭 → NicknameChangePanel 에서 호출되는 "유료 개명".
    /// TryRegister 와 정반대 조건입니다: 이미 이름이 있어야만 성공합니다.
    ///
    /// [TryRegister 와 저장 경로(Persist)를 공유하지 않는 이유]
    ///   TryRegister → Persist() 는 LoginScene 의 매니저 초기화 타이밍 문제(아래 Persist() 주석 참고)를
    ///   피하려고 SaveManager.Save() 대신 WriteCurrentToDisk() 를 직접 씁니다.
    ///   이 메서드는 반드시 MainScene 에서만 호출되므로 그 타이밍 문제가 없고, 오히려
    ///   SaveManager.Save() 를 써야 방금 SpendGem() 으로 줄어든 보석까지
    ///   (CurrencyManager.CaptureTo 를 통해) 같은 저장에 함께 기록됩니다.
    ///   WriteCurrentToDisk() 는 Current 를 그대로 파일에 쓸 뿐, 살아있는 매니저들의 최신 값을
    ///   Current 로 모아오지 않으므로 여기서 쓰면 보석 차감이 저장에서 누락될 수 있습니다.
    ///   (다른 MainScene 코드, 예: CompanionManager.AddCompanion 도 값 변경 후 SaveManager.Save() 를
    ///    쓰는 것과 같은 이유 — 동료도감 셋업 문서 §9 참고)
    /// </summary>
    public static bool TryChangeName(string raw, out string error)
    {
        SaveData data = Data;
        if (data == null)
        {
            error = "저장 시스템을 찾을 수 없습니다.";
            Debug.LogError("[PlayerProfile] SaveManager 가 없습니다.");
            return false;
        }

        if (!HasName)
        {
            // 정상 플로우라면 MainScene 에 도달한 시점엔 이미 이름이 있어야 합니다.
            // (에디터에서 MainScene 을 바로 실행한 경우 등을 방어)
            error = "등록된 이름이 없습니다. 먼저 이름을 설정해 주세요.";
            return false;
        }

        if (!Validate(raw, out string cleaned, out error))
            return false;

        // 지금 이름과 똑같으면 보석을 쓰지 않고 조용히 성공 처리합니다.
        // 이 검사가 없으면 오타로 같은 이름을 다시 입력한 유저의 보석이 이유 없이 사라집니다.
        if (cleaned == data.playerName)
        {
            error = string.Empty;
            return true;
        }

        if (CurrencyManager.Instance == null)
        {
            error = "재화 정보를 찾을 수 없습니다.";
            Debug.LogError("[PlayerProfile] CurrencyManager 가 없습니다.");
            return false;
        }

        // ★ 순서가 중요합니다: 유효성 검사를 전부 통과한 "직후", 실제로 이름을 바꾸기 "직전"에 소모합니다.
        //   먼저 차감부터 하고 나중에 실패할 수 있는 순서로 짜면(예: GachaSystem.Draw 가 먼저 보석을
        //   차감한 뒤 결과가 없으면 그냥 넘어가 버리는 기존 버그 — 동료도감 셋업 문서 §9 기록),
        //   실패 시 보석만 사라지는 사고가 납니다.
        if (!CurrencyManager.Instance.SpendGem(CHANGE_NAME_GEM_COST))
        {
            error = $"보석이 부족합니다. (필요: {CHANGE_NAME_GEM_COST})";
            return false;
        }

        data.playerName = cleaned;
        SaveManager.Instance.Save();   // 위 설명대로 TryRegister 의 Persist() 가 아니라 전체 Save() 사용

        Debug.Log($"[PlayerProfile] 이름 변경 완료: {cleaned} (보석 -{CHANGE_NAME_GEM_COST})");
        OnNameChanged?.Invoke(cleaned);
        return true;
    }

    // ───────── 파일 기록 ─────────

    /// <summary>
    /// Current 에 넣은 이름을 파일에 확정합니다. (TryRegister 전용 — TryChangeName 은 SaveManager.Save() 사용)
    ///
    /// ★ SaveManager.Save() 를 쓰지 않는 이유
    ///   Save() 는 살아있는 모든 매니저의 CaptureTo() 를 먼저 호출합니다.
    ///   LoginScene 시점에는 매니저들이 아직 ApplyFrom() 으로 세이브를 받기 전일 수 있어서,
    ///   그때 Save() 를 부르면 "기본값(레벨 1, 골드 0...)" 이 저장 파일을 덮어쓸 위험이 있습니다.
    ///
    /// ★ 첫 실행(세이브 파일 없음)이면 파일을 만들지 않는 이유
    ///   이때 Current 는 new SaveData() 의 기본값입니다. 여기서 파일을 만들어 버리면
    ///   HasSave() 가 true 가 되어, 매니저들이 "첫 실행 초기화" 대신 ApplyFrom() 을 타게 될 수 있습니다.
    ///   그러면 SaveData 의 기본값(baseDamage 0, attackSpd 1, criticalMultiplier 0)이
    ///   그대로 적용돼 공격력 0 이 되는 버그가 납니다. (스탯창 문서 3-2 의 그 문제)
    ///   → 이름은 Current 에만 넣어 두고, MainScene 의 첫 Save() 때 함께 기록되게 둡니다.
    ///     Save() 는 Current 를 바탕으로 저장하므로 이름이 사라지지 않습니다.
    /// </summary>
    private static void Persist()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null) return;

        if (sm.HasSave())
            sm.WriteCurrentToDisk();   // 기존 유저: 디스크 내용 + 이름만 바뀐 상태로 기록
        else
            Debug.Log("[PlayerProfile] 첫 실행 — 이름은 첫 저장 때 함께 기록됩니다.");
    }

    // ───────── 예전 PlayerPrefs 이름 이관 (1회성) ─────────

    /// <summary>
    /// 예전 PlayerName.cs 로 이름을 정했던 기존 유저를 위해,
    /// PlayerPrefs 에 남은 이름을 SaveData 로 옮깁니다. LoginScene 시작 시 한 번 호출하세요.
    /// - 새 규칙(글자 수·허용 문자)에 맞으면 그대로 가져오고, 안 맞으면 버립니다 (유저가 새로 정함).
    /// - 어느 쪽이든 PlayerPrefs 키는 지웁니다. 안 지우면 세이브 삭제 후 옛 이름이 되살아납니다.
    /// </summary>
    public static void MigrateLegacyIfNeeded()
    {
        if (!PlayerPrefs.HasKey(LEGACY_PREFS_KEY)) return;
        if (Data == null) return;   // SaveManager 가 준비될 때까지 키를 보존

        string legacy = PlayerPrefs.GetString(LEGACY_PREFS_KEY, string.Empty);

        if (!HasName && Validate(legacy, out string cleaned, out _))
        {
            Data.playerName = cleaned;
            Persist();
            Debug.Log($"[PlayerProfile] 예전 이름 이관: {cleaned}");

            // 세이브 파일이 없으면 Persist() 가 파일에 쓰지 않았으므로 이름은 아직 메모리(Current)에만 있습니다.
            // 이때 키를 지웠는데 첫 저장 전에 앱이 꺼지면 옛 이름이 영영 사라집니다.
            // → 파일에 확정될 때까지 키를 남겨 둡니다.
            //   다음 실행 때는 파일에 이름이 있으므로(HasName) 아래 else 로 가서 키가 지워집니다.
            if (!SaveManager.Instance.HasSave()) return;
        }
        else
        {
            Debug.Log($"[PlayerProfile] 예전 이름 이관 안 함 (값: '{legacy}', 이미 이름 있음={HasName})");
        }

        PlayerPrefs.DeleteKey(LEGACY_PREFS_KEY);
        PlayerPrefs.Save();
    }

    // ───────── ★ [계정 삭제] ─────────

    /// <summary>
    /// ★ 신규. 계정 삭제(AccountReset) 전용 — 예전 PlayerPrefs 이름 키를 지웁니다.
    ///
    /// [왜 필요한가]
    ///   이 클래스는 이름을 SaveData 에만 두므로 save.json 을 지우면 이름도 사라집니다.
    ///   그런데 위 MigrateLegacyIfNeeded() 는 "세이브 파일이 아직 없으면" 키를 일부러 남겨 둡니다.
    ///   그 키가 남은 채로 계정을 삭제하면, LoginScene 이 시작될 때 이관 코드가 옛 이름을 다시 가져와
    ///   "계정을 지웠는데 이름만 살아 있는" 상태가 됩니다.
    ///
    /// PlayerPrefs.Save() 는 부르지 않습니다 — AccountReset 이 여러 삭제를 모아 한 번에 부릅니다.
    /// 이름 자체(SaveData.playerName)는 SaveManager.TryDeleteSaveFile() 이 Current 를 비울 때 함께 사라집니다.
    /// </summary>
    public static void DeleteLegacyPrefsForReset()
    {
        PlayerPrefs.DeleteKey(LEGACY_PREFS_KEY);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 테스트 전용: 이름만 지워 첫 실행 상태로 되돌립니다. 다른 진행도는 유지됩니다.
    /// 릴리스 빌드에서는 컴파일되지 않으므로 게임 코드에서 호출할 수 없습니다.
    /// </summary>
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