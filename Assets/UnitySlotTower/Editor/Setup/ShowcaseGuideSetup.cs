using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 015 FR-004: ステージ1の見せ場の強調（暗幕4枚・レバーを指す大きな矢印・文字あり版の案内）を Portrait Root の最後の子に作り、
    /// 筐体に結ぶ。レバーの点滅も大きくする。何度実行しても同じ結果になる（前の部品は消して作り直す）。
    /// </summary>
    internal static class ShowcaseGuideSetup
    {
        private const string GuideName = "Showcase Guide";
        private static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color ArrowColor = new Color(1f, 0.85f, 0.2f);
        private static readonly string[] ShadeNames = { "Shade Top", "Shade Bottom", "Shade Left", "Shade Right" };

        [MenuItem("Tools/UnitySlotTower/Setup/Showcase Guide (spec 015)")]
        private static void Run()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var root = cabinet != null ? FindAncestor(cabinet.transform, "Portrait Root") : null;
            if (root == null)
            {
                Debug.LogError("[SlotTower Setup] スロット筐体か Portrait Root が見つからない。");
                return;
            }

            var old = root.Find(GuideName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var guideRect = Rect(GuideName, root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            guideRect.SetAsLastSibling();
            var guide = guideRect.gameObject.AddComponent<SlotShowcaseGuide>();
            var content = Rect("Content", guideRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

            // 暗幕: 左下を基準にし、位置と大きさは実行時に筐体の外側へ合わせる。入力も受け止める（レバー以外を押させない）。
            var shades = new Object[ShadeNames.Length];
            for (var i = 0; i < ShadeNames.Length; i++)
            {
                var shade = Rect(ShadeNames[i], content, Vector2.zero, Vector2.zero, Vector2.zero);
                var image = shade.gameObject.AddComponent<Image>();
                image.color = ShadeColor;
                image.raycastTarget = true;
                shades[i] = shade;
            }

            // 矢印: 下向きの大きな三角（先が下端）。幅はレバーの幅くらい。
            var lever = cabinet.LeverRect.rect;
            var size = Mathf.Clamp(lever.width * 0.8f, 120f, 220f);
            var arrow = Rect("Arrow", content, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            arrow.sizeDelta = new Vector2(size, size);
            var outline = Rect("Outline", arrow, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<TriangleGraphic>();
            outline.rectTransform.offsetMin = new Vector2(-10f, -14f);
            outline.rectTransform.offsetMax = new Vector2(10f, 6f);
            outline.color = new Color(0.25f, 0.12f, 0f);
            outline.Pointing = TriangleGraphic.Direction.Down;
            outline.raycastTarget = false;
            var triangle = Rect("Triangle", arrow, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<TriangleGraphic>();
            triangle.color = ArrowColor;
            triangle.Pointing = TriangleGraphic.Direction.Down;
            triangle.raycastTarget = false;

            // 案内: 筐体の真上、横いっぱい。文字は文字あり版だけ（TextModeLabel）。
            var message = Rect("Message", content, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            message.sizeDelta = new Vector2(1100f, 150f);
            TextModeSetup.AddShowcaseLabel(message, 96f);

            var serialized = new SerializedObject(guide);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            var shadeArray = serialized.FindProperty("shades");
            shadeArray.arraySize = shades.Length;
            for (var i = 0; i < shades.Length; i++)
            {
                shadeArray.GetArrayElementAtIndex(i).objectReferenceValue = shades[i];
            }

            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            serialized.FindProperty("arrow").objectReferenceValue = arrow;
            serialized.FindProperty("message").objectReferenceValue = message;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.gameObject.SetActive(false);

            // レバーの点滅を大きく（spec 015 FR-004）。
            var cabinetSerialized = new SerializedObject(cabinet);
            cabinetSerialized.FindProperty("leverHintPulse").floatValue = 0.3f;
            cabinetSerialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 見せ場の暗幕・矢印・案内を作った（spec 015）。");
        }

        private static RectTransform FindAncestor(Transform from, string name)
        {
            for (var t = from; t != null; t = t.parent)
            {
                if (t.name == name)
                {
                    return t as RectTransform;
                }
            }

            return null;
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
