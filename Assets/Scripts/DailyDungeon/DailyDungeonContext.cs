/// <summary>
/// 일일 던전 입장과 복귀 사이에 정보를 전달하는 정적 보관소. (EvolveStageContext 와 같은 방식)
///   - SelectedData / SelectedLevel : 어떤 던전의 몇 난이도로 입장했는지
///   - ReturnWorld / ReturnStage    : 끝난 뒤 돌아갈 원래 스테이지 위치
/// </summary>
public static class DailyDungeonContext
{
    public static DailyDungeonData SelectedData;
    public static int              SelectedLevel = 1;

    public static bool HasReturn;
    public static int  ReturnWorld;
    public static int  ReturnStage;

    /// <summary>입장 시 호출 — 던전/난이도와 복귀 위치를 기록.</summary>
    public static void Enter(DailyDungeonData data, int level, int returnWorld, int returnStage)
    {
        SelectedData  = data;
        SelectedLevel = level;
        ReturnWorld   = returnWorld;
        ReturnStage   = returnStage;
        HasReturn     = true;
    }

    /// <summary>복귀 처리를 끝낸 뒤 호출 — 같은 위치로 두 번 복귀하는 것 방지.</summary>
    public static void ClearReturn()
    {
        HasReturn = false;
    }
}
