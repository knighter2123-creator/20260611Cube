using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// <summary>GameLog.Info 켜기/끄기 메뉴 — 현재 빌드 타깃의 Define 에 GAME_VERBOSE_LOG 를 넣고 빼서 재컴파일 (⚠ 타깃별 저장이라 출시 빌드 전엔 끌 것).</summary>
public static class GameLogMenu
{
    private const string MenuPath = "Tools/Game Log/상세 로그(Info) 켜기";

    private static NamedBuildTarget CurrentTarget =>
        NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

    private static string[] GetSymbols()
    {
        PlayerSettings.GetScriptingDefineSymbols(CurrentTarget, out string[] defines);
        return defines;
    }

    private static bool IsEnabled => GetSymbols().Contains(GameLog.VerboseSymbol);

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        string[] defines = GetSymbols();
        defines = IsEnabled
            ? defines.Where(d => d != GameLog.VerboseSymbol).ToArray()
            : defines.Append(GameLog.VerboseSymbol).ToArray();

        PlayerSettings.SetScriptingDefineSymbols(CurrentTarget, defines);
        UnityEngine.Debug.Log($"[GameLog] 상세 로그 {(IsEnabled ? "켜짐" : "꺼짐")} ({CurrentTarget.TargetName}) — 다시 컴파일합니다.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }
}
