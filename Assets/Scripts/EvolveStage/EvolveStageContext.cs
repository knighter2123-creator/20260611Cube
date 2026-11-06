/// <summary>진화 스테이지 입장↔복귀 사이 정보(입장 티어, 복귀할 월드·스테이지)를 static 으로 보관 — 씬 전환에도 유지.</summary>
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