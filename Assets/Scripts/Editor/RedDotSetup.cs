using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 열기 버튼(우편·친구·채팅·인벤토리·공지·길드)에 RedDotBadge 를 붙이고, 인벤토리 슬롯 프리팹에 '새 아이템' 점을 만듦.
/// 이미 붙어 있으면 건너뛰므로 여러 번 실행해도 됩니다. 점 위치·크기·색은 만들어진 RedDot 오브젝트에서 고치세요.
/// </summary>
public static class RedDotSetup
{
    private static readonly (Type type, RedDotKey key)[] Buttons =
    {
        (typeof(OpenMailboxButton),   RedDotKey.Mail),
        (typeof(OpenFriendsButton),   RedDotKey.FriendRequest),
        (typeof(OpenChatButton),      RedDotKey.Chat),
        (typeof(OpenInventoryButton), RedDotKey.Inventory),
        (typeof(OpenNoticeButton),    RedDotKey.Notice),
        (typeof(OpenGuildButton),     RedDotKey.GuildApplicant),
    };

    [MenuItem("Tools/UI 굽기/레드닷 붙이기 (빌드 씬 전체)")]
    private static void SetupAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] restore = EditorSceneManager.GetSceneManagerSetup();

        var log = new List<string>();
        try
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
                Scene scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                int added = SetupScene(scene);
                if (added > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.Add($"{scene.name}: 버튼 {added}개");
                }
            }

            int slots = SetupSlotPrefabs();
            if (slots > 0) log.Add($"인벤토리 슬롯 프리팹: {slots}개");
        }
        finally
        {
            if (restore.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(restore);
        }

        EditorUtility.DisplayDialog("레드닷 붙이기",
            log.Count > 0 ? "붙였습니다.\n\n" + string.Join("\n", log) : "새로 붙일 곳이 없습니다 (이미 모두 붙어 있음).", "확인");
    }

    /// <summary>씬 안의 열기 버튼에 배지를 붙임 (꺼진 오브젝트 포함). 새로 붙인 수.</summary>
    private static int SetupScene(Scene scene)
    {
        int added = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach ((Type type, RedDotKey key) in Buttons)
                foreach (Component c in root.GetComponentsInChildren(type, true))
                {
                    if (c.GetComponent<RedDotBadge>() != null) continue;
                    var badge = Undo.AddComponent<RedDotBadge>(c.gameObject);   // Reset 에서 점이 만들어짐
                    badge.SetupInEditor(new[] { key });
                    added++;
                }
        return added;
    }

    /// <summary>InventorySlot 프리팹에 newMark 점이 없으면 만들어 연결. 바꾼 프리팹 수.</summary>
    private static int SetupSlotPrefabs()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<InventorySlot>() == null) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slot = contents.GetComponent<InventorySlot>();
                var so   = new SerializedObject(slot);
                SerializedProperty mark = so.FindProperty("newMark");
                if (mark == null || mark.objectReferenceValue != null) continue;

                GameObject dot = RedDotBadge.CreateDot((RectTransform)contents.transform, 22f);
                mark.objectReferenceValue = dot;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        return changed;
    }
}
