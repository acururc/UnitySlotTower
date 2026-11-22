using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T613（FR-113）: 開いている Game シーンに揃ったときの盤面全体の演出の層（<see cref="SlotBoardEffect"/>）を足す。
    /// 揃った時の演出の層（T366、Slot Align Effect）のすぐ下（先に描く）に置き、図柄の光と飛ぶ玉はこの演出より上に出る。
    /// 何度実行しても同じ結果になる。
    /// </summary>
    internal static class SlotBoardEffectSetup
    {
        private const string LayerName = "Slot Board Effect";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Slot Board Effect (T613)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var align = Object.FindAnyObjectByType<SlotAlignEffect>(FindObjectsInactive.Include);
            var camera = fieldView != null
                ? new SerializedObject(fieldView).FindProperty("battleCamera").objectReferenceValue as Camera
                : null;
            if (cabinet == null || battle == null || fieldView == null || align == null || camera == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・戦闘・フィールド・カメラ・揃った時の演出の層（T366）が見つからない。");
                return;
            }

            var root = (RectTransform)align.transform.parent;
            var old = root.Find(LayerName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var rect = new GameObject(LayerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = root.gameObject.layer;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(align.transform.GetSiblingIndex());

            var effect = rect.gameObject.AddComponent<SlotBoardEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("circleSprite").objectReferenceValue =
                AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 盤面全体の演出の層を足した（T613、{root.name} の {rect.GetSiblingIndex()} 番目、揃った時の演出は {align.transform.GetSiblingIndex()} 番目）。");
        }
    }
}
