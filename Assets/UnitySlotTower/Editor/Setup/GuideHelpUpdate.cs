using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 既存の Game シーンの説明と早見表を T351（plan 16.10）の形にする。再実行しても同じ結果。
    /// 説明: 進行とルート画面に結ぶ（ルートの題目・画面中は出さない）。
    /// 早見表: 上半分（スロットの行）を作り直し、味方の行の旧ヒーラーの絵をバッファーの絵に替え、本体の札の下にスキルの印を足す。
    /// </summary>
    internal static class GuideHelpUpdate
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Update Guide And Help (T351)")]
        private static void Update()
        {
            var guide = Object.FindAnyObjectByType<GuideOverlay>(FindObjectsInactive.Include);
            var help = Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include);
            if (guide == null || help == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの説明（Guide）か早見表（Help）が見つからない。");
                return;
            }

            var guideObject = new SerializedObject(guide);
            if (!GuideOverlaySetup.WireRoute(guideObject))
            {
                Debug.LogError("[SlotTower Setup] RunController か RouteScreen が見つからない。");
                return;
            }

            guideObject.ApplyModifiedPropertiesWithoutUndo();

            HelpScreenSetup.LoadAssets();
            var helpObject = new SerializedObject(help);
            var panel = ((GameObject)helpObject.FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();
            RemoveSlotRows(panel);
            HelpScreenSetup.BuildSlot(panel, helpObject);
            UpdateUnitRows(helpObject.FindProperty("unitRows"));
            helpObject.ApplyModifiedPropertiesWithoutUndo();

            var scene = help.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 説明と早見表を T351 の形にした。");
        }

        // 上半分の行: 以前の作り直しの親と、横いっぱいで区切り線（0.56）より上にある行（旧 T404 の作り方）。
        private static void RemoveSlotRows(RectTransform panel)
        {
            for (var i = panel.childCount - 1; i >= 0; i--)
            {
                var child = (RectTransform)panel.GetChild(i);
                var isRow = child.anchorMin.x == 0f && child.anchorMax.x == 1f && child.anchorMin.y >= 0.565f;
                if (child.name == HelpScreenSetup.SlotRowsName || isRow)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void UpdateUnitRows(SerializedProperty rows)
        {
            for (var i = 0; i < rows.arraySize; i++)
            {
                var element = rows.GetArrayElementAtIndex(i);
                var row = (RectTransform)((RectTransform)element.FindPropertyRelative("rangeArea").objectReferenceValue).parent;
                var body = row.Find("Body") as RectTransform;
                if (body != null)
                {
                    HelpScreenSetup.PlaceBody(body, HelpScreenSetup.UnitRowHeight);
                }

                DestroyChild(row, "Skill Mark");
                HelpScreenSetup.SkillMark(row, element, HelpScreenSetup.UnitRowHeight);

                var attack = (GameObject)element.FindPropertyRelative("attackIcon").objectReferenceValue;
                var atkIcon = (RectTransform)attack.transform.parent;
                DestroyChild(atkIcon, "Heal Icon");
                DestroyChild(atkIcon, "Buff Icon");
                HelpScreenSetup.AddBuffIcon(atkIcon, element);
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
