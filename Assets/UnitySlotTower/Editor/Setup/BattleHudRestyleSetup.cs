using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 戦闘画面の UI の試作（T383、plan 18.4）。状態（ライフ・残りの敵・所持金）と倍率を戦闘の上端の帯（Top Band）へ移し、
    /// 倍速・撤退・一時停止を帯の右端に置く。操作バーは味方4種の札（枠・状態の線・ブロック数）とスキルゲージだけにする。
    /// 部品の名前とコンポーネントは残して配置と見た目を替える（テストの参照を保つ）。動かす部品の文字あり版の値（TextModeLayout）は
    /// いったん外すので、続けて <c>Build Text Mode Layout (T359)</c> を実行する。何度実行してもよい。
    /// </summary>
    internal static class BattleHudRestyleSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const float Width = 1440f;
        private const float BarHeight = 240f;

        // 札（操作バーの中、px）。左端 = CardLeft + i × CardStep。
        private const float CardLeft = 10f;
        private const float CardStep = 357.5f;
        private const float CardWidth = 347f;

        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Restyle Battle HUD (T383)")]
        private static void Restyle()
        {
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            if (status == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンを開いてから実行する（操作バーが見つからない）。");
                return;
            }

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var root = (RectTransform)bar.parent;
            var rates = (RectTransform)status.transform.parent.Find("Rates");
            StripTextModeLayouts(bar);
            StripTextModeLayouts((RectTransform)status.transform);
            if (rates != null)
            {
                StripTextModeLayouts(rates);
            }

            var bar0 = bar.GetComponent<Image>();
            if (bar0 != null)
            {
                bar0.color = new Color(0.13f, 0.14f, 0.17f);
            }

            BuildBand(root, (RectTransform)status.transform, rates);
            var looks = UnitLooks.Read();
            for (var i = 0; i < 4; i++)
            {
                BuildCard(bar, i, i < looks.Count ? looks[i] : default);
            }

            // 撤退は選んだ味方の詳細の板へ（T384）。
            UnitDetailSetup.Build(root, (RectTransform)status.transform, bar, _font);
            // 文字なし版の説明の部品（T385）。
            SkillHelpSetup.Build(root, bar);

            RestyleActors();
            // UiPanel は CanvasRenderer がないと描かれない（RequireComponent を足す前に作ったものを補う）。
            foreach (var panel in root.GetComponentsInChildren<UiPanel>(true))
            {
                if (panel.GetComponent<CanvasRenderer>() == null)
                {
                    panel.gameObject.AddComponent<CanvasRenderer>();
                }
            }

            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            EditorSceneManager.SaveScene(bar.gameObject.scene);
            Debug.Log("[SlotTower Setup] 戦闘画面の UI を試作の形にした（続けて Build Text Mode Layout (T359) を実行する）。");
        }

        // --- 上端の帯 ---

        private static void BuildBand(RectTransform root, RectTransform status, RectTransform rates)
        {
            var bandHeight = (float)(PortraitLayout.BattleHeightRatio * PortraitLayout.TopBandRatio);
            var band = root.Find("Top Band") as RectTransform;
            if (band == null)
            {
                band = CreateRect("Top Band", root);
                var panel = band.gameObject.AddComponent<UiPanel>();
                panel.color = new Color(0.03f, 0.035f, 0.05f, 0.92f);
                panel.raycastTarget = false;
            }

            Anchor(band, new Vector2(0f, 1f - bandHeight), Vector2.one);
            // 操作バーのすぐ後ろ（ルート・ショップ・リザルトなどの画面より下）に描く。
            var bar = root.Find("Operation Bar");
            band.SetSiblingIndex(bar.GetSiblingIndex() + 1);
            status.SetParent(band, false);
            Anchor(status, Vector2.zero, Vector2.one);
            if (rates != null)
            {
                rates.SetParent(band, false);
                Anchor(rates, new Vector2(620f / Width, 0f), new Vector2(980f / Width, 1f));
                RateRow(rates, "Attack Rate", 0f, 0.49f);
                RateRow(rates, "Money Rate", 0.51f, 1f);
            }

            // アイコン＋数字（x は帯の左からの px）。
            IconValue(status, "Life Icon", "Life", 16f, 172f);
            IconValue(status, "Enemy Icon", "Remaining", 192f, 330f);
            IconValue(status, "Coin Icon", "Coins", 350f, 600f);

            CornerButton(status, "Speed Button", 1188f, 1306f);
            CornerButton(status, "Pause Button", 1318f, 1428f);
        }

        private static void IconValue(RectTransform status, string iconName, string valueName, float left, float right)
        {
            var icon = status.Find(iconName) as RectTransform;
            var value = status.Find(valueName) as RectTransform;
            if (icon == null || value == null)
            {
                Debug.LogWarning($"[SlotTower Setup] {iconName} / {valueName} がない。");
                return;
            }

            const float iconSize = 50f;
            Anchor(icon, new Vector2(left / Width, 0.5f), new Vector2(left / Width, 0.5f));
            icon.sizeDelta = new Vector2(iconSize, iconSize);
            icon.anchoredPosition = new Vector2(iconSize * 0.5f, 0f);
            Anchor(value, new Vector2((left + iconSize + 6f) / Width, 0.06f), new Vector2(right / Width, 0.94f));
            var text = value.GetComponent<TMP_Text>();
            if (text != null)
            {
                Number(text, 54f, TextAlignmentOptions.Left);
            }
        }

        private static void RateRow(RectTransform rates, string name, float left, float right)
        {
            var row = rates.Find(name) as RectTransform;
            if (row == null)
            {
                return;
            }

            Anchor(row, new Vector2(left, 0f), new Vector2(right, 1f));
            var icon = row.Find("Icon") as RectTransform;
            if (icon != null)
            {
                Anchor(icon, new Vector2(0f, 0.18f), new Vector2(0.3f, 0.82f));
            }

            var value = row.Find("Value") as RectTransform;
            if (value != null)
            {
                Anchor(value, new Vector2(0.32f, 0.06f), new Vector2(1f, 0.94f));
                Number(value.GetComponent<TMP_Text>(), 44f, TextAlignmentOptions.Left);
            }
        }

        /// <summary>帯の右の四角いボタン: 平らな暗い地に細い明るい縁（UiPanel の子 Frame）。</summary>
        internal static void CornerButton(RectTransform status, string name, float left, float right)
        {
            var button = status.Find(name) as RectTransform;
            if (button == null)
            {
                Debug.LogWarning($"[SlotTower Setup] {name} がない。");
                return;
            }

            Anchor(button, new Vector2(left / Width, 0.1f), new Vector2(right / Width, 0.9f));
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = new Color(0.13f, 0.14f, 0.18f, 0.95f);
            }

            var frame = Frame(button, UiStyle.PanelBorder);
            frame.transform.SetAsFirstSibling();
            var speed = button.Find("Speed") as RectTransform;
            if (speed != null)
            {
                Anchor(speed, Vector2.zero, Vector2.one);
                Number(speed.GetComponent<TMP_Text>(), 50f, TextAlignmentOptions.Center);
            }
        }

        // --- 札 ---

        private static void BuildCard(RectTransform bar, int slot, UnitLooks.Look look)
        {
            var left = CardLeft + slot * CardStep;
            var icon = bar.Find($"Unit Icon {slot + 1}") as RectTransform;
            var upgrade = bar.Find($"Upgrade Icon {slot + 1}") as RectTransform;
            var gauge = bar.Find($"Skill Gauge {slot + 1}") as RectTransform;
            if (icon == null)
            {
                Debug.LogWarning($"[SlotTower Setup] Unit Icon {slot + 1} がない。");
                return;
            }

            var card = bar.Find($"Card Frame {slot + 1}") as RectTransform;
            if (card == null)
            {
                card = CreateRect($"Card Frame {slot + 1}", bar);
                var panel = card.gameObject.AddComponent<UiPanel>();
                panel.raycastTarget = false;
            }

            card.GetComponent<UiPanel>().color = UiStyle.PanelFill;
            Px(card, left, 10f, left + CardWidth, 230f);
            card.SetSiblingIndex(icon.GetSiblingIndex());

            Px(icon, left + 12f, 36f, left + 184f, 218f);
            var body = icon.GetComponent<Image>();
            if (body != null)
            {
                body.sprite = null;
            }

            // 職の絵（T384）: 盤上の駒と同じ形の影絵。値段の下地より上に置き、待ち時間の覆いより後ろに描く。
            var jobArt = icon.Find("Job Art") as RectTransform ?? CreateRect("Job Art", icon);
            Anchor(jobArt, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.98f));
            jobArt.SetAsFirstSibling();
            UnitLooks.BuildShape(jobArt, look);

            var cooldown = icon.Find("Cooldown Gauge")?.GetComponent<Image>();
            if (cooldown != null)
            {
                cooldown.sprite = null;
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = (int)Image.Origin360.Top;
                cooldown.fillClockwise = false;
                cooldown.color = new Color(0.35f, 0.02f, 0.02f, 0.6f);
            }

            var cost = icon.Find("Cost") as RectTransform;
            if (cost != null)
            {
                Anchor(cost, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.3f));
                var backing = cost.parent.Find("Cost Back") as RectTransform;
                if (backing == null)
                {
                    backing = CreateRect("Cost Back", icon);
                    var image = backing.gameObject.AddComponent<Image>();
                    image.color = new Color(0f, 0f, 0f, 0.55f);
                    image.raycastTarget = false;
                }

                Anchor(backing, Vector2.zero, new Vector2(1f, 0.3f));
                backing.SetSiblingIndex(cost.GetSiblingIndex());
                Number(cost.GetComponent<TMP_Text>(), 44f, TextAlignmentOptions.Center);
            }

            var stock = icon.Find("Stock") as RectTransform;
            if (stock != null)
            {
                Anchor(stock, new Vector2(0.5f, 0.72f), new Vector2(1.04f, 1.04f));
                // 下地は数字の手前の兄弟（子にすると数字の上に描かれて隠す: T384）。
                var badge = (stock.Find("Badge") ?? icon.Find("Stock Badge")) as RectTransform;
                if (badge == null)
                {
                    badge = CreateRect("Stock Badge", icon);
                    var badgePanel = badge.gameObject.AddComponent<UiPanel>();
                    badgePanel.raycastTarget = false;
                }

                badge.name = "Stock Badge";
                badge.SetParent(icon, false);
                if (badge.GetSiblingIndex() > stock.GetSiblingIndex())
                {
                    badge.SetSiblingIndex(stock.GetSiblingIndex());
                }
                badge.anchorMin = stock.anchorMin;
                badge.anchorMax = stock.anchorMax;
                badge.offsetMin = badge.offsetMax = Vector2.zero;
                badge.gameObject.SetActive(stock.gameObject.activeSelf);
                var panel = badge.GetComponent<UiPanel>();
                panel.color = UiStyle.Ready;
                Number(stock.GetComponent<TMP_Text>(), 40f, TextAlignmentOptions.Center);
                stock.GetComponent<TMP_Text>().color = new Color(0.1f, 0.08f, 0.02f);
            }

            // 状態の線（アイコンの下）。UnitBarIcon.stateFrame が状態の色にする。
            var strip = card.Find("State Strip") as RectTransform;
            if (strip == null)
            {
                strip = CreateRect("State Strip", card);
                strip.gameObject.AddComponent<Image>().raycastTarget = false;
            }

            Anchor(strip, Vector2.zero, Vector2.zero);
            Px(strip, 12f, 20f, 184f, 28f);
            strip.GetComponent<Image>().color = UiStyle.PanelBorder;

            // ブロック数: 盾＋数字（アイコンの右上）。
            var block = card.Find("Block") as RectTransform;
            TMP_Text blockText;
            if (block == null)
            {
                block = CreateRect("Block", card);
                var shield = CreateRect("Shield", block).gameObject.AddComponent<UiPanel>();
                shield.raycastTarget = false;
                var valueObject = CreateRect("Value", block).gameObject;
                valueObject.AddComponent<CanvasRenderer>();
                var number = valueObject.AddComponent<TextMeshProUGUI>();
                number.raycastTarget = false;
                number.text = "1";
            }

            Anchor(block, Vector2.zero, Vector2.zero);
            Px(block, 192f, 160f, 296f, 218f);
            // ブロック数の絵は説明画面と同じ「壁と、それに当たる赤い三角（敵）」。
            var shieldRect = (RectTransform)block.Find("Shield");
            Anchor(shieldRect, new Vector2(0.02f, 0.05f), new Vector2(0.14f, 0.95f));
            var shieldPanel = shieldRect.GetComponent<UiPanel>();
            shieldPanel.color = new Color(0.62f, 0.66f, 0.72f);
            shieldPanel.SetBorder(new Color(0f, 0f, 0f, 0f));
            var enemy = block.Find("Enemy") as RectTransform;
            if (enemy == null)
            {
                enemy = CreateRect("Enemy", block);
                enemy.gameObject.AddComponent<CanvasRenderer>();
                var triangle = enemy.gameObject.AddComponent<TriangleGraphic>();
                triangle.color = UiStyle.Danger;
                triangle.raycastTarget = false;
            }

            Anchor(enemy, new Vector2(0.16f, 0.2f), new Vector2(0.42f, 0.8f));
            enemy.localEulerAngles = new Vector3(0f, 0f, 180f);
            blockText = block.Find("Value").GetComponent<TMP_Text>();
            Anchor((RectTransform)blockText.transform, new Vector2(0.48f, 0f), Vector2.one);
            Number(blockText, 46f, TextAlignmentOptions.Left);

            if (upgrade != null)
            {
                Px(upgrade, left + 192f, 36f, left + 300f, 150f);
                // T384: 矢印（上向き）と値段が重ならないように、上の段に矢印と値段、下の段に強化段階の点。
                var arrow = upgrade.Find("Arrow") as RectTransform;
                if (arrow != null)
                {
                    arrow.anchorMin = arrow.anchorMax = new Vector2(0.17f, 0.66f);
                    arrow.pivot = new Vector2(0.5f, 0.5f);
                    arrow.sizeDelta = new Vector2(30f, 28f);
                    arrow.anchoredPosition = Vector2.zero;
                }

                var upgradeCost = upgrade.Find("Cost") as RectTransform;
                if (upgradeCost != null)
                {
                    Anchor(upgradeCost, new Vector2(0.33f, 0.4f), new Vector2(0.98f, 0.94f));
                    Number(upgradeCost.GetComponent<TMP_Text>(), 34f, TextAlignmentOptions.Right);
                }

                for (var level = 0; level < 5; level++)
                {
                    var pip = upgrade.Find($"Level {level + 1}") as RectTransform;
                    if (pip != null)
                    {
                        var x = 0.08f + level * 0.18f;
                        Anchor(pip, new Vector2(x, 0.1f), new Vector2(x + 0.13f, 0.26f));
                    }
                }
            }

            if (gauge != null)
            {
                // 上の 160〜218 px はスキルの絵（T385、SkillHelpSetup）。
                Px(gauge, left + 306f, 20f, left + 337f, 156f);
            }

            var barIcon = icon.GetComponent<UnitBarIcon>();
            if (barIcon != null)
            {
                var serialized = new SerializedObject(barIcon);
                SetObject(serialized, "blockText", blockText);
                SetObject(serialized, "stateFrame", strip.GetComponent<Image>());
                SetObject(serialized, "stockBadge", icon.Find("Stock Badge")?.gameObject);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // --- 盤上 ---

        private static void RestyleActors()
        {
            var actors = Object.FindAnyObjectByType<BattleActorsView>(FindObjectsInactive.Include);
            if (actors == null)
            {
                return;
            }

            var serialized = new SerializedObject(actors);
            var size = serialized.FindProperty("gaugeSize");
            if (size != null)
            {
                size.vector2Value = new Vector2(0.72f, 0.07f);
            }

            SetColor(serialized, "gaugeBackColor", new Color(0.05f, 0.05f, 0.06f));
            SetColor(serialized, "unitGaugeColor", UiStyle.Ally);
            SetColor(serialized, "enemyGaugeColor", UiStyle.Danger);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- 共通 ---

        /// <summary>部品と子孫の TextModeLayout を文字なし版に戻してから外す（新しい配置を文字なし版の値として読み直させる）。</summary>
        private static void StripTextModeLayouts(RectTransform target)
        {
            var help = target.Find("Open Button");
            var home = target.Find("Home Button");
            foreach (var layout in target.GetComponentsInChildren<TextModeLayout>(true))
            {
                // T423 owns these controls in both layouts.
                if ((help != null && layout.transform.IsChildOf(help)) ||
                    (home != null && layout.transform.IsChildOf(home))) continue;
                if (layout.GetComponent<TextModeLabel>() != null || layout.transform.parent.GetComponent<TextModeLabel>() != null)
                {
                    // ラベルの配置は文字あり版にしか出ないので、そのまま残して Build Text Mode Layout で置き直す。
                    continue;
                }

                layout.Apply(false);
                Object.DestroyImmediate(layout);
            }
        }

        private static UiPanel Frame(RectTransform parent, Color border)
        {
            var frame = parent.Find("Frame") as RectTransform;
            if (frame == null)
            {
                frame = CreateRect("Frame", parent);
                frame.gameObject.AddComponent<UiPanel>();
            }

            Anchor(frame, Vector2.zero, Vector2.one);
            var panel = frame.GetComponent<UiPanel>();
            panel.color = new Color(0f, 0f, 0f, 0f);
            panel.SetBorder(border);
            panel.raycastTarget = false;
            return panel;
        }

        private static void Number(TMP_Text text, float size, TextAlignmentOptions alignment)
        {
            if (text == null)
            {
                return;
            }

            if (_font != null)
            {
                text.font = _font;
            }

            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.5f;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>操作バー（1440×240）の中の px で置く。</summary>
        private static void Px(RectTransform rect, float left, float bottom, float right, float top)
        {
            var parent = (RectTransform)rect.parent;
            var w = parent.name == "Operation Bar" ? Width : parent.rect.width;
            var h = parent.name == "Operation Bar" ? BarHeight : parent.rect.height;
            if (parent.name == "Operation Bar")
            {
                Anchor(rect, new Vector2(left / w, bottom / h), new Vector2(right / w, top / h));
                return;
            }

            // 札の子は札の左下からの px（札の大きさは CardWidth × 220）。
            Anchor(rect, new Vector2(left / CardWidth, bottom / 220f - 10f / 220f), new Vector2(right / CardWidth, top / 220f - 10f / 220f));
        }

        private static void SetObject(SerializedObject serialized, string name, Object value)
        {
            var property = serialized.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning($"[SlotTower Setup] {serialized.targetObject.GetType().Name}.{name} がない。");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetColor(SerializedObject serialized, string name, Color value)
        {
            var property = serialized.FindProperty(name);
            if (property != null)
            {
                property.colorValue = value;
            }
        }
    }
}
