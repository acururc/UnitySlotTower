using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 文字なし版の説明の部品（T385、FR-072、plan 18.5）を作る。<see cref="BattleHudRestyleSetup"/> から呼ぶ（何度でも可）。
    /// ・スキルゲージの上のスキルの絵の置き場（絵は <see cref="UnitBarIcon"/> が実行時に作る）
    /// ・一時停止中にゲージを押すと出るスキルの説明（<see cref="SkillHelpPanel"/>）
    /// ・向きを選んでいる間にマスの上に出すブロック数の印（<see cref="PlacementBlockBadge"/>）
    /// </summary>
    internal static class SkillHelpSetup
    {
        private const float ArtSize = 56f;

        internal static void Build(RectTransform root, RectTransform bar)
        {
            var help = BuildSkillHelp(root);
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            for (var slot = 0; slot < 4; slot++)
            {
                var gauge = bar.Find($"Skill Gauge {slot + 1}") as RectTransform;
                var icon = bar.Find($"Unit Icon {slot + 1}")?.GetComponent<UnitBarIcon>();
                if (gauge == null || icon == null)
                {
                    Debug.LogWarning($"[SlotTower Setup] Skill Gauge / Unit Icon {slot + 1} がない。");
                    continue;
                }

                // ゲージの真上に正方形（札の右上の空き）。押すとゲージを押したことになるよう透明な当たりを付ける。
                var art = UnitDetailSetup.Ensure(gauge, "Skill Art");
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 1f);
                art.pivot = new Vector2(0.5f, 0f);
                art.sizeDelta = new Vector2(ArtSize, ArtSize);
                art.anchoredPosition = new Vector2(-4f, 4f);
                var hit = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = true;
                for (var i = art.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(art.GetChild(i).gameObject);
                }

                var serialized = new SerializedObject(icon);
                serialized.FindProperty("skillArt").objectReferenceValue = art;
                serialized.FindProperty("skillHelp").objectReferenceValue = help;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildPlacementBadge(root, bar);
            var detail = root.Find("Unit Detail")?.GetComponent<UnitDetailPanel>();
            if (detail != null)
            {
                var serialized = new SerializedObject(detail);
                serialized.FindProperty("knob").objectReferenceValue = knob;
                var colors = serialized.FindProperty("skillColors");
                colors.arraySize = SlotSymbolArt.SevenColors.Length;
                for (var i = 0; i < SlotSymbolArt.SevenColors.Length; i++)
                {
                    colors.GetArrayElementAtIndex(i).colorValue = SlotSymbolArt.SevenColors[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 全画面の暗い下地（押すと閉じる）と、戦闘の領域の中央の板。
        private static SkillHelpPanel BuildSkillHelp(RectTransform root)
        {
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var help = UnitDetailSetup.Ensure(root, "Skill Help");
            Fill(help);
            help.SetAsLastSibling();
            var content = UnitDetailSetup.Ensure(help, "Content");
            Fill(content);
            var dim = content.GetComponent<Image>() ?? content.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;

            var board = UnitDetailSetup.Ensure(content, "Board");
            var size = new Vector2((float)Core.Layout.PortraitLayout.ReferenceWidth, (float)Core.Layout.PortraitLayout.ReferenceHeight);
            board.anchorMin = new Vector2(80f / size.x, 1110f / size.y);
            board.anchorMax = new Vector2(1360f / size.x, 1700f / size.y);
            board.offsetMin = board.offsetMax = Vector2.zero;
            if (board.GetComponent<CanvasRenderer>() == null)
            {
                board.gameObject.AddComponent<CanvasRenderer>();
            }

            var panel = board.GetComponent<UiPanel>() ?? board.gameObject.AddComponent<UiPanel>();
            panel.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            panel.SetBorder(UiStyle.PanelBorder);
            panel.raycastTarget = false;

            var component = help.GetComponent<SkillHelpPanel>() ?? help.gameObject.AddComponent<SkillHelpPanel>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            serialized.FindProperty("board").objectReferenceValue = board;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.gameObject.SetActive(false);
            return component;
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
