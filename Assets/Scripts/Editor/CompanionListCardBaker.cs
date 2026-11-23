using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료 목록 카드에 '한눈에 보기' 요소를 만드는 일회용 도구 — 등급 테두리, 성급 글자, '배치 중' 배지, 배치 시 어둡게 덮는 판.
/// 이미 연결된 칸은 건너뜀. 만든 뒤 위치·색·크기는 프리팹에서 바꾸세요.
/// </summary>
public static class CompanionListCardBaker
{
    [MenuItem("Tools/UI 굽기/동료 목록 카드 - 상태 표시")]
    private static void Bake()
    {
        int changed = 0, skipped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            var    asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<CompanionListItem>(true) == null) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            try
            {
                foreach (CompanionListItem item in contents.GetComponentsInChildren<CompanionListItem>(true))
                {
                    int before = changed;
                    BakeOne(item, path, ref changed, ref skipped);
                    if (changed != before) dirty = true;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        EditorUtility.DisplayDialog("동료 목록 카드",
            changed > 0 ? $"만들었습니다 ({changed}개). 겹치면 프리팹에서 위치를 옮기세요."
                        : skipped > 0 ? "이미 다 연결돼 있습니다." : "CompanionListItem 이 든 프리팹을 찾지 못했습니다.",
            "확인");
    }

    private static void BakeOne(CompanionListItem item, string path, ref int changed, ref int skipped)
    {
        var so = new SerializedObject(item);
        Transform root = item.transform;

        Transform card     = root.Find("Card");
        var       cardImg  = card != null ? card.GetComponent<Image>() : null;
        var       tagText  = so.FindProperty("tagText").objectReferenceValue as TMP_Text;
        var       nameText = so.FindProperty("nameText").objectReferenceValue as TMP_Text;
        var       fragText = so.FindProperty("fragmentText").objectReferenceValue as TMP_Text;

        if (cardImg == null)
        {
            Debug.LogWarning($"[CompanionListCardBaker] {path}: 'Card' 이미지를 찾지 못했습니다. 직접 만들어 연결하세요.");
            return;
        }

        // ── 등급 테두리 — 카드 복제를 카드 바로 뒤에 조금 크게 ──
        SerializedProperty frameProp = so.FindProperty("gradeFrame");
        if (frameProp.objectReferenceValue == null)
        {
            Image frame = CloneImage(cardImg, "Grade Frame", card.GetSiblingIndex());
            ((RectTransform)frame.transform).sizeDelta += new Vector2(10f, 10f);
            frame.color = new Color(0.12f, 0.42f, 0.90f);   // 실행 중엔 등급 색으로 바뀜
            frameProp.objectReferenceValue = frame;
            changed++;
        }
        else skipped++;

        // ── 성급 글자 — 이름 아래 (안 쓰는 예전 조각 글자 자리) ──
        SerializedProperty starProp = so.FindProperty("starText");
        TMP_Text source = fragText != null ? fragText : nameText;
        if (starProp.objectReferenceValue == null && source != null)
        {
            GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = "Star Text";
            copy.SetActive(true);
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            var rt = (RectTransform)copy.transform;
            rt.anchoredPosition = new Vector2(0f, -112f);
            rt.sizeDelta        = new Vector2(190f, 30f);

            var text = copy.GetComponent<TMP_Text>();
            text.text          = "★★★☆☆";
            text.fontSize      = 22f;
            text.color         = new Color(0.95f, 0.65f, 0.05f);   // 금색
            text.alignment     = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            starProp.objectReferenceValue = text;
            changed++;
        }
        else if (starProp.objectReferenceValue != null) skipped++;

        // ── 배치 시 어둡게 덮는 판 — 카드 전체 위, 배지 아래 ──
        SerializedProperty dimProp = so.FindProperty("placedDim");
        if (dimProp.objectReferenceValue == null)
        {
            Image dim = CloneImage(cardImg, "Placed Dim", root.childCount);
            dim.color = new Color(0f, 0f, 0f, 0.35f);
            dim.gameObject.SetActive(false);
            dimProp.objectReferenceValue = dim.gameObject;
            changed++;
        }
        else skipped++;

        // ── '배치 중' 배지 — 오른쪽 위 (계열 배지 반대편) ──
        SerializedProperty badgeProp = so.FindProperty("placedBadge");
        if (badgeProp.objectReferenceValue == null)
        {
            Image badge = CloneImage(cardImg, "Placed Badge", root.childCount);
            var brt = (RectTransform)badge.transform;
            brt.anchoredPosition = new Vector2(60f, 34f);
            brt.sizeDelta        = new Vector2(70f, 30f);
            badge.color = new Color(0.16f, 0.65f, 0.30f);   // 초록

            TMP_Text label = null;
            if (tagText != null)
            {
                GameObject t = Object.Instantiate(tagText.gameObject, badge.transform);
                t.SetActive(true);
                label = t.GetComponent<TMP_Text>();
            }
            else if (nameText != null)
            {
                label = Object.Instantiate(nameText.gameObject, badge.transform).GetComponent<TMP_Text>();
            }

            if (label != null)
            {
                label.gameObject.name = "Text";
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.pivot     = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = Vector2.zero;
                label.richText      = false;
                label.text          = "배치 중";
                label.fontSize      = 18f;
                label.color         = Color.white;
                label.alignment     = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }

            badge.gameObject.SetActive(false);
            badgeProp.objectReferenceValue = badge.gameObject;
            changed++;
        }
        else skipped++;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>카드 이미지만 복제 (자식 없이) — 클릭은 카드 루트 버튼이 받게 raycast 끔.</summary>
    private static Image CloneImage(Image src, string name, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = src.gameObject.layer;

        var rt  = (RectTransform)go.transform;
        var srt = (RectTransform)src.transform;
        rt.SetParent(srt.parent, false);
        rt.anchorMin = srt.anchorMin; rt.anchorMax = srt.anchorMax; rt.pivot = srt.pivot;
        rt.anchoredPosition = srt.anchoredPosition; rt.sizeDelta = srt.sizeDelta;
        rt.localScale = Vector3.one;
        rt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, rt.parent.childCount - 1));

        var img = go.GetComponent<Image>();
        img.sprite         = src.sprite;
        img.type           = src.type;
        img.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        img.raycastTarget  = false;
        return img;
    }
}
