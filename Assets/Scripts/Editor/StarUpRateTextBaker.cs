using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>인벤토리 상세창에 '상승 확률 nn%' 글자를 만드는 일회용 도구 — 조각 수 글자(progressText)를 복제해 바로 아래에 둠.</summary>
public static class StarUpRateTextBaker
{
    [MenuItem("Tools/UI 굽기/인벤토리 상세 - 성급 확률 글자")]
    private static void Bake()
    {
        int changed = 0, skipped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            var    asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<InventoryItemDetail>(true) == null) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (InventoryItemDetail detail in contents.GetComponentsInChildren<InventoryItemDetail>(true))
                {
                    var so = new SerializedObject(detail);
                    SerializedProperty rateProp     = so.FindProperty("starUpRateText");
                    SerializedProperty progressProp = so.FindProperty("progressText");
                    if (rateProp.objectReferenceValue != null) { skipped++; continue; }

                    var progress = progressProp.objectReferenceValue as TMP_Text;
                    if (progress == null)
                    {
                        Debug.LogWarning($"[StarUpRateTextBaker] {path}: progressText 가 비어 있어 복제할 글자가 없습니다. 직접 만들어 Star Up Rate Text 에 연결하세요.");
                        continue;
                    }

                    GameObject copy = Object.Instantiate(progress.gameObject, progress.transform.parent);
                    copy.name = "StarUpRateText";
                    copy.transform.SetSiblingIndex(progress.transform.GetSiblingIndex() + 1);

                    var rt  = (RectTransform)copy.transform;
                    var src = (RectTransform)progress.transform;
                    rt.anchoredPosition = src.anchoredPosition - new Vector2(0f, src.rect.height);   // 바로 아래 한 줄

                    var text = copy.GetComponent<TMP_Text>();
                    text.text  = "상승 확률 60%";
                    text.color = new Color(1f, 0.85f, 0.2f);   // 금색 — 인스펙터에서 바꾸세요

                    rateProp.objectReferenceValue = text;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }

                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        EditorUtility.DisplayDialog("성급 확률 글자",
            changed > 0 ? $"만들었습니다 ({changed}개). 위치가 겹치면 프리팹의 StarUpRateText 를 옮기세요."
                        : skipped > 0 ? "이미 연결돼 있습니다." : "InventoryItemDetail 이 든 프리팹을 찾지 못했습니다.",
            "확인");
    }
}
