using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンに一時停止中の説明（T404）を組む一回限りの手順。Portrait Root のリザルトの手前に置き、BattleController と筐体に結ぶ。
    /// 既にあれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// 「?」ボタンは戦闘の右下（操作バーの一時停止ボタンの上）。早見表は上がスロット（ハズレと6図柄の確率・効果）、下が味方4種（範囲図と数値）。
    /// </summary>
    internal static class HelpScreenSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "Help";
        private const int UnitCount = 4;

        // 基準 1440×1920 で正方形にするための、高さに対する幅の比。
        private const float Square = 1920f / 1440f;

        // 表を読みやすくするため、下の戦闘は透かさない（リザルトと同じ）。
        private static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.3f, 0.34f);
        private static readonly Color HelpColor = new Color(0.2f, 0.45f, 0.85f);
        private static readonly Color DimColor = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color DarkColor = new Color(0.12f, 0.12f, 0.15f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Help Screen (T404)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 説明画面は既にあるため作成しない。");
                return;
            }

            var root = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
            var bar = GameObject.Find("Operation Bar")?.transform;
            if (root == null || battle == null || cabinet == null || result == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / BattleController / SlotCabinet / Result / Operation Bar が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var screenRect = CreateRect(RootName, root, Vector2.zero, Vector2.one);
            screenRect.SetSiblingIndex(result.transform.GetSiblingIndex());
            var screen = screenRect.gameObject.AddComponent<HelpScreen>();
            var serialized = new SerializedObject(screen);

            // 「?」ボタン: 一時停止中だけ出る。
            var open = CreateButton("Open Button", screenRect, new Vector2(0.88f, 0.41f), new Vector2(0.98f, 0.41f + 0.1f / Square),
                HelpColor);
            var mark = CreateText("Mark", open.GetComponent<RectTransform>(), new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f),
                Color.white);
            mark.text = "?";
            mark.alignment = TextAlignmentOptions.Center;

            // 背景が下の戦闘・操作バー・筐体へのクリックを遮る。
            var panel = CreateImage("Panel", screenRect, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;
            var area = panel.rectTransform;

            var close = CreateButton("Close Button", area, new Vector2(0.03f, 0.935f), new Vector2(0.15f, 0.985f), ButtonColor);
            var back = CreateRect("Arrow", close.GetComponent<RectTransform>(), new Vector2(0.35f, 0.2f), new Vector2(0.65f, 0.8f));
            back.localEulerAngles = new Vector3(0f, 0f, 180f);
            back.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            var header = CreateText("Mark", area, new Vector2(0.44f, 0.935f), new Vector2(0.56f, 0.985f), HelpColor);
            header.text = "?";
            header.alignment = TextAlignmentOptions.Center;

            BuildSlot(area, serialized);
            CreateImage("Divider", area, new Vector2(0.04f, 0.556f), new Vector2(0.96f, 0.56f), DimColor, null);
            BuildUnits(area, bar, serialized);

            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("slotCabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("openButton").objectReferenceValue = open;
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 実行時は一時停止すると「?」が出る。シーンの編集の邪魔にならないよう閉じて保存する。
            open.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 説明画面を作成した。");
        }

        // スロット: 1行目はハズレ（暗い枠に赤い×）、続いて6図柄。各行は 図柄・確率・▶・効果。
        private static void BuildSlot(RectTransform area, SerializedObject serialized)
        {
            const float top = 0.92f;
            const float height = 0.05f;
            var miss = Row(area, "Miss", top, height);
            var missIcon = CreateImage("Icon", miss, new Vector2(0.06f, 0.08f), new Vector2(0.06f + 0.84f * height * Square, 0.92f),
                DarkColor, _box);
            missIcon.type = Image.Type.Sliced;
            var cross = CreateText("Cross", missIcon.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f),
                new Color(0.95f, 0.2f, 0.2f));
            cross.text = "×";
            cross.alignment = TextAlignmentOptions.Center;
            serialized.FindProperty("missText").objectReferenceValue = Chance(miss);

            var chances = serialized.FindProperty("chanceTexts");
            var effects = serialized.FindProperty("effectTexts");
            var seconds = serialized.FindProperty("secondsTexts");
            chances.arraySize = effects.arraySize = seconds.arraySize = SlotSymbols.Count;
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var symbol = (SlotSymbol)s;
                var row = Row(area, SlotSymbols.Ids[s], top - (s + 1) * height, height);
                // リールのコマと同じく明るい札の上に描く（BAR の黒い帯が暗い背景に沈まないように）。
                var card = CreateImage("Icon", row, new Vector2(0.06f, 0.04f), new Vector2(0.06f + 0.92f * height * Square, 0.96f),
                    new Color(0.92f, 0.92f, 0.95f), _box);
                card.type = Image.Type.Sliced;
                var icon = CreateRect("Symbol", card.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
                SlotSymbolArt.Create(symbol, icon);
                chances.GetArrayElementAtIndex(s).objectReferenceValue = Chance(row);
                var arrow = CreateRect("Arrow", row, new Vector2(0.42f, 0.3f), new Vector2(0.45f, 0.7f));
                arrow.gameObject.AddComponent<TriangleGraphic>().color = DimColor;

                // T341 のつなぎ: コインの図柄はコインのアイコンを前に置き、バー（旧の攻撃力アップ）は時計と秒数の欄を持つ。作り直しは T351。
                var paysCoins = symbol == SlotSymbol.Coin;
                var valueMin = 0.48f;
                if (paysCoins)
                {
                    CreateImage("Coin Icon", row, new Vector2(0.48f, 0.18f), new Vector2(0.48f + 0.64f * height * Square, 0.82f),
                        SlotSymbolArt.CoinColor, _knob);
                    valueMin = 0.54f;
                }

                var value = CreateText("Effect", row, new Vector2(valueMin, 0.05f), new Vector2(0.78f, 0.95f), Color.white);
                effects.GetArrayElementAtIndex(s).objectReferenceValue = value;
                if (symbol == SlotSymbol.Bar)
                {
                    Clock(row, new Vector2(0.8f, 0.18f), new Vector2(0.8f + 0.64f * height * Square, 0.82f));
                    seconds.GetArrayElementAtIndex(s).objectReferenceValue =
                        CreateText("Seconds", row, new Vector2(0.86f, 0.05f), new Vector2(0.97f, 0.95f), Color.white);
                }
            }
        }

        private static RectTransform Row(RectTransform area, string name, float top, float height) =>
            CreateRect(name, area, new Vector2(0f, top - height), new Vector2(1f, top));

        private static TextMeshProUGUI Chance(RectTransform row)
        {
            var text = CreateText("Chance", row, new Vector2(0.16f, 0.05f), new Vector2(0.4f, 0.95f), Color.white);
            text.alignment = TextAlignmentOptions.Right;
            return text;
        }

        // 味方: 本体の色の札、範囲図（実行時に作る）、左列に HP・攻撃（回復）・防御・攻撃間隔、右列にブロック・コスト・待ち時間。
        private static void BuildUnits(RectTransform area, Transform bar, SerializedObject serialized)
        {
            const float top = 0.54f;
            const float height = 0.13f;
            var rows = serialized.FindProperty("unitRows");
            rows.arraySize = UnitCount;
            for (var i = 0; i < UnitCount; i++)
            {
                var row = Row(area, $"Unit {i + 1}", top - i * height, height);
                var body = bar.Find($"Unit Icon {i + 1}")?.GetComponent<Image>();
                var color = body != null ? body.color : Color.white;
                var card = CreateImage("Body", row, new Vector2(0.04f, 0.2f), new Vector2(0.04f + 0.6f * height * Square, 0.8f),
                    color, _box);
                card.type = Image.Type.Sliced;
                var rangeArea = CreateRect("Range", row, new Vector2(0.17f, 0.06f), new Vector2(0.45f, 0.94f));

                var element = rows.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("rangeArea").objectReferenceValue = rangeArea;
                element.FindPropertyRelative("bodyColor").colorValue = color;

                var line = 1f / 4f;
                Stat(row, element, "hpText", 0.5f, 3, line, HpIcon);
                var atk = Stat(row, element, "atkText", 0.5f, 2, line, AttackIcon);
                var heal = CreateRect("Heal Icon", atk.parent as RectTransform, Vector2.zero, Vector2.one);
                SlotSymbolArt.Heal(heal);
                element.FindPropertyRelative("attackIcon").objectReferenceValue = atk.gameObject;
                element.FindPropertyRelative("healIcon").objectReferenceValue = heal.gameObject;
                heal.gameObject.SetActive(false);
                Stat(row, element, "defText", 0.5f, 1, line, DefenseIcon);
                Stat(row, element, "intervalText", 0.5f, 0, line, (parent) => Clock(parent, Vector2.zero, Vector2.one));
                Stat(row, element, "blockText", 0.75f, 3, line, BlockIcon);
                Stat(row, element, "costText", 0.75f, 2, line, (parent) =>
                    CreateImage("Coin", parent, Vector2.zero, Vector2.one, SlotSymbolArt.CoinColor, _knob));
                Stat(row, element, "cooldownText", 0.75f, 1, line, CooldownIcon);
            }
        }

        // 数値1つ: 正方形のアイコン枠（中身は draw で描く）と右の数字。line は下から数えた行。戻り値はアイコンの中身の最初の子。
        private static Transform Stat(RectTransform row, SerializedProperty element, string property, float x, int line,
            float lineHeight, System.Action<RectTransform> draw)
        {
            var y = line * lineHeight;
            var iconWidth = 0.7f * lineHeight * 0.13f * Square;
            var icon = CreateRect(property.Replace("Text", " Icon"), row, new Vector2(x, y + 0.15f * lineHeight),
                new Vector2(x + iconWidth, y + 0.85f * lineHeight));
            var content = CreateRect("Content", icon, Vector2.zero, Vector2.one);
            draw(content);
            var text = CreateText(property.Replace("Text", ""), row, new Vector2(x + iconWidth + 0.01f, y),
                new Vector2(x + 0.24f, y + lineHeight), Color.white);
            element.FindPropertyRelative(property).objectReferenceValue = text;
            return content;
        }

        // HP: 暗い枠に緑のゲージ。
        private static void HpIcon(RectTransform parent)
        {
            CreateImage("Frame", parent, new Vector2(0f, 0.2f), new Vector2(1f, 0.8f), DimColor, null);
            CreateImage("Fill", parent, new Vector2(0.1f, 0.32f), new Vector2(0.72f, 0.68f), new Color(0.3f, 0.85f, 0.35f), null);
        }

        // 攻撃: 赤い ▶。
        private static void AttackIcon(RectTransform parent)
        {
            var triangle = CreateRect("Triangle", parent, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f))
                .gameObject.AddComponent<TriangleGraphic>();
            triangle.color = SlotSymbolArt.AttackColor;
            triangle.raycastTarget = false;
        }

        // 防御: 青い盾（外枠と明るい内側）。
        private static void DefenseIcon(RectTransform parent)
        {
            CreateImage("Outer", parent, new Vector2(0.1f, 0f), new Vector2(0.9f, 1f), new Color(0.25f, 0.45f, 0.9f), _box)
                .type = Image.Type.Sliced;
            CreateImage("Inner", parent, new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.75f), new Color(0.6f, 0.75f, 1f), null);
        }

        // ブロック数: 灰色の壁と、それに当たって止まる赤い ◀（敵）。
        private static void BlockIcon(RectTransform parent)
        {
            CreateImage("Wall", parent, new Vector2(0.05f, 0f), new Vector2(0.35f, 1f), DimColor, null);
            var enemy = CreateRect("Enemy", parent, new Vector2(0.42f, 0.2f), new Vector2(1f, 0.8f));
            enemy.localEulerAngles = new Vector3(0f, 0f, 180f);
            var triangle = enemy.gameObject.AddComponent<TriangleGraphic>();
            triangle.color = new Color(0.9f, 0.3f, 0.3f);
            triangle.raycastTarget = false;
        }

        // 待ち時間: 操作バーのゲージと同じく、明るい札の下を暗く覆う。
        private static void CooldownIcon(RectTransform parent)
        {
            CreateImage("Card", parent, new Vector2(0.15f, 0f), new Vector2(0.85f, 1f), DimColor, null);
            CreateImage("Gauge", parent, new Vector2(0.15f, 0f), new Vector2(0.85f, 0.6f), new Color(0f, 0f, 0f, 0.6f), null);
        }

        // 時計: 白い円に濃い針2本。
        private static void Clock(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var face = CreateImage("Clock", parent, anchorMin, anchorMax, Color.white, _knob);
            face.preserveAspect = true;
            CreateImage("Hand Up", face.rectTransform, new Vector2(0.45f, 0.48f), new Vector2(0.55f, 0.85f), DarkColor, null);
            CreateImage("Hand Right", face.rectTransform, new Vector2(0.48f, 0.45f), new Vector2(0.78f, 0.55f), DarkColor, null);
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = "0";
            text.color = color;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }
    }
}
