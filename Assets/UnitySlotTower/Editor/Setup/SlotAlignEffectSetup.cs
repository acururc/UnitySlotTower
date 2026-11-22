using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T366（FR-044、plan 17.6）: 開いている Game シーンの UI Canvas に揃った時の演出の層（<see cref="SlotAlignEffect"/>）を足す。
    /// 筐体・操作バー・開始の演出より上、ヘルプ・リザルトより下に置く。何度実行しても同じ結果になる。
    /// </summary>
    internal static class SlotAlignEffectSetup
    {
        private const string LayerName = "Slot Align Effect";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Slot Align Effect (T366)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var canvas = status != null ? status.GetComponentInParent<Canvas>(true)?.rootCanvas : null;
            if (cabinet == null || status == null || canvas == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・操作バー・UI Canvas が見つからない。");
                return;
            }

            // 縦長の画面の親（Portrait Root）の中、早見表（Help）のすぐ前。ヘルプ・リザルトはこの演出より上に出る。
            var help = Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include);
            var root = help != null ? (RectTransform)help.transform.parent : (RectTransform)canvas.transform;
            foreach (var parent in new[] { (Transform)canvas.transform, root })
            {
                var old = parent.Find(LayerName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var rect = new GameObject(LayerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = root.gameObject.layer;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var index = help != null ? help.transform.GetSiblingIndex() : root.childCount - 1;
            rect.SetSiblingIndex(index);

            var effect = rect.gameObject.AddComponent<SlotAlignEffect>();
            var icons = Object.FindObjectsByType<UnitBarIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("status").objectReferenceValue = status;
            serialized.FindProperty("glowSprite").objectReferenceValue =
                AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var list = serialized.FindProperty("icons");
            list.arraySize = icons.Length;
            for (var i = 0; i < icons.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 揃った時の演出の層を足した（T366、行き先のアイコン {icons.Length} 個、{root.name} の {rect.GetSiblingIndex()} 番目）。");
        }
    }
}
