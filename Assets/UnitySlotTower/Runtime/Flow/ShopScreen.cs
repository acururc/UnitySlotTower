using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ショップマスの画面（plan 16.2・16.7、FR-017、AC-013）。入ったときに並んだ品（<see cref="Shop"/>）を札にして並べ、
    /// 下に両替（<see cref="Exchange"/>、所持金→コイン・コイン→所持金）を置く。買えない品・足りない両替は暗くして押せない。
    /// 文字は使わず、品は職の色の台と白い印（ストック＝＋、ゲージ＝点、強化＝▲）で見分ける。絵は仮（M5 で素材に差し替える）。
    /// 使い方は画面の絵で伝える（plan 16.10）: 押せる札には値段から品へ向かう上向きの矢印、押せる両替には払う側から受け取る側への矢印を動かす。
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
        [Tooltip("文字あり版で両替を文（「所持金 6 → コイン 3」）で出すラベル（FR-006、なくてもよい）。")]
        [SerializeField] private TextModeLabel buyCoinsRateLabel;
        [SerializeField] private TextModeLabel sellCoinsRateLabel;

        [Header("Look")]
        [Tooltip("色7の色（赤・青・緑・黄）。職の色はその職のスキルの色。")]
        [SerializeField] private Color[] sevenColors =
        {
            new Color(0.9f, 0.05f, 0.1f),
            new Color(0.15f, 0.35f, 0.95f),
            new Color(0.1f, 0.65f, 0.2f),
            new Color(0.95f, 0.75f, 0.05f),
        };

        [SerializeField] private Color moneyColor = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.3f;

        private readonly List<(Button Button, CanvasGroup Group, TMP_Text Price, NudgeArrow Arrow)> _cards =
            new List<(Button, CanvasGroup, TMP_Text, NudgeArrow)>();

        private RunState _state;
        private SkillTable _skills;
        private CanvasGroup _buyGroup;
        private CanvasGroup _sellGroup;
        private NudgeArrow _buyArrow;
        private NudgeArrow _sellArrow;
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

        /// <summary>品の札の矢印（<see cref="Shop.Offers"/> の順、押せる札だけ動く）。</summary>
        public IReadOnlyList<NudgeArrow> OfferArrows
        {
            get
            {
                var arrows = new NudgeArrow[_cards.Count];
                for (var i = 0; i < arrows.Length; i++)
                {
                    arrows[i] = _cards[i].Arrow;
                }

                return arrows;
            }
        }

        /// <summary>「所持金→コイン」の矢印（押せるときだけ動く）。</summary>
        public NudgeArrow BuyCoinsArrow => _buyArrow;

        /// <summary>「コイン→所持金」の矢印（押せるときだけ動く）。</summary>
        public NudgeArrow SellCoinsArrow => _sellArrow;

        /// <summary>文字あり版の両替の文（テスト用）。</summary>
        public TextModeLabel BuyCoinsRateLabel => buyCoinsRateLabel;

        public TextModeLabel SellCoinsRateLabel => sellCoinsRateLabel;

        public override string Kind => RouteNodeKinds.Shop;

        protected override void Awake()
        {
            base.Awake();
            _buyGroup = GroupOf(buyCoinsButton);
            _sellGroup = GroupOf(sellCoinsButton);
            _buyArrow = ArrowOf(buyCoinsButton);
            _sellArrow = ArrowOf(sellCoinsButton);
            buyCoinsButton.onClick.AddListener(() => Trade(true));
            sellCoinsButton.onClick.AddListener(() => Trade(false));
        }

        protected override void OnOpen(RunController run, RouteNode node)
        {
            _state = run.State;
            _skills = run.Skills;
            Shop = run.CreateShop();
            Exchange = run.CreateExchange();
            buyCoinsMoneyText.SetText(NumericText.YenFormat, Exchange.MoneyPerTrade);
            buyCoinsCoinsText.SetText("{0}", Exchange.CoinsPerTrade);
            sellCoinsCoinsText.SetText("{0}", Exchange.CoinsPerTrade);
            sellCoinsMoneyText.SetText(NumericText.YenFormat, Exchange.MoneyPerTrade);
            if (buyCoinsRateLabel != null)
            {
                buyCoinsRateLabel.SetFormat("shop_buy_rate", Exchange.MoneyPerTrade, Exchange.CoinsPerTrade);
            }

            if (sellCoinsRateLabel != null)
            {
                sellCoinsRateLabel.SetFormat("shop_sell_rate", Exchange.CoinsPerTrade, Exchange.MoneyPerTrade);
            }

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
                    new Vector2(left + pad, 0f), new Vector2(right - pad, 1f), UiStyle.ButtonFill, null);
                card.raycastTarget = true;
                var button = card.gameObject.AddComponent<Button>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var group = card.gameObject.AddComponent<CanvasGroup>();
                // 角を切った札の見た目（T387）。押した色の変化は子の Skin に移る。
                UiSkin.Apply(card, UiStyle.ButtonFill, UiStyle.PanelBorder);
                var index = i;
                button.onClick.AddListener(() => Buy(index));

                // 文字あり版では札の上に名前と説明の3行（AddOfferLabel）が入るので、品の絵と矢印を下げる（plan 16.13）。
                var icon = RouteNodeIcons.CreateRect("Icon", card.rectTransform, new Vector2(0.12f, 0.38f), new Vector2(0.88f, 0.94f));
                TextModeRect(icon, new Vector2(0.12f, 0.43f), new Vector2(0.88f, 0.71f));
                BuildIcon(icon, item);

                // 値段（下）→ 品（上）: お金を払うと品が手に入る、を上向きの矢印で示す。
                var arrowRect = RouteNodeIcons.CreateRect("Buy Arrow", card.rectTransform, new Vector2(0.43f, 0.27f),
                    new Vector2(0.57f, 0.36f));
                arrowRect.localEulerAngles = new Vector3(0f, 0f, 90f);
                TextModeRect(arrowRect, new Vector2(0.43f, 0.31f), new Vector2(0.57f, 0.40f));
                var arrowGraphic = arrowRect.gameObject.AddComponent<TriangleGraphic>();
                arrowGraphic.color = moneyColor;
                arrowGraphic.raycastTarget = false;
                var arrow = arrowRect.gameObject.AddComponent<NudgeArrow>();
                arrow.Configure(0.4f, 1.2f);

                MoneyIconArt.Create("Money Icon", card.rectTransform, new Vector2(0.1f, 0.06f), new Vector2(0.34f, 0.24f),
                    boxSprite, knobSprite);
                var price = CreateNumber("Price", card.rectTransform, new Vector2(0.36f, 0.04f), new Vector2(0.92f, 0.26f));
                AddOfferLabel(card.rectTransform, item);
                _cards.Add((button, group, price, arrow));
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
            var kind = OfferTextId(item.Kind);
            label.SetIds("unit_" + item.UnitId, kind, kind + "_desc");
            label.gameObject.SetActive(true);
        }

        // 実行中に作る部品に文字あり版の位置を付ける（シーンの部品は Setup が付ける。NudgeArrow の基準の位置は 0 のままなので両立する）。
        private static void TextModeRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            var noText = TextModeLayout.Read(rect.gameObject);
            var text = noText;
            text.anchorMin = anchorMin;
            text.anchorMax = anchorMax;
            text.offsetMin = Vector2.zero;
            text.offsetMax = Vector2.zero;
            var layout = rect.gameObject.AddComponent<TextModeLayout>();
            layout.Configure(TextModeLayout.Parts.Rect, noText, text);
            layout.Apply(GameSession.TextMode);
        }

        private static string OfferTextId(ShopItemKind kind)
        {
            switch (kind)
            {
                case ShopItemKind.Gauge:
                    return "shop_gauge";
                default:
                    return "shop_upgrade";
            }
        }

        // 職の色の台に白い印。ゲージ＝点、強化＝上向きの三角。
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
            moneyText.SetText(NumericText.YenFormat, _shownMoney);
            coinsText.SetText("{0}", _shownCoins);
            for (var i = 0; i < _cards.Count; i++)
            {
                var (button, group, price, arrow) = _cards[i];
                var result = Shop.Check(i);
                button.interactable = result == ShopResult.Bought;
                arrow.Playing = result == ShopResult.Bought;
                arrow.gameObject.SetActive(!Shop.IsSold(i) && result != ShopResult.Unavailable);
                group.alpha = result == ShopResult.Bought ? 1f : disabledAlpha;
                var shown = Shop.IsSold(i) || result == ShopResult.Unavailable;
                price.gameObject.SetActive(!shown);
                price.SetText(NumericText.YenFormat, Shop.PriceOf(i));
            }

            SetEnabled(buyCoinsButton, _buyGroup, _buyArrow, Exchange.CanBuyCoins);
            SetEnabled(sellCoinsButton, _sellGroup, _sellArrow, Exchange.CanSellCoins);
        }

        private void SetEnabled(Button button, CanvasGroup group, NudgeArrow arrow, bool enabled)
        {
            button.interactable = enabled;
            group.alpha = enabled ? 1f : disabledAlpha;
            if (arrow != null)
            {
                arrow.Playing = enabled;
            }
        }

        // 両替のボタンの中の矢印（Setup が "Arrow" の名前で置く）に動きを付ける。見つからなければ動かさないだけ。
        private static NudgeArrow ArrowOf(Button button)
        {
            var arrow = button.transform.Find("Arrow");
            if (arrow == null)
            {
                return null;
            }

            return arrow.TryGetComponent<NudgeArrow>(out var nudge) ? nudge : arrow.gameObject.AddComponent<NudgeArrow>();
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
