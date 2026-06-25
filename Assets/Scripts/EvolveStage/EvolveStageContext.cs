/// <summary>
/// 진화 스테이지 입장과 복귀 사이에 정보를 전달하는 정적 보관소.
/// 씬을 완전히 전환해도 static 값은 앱이 실행되는 동안 유지됩니다.
///   - SelectedData : 어떤 티어로 입장했는지 (보스 배율/보상)
///   - ReturnWorld/ReturnStage : 클리어 후 돌아갈 원래 스테이지 위치
/// </summary>
public static class EvolveStageContext
{
    public static EvolveStageData SelectedData;

    public static bool HasReturn;
    public static int  ReturnWorld;
    public static int  ReturnStage;

    /// <summary>입장 시 호출 — 티어 데이터와 복귀 위치를 기록.</summary>
    public static void Enter(EvolveStageData data, int returnWorld, int returnStage)
    {
        SelectedData = data;
        ReturnWorld  = returnWorld;
        ReturnStage  = returnStage;
        HasReturn    = true;
    }

    /// <summary>복귀 처리를 끝낸 뒤 호출 — 같은 위치로 두 번 복귀하는 것 방지.</summary>
    public static void ClearReturn()
    {
        HasReturn = false;
    }
}