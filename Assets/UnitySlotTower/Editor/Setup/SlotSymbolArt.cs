using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// スロットの6図柄の仮の絵（T315。M5 で素材に差し替える）。リールのコマと操作バーの効果アイコンで同じ絵を使う。
    /// 7＝赤い数字、BAR＝黒い帯、攻撃力アップ＝赤丸に上矢印、攻撃速度アップ＝水色丸に早送り、全体HP回復＝緑丸に十字、コイン＝金色の二重丸。
    /// </summary>
    internal static class SlotSymbolArt
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";

        public static readonly Color AttackColor = new Color(0.9f, 0.25f, 0.2f);
        public static readonly Color SpeedColor = new Color(0.2f, 0.75f, 0.95f);
        public static readonly Color HealColor = new Color(0.25f, 0.75f, 0.3f);
        public static readonly Color CoinColor = new Color(1f, 0.8f, 0.15f);

        private static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        private static Sprite Box => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        /// <summary>
        /// <paramref name="cell"/> の子に6図柄を <see cref="SlotSymbol"/> の順で作り、すべて非表示にする（SlotReelView が番号で切り替える）。
        /// </summary>
        public static void CreateAllSymbols(RectTransform cell)
        {
            for (var s = SlotSymbol.Seven; s <= SlotSymbol.Coin; s++)
            {
                Create(s, cell).gameObject.SetActive(false);
            }
        }

        /// <summary>図柄1つの絵を <paramref name="parent"/> いっぱいに作る。</summary>
        public static RectTransform Create(SlotSymbol symbol, RectTransform parent)
        {
            switch (symbol)
            {
                case SlotSymbol.Seven:
                {
                    var text = CreateRect("Seven", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
                    text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                    text.text = "7";
                    text.color = new Color(0.9f, 0.05f, 0.1f);
                    text.fontStyle = FontStyles.Bold;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 10f;
                    text.fontSizeMax = 200f;
                    text.alignment = TextAlignmentOptions.Center;
                    text.raycastTarget = false;
                    return text.rectTransform;
                }
                case SlotSymbol.Bar:
                    return CreateImage("Bar", parent, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f),
                        new Color(0.1f, 0.1f, 0.12f), Box, false).rectTransform;
                case SlotSymbol.Atk:
                {
                    var root = Circle("Attack", parent, AttackColor);
                    Triangle("Head", root, new Vector2(0.28f, 0.45f), new Vector2(0.72f, 0.82f), 90f);
                    CreateImage("Stem", root, new Vector2(0.42f, 0.2f), new Vector2(0.58f, 0.5f), Color.white, null, false);
                    return root;
                }
                case SlotSymbol.Speed:
                {
                    var root = Circle("Speed", parent, SpeedColor);
                    Triangle("Front", root, new Vector2(0.48f, 0.28f), new Vector2(0.78f, 0.72f), 0f);
                    Triangle("Back", root, new Vector2(0.22f, 0.28f), new Vector2(0.52f, 0.72f), 0f);
                    return root;
                }
                case SlotSymbol.Heal:
                {
                    var root = Circle("Heal", parent, HealColor);
                    CreateImage("Vertical", root, new Vector2(0.42f, 0.2f), new Vector2(0.58f, 0.8f), Color.white, null, false);
                    CreateImage("Horizontal", root, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.58f), Color.white, null, false);
                    return root;
                }
                default:
                {
                    var root = Circle("Coin", parent, CoinColor);
                    CreateImage("Inner", root, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f),
                        new Color(0.85f, 0.6f, 0.05f), Knob, true);
                    return root;
                }
            }
        }

        // 正方形の丸。中の絵はこの丸に対するアンカーで置く。
        private static RectTransform Circle(string name, RectTransform parent, Color color)
        {
            var root = CreateRect(name, parent, Vector2.zero, Vector2.one);
            var fitter = root.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            root.localScale = new Vector3(0.85f, 0.85f, 1f);
            CreateImage("Circle", root, Vector2.zero, Vector2.one, color, Knob, false);
            return root;
        }

        private static void Triangle(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float angle)
        {
            var triangle = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TriangleGraphic>();
            triangle.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            triangle.color = Color.white;
            triangle.raycastTarget = false;
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
            Sprite sprite, bool preserveAspect)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }
    }
}
