using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンにショップと回復の画面（T350c、plan 16.7）を組む一回限りの手順。既にあれば何もしない。
    /// どちらも画面全体（筐体も含む）を覆う2Dのオーバーレイで、左下に ◀。ショップは上端に所持金・コイン、中央に品の札の枠
    /// （札は実行時に ShopScreen が並べる）、その下に両替の2つのボタン。回復は大きな十字と回復量・今のライフ。
    /// RunController に shop_items.csv と画面の一覧を結ぶ。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class NodeScreensSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string ShopTablePath = "Assets/UnitySlotTower/Data/Tables/shop_items.csv";

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.12f, 1f);
        private static readonly Color StripColor = new Color(0.12f, 0.13f, 0.18f, 1f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.22f, 0.3f);
        private static readonly Color LifeColor = new Color(0.9f, 0.25f, 0.3f);
        private static readonly Color GoldColor = new Color(0.98f, 0.8f, 0.2f);
        private static readonly Color CoinInnerColor = new Color(0.7f, 0.5f, 0.05f);
        private static readonly Color HealColor = new Color(0.45f, 0.95f, 0.55f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Shop And Rest Screens (T350c)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<NodeScreen>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] ショップか回復の画面は既にあるため作成しない。");
                return;
            }

            var root = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var run = Object.FindAnyObjectByType<RunController>(FindObjectsInactive.Include);
            var route = Object.FindAnyObjectByType<RouteScreen>(FindObjectsInactive.Include);
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>(ShopTablePath);
            if (root == null || run == null || route == null || table == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root / RunController / Route Screen か shop_items.csv が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 筐体（Slot Area）も覆い、結果・説明・一時停止の画面より下に来るよう、Slot Area のすぐ後ろに置く。
            var slotArea = root.Find("Slot Area");
            if (slotArea == null)
            {
                Debug.LogError("[SlotTower Setup] Portrait Root の下に Slot Area が見つからない。");
                return;
            }

            var index = slotArea.GetSiblingIndex() + 1;
            var shop = BuildShop(root);
            shop.transform.SetSiblingIndex(index);
            var rest = BuildRest(root);
            rest.transform.SetSiblingIndex(index + 1);

            var runSerialized = new SerializedObject(run);
            runSerialized.FindProperty("shopItemsTable").objectReferenceValue = table;
            SetArray(runSerialized.FindProperty("nodeScreens"), new Object[] { shop, rest });
            runSerialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] ショップと回復の画面を作成した。");
        }

        private static ShopScreen BuildShop(RectTransform root)
        {
            var screenRect = CreateRect("Shop Screen", root, Vector2.zero, Vector2.one);
            var screen = screenRect.gameObject.AddComponent<ShopScreen>();
            var panel = CreatePanel(screenRect);
            var area = panel.rectTransform;

            // 上端: 所持金・コイン。
            var strip = CreateImage("Status", area, new Vector2(0f, 0.945f), Vector2.one, StripColor, null).rectTransform;
            MoneyIcon("Money Icon", strip, new Vector2(0.03f, 0.2f), new Vector2(0.08f, 0.8f));
            var money = CreateNumber("Money", strip, new Vector2(0.09f, 0.1f), new Vector2(0.3f, 0.9f), TextAlignmentOptions.Left);
            CoinIcon("Coin Icon", strip, new Vector2(0.34f, 0.2f), new Vector2(0.39f, 0.8f));
            var coins = CreateNumber("Coins", strip, new Vector2(0.4f, 0.1f), new Vector2(0.6f, 0.9f), TextAlignmentOptions.Left);

            // 見出し: ショップのアイコン（ルートのマスと同じ袋）。
            var header = CreateRect("Header", area, new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.925f));
            RouteNodeIcons.Create(header, RouteNodeKinds.Shop, _knob, _box);

            var offers = CreateRect("Offers", area, new Vector2(0.06f, 0.47f), new Vector2(0.94f, 0.79f));

            // 両替: 所持金 → コイン、コイン → 所持金。
            var exchange = CreateImage("Exchange", area, new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.42f), StripColor, _box);
            exchange.type = Image.Type.Sliced;
            var (buy, buyFrom, buyTo) = TradeButton("Buy Coins", exchange.rectTransform, 0.03f, true);
            var (sell, sellFrom, sellTo) = TradeButton("Sell Coins", exchange.rectTransform, 0.52f, false);

            var back = BackButton(area);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("backButton").objectReferenceValue = back;
            serialized.FindProperty("moneyText").objectReferenceValue = money;
            serialized.FindProperty("coinsText").objectReferenceValue = coins;
            serialized.FindProperty("offersArea").objectReferenceValue = offers;
            serialized.FindProperty("numberFont").objectReferenceValue = _font;
            serialized.FindProperty("knobSprite").objectReferenceValue = _knob;
            serialized.FindProperty("boxSprite").objectReferenceValue = _box;
            serialized.FindProperty("buyCoinsButton").objectReferenceValue = buy;
            serialized.FindProperty("buyCoinsMoneyText").objectReferenceValue = buyFrom;
            serialized.FindProperty("buyCoinsCoinsText").objectReferenceValue = buyTo;
            serialized.FindProperty("sellCoinsButton").objectReferenceValue = sell;
            serialized.FindProperty("sellCoinsCoinsText").objectReferenceValue = sellFrom;
            serialized.FindProperty("sellCoinsMoneyText").objectReferenceValue = sellTo;
            var colors = serialized.FindProperty("sevenColors");
            colors.arraySize = SlotSymbolArt.SevenColors.Length;
            for (var i = 0; i < colors.arraySize; i++)
            {
                colors.GetArrayElementAtIndex(i).colorValue = SlotSymbolArt.SevenColors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return screen;
        }

        private static RestScreen BuildRest(RectTransform root)
        {
            var screenRect = CreateRect("Rest Screen", root, Vector2.zero, Vector2.one);
            var screen = screenRect.gameObject.AddComponent<RestScreen>();
            var panel = CreatePanel(screenRect);
            var area = panel.rectTransform;

            var icon = CreateRect("Icon", area, new Vector2(0.3f, 0.52f), new Vector2(0.7f, 0.8f));
            RouteNodeIcons.Create(icon, RouteNodeKinds.Rest, _knob, _box);
            var healed = CreateNumber("Healed", area, new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.5f), TextAlignmentOptions.Center);
            healed.color = HealColor;
            CreateImage("Life Icon", area, new Vector2(0.36f, 0.3f), new Vector2(0.44f, 0.37f), LifeColor, _knob).preserveAspect = true;
            var life = CreateNumber("Life", area, new Vector2(0.46f, 0.29f), new Vector2(0.66f, 0.38f), TextAlignmentOptions.Left);

            var back = BackButton(area);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("backButton").objectReferenceValue = back;
            serialized.FindProperty("healedText").objectReferenceValue = healed;
            serialized.FindProperty("lifeText").objectReferenceValue = life;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return screen;
        }

        private static Image CreatePanel(RectTransform screen)
        {
            var panel = CreateImage("Panel", screen, Vector2.zero, Vector2.one, PanelColor, null);
            panel.raycastTarget = true;
            return panel;
        }

        // [所持金 n] ▶ [コイン n]（買う）か [コイン n] ▶ [所持金 n]（売る）。
        private static (Button Button, TextMeshProUGUI From, TextMeshProUGUI To) TradeButton(string name, RectTransform parent,
            float left, bool buyCoins)
        {
            var image = CreateImage(name, parent, new Vector2(left, 0.15f), new Vector2(left + 0.45f, 0.85f), ButtonColor, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            image.gameObject.AddComponent<CanvasGroup>();
            var rect = image.rectTransform;

            Icon(buyCoins, rect, new Vector2(0.04f, 0.3f), new Vector2(0.18f, 0.7f));
            var from = CreateNumber("From", rect, new Vector2(0.19f, 0.2f), new Vector2(0.4f, 0.8f), TextAlignmentOptions.Left);
            var arrow = CreateRect("Arrow", rect, new Vector2(0.43f, 0.35f), new Vector2(0.55f, 0.65f));
            arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            Icon(!buyCoins, rect, new Vector2(0.58f, 0.3f), new Vector2(0.72f, 0.7f));
            var to = CreateNumber("To", rect, new Vector2(0.73f, 0.2f), new Vector2(0.96f, 0.8f), TextAlignmentOptions.Left);
            return (button, from, to);

            static void Icon(bool money, RectTransform parent, Vector2 min, Vector2 max)
            {
                if (money)
                {
                    MoneyIcon("Money Icon", parent, min, max);
                }
                else
                {
                    CoinIcon("Coin Icon", parent, min, max);
                }
            }
        }

        // 左下の ◀（ルート画面に戻る）。
        private static Button BackButton(RectTransform area)
        {
            var image = CreateImage("Back", area, new Vector2(0.03f, 0.03f), new Vector2(0.2f, 0.11f), ButtonColor, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var arrow = CreateRect("Arrow", image.rectTransform, new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.8f));
            arrow.localEulerAngles = new Vector3(0f, 0f, 180f);
            arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            return button;
        }

        private static void MoneyIcon(string name, RectTransform parent, Vector2 min, Vector2 max) =>
            MoneyIconArt.Apply(CreateImage(name, parent, min, max, GoldColor, _knob), _box, _knob);

        // コインは所持金と同じ金色に、内側の暗い輪で見分ける（ルート画面と同じ仮の絵）。
        private static void CoinIcon(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            var icon = CreateImage(name, parent, min, max, GoldColor, _knob);
            icon.preserveAspect = true;
            CreateImage("Inner", icon.rectTransform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), CoinInnerColor, _knob)
                .preserveAspect = true;
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

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = "0";
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = alignment;
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
