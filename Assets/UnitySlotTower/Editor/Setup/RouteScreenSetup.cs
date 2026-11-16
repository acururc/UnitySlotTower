using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンにルート画面（T350b、plan 16.7）を組む一回限りの手順。既にあれば何もしない。
    /// 戦闘フィールドと操作バー（上 68%）を覆うオーバーレイ（上端にライフ・所持金・コイン・ゲージ、中央にマップの枠、
    /// 下端の操作バーの位置に各リールの真上の矢印と行き先の枠）と、筐体の各リールに重ねるルート選びのボタンと暗幕を作り、
    /// RunController・SlotCabinet に結ぶ。マップのマスは実行時に RouteScreen が並べる。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class RouteScreenSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string RootName = "Route Screen";

        // 操作バーの下端（Game シーンの 6:4 配置: GameSceneSetup.SlotAreaTop）から上を覆う。
        private const float ScreenBottom = 0.32f;

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.12f, 1f);
        private static readonly Color StripColor = new Color(0.12f, 0.13f, 0.18f, 1f);
        private static readonly Color LifeColor = new Color(0.9f, 0.25f, 0.3f);
        private static readonly Color GoldColor = new Color(0.98f, 0.8f, 0.2f);
        private static readonly Color ArrowButtonColor = new Color(0.2f, 0.22f, 0.3f);
        private static readonly float[] ArrowAngles = { 135f, 90f, 45f };

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Route Screen (T350b)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<RouteScreen>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] ルート画面は既にあるため作成しない。");
                return;
            }

            var root = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            var run = Object.FindAnyObjectByType<RunController>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            if (root == null || bar == null || run == null || cabinet == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / Operation Bar / RunController / SlotCabinet が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 結果・説明・一時停止の画面より下に来るよう、操作バーのすぐ後ろに置く。
            var screenRect = CreateRect(RootName, root, new Vector2(0f, ScreenBottom), Vector2.one);
            screenRect.SetSiblingIndex(bar.GetSiblingIndex() + 1);
            var screen = screenRect.gameObject.AddComponent<RouteScreen>();

            var panel = CreateImage("Panel", screenRect, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;
            var area = panel.rectTransform;

            // 上端: ライフ・所持金・コイン・ゲージ。
            var strip = CreateImage("Status", area, new Vector2(0f, 0.92f), Vector2.one, StripColor, null).rectTransform;
            CreateImage("Life Icon", strip, new Vector2(0.02f, 0.2f), new Vector2(0.07f, 0.8f), LifeColor, _knob).preserveAspect = true;
            var life = CreateNumber("Life", strip, new Vector2(0.075f, 0.1f), new Vector2(0.19f, 0.9f));
            MoneyIconArt.Apply(CreateImage("Money Icon", strip, new Vector2(0.21f, 0.2f), new Vector2(0.26f, 0.8f), GoldColor, _knob), _box, _knob);
            var money = CreateNumber("Money", strip, new Vector2(0.265f, 0.1f), new Vector2(0.42f, 0.9f));
            // コインは筐体のコインと同じ金色に、内側の暗い輪で所持金と見分ける（仮）。
            var coinIcon = CreateImage("Coin Icon", strip, new Vector2(0.44f, 0.2f), new Vector2(0.49f, 0.8f), GoldColor, _knob);
            coinIcon.preserveAspect = true;
            CreateImage("Inner", coinIcon.rectTransform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f),
                new Color(0.7f, 0.5f, 0.05f), _knob).preserveAspect = true;
            var coins = CreateNumber("Coins", strip, new Vector2(0.495f, 0.1f), new Vector2(0.62f, 0.9f));

            const int dotsPerColor = 5;
            var dots = new Image[SlotSymbolArt.SevenColors.Length * dotsPerColor];
            for (var c = 0; c < SlotSymbolArt.SevenColors.Length; c++)
            {
                var left = 0.64f + c * 0.087f;
                var group = CreateRect($"Gauge {c + 1}", strip, new Vector2(left, 0.3f), new Vector2(left + 0.08f, 0.7f));
                for (var k = 0; k < dotsPerColor; k++)
                {
                    var x = k / (float)dotsPerColor;
                    var dot = CreateImage($"Dot {k + 1}", group, new Vector2(x + 0.02f, 0f), new Vector2(x + 0.18f, 1f),
                        SlotSymbolArt.SevenColors[c], _knob);
                    dot.preserveAspect = true;
                    dots[c * dotsPerColor + k] = dot;
                }
            }

            // 中央: マップの枠（マスは実行時に並べる）。
            var map = CreateRect("Map", area, new Vector2(0.06f, 0.135f), new Vector2(0.94f, 0.905f));

            // 下端: 各リールの真上に矢印と行き先の枠。
            var arrowButtons = new Button[SlotMachine.ReelCount];
            var arrowGroups = new CanvasGroup[SlotMachine.ReelCount];
            var arrowTargets = new RectTransform[SlotMachine.ReelCount];
            var reels = ReelRects(cabinet);
            for (var i = 0; i < SlotMachine.ReelCount; i++)
            {
                var center = CenterX(reels[i], area);
                var button = CreateImage($"Direction {i + 1}", area, new Vector2(center - 0.08f, 0.012f),
                    new Vector2(center + 0.08f, 0.108f), ArrowButtonColor, _box);
                button.type = Image.Type.Sliced;
                button.raycastTarget = true;
                arrowButtons[i] = button.gameObject.AddComponent<Button>();
                arrowButtons[i].navigation = new Navigation { mode = Navigation.Mode.None };
                arrowGroups[i] = button.gameObject.AddComponent<CanvasGroup>();
                // 回す前の ▶ を横長にして先を尖らせ、斜めでも向きが読めるようにする。
                var arrow = CreateRect("Arrow", button.rectTransform, new Vector2(0.06f, 0.3f), new Vector2(0.46f, 0.7f));
                arrow.localEulerAngles = new Vector3(0f, 0f, ArrowAngles[i]);
                arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
                arrowTargets[i] = CreateRect("Target", button.rectTransform, new Vector2(0.52f, 0.1f), new Vector2(0.92f, 0.9f));
            }

            // 筐体: 各リールに重ねる透明のボタン（クリックで止める）と、行き先のないリールの暗幕。
            var picks = new Button[SlotMachine.ReelCount];
            var dims = new GameObject[SlotMachine.ReelCount];
            for (var i = 0; i < SlotMachine.ReelCount; i++)
            {
                var dim = CreateImage("Route Dim", reels[i], Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.65f), null);
                dim.gameObject.SetActive(false);
                dims[i] = dim.gameObject;
                var pick = CreateImage("Route Pick", reels[i], Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), null);
                pick.raycastTarget = true;
                picks[i] = pick.gameObject.AddComponent<Button>();
                picks[i].navigation = new Navigation { mode = Navigation.Mode.None };
                picks[i].transition = Selectable.Transition.None;
                pick.gameObject.SetActive(false);
            }

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("mapArea").objectReferenceValue = map;
            serialized.FindProperty("knobSprite").objectReferenceValue = _knob;
            serialized.FindProperty("boxSprite").objectReferenceValue = _box;
            serialized.FindProperty("lifeText").objectReferenceValue = life;
            serialized.FindProperty("moneyText").objectReferenceValue = money;
            serialized.FindProperty("coinsText").objectReferenceValue = coins;
            SetArray(serialized.FindProperty("gaugeDots"), dots);
            SetArray(serialized.FindProperty("arrowButtons"), arrowButtons);
            SetArray(serialized.FindProperty("arrowGroups"), arrowGroups);
            SetArray(serialized.FindProperty("arrowTargets"), arrowTargets);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var cabinetSerialized = new SerializedObject(cabinet);
            SetArray(cabinetSerialized.FindProperty("routeReelButtons"), picks);
            SetArray(cabinetSerialized.FindProperty("routeReelDims"), dims);
            cabinetSerialized.ApplyModifiedPropertiesWithoutUndo();

            var runSerialized = new SerializedObject(run);
            runSerialized.FindProperty("routeScreen").objectReferenceValue = screen;
            runSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 実行時は戦闘マスが終わると開く。シーンの編集の邪魔にならないよう閉じて保存する。
            panel.gameObject.SetActive(false);
            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] ルート画面を作成した。");
        }

        private static RectTransform[] ReelRects(SlotCabinet cabinet)
        {
            var property = new SerializedObject(cabinet).FindProperty("reels");
            var rects = new RectTransform[property.arraySize];
            for (var i = 0; i < rects.Length; i++)
            {
                rects[i] = (RectTransform)((Component)property.GetArrayElementAtIndex(i).objectReferenceValue).transform;
            }

            return rects;
        }

        // リールの中心の x を、area の左端 0・右端 1 の割合で返す（同じキャンバスの中なのでワールド座標で比べる）。
        private static float CenterX(RectTransform reel, RectTransform area)
        {
            var reelCorners = new Vector3[4];
            var areaCorners = new Vector3[4];
            reel.GetWorldCorners(reelCorners);
            area.GetWorldCorners(areaCorners);
            var center = (reelCorners[0].x + reelCorners[2].x) / 2f;
            return (center - areaCorners[0].x) / (areaCorners[2].x - areaCorners[0].x);
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

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = "0";
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Left;
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
