using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ショップマスの画面（plan 16.2・16.7、FR-017、AC-013）。入ったときに並んだ品（<see cref="Shop"/>）を札にして並べ、
    /// 下に両替（<see cref="Exchange"/>、所持金→コイン・コイン→所持金）を置く。買えない品・足りない両替は暗くして押せない。
    /// 文字は使わず、品は職の色の台と白い印（ストック＝＋、ゲージ＝点、強化＝▲）で見分ける。絵は仮（M5 で素材に差し替える）。
    /// </summary>
    public sealed class ShopScreen : NodeScreen
    {
        [Header("Status")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text coinsText;

        [Header("Offers")]
        [Tooltip("品の札を並べる枠（札は開くたびに作り直す）。")]
        [SerializeField] private RectTransform offersArea;
        [SerializeField] private TMP_FontAsset numberFont;
        [SerializeField] private Sprite knobSprite;
        [SerializeField] private Sprite boxSprite;
        [Tooltip("文字あり版で品の名前を出すラベルのひな形（非表示で置き、札ごとに複製する、FR-006）。")]
        [SerializeField] private TextModeLabel offerLabelTemplate;

        [Header("Exchange")]
        [SerializeField] private Button buyCoinsButton;
        [SerializeField] private TMP_Text buyCoinsMoneyText;
        [SerializeField] private TMP_Text buyCoinsCoinsText;
        [SerializeField] private Button sellCoinsButton;
        [SerializeField] private TMP_Text sellCoinsCoinsText;
        [SerializeField] private TMP_Text sellCoinsMoneyText;

        [Header("Look")]
        [Tooltip("色7の色（赤・青・緑・黄）。職の色はその職のスキルの色。")]
        [SerializeField] private Color[] sevenColors =
        {
            new Color(0.9f, 0.05f, 0.1f),
            new Color(0.15f, 0.35f, 0.95f),
            new Color(0.1f, 0.65f, 0.2f),
            new Color(0.95f, 0.75f, 0.05f),
        };

        [SerializeField] private Color cardColor = new Color(0.2f, 0.22f, 0.3f);
        [SerializeField] private Color moneyColor = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.3f;

        private readonly List<(Button Button, CanvasGroup Group, TMP_Text Price)> _cards =
            new List<(Button, CanvasGroup, TMP_Text)>();

        private RunState _state;
        private SkillTable _skills;
        private CanvasGroup _buyGroup;
        private CanvasGroup _sellGroup;
        private int _shownMoney;
        private int _shownCoins;

        /// <summary>開いているショップ（閉じたら null）。</summary>
        public Shop Shop { get; private set; }

        public Exchange Exchange { get; private set; }

        /// <summary>品の札のボタン（<see cref="Shop.Offers"/> の順）。</summary>
        public IReadOnlyList<Button> OfferButtons
        {
            get
            {
                var buttons = new Button[_cards.Count];
                for (var i = 0; i < buttons.Length; i++)
                {
                    buttons[i] = _cards[i].Button;
                }

                return buttons;
            }
        }

        public Button BuyCoinsButton => buyCoinsButton;

        public Button SellCoinsButton => sellCoinsButton;

        public override string Kind => RouteNodeKinds.Shop;

        protected override void Awake()
        {
            base.Awake();
            _buyGroup = GroupOf(buyCoinsButton);
            _sellGroup = GroupOf(sellCoinsButton);
            buyCoinsButton.onClick.AddListener(() => Trade(true));
            sellCoinsButton.onClick.AddListener(() => Trade(false));
        }

        protected override void OnOpen(RunController run, RouteNode node)
        {
            _state = run.State;
            _skills = run.Skills;
            Shop = run.CreateShop();
            Exchange = run.CreateExchange();
            buyCoinsMoneyText.SetText("{0}", Exchange.MoneyPerTrade);
            buyCoinsCoinsText.SetText("{0}", Exchange.CoinsPerTrade);
            sellCoinsCoinsText.SetText("{0}", Exchange.CoinsPerTrade);
            sellCoinsMoneyText.SetText("{0}", Exchange.MoneyPerTrade);
            BuildCards();
            Refresh();
        }

        protected override void OnClose()
        {
            Shop = null;
            Exchange = null;
        }

        private void BuildCards()
        {
            for (var i = offersArea.childCount - 1; i >= 0; i--)
            {
                Destroy(offersArea.GetChild(i).gameObject);
            }

            _cards.Clear();
            var count = Shop.Offers.Count;
            for (var i = 0; i < count; i++)
            {
                var item = Shop.Offers[i];
                var left = i / (float)count;
                var right = (i + 1) / (float)count;
                var pad = 0.04f / count * 3f;
                var card = RouteNodeIcons.CreateImage($"Offer {i + 1} {item.Id}", offersArea,
                    new Vector2(left + pad, 0f), new Vector2(right - pad, 1f), cardColor, boxSprite);
                card.type = Image.Type.Sliced;
                card.raycastTarget = true;
                var button = card.gameObject.AddComponent<Button>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var group = card.gameObject.AddComponent<CanvasGroup>();
                var index = i;
                button.onClick.AddListener(() => Buy(index));

                var icon = RouteNodeIcons.CreateRect("Icon", card.rectTransform, new Vector2(0.12f, 0.3f), new Vector2(0.88f, 0.92f));
                BuildIcon(icon, item);

                var priceIcon = RouteNodeIcons.CreateImage("Money Icon", card.rectTransform, new Vector2(0.1f, 0.06f),
                    new Vector2(0.34f, 0.24f), moneyColor, knobSprite);
                priceIcon.preserveAspect = true;
                var price = CreateNumber("Price", card.rectTransform, new Vector2(0.36f, 0.04f), new Vector2(0.92f, 0.26f));
                AddOfferLabel(card.rectTransform, item);
                _cards.Add((button, group, price));
            }
        }

        private void AddOfferLabel(RectTransform card, ShopItem item)
        {
            if (offerLabelTemplate == null)
            {
                return;
            }

            var label = Instantiate(offerLabelTemplate, card, false);
            label.name = "Text Label";
            label.SetIds("unit_" + item.UnitId, OfferTextId(item.Kind));
            label.gameObject.SetActive(true);
        }

        private static string OfferTextId(ShopItemKind kind)
        {
            switch (kind)
            {
                case ShopItemKind.Stock:
                    return "shop_stock";
                case ShopItemKind.Gauge:
                    return "shop_gauge";
                default:
                    return "shop_upgrade";
            }
        }

        // 職の色の台に白い印。ストック＝十字、ゲージ＝点、強化＝上向きの三角。
        private void BuildIcon(RectTransform icon, ShopItem item)
        {
            var plateColor = _skills != null && _skills.TryGetColor(item.UnitId, out var color) && (int)color < sevenColors.Length
                ? sevenColors[(int)color]
                : Color.gray;
            var plate = RouteNodeIcons.CreateImage("Plate", icon, Vector2.zero, Vector2.one, plateColor,
                item.Kind == ShopItemKind.Gauge ? knobSprite : boxSprite);
            plate.preserveAspect = true;
            var rect = plate.rectTransform;
            switch (item.Kind)
            {
                case ShopItemKind.Stock:
                    RouteNodeIcons.CreateImage("Plus V", rect, new Vector2(0.42f, 0.2f), new Vector2(0.58f, 0.8f), Color.white, boxSprite);
                    RouteNodeIcons.CreateImage("Plus H", rect, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.58f), Color.white, boxSprite);
                    break;
                case ShopItemKind.Gauge:
                    for (var k = 0; k < 3; k++)
                    {
                        var x = 0.22f + k * 0.2f;
                        RouteNodeIcons.CreateImage($"Dot {k + 1}", rect, new Vector2(x, 0.4f), new Vector2(x + 0.16f, 0.6f),
                            Color.white, knobSprite).preserveAspect = true;
                    }

                    break;
                case ShopItemKind.Upgrade:
                    var arrow = RouteNodeIcons.CreateRect("Up", rect, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
                    arrow.localEulerAngles = new Vector3(0f, 0f, 90f);
                    arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
                    break;
            }
        }

        private void Buy(int index)
        {
            ClearSelection();
            if (Shop != null && Shop.TryBuy(index) == ShopResult.Bought)
            {
                Refresh();
            }
        }

        private void Trade(bool buyCoins)
        {
            ClearSelection();
            if (Exchange != null && (buyCoins ? Exchange.TryBuyCoins() : Exchange.TrySellCoins()))
            {
                Refresh();
            }
        }

        // 所持金・コインが変わったら札とボタンを読み直す（変わらないフレームは何もしない）。
        private void Update()
        {
            if (Shop != null && (_state.Money.Coins != _shownMoney || _state.Coins.Coins != _shownCoins))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            _shownMoney = _state.Money.Coins;
            _shownCoins = _state.Coins.Coins;
            moneyText.SetText("{0}", _shownMoney);
            coinsText.SetText("{0}", _shownCoins);
            for (var i = 0; i < _cards.Count; i++)
            {
                var (button, group, price) = _cards[i];
                var result = Shop.Check(i);
                button.interactable = result == ShopResult.Bought;
                group.alpha = result == ShopResult.Bought ? 1f : disabledAlpha;
                var shown = Shop.IsSold(i) || result == ShopResult.Unavailable;
                price.gameObject.SetActive(!shown);
                price.SetText("{0}", Shop.PriceOf(i));
            }

            SetEnabled(buyCoinsButton, _buyGroup, Exchange.CanBuyCoins);
            SetEnabled(sellCoinsButton, _sellGroup, Exchange.CanSellCoins);
        }

        private void SetEnabled(Button button, CanvasGroup group, bool enabled)
        {
            button.interactable = enabled;
            group.alpha = enabled ? 1f : disabledAlpha;
        }

        private TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = RouteNodeIcons.CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = numberFont;
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private static CanvasGroup GroupOf(Button button) =>
            button.TryGetComponent<CanvasGroup>(out var group) ? group : button.gameObject.AddComponent<CanvasGroup>();

        private static void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
