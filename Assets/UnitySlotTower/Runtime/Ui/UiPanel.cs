using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Ui
{
    /// <summary>角を斜めに切ったパネル（T383、plan 18.4）。地の色は color、内側に細い縁（borderColor）、左端に色の帯（accent、任意）。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiPanel : MaskableGraphic
    {
        [Flags]
        public enum Corners { None = 0, TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8 }

        [SerializeField] private Corners corners = Corners.TopLeft | Corners.BottomRight;
        [SerializeField] private float cut = UiStyle.CornerCut;
        [SerializeField] private float borderWidth = UiStyle.BorderWidth;
        [SerializeField] private Color borderColor = UiStyle.PanelBorder;
        [SerializeField] private Color accentColor = UiStyle.Ready;
        [SerializeField] private float accentWidth;

        private readonly Vector2[] _outer = new Vector2[8];
        private readonly Vector2[] _inner = new Vector2[8];
        private readonly Vector2[] _accent = new Vector2[10];

#if UNITY_EDITOR
        // 付けたときの既定（コンストラクタでは gameObject に触れられないため Reset で置く）。
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            color = UiStyle.PanelFill;
        }
#endif

        public void SetBorder(Color value)
        {
            borderColor = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            var half = Mathf.Min(rect.width, rect.height) * 0.5f;
            var cornerCut = Mathf.Clamp(cut, 0f, half);
            var border = Mathf.Clamp(borderWidth, 0f, half);
            Shape(_outer, rect, cornerCut);
            if (border >= half)
            {
                Fill(vh, _outer, 8, borderColor);
                return;
            }

            var inside = Rect.MinMaxRect(rect.xMin + border, rect.yMin + border,
                rect.xMax - border, rect.yMax - border);
            // Offset diagonal edges by the same perpendicular distance as straight edges.
            var innerCut = Mathf.Clamp(cornerCut - (2f - Mathf.Sqrt(2f)) * border,
                0f, Mathf.Min(inside.width, inside.height) * 0.5f);
            Shape(_inner, inside, innerCut);
            Fill(vh, _inner, 8, color);
            if (border > 0f)
            {
                var start = vh.currentVertCount;
                for (var i = 0; i < 8; i++)
                {
                    vh.AddVert(_outer[i], borderColor, Vector2.zero);
                    vh.AddVert(_inner[i], borderColor, Vector2.zero);
                }
                for (var i = 0; i < 8; i++)
                {
                    var a = start + i * 2;
                    var b = start + ((i + 1) % 8) * 2;
                    vh.AddTriangle(a, b, b + 1);
                    vh.AddTriangle(a, b + 1, a + 1);
                }
            }

            if (accentWidth <= 0f) return;
            // Clip the accent to the inner polygon, including the cut corners.
            var right = inside.xMin + Mathf.Min(accentWidth, inside.width);
            var count = 0;
            var previous = _inner[7];
            for (var i = 0; i < 8; i++)
            {
                var current = _inner[i];
                if ((previous.x <= right) != (current.x <= right))
                {
                    _accent[count++] = Vector2.Lerp(previous, current,
                        (right - previous.x) / (current.x - previous.x));
                }
                if (current.x <= right) _accent[count++] = current;
                previous = current;
            }
            Fill(vh, _accent, count, accentColor);
        }

        private void Shape(Vector2[] points, Rect rect, float size)
        {
            var tl = (corners & Corners.TopLeft) != 0 ? size : 0f;
            var tr = (corners & Corners.TopRight) != 0 ? size : 0f;
            var br = (corners & Corners.BottomRight) != 0 ? size : 0f;
            var bl = (corners & Corners.BottomLeft) != 0 ? size : 0f;
            points[0] = new Vector2(rect.xMin, rect.yMax - tl);
            points[1] = new Vector2(rect.xMin + tl, rect.yMax);
            points[2] = new Vector2(rect.xMax - tr, rect.yMax);
            points[3] = new Vector2(rect.xMax, rect.yMax - tr);
            points[4] = new Vector2(rect.xMax, rect.yMin + br);
            points[5] = new Vector2(rect.xMax - br, rect.yMin);
            points[6] = new Vector2(rect.xMin + bl, rect.yMin);
            points[7] = new Vector2(rect.xMin, rect.yMin + bl);
        }

        private static void Fill(VertexHelper vh, Vector2[] points, int count, Color tint)
        {
            var start = vh.currentVertCount;
            for (var i = 0; i < count; i++) vh.AddVert(points[i], tint, Vector2.zero);
            for (var i = 1; i < count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
