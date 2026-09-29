using UnityEngine;

/// <summary>
/// "튜토리얼을 자동으로 띄워야 하는가?"를 SaveData.tutorialDone 으로 판단하는 얇은 창구.
///
/// 규칙
///   신규 유저 (세이브 파일 없음)         → SaveManager.Load 가 new SaveData() → false → 자동 팝업
///   기존 유저 (구버전 세이브)            → SaveManager.Load 마이그레이션이 true 로 → 안 뜸
///   끝까지 봤거나 스킵함                 → MarkDone() 이 true 로 저장 → 다시 안 뜸
///   설정 → 세이브 삭제(DeleteSave)        → new SaveData() → false → 다음 MainScene 에서 다시 뜸
///
/// ★ LoginScene 에서 이름을 정할 때 세이브 파일이 생겨도 괜찮은 이유:
///   "파일이 있냐"가 아니라 "tutorialDone 이 true 냐"를 보기 때문입니다.
///   이름 저장(WriteCurrentToDisk)은 tutorialDone = false 를 그대로 기록합니다.
///
/// TutorialManager 가 SaveManager 를 직접 만지지 않고 이 클래스를 거치는 이유:
///   나중에 저장 방식이 바뀌어도 이 파일만 고치면 되도록.
/// </summary>
public static class TutorialProgress
{
    public static bool ShouldAutoShow
    {
        get
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Current : null;
            if (data == null)
            {
                // 에디터에서 MainScene 을 바로 재생하면 LoginScene 의 SaveManager 가 없어서 여기로 옵니다.
                // 세이브를 모르는 상태에서 띄웠다가 기존 유저에게 뜨는 것보다는 안 띄우는 게 안전.
                Debug.LogWarning("[Tutorial] SaveManager 가 없어 자동 팝업 여부를 알 수 없습니다. (LoginScene 부터 실행하세요)");
                return false;
            }
            return !data.tutorialDone;
        }
    }

    /// <summary>끝까지 봤거나 스킵했을 때 호출.</summary>
    public static void MarkDone()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null || sm.Current.tutorialDone) return;

        sm.Current.tutorialDone = true;

        // ★ Save() 가 아니라 WriteCurrentToDisk() 를 쓰는 이유:
        //   Save() 는 모든 매니저의 CaptureTo() 를 다시 돌립니다. 튜토리얼은 MainScene 2프레임째에
        //   시작될 수 있는데, 그때 아직 ApplyFrom 을 안 끝낸 매니저가 있으면 기본값이 파일을 덮어씁니다.
        //   WriteCurrentToDisk() 는 "마지막으로 불러오거나 저장한 내용 + 이 플래그"만 쓰므로 안전합니다.
        //   (진행도는 어차피 다음 Save() — 백그라운드 전환/종료/로그인 복귀 — 때 저장됩니다)
        sm.WriteCurrentToDisk();
    }

    /// <summary>테스트용: 튜토리얼만 다시 신규 상태로. (세이브 전체를 지우려면 SaveManager ⋮ → 세이브 삭제)</summary>
    public static void ResetForDebug()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null) return;

        sm.Current.tutorialDone = false;
        sm.WriteCurrentToDisk();
    }
}