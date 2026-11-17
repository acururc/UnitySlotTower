using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 矩形いっぱいの三角形（既定は右向きの ▶）を描く uGUI 部品。画像を用意せずに開始・強化の印を出すための仮（M5 で素材に差し替える）。
    /// 向きは RectTransform の回転か <see cref="Pointing"/>（矩形を回さずに上下左右へ向ける: T385）で変える。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TriangleGraphic : MaskableGraphic
    {
        public enum Direction { Right, Up, Left, Down }

        [SerializeField] private Direction pointing = Direction.Right;

        /// <summary>尖った先の向き。</summary>
        public Direction Pointing
        {
            get => pointing;
            set
            {
                pointing = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            switch (pointing)
            {
                case Direction.Up:
                    Add(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.center.x, r.yMax));
                    break;
                case Direction.Left:
                    Add(vh, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.center.y));
                    break;
                case Direction.Down:
                    Add(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.center.x, r.yMin));
                    break;
                default:
                    Add(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.center.y));
                    break;
            }
        }

        private void Add(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
