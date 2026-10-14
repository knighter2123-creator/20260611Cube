using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// GameLog.Info 를 켜고 끄는 메뉴. 현재 빌드 타깃의 Scripting Define Symbols 에
/// GAME_VERBOSE_LOG 를 넣거나 뺍니다. 바꾸면 스크립트가 다시 컴파일됩니다.
///
/// ★ Define 은 빌드 타깃별로 저장됩니다. 켠 채로 안드로이드 빌드를 뽑으면 빌드에도 로그가 들어가므로,
///   출시 빌드 전에는 꺼 두세요.
/// </summary>
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
