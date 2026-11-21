using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 向きを選んでいる間にマスの上に出すブロック数の印（T385、<see cref="PlacementBlockBadge"/>）を作る。
    /// <see cref="BattleHudRestyleSetup"/> から呼ぶ（何度でも可）。
    /// </summary>
    internal static class PlacementBadgeSetup
    {
        internal static void Build(RectTransform root, RectTransform bar)
        {
            BuildPlacementBadge(root, bar);
        }

        // 札のブロック数の絵を写した印（暗い下地つき）。位置は PlacementBlockBadge が毎フレーム決める。
        private static void BuildPlacementBadge(RectTransform root, RectTransform bar)
        {
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var camera = input != null ? new SerializedObject(input).FindProperty("battleCamera").objectReferenceValue : null;
            var cardBlock = bar.Find("Card Frame 1/Block") as RectTransform;
            if (battle == null || field == null || camera == null || cardBlock == null)
            {
                Debug.LogError("[SlotTower Setup] ブロック数の印: BattleController / BattleFieldView / カメラ / Card Frame 1/Block がない。");
                return;
            }

            var holder = UnitDetailSetup.Ensure(root, "Placement Block");
            Fill(holder);
            holder.SetSiblingIndex(root.Find("Unit Detail").GetSiblingIndex());
            var badge = UnitDetailSetup.Ensure(holder, "Badge");
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0f);
            badge.sizeDelta = new Vector2(150f, 78f);
            if (badge.GetComponent<CanvasRenderer>() == null)
            {
                badge.gameObject.AddComponent<CanvasRenderer>();
            }

            var panel = badge.GetComponent<UiPanel>() ?? badge.gameObject.AddComponent<UiPanel>();
            panel.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            panel.SetBorder(UiStyle.PanelBorder);
            panel.raycastTarget = false;

            var icon = badge.Find("Icon") as RectTransform;
            if (icon == null)
            {
                icon = (RectTransform)Object.Instantiate(cardBlock, badge, false).transform;
                icon.name = "Icon";
            }

            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(120f, 64f);
            icon.anchoredPosition = Vector2.zero;
            foreach (var graphic in icon.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            var component = holder.GetComponent<PlacementBlockBadge>() ?? holder.gameObject.AddComponent<PlacementBlockBadge>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("badge").objectReferenceValue = badge;
            serialized.FindProperty("value").objectReferenceValue = icon.Find("Value")?.GetComponent<TMP_Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            badge.gameObject.SetActive(false);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
