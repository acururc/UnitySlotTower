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
    /// Game シーンにリールのカスタム画面（T316 → T322 でドラッグ＆ドロップに作り直し）を組む手順。既にあれば消して同じ位置に作り直し、
    /// BattleController と説明（GuideOverlay）の参照を付け替える。
    /// 上から: ハズレ率（×）と当たり率（3つ並んだ丸）、マス（9×2、実行時にロック配置の数だけ複製）、入れられる4図柄、図柄ごとの%、✔。
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
        private static readonly Color HoleColor = new Color(0.12f, 0.12f, 0.15f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Reel Custom Screen (T322)")]
        private static void Build()
        {
            var root = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            if (root == null || battle == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / BattleController が見つからない。");
                return;
            }

            // 古い画面（T316）は消し、同じ重なり順に作り直す。
            var old = Object.FindAnyObjectByType<ReelCustomScreen>(FindObjectsInactive.Include);
            var sibling = -1;
            if (old != null)
            {
                sibling = old.transform.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var screenRect = CreateRect(RootName, root, Vector2.zero, Vector2.one);
            if (sibling >= 0)
            {
                screenRect.SetSiblingIndex(sibling);
            }
            else
            {
                screenRect.SetAsLastSibling();
            }

            var screen = screenRect.gameObject.AddComponent<ReelCustomScreen>();

            // 背景が下の戦闘・操作バー・筐体へのクリックを遮る。
            var panel = CreateImage("Panel", screenRect, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;
            var area = panel.rectTransform;

            // ハズレ率と当たり率。
            var missIcon = CreateImage("Miss Icon", area, new Vector2(0.14f, 0.82f), new Vector2(0.24f, 0.89f), MarkColor, _box);
            CreateText("Cross", missIcon.rectTransform, Vector2.zero, Vector2.one, new Color(0.75f, 0.75f, 0.8f)).text = "×";
            var miss = CreateText("Miss Rate", area, new Vector2(0.25f, 0.82f), new Vector2(0.48f, 0.89f),
                new Color(0.75f, 0.75f, 0.8f));
            var hitIcon = CreateImage("Hit Icon", area, new Vector2(0.52f, 0.82f), new Vector2(0.66f, 0.89f), MarkColor, _box);
            for (var i = 0; i < 3; i++)
            {
                var left = 0.08f + i * 0.3f;
                CreateImage($"Dot {i + 1}", hitIcon.rectTransform, new Vector2(left, 0.25f), new Vector2(left + 0.24f, 0.75f),
                    SlotSymbolArt.CoinColor, _knob).preserveAspect = true;
            }

            var hit = CreateText("Hit Rate", area, new Vector2(0.67f, 0.82f), new Vector2(0.9f, 0.89f), SlotSymbolArt.CoinColor);

            // マス（9×2）。複製元を1つだけ置き、実行時にロック配置の数だけ増やして並べる。
            var cells = CreateRect("Cells", area, new Vector2(0.03f, 0.58f), new Vector2(0.97f, 0.76f));
            var template = CreateCell(cells);

            // 入れられる4図柄（攻撃力・攻撃速度・回復・コイン）。ここからマスへドラッグする。
            var palette = new RectTransform[ReelLocks.Fillable.Length];
            for (var i = 0; i < palette.Length; i++)
            {
                var left = 0.2f + i * 0.155f;
                var item = CreateImage($"Palette {i + 1}", area, new Vector2(left, 0.44f), new Vector2(left + 0.13f, 0.537f),
                    Color.white, _box);
                item.type = Image.Type.Sliced;
                item.raycastTarget = true;
                var art = CreateRect("Art", item.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
                SlotSymbolArt.Create(ReelLocks.Fillable[i], art);
                CreateImage("Frame", item.rectTransform, new Vector2(0f, -0.2f), new Vector2(1f, -0.08f), FrameColor, _box)
                    .gameObject.SetActive(false);
                palette[i] = item.rectTransform;
            }

            // 図柄ごとの印と当たり時の%。
            var percents = new TMP_Text[SlotSymbols.Count];
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var left = 0.04f + s * 0.155f;
                var mark = CreateImage($"Symbol Mark {s + 1}", area, new Vector2(left, 0.23f), new Vector2(left + 0.145f, 0.38f),
                    MarkColor, _box);
                var art = CreateRect("Art", mark.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.95f));
                SlotSymbolArt.Create((SlotSymbol)s, art);
                percents[s] = CreateText("Percent", mark.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.38f),
                    Color.white);
            }

            // ✔: 緑の札に白いチェック（短い棒と長い棒）。空きが残っているあいだは押せない（灰色）。
            var confirmImage = CreateImage("Confirm Button", area, new Vector2(0.36f, 0.07f), new Vector2(0.64f, 0.17f),
                new Color(0.2f, 0.7f, 0.3f), _box);
            confirmImage.raycastTarget = true;
            var confirm = confirmImage.gameObject.AddComponent<Button>();
            confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = confirm.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            confirm.colors = colors;
            // 2本の棒は札の下寄り（幅 0.44・高さ 0.28 の点）で交わる。短い棒は左上へ、長い棒は右上へ伸びる。
            CreateStroke("Check Short", confirmImage.rectTransform, new Vector2(0.387f, 0.39f), 60f, 45f);
            CreateStroke("Check Long", confirmImage.rectTransform, new Vector2(0.546f, 0.547f), 134f, -40f);

            // ドラッグ中の図柄。クリックを遮らないよう CanvasGroup で当たり判定を切る。
            var ghost = CreateRect("Drag Ghost", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ghost.sizeDelta = new Vector2(150f, 150f);
            ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var ghostSymbols = CreateRect("Symbols", ghost, Vector2.zero, Vector2.one);
            SlotSymbolArt.CreateAllSymbols(ghostSymbols);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("cellArea").objectReferenceValue = cells;
            serialized.FindProperty("cellTemplate").objectReferenceValue = template;
            serialized.FindProperty("dragGhost").objectReferenceValue = ghost;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("missText").objectReferenceValue = miss;
            serialized.FindProperty("hitText").objectReferenceValue = hit;
            SetArray(serialized.FindProperty("paletteItems"), palette);
            SetArray(serialized.FindProperty("symbolPercentTexts"), percents);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assign(battle, "reelCustomScreen", screen);
            var guide = Object.FindAnyObjectByType<GuideOverlay>(FindObjectsInactive.Include);
            if (guide != null)
            {
                Assign(guide, "reelCustomScreen", screen);
            }

            // 実行時は BattleController が開く。シーンの編集の邪魔にならないよう閉じて保存する。
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] リールのカスタム画面を作成した（T322）。");
        }

        // マス: 札（ロックは灰色、空き・入れたマスは白）。子に6図柄（Symbols）、選択中の下線（Frame）、鍵（Lock）、空きの点滅枠（Empty）。
        private static RectTransform CreateCell(RectTransform parent)
        {
            var image = CreateImage("Cell Template", parent, new Vector2(0f, 0.5f), new Vector2(0.1f, 1f), Color.white, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var empty = CreateImage("Empty", image.rectTransform, Vector2.zero, Vector2.one, FrameColor, _box);
            empty.type = Image.Type.Sliced;
            CreateImage("Hole", empty.rectTransform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), HoleColor, _box).type =
                Image.Type.Sliced;
            var symbols = CreateRect("Symbols", image.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            SlotSymbolArt.CreateAllSymbols(symbols);

            // 鍵: 右上の小さな暗い札に、輪（つる）。
            var lockIcon = CreateRect("Lock", image.rectTransform, new Vector2(0.62f, 0.66f), new Vector2(0.96f, 0.98f));
            CreateImage("Shackle", lockIcon, new Vector2(0.22f, 0.4f), new Vector2(0.78f, 1f), new Color(0.15f, 0.15f, 0.18f), _knob);
            CreateImage("Body", lockIcon, new Vector2(0.1f, 0f), new Vector2(0.9f, 0.6f), new Color(0.15f, 0.15f, 0.18f), _box);

            CreateImage("Frame", image.rectTransform, new Vector2(0f, -0.16f), new Vector2(1f, -0.05f), FrameColor, _box)
                .gameObject.SetActive(false);
            return image.rectTransform;
        }

        // 太さ 16 の白い棒。length は基準解像度での長さ、angle は縦向きからの回転（正で左に倒れる）。
        private static void CreateStroke(string name, RectTransform parent, Vector2 center, float length, float angle)
        {
            var stroke = CreateImage(name, parent, center, center, Color.white, null);
            stroke.rectTransform.sizeDelta = new Vector2(16f, length);
            stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
