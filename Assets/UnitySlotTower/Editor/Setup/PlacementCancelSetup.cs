using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 向きを選んでいる間にマスの右上に出す × のボタン（<see cref="PlacementCancelButton"/>）を作る。
    /// T385 のブロック数の印（`Placement Block`）があれば消す。<see cref="BattleHudRestyleSetup"/> からも呼ぶ（何度でも可）。
    /// </summary>
    internal static class PlacementCancelSetup
    {
        private const float Size = 96f;
        private const float Stroke = 14f;

        [MenuItem("Tools/UnitySlotTower/Setup/Placement Cancel Button")]
        private static void BuildFromMenu()
        {
            var detail = Object.FindAnyObjectByType<UnitDetailPanel>(FindObjectsInactive.Include);
            var root = detail != null ? detail.transform.parent as RectTransform : null;
            if (root == null)
            {
                Debug.LogError("[SlotTower Setup] × のボタン: Game シーンを開いて実行する（Unit Detail が見つからない）。");
                return;
            }

            Build(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("[SlotTower Setup] × のボタンを作った。");
        }

        internal static void Build(RectTransform root)
        {
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var camera = input != null ? new SerializedObject(input).FindProperty("battleCamera").objectReferenceValue : null;
            if (battle == null || field == null || camera == null || root.Find("Unit Detail") == null)
            {
                Debug.LogError("[SlotTower Setup] × のボタン: BattleController / BattleFieldView / カメラ / Unit Detail がない。");
                return;
            }

            var old = root.Find("Placement Block");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var holder = UnitDetailSetup.Ensure(root, "Placement Cancel");
            holder.anchorMin = Vector2.zero;
            holder.anchorMax = Vector2.one;
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.offsetMin = holder.offsetMax = Vector2.zero;
            holder.SetSiblingIndex(root.Find("Unit Detail").GetSiblingIndex());

            var rect = UnitDetailSetup.Ensure(holder, "Cancel Button");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            if (rect.GetComponent<CanvasRenderer>() == null)
            {
                rect.gameObject.AddComponent<CanvasRenderer>();
            }

            var panel = rect.GetComponent<UiPanel>() ?? rect.gameObject.AddComponent<UiPanel>();
            panel.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            panel.SetBorder(UiStyle.PanelBorder);
            panel.raycastTarget = true;

            var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // × は2本の棒（字体に頼らない）。
            Bar(rect, "Stroke A", 45f);
            Bar(rect, "Stroke B", -45f);

            var component = holder.GetComponent<PlacementCancelButton>() ?? holder.gameObject.AddComponent<PlacementCancelButton>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rect.gameObject.SetActive(false);
        }

        private static void Bar(RectTransform parent, string name, float angle)
        {
            var bar = UnitDetailSetup.Ensure(parent, name);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(Size * 0.62f, Stroke);
            bar.anchoredPosition = Vector2.zero;
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = bar.GetComponent<Image>() ?? bar.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.color = UiStyle.TextColor;
            image.raycastTarget = false;
        }
    }
}
