using UnityEngine;

/// <summary>튜토리얼 자동 표시 여부를 SaveData.tutorialDone 으로 판단하는 창구(저장 방식이 바뀌어도 여기만 수정) — 신규·세이브 삭제면 false 라 뜨고, 구버전은 마이그레이션으로 true, 완료/스킵은 MarkDone (파일 존재가 아닌 플래그를 봐서 이름 저장으로 파일이 생겨도 무관).</summary>
public static class TutorialProgress
{
    public static bool ShouldAutoShow
    {
        get
        {
            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Current : null;
            if (data == null)
            {
                // SaveManager 없음(에디터에서 MainScene 직접 재생) — 모르면 안 띄우는 게 안전
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

        // WriteCurrentToDisk — Save() 는 CaptureTo 를 돌려 아직 ApplyFrom 전인 매니저 기본값이 덮어쓸 수 있음 (진행도는 다음 Save 때)
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