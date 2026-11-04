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
    /// Game シーンの「Slot Area」に仮のスロット筐体（T110、T315 で作り直し）を組む一回限りの手順。既に筐体があれば何もしない。
    /// T110 の筐体があるシーンは「Update Slot Cabinet (T315)」で新しい図柄・ボーナス表示・有効ラインの印に置き換える。
    /// 以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class SlotCabinetSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const int CellCount = 5;
        private const string PaylineName = "Payline Mark";
        private const string BonusGamesName = "Bonus Games";

        // ボーナスのランプと残りゲーム数はリール窓の上に並べる（T315）。
        private static readonly Vector2 BonusLampMin = new Vector2(0.36f, 0.91f);
        private static readonly Vector2 BonusLampMax = new Vector2(0.48f, 0.98f);
        private static readonly Vector2 BonusGamesMin = new Vector2(0.5f, 0.905f);
        private static readonly Vector2 BonusGamesMax = new Vector2(0.66f, 0.985f);

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
            var stops = new Button[SlotMachine.ReelCount];
            for (var i = 0; i < reels.Length; i++)
            {
                var left = 0.03f + i * 0.32f;
                reels[i] = CreateReel(window, i, new Vector2(left, 0.02f), new Vector2(left + 0.3f, 0.98f));

                // ストップボタンは各リールの真下に置く。
                var center = Mathf.Lerp(window.anchorMin.x, window.anchorMax.x, left + 0.15f);
                stops[i] = CreateButton($"Stop Button {i + 1}", slotArea, new Vector2(center - 0.07f, 0.2f),
                    new Vector2(center + 0.07f, 0.3f), new Color(0.85f, 0.15f, 0.15f));
            }

            var lever = CreateButton("Lever Button", slotArea, new Vector2(0.04f, 0.04f), new Vector2(0.2f, 0.16f),
                new Color(0.85f, 0.85f, 0.88f));

            var bonus = CreateImage("Bonus Lamp", slotArea, BonusLampMin, BonusLampMax, new Color(1f, 0.2f, 0.2f), _knob, true);
            CreatePaylineMarks(slotArea, window);

            var coin = CreateImage("Coin Icon", slotArea, new Vector2(0.1f, 0.365f), new Vector2(0.17f, 0.425f),
                new Color(1f, 0.82f, 0.2f), _knob, true);
            var credit = CreateNumber("Credit", slotArea, new Vector2(0.18f, 0.36f), new Vector2(0.42f, 0.43f), Color.white);
            var payout = CreateNumber("Payout", slotArea, new Vector2(0.45f, 0.36f), new Vector2(0.65f, 0.43f),
                new Color(1f, 0.82f, 0.2f));
            var counter = CreateNumber(BonusGamesName, slotArea, BonusGamesMin, BonusGamesMax, new Color(1f, 0.2f, 0.2f));

            var serialized = new SerializedObject(cabinet);
            SetTable(serialized, "gameConfigTable", "game_config.csv");
            SetTable(serialized, "settingsTable", SlotSettings.FileName);
            SetTable(serialized, "rulesTable", SlotRules.FileName);
            SetTable(serialized, "lockedReelTable", ReelLocks.FileName);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("leverButton").objectReferenceValue = lever;
            SetArray(serialized.FindProperty("stopButtons"), stops);
            SetArray(serialized.FindProperty("reels"), reels);
            serialized.FindProperty("bonusLamp").objectReferenceValue = bonus;
            serialized.FindProperty("coinIcon").objectReferenceValue = coin;
            serialized.FindProperty("creditText").objectReferenceValue = credit;
            serialized.FindProperty("payoutText").objectReferenceValue = payout;
            serialized.FindProperty("bonusGamesText").objectReferenceValue = counter;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = slotArea.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] スロット筐体を作成した。");
        }

        /// <summary>
        /// T110 の筐体を T315 の表示に置き換える: 先バレ・確変・リプレイのランプと音源を消し、コマの図柄を6図柄に作り直し、
        /// ボーナスのランプと残りゲーム数をリール窓の上に並べ、中段（有効ライン）の左右に印を置く。済んでいれば何もしない。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Setup/Update Slot Cabinet (T315)")]
        private static void UpdateCabinet()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var slotArea = cabinet != null ? (RectTransform)cabinet.transform : null;
            var window = slotArea != null ? slotArea.Find("Reel Window") as RectTransform : null;
            if (window == null)
            {
                Debug.LogError("[SlotTower Setup] スロット筐体の Reel Window が見つからない。");
                return;
            }

            if (slotArea.Find(PaylineName + " Left") != null)
            {
                Debug.LogWarning("[SlotTower Setup] スロット筐体は T315 の表示になっているため何もしない。");
                return;
            }

            LoadAssets();
            foreach (var name in new[] { "Premonition Lamp", "Kakuhen Lamp", "Replay Lamp" })
            {
                var lamp = slotArea.Find(name);
                if (lamp != null)
                {
                    Object.DestroyImmediate(lamp.gameObject);
                }
            }

            var audio = slotArea.GetComponent<AudioSource>();
            if (audio != null)
            {
                Object.DestroyImmediate(audio);
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

            var serialized = new SerializedObject(cabinet);
            var bonusLamp = (Graphic)serialized.FindProperty("bonusLamp").objectReferenceValue;
            SetAnchors(bonusLamp.rectTransform, BonusLampMin, BonusLampMax);
            var counter = (TMP_Text)serialized.FindProperty("bonusGamesText").objectReferenceValue;
            counter.name = BonusGamesName;
            SetAnchors(counter.rectTransform, BonusGamesMin, BonusGamesMax);
            CreatePaylineMarks(slotArea, window);

            var scene = slotArea.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] スロット筐体を T315 の表示に更新した。");
        }

        private static void LoadAssets()
        {
            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
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
