using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンにリールのカスタム画面（T316）を組む一回限りの手順。Portrait Root の最前面に重ね、BattleController に結ぶ。
    /// 既にあれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// 上から: ハズレ率（×）と当たり率（3つ並んだ丸）、マスの列（複製元1つ、実行時に reel_length 個にする）、図柄ごとの%、✔。
    /// </summary>
    internal static class ReelCustomSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "Reel Custom";

        // リニア色空間では薄い透過でも下がかなり見えるので、ほぼ不透明にする。
        private static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.97f);
        private static readonly Color MarkColor = new Color(0.25f, 0.25f, 0.3f);
        private static readonly Color FrameColor = new Color(1f, 0.85f, 0.2f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Reel Custom Screen (T316)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<ReelCustomScreen>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] リールのカスタム画面は既にあるため作成しない。");
                return;
            }

            var root = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            if (root == null || battle == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / BattleController が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var screenRect = CreateRect(RootName, root, Vector2.zero, Vector2.one);
            screenRect.SetAsLastSibling();
            var screen = screenRect.gameObject.AddComponent<ReelCustomScreen>();

            // 背景が下の戦闘・操作バー・筐体へのクリックを遮る。
            var panel = CreateImage("Panel", screenRect, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;

            // ハズレ率と当たり率。
            var missIcon = CreateImage("Miss Icon", panel.rectTransform, new Vector2(0.14f, 0.76f), new Vector2(0.24f, 0.83f),
                MarkColor, _box);
            CreateText("Cross", missIcon.rectTransform, Vector2.zero, Vector2.one, new Color(0.75f, 0.75f, 0.8f)).text = "×";
            var miss = CreateText("Miss Rate", panel.rectTransform, new Vector2(0.25f, 0.76f), new Vector2(0.48f, 0.83f),
                new Color(0.75f, 0.75f, 0.8f));
            var hitIcon = CreateImage("Hit Icon", panel.rectTransform, new Vector2(0.52f, 0.76f), new Vector2(0.66f, 0.83f),
                MarkColor, _box);
            for (var i = 0; i < 3; i++)
            {
                var left = 0.08f + i * 0.3f;
                CreateImage($"Dot {i + 1}", hitIcon.rectTransform, new Vector2(left, 0.25f), new Vector2(left + 0.24f, 0.75f),
                    SlotSymbolArt.CoinColor, _knob).preserveAspect = true;
            }

            var hit = CreateText("Hit Rate", panel.rectTransform, new Vector2(0.67f, 0.76f), new Vector2(0.9f, 0.83f),
                SlotSymbolArt.CoinColor);

            // マスの列。複製元を1つだけ置き、実行時に reel_length 個へ増やす。
            var row = CreateRect("Cells", panel.rectTransform, new Vector2(0.03f, 0.53f), new Vector2(0.97f, 0.6f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var template = CreateCell(row);

            // 図柄ごとの印と当たり時の%。
            var marks = new Graphic[SlotSymbols.Count];
            var percents = new TMP_Text[SlotSymbols.Count];
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var left = 0.04f + s * 0.155f;
                var mark = CreateImage($"Symbol Mark {s + 1}", panel.rectTransform, new Vector2(left, 0.3f),
                    new Vector2(left + 0.145f, 0.45f), MarkColor, _box);
                var art = CreateRect("Art", mark.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.95f));
                SlotSymbolArt.Create((SlotSymbol)s, art);
                marks[s] = mark;
                percents[s] = CreateText("Percent", mark.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.38f),
                    Color.white);
            }

            // ✔: 緑の札に白いチェック（短い棒と長い棒）。条件を満たさないあいだは押せない（灰色）。
            var confirmImage = CreateImage("Confirm Button", panel.rectTransform, new Vector2(0.36f, 0.1f),
                new Vector2(0.64f, 0.2f), new Color(0.2f, 0.7f, 0.3f), _box);
            confirmImage.raycastTarget = true;
            var confirm = confirmImage.gameObject.AddComponent<Button>();
            confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = confirm.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            confirm.colors = colors;
            // 2本の棒は札の下寄り（幅 0.44・高さ 0.28 の点）で交わる。短い棒は左上へ、長い棒は右上へ伸びる。
            CreateStroke("Check Short", confirmImage.rectTransform, new Vector2(0.387f, 0.39f), 60f, 45f);
            CreateStroke("Check Long", confirmImage.rectTransform, new Vector2(0.546f, 0.547f), 134f, -40f);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("cellTemplate").objectReferenceValue = template;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("missText").objectReferenceValue = miss;
            serialized.FindProperty("hitText").objectReferenceValue = hit;
            SetArray(serialized.FindProperty("symbolMarks"), marks);
            SetArray(serialized.FindProperty("symbolPercentTexts"), percents);
            serialized.FindProperty("markColor").colorValue = MarkColor;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var battleSerialized = new SerializedObject(battle);
            battleSerialized.FindProperty("reelCustomScreen").objectReferenceValue = screen;
            battleSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 実行時は BattleController が開く。シーンの編集の邪魔にならないよう閉じて保存する。
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] リールのカスタム画面を作成した。");
        }

        // マス: 白い札のボタン。子に選択中の下線（Frame）と6図柄（Symbols）。
        private static Button CreateCell(RectTransform row)
        {
            var image = CreateImage("Cell Template", row, Vector2.zero, Vector2.one, Color.white, _box);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var frame = CreateImage("Frame", image.rectTransform, new Vector2(0f, -0.2f), new Vector2(1f, -0.08f),
                FrameColor, _box);
            frame.gameObject.SetActive(false);
            var symbols = CreateRect("Symbols", image.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
            SlotSymbolArt.CreateAllSymbols(symbols);
            return button;
        }

        // 太さ 16 の白い棒。length は基準解像度での長さ、angle は縦向きからの回転（正で左に倒れる）。
        private static void CreateStroke(string name, RectTransform parent, Vector2 center, float length, float angle)
        {
            var stroke = CreateImage(name, parent, center, center, Color.white, null);
            stroke.rectTransform.sizeDelta = new Vector2(16f, length);
            stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
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
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
