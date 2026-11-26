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
    /// Game シーンの「Slot Area」にスロット筐体を組む一回限りの手順。既に筐体があれば何もしない。
    /// 旧筐体があるシーンは「Rebuild Slot Cabinet (T348)」でレバーだけの筐体（plan 16.8）に作り直す。
    /// 以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class SlotCabinetSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const int CellCount = 5;
        private const string PaylineName = "Payline Mark";
        private const string LegacyIconName = "Fixed Spins Icon";
        private const string ReelCustomName = "Reel Custom";

        // T348 で消す旧筐体の部品（ストップボタン・ボーナス・払い出し）。
        private static readonly string[] RemovedParts =
            { "Stop Button 1", "Stop Button 2", "Stop Button 3", "Bonus Lamp", "Bonus Games", "Payout" };

        // コインと旧表示の印の位置。
        private static readonly Vector2 CoinIconMin = new Vector2(0.76f, 0.261f);
        private static readonly Vector2 CoinIconMax = new Vector2(0.819f, 0.362f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Slot Cabinet")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] スロット筐体は既にあるため作成しない。");
                return;
            }

            var slotArea = GameObject.Find("Slot Area")?.GetComponent<RectTransform>();
            var window = slotArea != null ? slotArea.Find("Reel Window") as RectTransform : null;
            if (window == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Slot Area / Reel Window が見つからない。");
                return;
            }

            LoadAssets();
            var cabinet = slotArea.gameObject.AddComponent<SlotCabinet>();

            var reels = new SlotReelView[SlotMachine.ReelCount];
            for (var i = 0; i < reels.Length; i++)
            {
                var left = 0.03f + i * 0.32f;
                reels[i] = CreateReel(window, i, new Vector2(left, 0.02f), new Vector2(left + 0.3f, 0.98f));
            }

            var lever = CreateButton("Lever Button", slotArea, new Vector2(0.03f, 0.065f), new Vector2(0.18f, 0.267f),
                new Color(0.85f, 0.85f, 0.88f));
            CreatePaylineMarks(slotArea, window);

            var coin = CreateImage("Coin Icon", slotArea, CoinIconMin, CoinIconMax, new Color(1f, 0.82f, 0.2f), _knob, true);
            var credit = CreateNumber("Credit", slotArea, new Vector2(0.823f, 0.252f), new Vector2(0.986f, 0.371f), Color.white);
            var legacyIcon = CreateLegacyIcon(slotArea);

            var serialized = new SerializedObject(cabinet);
            SetTable(serialized, "gameConfigTable", "game_config.csv");
            SetTable(serialized, "patternsTable", SlotPatterns.FileName);
            SetTable(serialized, "rulesTable", SlotRules.FileName);
            SetTable(serialized, "reelsTable", SlotReels.FileName);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("leverButton").objectReferenceValue = lever;
            SetArray(serialized.FindProperty("reels"), reels);
            serialized.FindProperty("reelWindow").objectReferenceValue = window;
            serialized.FindProperty("coinIcon").objectReferenceValue = coin;
            serialized.FindProperty("fixedSpinsIcon").objectReferenceValue = legacyIcon;
            serialized.FindProperty("creditText").objectReferenceValue = credit;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = slotArea.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] スロット筐体を作成した。");
        }

        /// <summary>
        /// 旧筐体を T348 の筐体に作り直す: ストップボタン・ボーナスのランプと残りゲーム数・払い出し・カスタム画面の残骸を消し、
        /// コマの図柄を新しい6図柄の順に作り直し、旧表示の印への参照を保つ。何度実行しても同じ結果になる。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Setup/Rebuild Slot Cabinet (T348)")]
        private static void Rebuild()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var slotArea = cabinet != null ? (RectTransform)cabinet.transform : null;
            var window = slotArea != null ? slotArea.Find("Reel Window") as RectTransform : null;
            if (window == null)
            {
                Debug.LogError("[SlotTower Setup] スロット筐体の Reel Window が見つからない。");
                return;
            }

            LoadAssets();
            var scene = slotArea.gameObject.scene;
            var removed = 0;
            foreach (var name in RemovedParts)
            {
                var part = slotArea.Find(name);
                if (part != null)
                {
                    Object.DestroyImmediate(part.gameObject);
                    removed++;
                }
            }

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == ReelCustomName && t.gameObject.scene == scene)
                {
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
            }

            foreach (var reel in window.GetComponentsInChildren<SlotReelView>(true))
            {
                var cells = new SerializedObject(reel).FindProperty("cells");
                for (var c = 0; c < cells.arraySize; c++)
                {
                    var cell = (RectTransform)cells.GetArrayElementAtIndex(c).objectReferenceValue;
                    for (var k = cell.childCount - 1; k >= 0; k--)
                    {
                        Object.DestroyImmediate(cell.GetChild(k).gameObject);
                    }

                    SlotSymbolArt.CreateAllSymbols(cell);
                }
            }

            var existingIcon = slotArea.Find(LegacyIconName);
            var legacyIcon = existingIcon != null ? existingIcon.gameObject : CreateLegacyIcon(slotArea);

            var serialized = new SerializedObject(cabinet);
            serialized.FindProperty("reelWindow").objectReferenceValue = window;
            serialized.FindProperty("fixedSpinsIcon").objectReferenceValue = legacyIcon;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var missing = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.gameObject.scene == scene && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                {
                    Debug.LogWarning($"[SlotTower Setup] スクリプトが欠けたオブジェクトが残っている: {t.name}", t);
                    missing++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] スロット筐体を T348 の形に作り直した（消した部品 {removed}、欠けたスクリプト {missing}）。");
        }

        private static void LoadAssets()
        {
            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        // 旧表示の印: 赤い枠に白いリール3本（小さなスロット窓）。文字を使わない（FR-003）。最初は隠しておく。
        private static GameObject CreateLegacyIcon(RectTransform slotArea)
        {
            var frame = CreateImage(LegacyIconName, slotArea, CoinIconMin, CoinIconMax, new Color(0.85f, 0.2f, 0.2f), _box, false);
            for (var i = 0; i < SlotMachine.ReelCount; i++)
            {
                var left = 0.14f + i * 0.255f;
                CreateImage($"Reel {i + 1}", frame.rectTransform, new Vector2(left, 0.2f), new Vector2(left + 0.21f, 0.8f),
                    Color.white, null, false);
            }

            frame.gameObject.SetActive(false);
            return frame.gameObject;
        }

        private const string BetButtonName = "Bet Button";
        private const string LineMarkName = "Line Mark";
        private const string LeverHintName = "Lever Hint";
        private static readonly string[] RowNames = { "Top", "Middle", "Bottom" };

        /// <summary>
        /// spec 011: BET ボタン（コインの絵1〜3枚）・有効ラインの印（左右に上・中・下）・見せ場のレバーの矢印を足し、筐体に結ぶ。
        /// 旧の中段だけの印は隠す。何度実行しても同じ結果になる（前の部品は消して作り直す）。位置は仮置き。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Setup/Update Slot Bet (spec 011)")]
        private static void UpdateSlotBet()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var slotArea = cabinet != null ? (RectTransform)cabinet.transform : null;
            var window = slotArea != null ? slotArea.Find("Reel Window") as RectTransform : null;
            if (window == null)
            {
                Debug.LogError("[SlotTower Setup] スロット筐体の Reel Window が見つからない。");
                return;
            }

            LoadAssets();
            for (var i = slotArea.childCount - 1; i >= 0; i--)
            {
                var child = slotArea.GetChild(i);
                if (child.name == BetButtonName || child.name.StartsWith(LineMarkName) || child.name == LeverHintName)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
                else if (child.name.StartsWith(PaylineName))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // BET ボタン: レバーのすぐ右に同じ高さで（リールにかぶらない）。押すと 1→2→3→1。
            // 左から1〜3枚目の場所に、賭けていない分は黄色くない丸の枠、賭けた分はその上にコインの絵を出す。
            var leverRect = cabinet.LeverRect;
            var bet = CreateButton(BetButtonName, slotArea, new Vector2(leverRect.anchorMax.x + 0.015f, leverRect.anchorMin.y),
                new Vector2(leverRect.anchorMax.x + 0.2f, leverRect.anchorMax.y), new Color(0.25f, 0.22f, 0.3f));
            bet.GetComponent<Image>().sprite = _box;
            bet.GetComponent<Image>().type = Image.Type.Sliced;
            bet.GetComponent<Image>().preserveAspect = false;
            var coins = new Object[SlotMachine.MaxBet];
            var frames = new Object[SlotMachine.MaxBet];
            for (var i = 0; i < coins.Length; i++)
            {
                var left = 0.05f + i * 0.31f;
                var min = new Vector2(left, 0.15f);
                var max = new Vector2(left + 0.28f, 0.85f);
                // 丸の枠: 灰色の丸の内側をボタンの色でくり抜く。
                var frame = CreateImage($"Bet Slot {i + 1}", (RectTransform)bet.transform, min, max, new Color(0.7f, 0.7f, 0.75f), _knob, true);
                CreateImage("Hole", frame.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f),
                    new Color(0.25f, 0.22f, 0.3f), _knob, true);
                frames[i] = frame.gameObject;
                coins[i] = CreateImage($"Bet Coin {i + 1}", (RectTransform)bet.transform, min, max, new Color(1f, 0.82f, 0.2f), _knob, true)
                    .gameObject;
            }

            // 有効ラインの印: 窓の左右、上段・中段・下段の高さに三角（左の3つ、右の3つの順）。
            var marks = new Object[SlotMachine.RowCount * 2];
            var windowHeight = window.anchorMax.y - window.anchorMin.y;
            var height = windowHeight * 0.06f;
            for (var side = 0; side < 2; side++)
            {
                for (var r = 0; r < SlotMachine.RowCount; r++)
                {
                    var y = window.anchorMax.y - windowHeight * (r + 0.5f) / SlotMachine.RowCount;
                    var edge = side == 0 ? window.anchorMin.x : window.anchorMax.x;
                    var min = new Vector2(side == 0 ? edge - 0.035f : edge + 0.005f, y - height);
                    var max = new Vector2(side == 0 ? edge - 0.005f : edge + 0.035f, y + height);
                    var mark = CreateRect($"{LineMarkName} {RowNames[r]} {(side == 0 ? "Left" : "Right")}", slotArea, min, max)
                        .gameObject.AddComponent<TriangleGraphic>();
                    mark.color = new Color(1f, 1f, 1f, 0.12f);
                    mark.raycastTarget = false;
                    if (side == 1)
                    {
                        mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                    }

                    marks[side * SlotMachine.RowCount + r] = mark;
                }
            }

            // 見せ場の矢印: レバーの下から上を指す三角（右は BET ボタン）。
            var leverWidth = leverRect.anchorMax.x - leverRect.anchorMin.x;
            var hintMin = new Vector2(leverRect.anchorMin.x + leverWidth * 0.3f, leverRect.anchorMin.y - 0.09f);
            var hintMax = new Vector2(leverRect.anchorMin.x + leverWidth * 0.7f, leverRect.anchorMin.y - 0.01f);
            var hint = CreateRect(LeverHintName, slotArea, hintMin, hintMax).gameObject.AddComponent<TriangleGraphic>();
            hint.color = new Color(1f, 0.85f, 0.2f);
            hint.raycastTarget = false;
            hint.Pointing = TriangleGraphic.Direction.Up;
            hint.gameObject.SetActive(false);

            var serialized = new SerializedObject(cabinet);
            serialized.FindProperty("betButton").objectReferenceValue = bet;
            SetArray(serialized.FindProperty("betCoins"), coins);
            SetArray(serialized.FindProperty("betSlots"), frames);
            SetArray(serialized.FindProperty("lineMarks"), marks);
            serialized.FindProperty("leverHint").objectReferenceValue = hint.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = slotArea.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] BET ボタン・ラインの印・レバーの矢印を作った（spec 011）。");
        }

        // 中段（有効ライン）の高さで、リール窓の左右から内向きの三角を出す。
        private static void CreatePaylineMarks(RectTransform slotArea, RectTransform window)
        {
            var y = Mathf.Lerp(window.anchorMin.y, window.anchorMax.y, 0.5f);
            var height = (window.anchorMax.y - window.anchorMin.y) * 0.08f;
            for (var side = 0; side < 2; side++)
            {
                var edge = side == 0 ? window.anchorMin.x : window.anchorMax.x;
                var min = new Vector2(side == 0 ? edge - 0.035f : edge + 0.005f, y - height);
                var max = new Vector2(side == 0 ? edge - 0.005f : edge + 0.035f, y + height);
                var mark = CreateRect($"{PaylineName} {(side == 0 ? "Left" : "Right")}", slotArea, min, max)
                    .gameObject.AddComponent<TriangleGraphic>();
                mark.color = new Color(1f, 0.85f, 0.2f);
                mark.raycastTarget = false;
                if (side == 1)
                {
                    mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                }
            }
        }

        private static SlotReelView CreateReel(RectTransform window, int index, Vector2 anchorMin, Vector2 anchorMax)
        {
            var reel = CreateImage($"Reel {index + 1}", window, anchorMin, anchorMax, Color.white, null, false);
            reel.gameObject.AddComponent<RectMask2D>();
            var view = reel.gameObject.AddComponent<SlotReelView>();
            var cells = new RectTransform[CellCount];
            for (var c = 0; c < CellCount; c++)
            {
                var cell = CreateRect($"Cell {c - CellCount / 2}", reel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
                cells[c] = cell;
                SlotSymbolArt.CreateAllSymbols(cell);
            }

            var serialized = new SerializedObject(view);
            SetArray(serialized.FindProperty("cells"), cells);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);
            return rect;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite, bool preserveAspect)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _knob, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
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

        private static void SetTable(SerializedObject serialized, string property, string fileName) =>
            serialized.FindProperty(property).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + fileName);

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
