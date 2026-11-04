using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンにリザルト（T402）を組む一回限りの手順。Portrait Root の最前面に重ね、BattleController に結ぶ。
    /// 既にあれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// 上から: 結果のアイコン（クリア＝金の王冠、ゲームオーバー＝灰色に赤い×）、旗と到達 S-W、コインと所持数、▶（リトライ）と ◀（タイトル）。
    /// </summary>
    internal static class ResultScreenSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "Result";

        // 結果に集中できるよう、下の戦闘は透かさない。
        private static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.8f, 0.15f);
        private static readonly Color FrameColor = new Color(1f, 0.85f, 0.2f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Result Screen (T402)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] リザルトは既にあるため作成しない。");
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
            var screen = screenRect.gameObject.AddComponent<ResultScreen>();

            // 背景が下の戦闘・操作バー・筐体へのクリックを遮る。
            var panel = CreateImage("Panel", screenRect, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;
            var area = panel.rectTransform;

            // クリア: 金のメダルに白い王冠（上向きの三角3つと台）。
            var clear = CreateImage("Clear Icon", area, new Vector2(0.32f, 0.6f), new Vector2(0.68f, 0.84f), GoldColor, _knob);
            clear.preserveAspect = true;
            // 三角は ▶ を90度回して上に向けるので、矩形の横幅が見た目の高さ、縦幅が見た目の幅になる。
            for (var i = 0; i < 3; i++)
            {
                var x = 0.32f + i * 0.18f;
                var spike = CreateRect($"Crown {i + 1}", clear.rectTransform, new Vector2(x - 0.13f, 0.52f),
                    new Vector2(x + 0.13f, 0.68f));
                spike.localEulerAngles = new Vector3(0f, 0f, 90f);
                spike.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            }

            CreateImage("Crown Base", clear.rectTransform, new Vector2(0.24f, 0.34f), new Vector2(0.76f, 0.46f), Color.white, null);

            // ゲームオーバー: 灰色のメダルに赤い×。
            var gameOver = CreateImage("Game Over Icon", area, new Vector2(0.32f, 0.6f), new Vector2(0.68f, 0.84f),
                new Color(0.35f, 0.35f, 0.4f), _knob);
            gameOver.preserveAspect = true;
            var cross = CreateText("Cross", gameOver.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f),
                new Color(0.95f, 0.2f, 0.2f));
            cross.text = "×";
            cross.alignment = TextAlignmentOptions.Center;
            gameOver.gameObject.SetActive(false);

            // 到達したステージ-ウェーブ: 旗（白い竿と赤い三角）と S-W。
            var flag = CreateRect("Wave Icon", area, new Vector2(0.26f, 0.47f), new Vector2(0.36f, 0.55f));
            CreateImage("Pole", flag, new Vector2(0.2f, 0f), new Vector2(0.3f, 1f), Color.white, null);
            CreateRect("Flag", flag, new Vector2(0.3f, 0.5f), new Vector2(0.9f, 1f)).gameObject.AddComponent<TriangleGraphic>()
                .color = new Color(0.9f, 0.25f, 0.3f);
            var wave = CreateText("Wave", area, new Vector2(0.4f, 0.46f), new Vector2(0.74f, 0.56f), Color.white);
            wave.text = "1-1";

            // 所持コイン。
            CreateImage("Coin Icon", area, new Vector2(0.26f, 0.35f), new Vector2(0.36f, 0.43f), GoldColor, _knob)
                .preserveAspect = true;
            var coins = CreateText("Coins", area, new Vector2(0.4f, 0.34f), new Vector2(0.74f, 0.44f), Color.white);

            // ▶ リトライ・◀ タイトル。キーボードで選んでいる方の下に黄色の線。
            var retry = CreateButton("Retry Button", area, new Vector2(0.14f, 0.14f), new Vector2(0.44f, 0.25f),
                new Color(0.25f, 0.6f, 0.3f), 0f, out var retryFrame);
            var title = CreateButton("Title Button", area, new Vector2(0.56f, 0.14f), new Vector2(0.86f, 0.25f),
                new Color(0.3f, 0.3f, 0.34f), 180f, out var titleFrame);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("clearIcon").objectReferenceValue = clear.gameObject;
            serialized.FindProperty("gameOverIcon").objectReferenceValue = gameOver.gameObject;
            serialized.FindProperty("waveText").objectReferenceValue = wave;
            serialized.FindProperty("coinsText").objectReferenceValue = coins;
            serialized.FindProperty("retryButton").objectReferenceValue = retry;
            serialized.FindProperty("titleButton").objectReferenceValue = title;
            var frames = serialized.FindProperty("selectionFrames");
            frames.arraySize = 2;
            frames.GetArrayElementAtIndex(0).objectReferenceValue = retryFrame;
            frames.GetArrayElementAtIndex(1).objectReferenceValue = titleFrame;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 実行時はゲームが終わると開く。シーンの編集の邪魔にならないよう閉じて保存する。
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] リザルトを作成した。");
        }

        // 札のボタンに白い三角（angle: 0 で ▶、180 で ◀）と、選択中に点く下線。
        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            float angle, out GameObject frame)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var arrow = CreateRect("Arrow", image.rectTransform, new Vector2(0.4f, 0.2f), new Vector2(0.6f, 0.8f));
            arrow.localEulerAngles = new Vector3(0f, 0f, angle);
            arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            frame = CreateImage("Frame", image.rectTransform, new Vector2(0f, -0.2f), new Vector2(1f, -0.08f), FrameColor, _box)
                .gameObject;
            frame.SetActive(false);
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
