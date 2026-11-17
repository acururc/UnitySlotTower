using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// スキルの効果の絵（T385、FR-072、plan 18.5）。skills.csv の id（色7）ごとに図形で描く仮の絵（M5 で素材に差し替える）。
    /// 赤＝攻撃が強くなる（星形の衝撃）、青＝攻撃が速くなる（早送りの ▶▶）、緑＝味方を強くする（上向きの矢印）、黄＝守りが固くなる（盾）。
    /// 実行時に作る（シーンを作り直さずに足せ、文字なしの説明の板でも同じ絵を使う）。
    /// </summary>
    public static class SkillPictures
    {
        private static readonly Color Plate = new Color(0.06f, 0.07f, 0.1f, 0.95f);

        /// <summary>parent いっぱい（正方形に収める）に、丸い下地と色7の色の効果の絵を作る。knob は丸の絵（null なら四角）。</summary>
        public static RectTransform Create(RectTransform parent, SlotSymbol skill, Color color, Sprite knob)
        {
            var root = Rect("Skill Art", parent, Vector2.zero, Vector2.one);
            var fit = root.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            var plate = Image("Plate", root, Vector2.zero, Vector2.one, Plate, knob);
            var ring = Image("Ring", plate.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), color, knob);
            Image("Inner", ring.rectTransform, new Vector2(0.09f, 0.09f), new Vector2(0.91f, 0.91f), Plate, knob);
            var glyph = Rect("Glyph", root, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            switch (skill)
            {
                case SlotSymbol.Red7:
                    Burst(glyph, color);
                    break;
                case SlotSymbol.Blue7:
                    Triangle("Fast 1", glyph, new Vector2(0.02f, 0.15f), new Vector2(0.5f, 0.85f), TriangleGraphic.Direction.Right, color);
                    Triangle("Fast 2", glyph, new Vector2(0.48f, 0.15f), new Vector2(0.96f, 0.85f), TriangleGraphic.Direction.Right, color);
                    break;
                case SlotSymbol.Green7:
                    Triangle("Head", glyph, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 1f), TriangleGraphic.Direction.Up, color);
                    Image("Stem", glyph, new Vector2(0.34f, 0f), new Vector2(0.66f, 0.56f), color, null);
                    break;
                default:
                    Shield(glyph, color);
                    break;
            }

            return root;
        }

        // 8つの角の星（四角を2枚、45°ずらして重ねる）と白い芯。
        private static void Burst(RectTransform parent, Color color)
        {
            Image("Square", parent, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), color, null);
            Image("Diamond", parent, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), color, null)
                .rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image("Core", parent, new Vector2(0.36f, 0.36f), new Vector2(0.64f, 0.64f), Color.white, null)
                .rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        // 上が四角・下が下向きの三角の盾と、白い縦の帯。
        private static void Shield(RectTransform parent, Color color)
        {
            Image("Top", parent, new Vector2(0.12f, 0.45f), new Vector2(0.88f, 1f), color, null);
            Triangle("Point", parent, new Vector2(0.12f, 0f), new Vector2(0.88f, 0.47f), TriangleGraphic.Direction.Down, color);
            Image("Stripe", parent, new Vector2(0.44f, 0.22f), new Vector2(0.56f, 0.9f), Color.white, null);
        }

        internal static TriangleGraphic Triangle(string name, RectTransform parent, Vector2 min, Vector2 max,
            TriangleGraphic.Direction pointing, Color color)
        {
            var triangle = Rect(name, parent, min, max).gameObject.AddComponent<TriangleGraphic>();
            triangle.Pointing = pointing;
            triangle.color = color;
            triangle.raycastTarget = false;
            return triangle;
        }

        internal static RectTransform Rect(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image Image(string name, RectTransform parent, Vector2 min, Vector2 max, Color color, Sprite sprite)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
