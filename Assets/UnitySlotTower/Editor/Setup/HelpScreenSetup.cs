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
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンに一時停止中の説明（T404）を組む一回限りの手順。Portrait Root のリザルトの手前に置き、BattleController と筐体に結ぶ。
    /// 既にあれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// 「?」ボタンは戦闘の右下（操作バーの一時停止ボタンの上）。早見表の中身（5ページ）は HelpRebuildSetup（spec 016）。
    /// </summary>
    internal static class HelpScreenSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "Help";

        // 基準 1440×1920 で正方形にするための、高さに対する幅の比。
        private const float Square = 1920f / 1440f;

        // 表を読みやすくするため、下の戦闘は透かさない（リザルトと同じ）。
        private static readonly Color PanelColor = UiStyle.ScreenFill;
        private static readonly Color ButtonColor = UiStyle.ButtonFill;
        private static readonly Color HelpColor = new Color(0.2f, 0.45f, 0.85f);

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

            var portrait = GameObject.Find("Portrait Root");
            var root = portrait == null ? null : portrait.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
            var operationBar = GameObject.Find("Operation Bar");
            var bar = operationBar == null ? null : operationBar.transform;
            if (root == null || battle == null || cabinet == null || result == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / BattleController / SlotCabinet / Result / Operation Bar が見つからない。");
                return;
            }

            LoadAssets();
            var screenRect = CreateRect(RootName, root, Vector2.zero, Vector2.one);
            screenRect.SetSiblingIndex(result.transform.GetSiblingIndex());
            var screen = screenRect.gameObject.AddComponent<HelpScreen>();
            var serialized = new SerializedObject(screen);

            // 「?」ボタン: 一時停止中だけ出る。
            var status = root.Find("Top Band/Status") as RectTransform;
            var open = CreateButton("Open Button", status != null ? status : screenRect, Vector2.zero, Vector2.one, HelpColor);
            if (status != null)
                PauseMenuSetup.StyleHelpButton(status, open);
            else
            {
                // Older setup order: T423 moves this button after the status band exists.
                var rect = (RectTransform)open.transform;
                rect.anchorMin = new Vector2(0.88f, 0.41f);
                rect.anchorMax = new Vector2(0.98f, 0.41f + 0.1f / Square);
            }
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

            // ページの中身は spec 016 の HelpRebuildSetup（設定を書いた後に作る）。

            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("slotCabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("openButton").objectReferenceValue = open;
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            HelpRebuildSetup.Build(screen, bar);

            // 実行時は一時停止すると「?」が出る。シーンの編集の邪魔にならないよう閉じて保存する。
            open.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 説明画面を作成した。");
        }

        internal static void LoadAssets()
        {
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        internal static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        internal static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
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
