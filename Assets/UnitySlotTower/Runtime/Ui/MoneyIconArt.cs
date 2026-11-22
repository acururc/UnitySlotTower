using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Ui
{
    /// <summary>
    /// 所持金（円）のアイコン（FR-007、D-77、plan 17.1）。コイン（金色の円）と形も色も分けるため、緑のお札の形にする。
    /// 元のアイコンの Image は枠として残して描かず（配置は今までどおり TextModeLayout などが動かす）、
    /// 子にお札（横長の角丸の四角、縦横比は保つ）と中央の丸を足す。何度呼んでも同じ見た目になる。
    /// </summary>
    public static class MoneyIconArt
    {
        public const string BillName = "Bill";
        public const string MarkName = "Mark";

        /// <summary>お札の横 ÷ 縦。</summary>
        public const float BillAspect = 1.6f;

        public static readonly Color BillColor = new Color(0.33f, 0.72f, 0.4f);
        public static readonly Color MarkColor = new Color(0.17f, 0.45f, 0.24f);

        /// <summary><paramref name="icon"/> をお札の絵にする。box は角丸の四角、knob は円のスプライト。</summary>
        public static void Apply(Image icon, Sprite box, Sprite knob)
        {
            icon.enabled = false;
            icon.raycastTarget = false;
            var root = icon.rectTransform;

            var bill = Child(root, BillName);
            var fitter = bill.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = bill.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = BillAspect;
            var billImage = ImageOf(bill);
            billImage.sprite = box;
            billImage.type = box != null && box.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            billImage.color = BillColor;

            var mark = Child(bill, MarkName);
            mark.anchorMin = new Vector2(0.3f, 0.18f);
            mark.anchorMax = new Vector2(0.7f, 0.82f);
            mark.offsetMin = mark.offsetMax = Vector2.zero;
            var markImage = ImageOf(mark);
            markImage.sprite = knob;
            markImage.color = MarkColor;
            markImage.preserveAspect = true;
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null)
            {
                return found;
            }

            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image ImageOf(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.raycastTarget = false;
            return image;
        }
    }
}
