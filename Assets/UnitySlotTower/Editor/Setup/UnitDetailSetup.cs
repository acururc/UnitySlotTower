using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 選んだ味方の詳細の板（T384、plan 18.4）と、札・板の職の絵を作る。<see cref="BattleHudRestyleSetup"/> から呼ぶ。
    /// 板は戦闘の領域の下端（盤面と札の間）に置き、両方の版で同じ配置。文字あり版では各欄の名前（TextModeLabel）が出る。
    /// 撤退ボタンは上端の帯から板の中へ移す（名前・押したときの処理は今までどおり）。
    /// </summary>
    internal static class UnitDetailSetup
    {
        private const string LabelPrefix = "Text Label ";

        // 板の位置（画面の左下からの px）と中の欄（板の左からの px、板の高さ 124 px）。
        private const float PanelLeft = 16f;
        private const float PanelRight = 1424f;
        private const float PanelBottom = 972f;
        private const float PanelTop = 1096f;
        private const float LabelBottom = 76f;
        private const float LabelTop = 120f;
        private const float ValueBottom = 8f;
        private const float ValueTop = 72f;

        internal static void Build(RectTransform root, RectTransform status, RectTransform bar, TMP_FontAsset numberFont)
        {
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var retreat = (status.Find("Retreat Button") ?? root.Find("Unit Detail/Content/Retreat Button")) as RectTransform;
            if (battle == null || input == null || retreat == null)
            {
                Debug.LogError("[SlotTower Setup] 詳細の板: BattleController / UnitPlacementInput / Retreat Button がない。");
                return;
            }

            var detail = Ensure(root, "Unit Detail");
            Fill(detail);
            detail.SetSiblingIndex(root.Find("Top Band").GetSiblingIndex() + 1);
            var content = Ensure(detail, "Content");
            Px(content, PanelLeft, PanelBottom, PanelRight, PanelTop, root);
            var panel = content.GetComponent<UiPanel>() ?? content.gameObject.AddComponent<UiPanel>();
            if (content.GetComponent<CanvasRenderer>() == null)
            {
                content.gameObject.AddComponent<CanvasRenderer>();
            }

            panel.color = UiStyle.PanelFill;
            panel.SetBorder(UiStyle.PanelBorder);
            panel.raycastTarget = true; // 板の上のクリックが盤面の選び直しにならないように。

            // 職の絵（札と同じ。選んだ味方の職のものだけ出す）と強化段階の点。
            var jobs = Ensure(content, "Job");
            Box(jobs, 14f, 34f, 114f, 116f);
            var looks = UnitLooks.Read();
            var jobArts = new GameObject[4];
            for (var i = 0; i < 4; i++)
            {
                var art = Ensure(jobs, $"Job {i + 1}");
                Fill(art);
                var image = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var look = i < looks.Count ? looks[i] : default;
                image.color = look.Color;
                UnitLooks.BuildShape(art, look);
                jobArts[i] = art.gameObject;
            }

            var pips = new Image[5];
            for (var level = 0; level < pips.Length; level++)
            {
                var pip = Ensure(content, $"Level {level + 1}");
                var x = 14f + level * 21f;
                Box(pip, x, 12f, x + 16f, 26f);
                pips[level] = pip.GetComponent<Image>() ?? pip.gameObject.AddComponent<Image>();
                pips[level].raycastTarget = false;
            }

            // 体力: 名前、バー（味方の青）と数字。
            var hp = Ensure(content, "Hp");
            Box(hp, 140f, 0f, 540f, 124f);
            Label(hp, "detail_hp", retreat);
            var back = Ensure(hp, "Back");
            Box(back, 0f, 26f, 250f, 54f);
            SetImage(back, new Color(0.05f, 0.05f, 0.06f, 0.95f));
            var fill = Ensure(back, "Fill");
            Fill(fill);
            var fillImage = SetImage(fill, UiStyle.Ally);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.sprite = null;
            var hpText = Number(hp, "Value", 262f, 400f, numberFont, 48f);

            // 攻撃力: 上端の帯の攻撃力の印と同じ絵＋数字。
            var atk = Ensure(content, "Atk");
            Box(atk, 570f, 0f, 820f, 124f);
            Label(atk, "detail_atk", retreat);
            CopyIcon(root.Find("Top Band/Rates/Attack Rate/Icon"), atk, 0f, 14f, 56f, 66f);
            var atkText = Number(atk, "Value", 66f, 250f, numberFont, 48f);

            // ブロック数: 札と同じ「壁と赤い三角」＋数字。
            var block = Ensure(content, "Block");
            Box(block, 830f, 0f, 990f, 124f);
            Label(block, "detail_block", retreat);
            var cardBlock = bar.Find("Card Frame 1/Block") as RectTransform;
            TMP_Text blockText;
            var copy = block.Find("Icon") as RectTransform;
            if (copy == null && cardBlock != null)
            {
                copy = (RectTransform)Object.Instantiate(cardBlock, block, false).transform;
                copy.name = "Icon";
            }

            if (copy == null)
            {
                Debug.LogError("[SlotTower Setup] 詳細の板: 札のブロック数の絵（Card Frame 1/Block）がない。");
                return;
            }

            Box(copy, 0f, 10f, 104f, 68f);
            blockText = copy.Find("Value").GetComponent<TMP_Text>();

            // スキル（T385）: 職のスキルの絵（UnitDetailPanel が実行時に作る）。
            var skill = Ensure(content, "Skill");
            Box(skill, 1006f, 0f, 1126f, 124f);
            Label(skill, "bar_skill", retreat);
            var skillArt = Ensure(skill, "Art");
            Box(skillArt, 0f, 4f, 100f, 74f);

            // 撤退ボタン: 帯から板の右端へ。×印・名前（文字あり版）・戻る額。
            retreat.SetParent(content, false);
            retreat.gameObject.SetActive(true);
            Box(retreat, 1140f, 10f, 1396f, 114f);
            var buttonImage = retreat.GetComponent<Image>();
            buttonImage.sprite = null;
            buttonImage.color = new Color(0.42f, 0.09f, 0.08f, 0.95f);
            var frame = retreat.Find("Frame")?.GetComponent<UiPanel>();
            if (frame != null)
            {
                frame.SetBorder(UiStyle.Danger);
            }

            foreach (var stroke in new[] { "Stroke 1", "Stroke 2" })
            {
                var rect = retreat.Find(stroke) as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.15f, 0.5f);
                    rect.sizeDelta = new Vector2(12f, 66f);
                    rect.anchoredPosition = Vector2.zero;
                }
            }

            var retreatLabel = retreat.Find(LabelPrefix + "bar_retreat") as RectTransform;
            if (retreatLabel != null)
            {
                RemoveLayout(retreatLabel);
                Box(retreatLabel, 50f, 20f, 138f, 84f);
                retreatLabel.GetComponent<TextModeLabel>().Text.fontSize = 36f;
            }

            var refund = Number(retreat, "Refund", 136f, 250f, numberFont, 46f);
            refund.color = UiStyle.Yen;
            refund.alignment = TextAlignmentOptions.Right;

            var component = detail.GetComponent<UnitDetailPanel>() ?? detail.gameObject.AddComponent<UnitDetailPanel>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("placementInput").objectReferenceValue = input;
            serialized.FindProperty("content").objectReferenceValue = content.gameObject;
            SetArray(serialized.FindProperty("jobArts"), jobArts);
            SetArray(serialized.FindProperty("levelPips"), pips);
            serialized.FindProperty("hpFill").objectReferenceValue = fillImage;
            serialized.FindProperty("hpText").objectReferenceValue = hpText;
            serialized.FindProperty("atkText").objectReferenceValue = atkText;
            serialized.FindProperty("blockText").objectReferenceValue = blockText;
            serialized.FindProperty("refundText").objectReferenceValue = refund;
            serialized.FindProperty("skillArt").objectReferenceValue = skillArt;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // 保存時は隠す（Awake でも隠す）。
            content.gameObject.SetActive(false);
        }

        // 欄の名前（文字あり版だけ出る）。撤退ボタンのラベルを写して id を変える。
        private static void Label(RectTransform column, string id, RectTransform retreat)
        {
            var name = LabelPrefix + id;
            var label = column.Find(name) as RectTransform;
            if (label == null)
            {
                var source = retreat.Find(LabelPrefix + "bar_retreat");
                if (source == null)
                {
                    Debug.LogWarning($"[SlotTower Setup] {name}: 写すラベルがない。");
                    return;
                }

                label = (RectTransform)Object.Instantiate(source, column, false).transform;
                label.name = name;
                RemoveLayout(label);
                var textModeLabel = label.GetComponent<TextModeLabel>();
                var serialized = new SerializedObject(textModeLabel);
                serialized.FindProperty("id").stringValue = id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                textModeLabel.Text.alignment = TextAlignmentOptions.Left;
                textModeLabel.Text.fontSize = 36f;
            }

            Box(label, 0f, LabelBottom, column.rect.width, LabelTop);
        }

        private static void RemoveLayout(RectTransform target)
        {
            foreach (var layout in target.GetComponentsInChildren<TextModeLayout>(true))
            {
                layout.Apply(false);
                Object.DestroyImmediate(layout);
            }
        }

        private static void CopyIcon(Transform source, RectTransform parent, float left, float bottom, float right, float top)
        {
            var icon = parent.Find("Icon") as RectTransform;
            if (icon == null && source != null)
            {
                icon = (RectTransform)Object.Instantiate(source, parent, false).transform;
                icon.name = "Icon";
                RemoveLayout(icon);
            }

            if (icon != null)
            {
                Box(icon, left, bottom, right, top);
            }
        }

        private static TMP_Text Number(RectTransform parent, string name, float left, float right, TMP_FontAsset font, float size)
        {
            var rect = Ensure(parent, name);
            Box(rect, left, ValueBottom, right, ValueTop);
            var text = rect.GetComponent<TMP_Text>();
            if (text == null)
            {
                rect.gameObject.AddComponent<CanvasRenderer>();
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = "0";
            }

            if (font != null)
            {
                text.font = font;
            }

            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.5f;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
            text.color = UiStyle.TextColor;
            text.raycastTarget = false;
            return text;
        }

        private static Image SetImage(RectTransform rect, Color color)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        internal static RectTransform Ensure(Transform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.gameObject.layer = parent.gameObject.layer;
            }

            return rect;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // 親の左下からの px で置く（親の大きさに依らない）。
        internal static void Box(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(right - left, top - bottom);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        }

        // ルート（1440×1920）の左下からの px。
        private static void Px(RectTransform rect, float left, float bottom, float right, float top, RectTransform root)
        {
            var size = new Vector2((float)Core.Layout.PortraitLayout.ReferenceWidth, (float)Core.Layout.PortraitLayout.ReferenceHeight);
            rect.anchorMin = new Vector2(left / size.x, bottom / size.y);
            rect.anchorMax = new Vector2(right / size.x, top / size.y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
