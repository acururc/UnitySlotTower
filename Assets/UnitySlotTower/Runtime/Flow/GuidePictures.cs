using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 説明（T369、FR-004、D-91）で並べる絵。シーンを作り直さずに足すので実行時に作る。絵は仮（M5 で素材に差し替える）。
    /// 作る部品は中心が原点・pivot が左下で、<see cref="GuideOverlay"/> が root のローカル座標の矩形に置く。
    /// </summary>
    internal static class GuidePictures
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.14f, 0.95f);
        private static readonly Color ArrowColor = new Color(1f, 0.9f, 0.3f);

        public const float BlockPanelWidth = 760f;
        public const float BlockRowHeight = 130f;

        /// <summary>職ごとの1本の道のアニメ（T385）: 止められる数だけ敵が止まり、残り1体が通り抜けて本拠地が赤く光る。左端に数（0 は ×）。</summary>
        public static RectTransform BlockPanel(RectTransform parent, IReadOnlyList<(RectTransform art, Color color, int block)> units,
            TMP_FontAsset font, Sprite box)
        {
            var panel = Panel("Block Panel", parent, box);
            for (var i = 0; i < units.Count; i++)
            {
                var y1 = 1f - (float)i / units.Count;
                var y0 = 1f - (float)(i + 1) / units.Count;
                var row = RouteNodeIcons.CreateRect($"Unit {i + 1}", panel, new Vector2(0.03f, y0 + 0.02f),
                    new Vector2(0.97f, y1 - 0.08f));
                DemoLane.Block(RouteNodeIcons.CreateRect("Lane", row, new Vector2(0.12f, 0f), Vector2.one), units[i].art,
                    units[i].color, units[i].block);
                var block = units[i].block;
                var number = RouteNodeIcons.CreateRect("Block", row, new Vector2(0f, 0.1f), new Vector2(0.1f, 0.9f))
                    .gameObject.AddComponent<TextMeshProUGUI>();
                number.font = font;
                number.text = block > 0 ? block.ToString() : "×";
                number.fontSize = 60f;
                number.enableAutoSizing = false;
                number.alignment = TextAlignmentOptions.Center;
                number.color = block > 0 ? Color.white : new Color(1f, 0.45f, 0.4f);
                number.raycastTarget = false;
            }

            return panel;
        }

        /// <summary>右向きの矢印（三角）。向きは回転で変える。</summary>
        public static RectTransform Arrow(RectTransform parent, Color color)
        {
            var rect = RouteNodeIcons.CreateRect("Arrow", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var triangle = rect.gameObject.AddComponent<TriangleGraphic>();
            triangle.color = color;
            triangle.raycastTarget = false;
            return rect;
        }

        public static RectTransform PointerArrow(RectTransform parent) => Arrow(parent, ArrowColor);

        private static RectTransform Panel(string name, RectTransform parent, Sprite box)
        {
            var image = RouteNodeIcons.CreateImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), PanelColor, box);
            image.type = Image.Type.Sliced;
            var rect = image.rectTransform;
            rect.pivot = Vector2.zero;
            return rect;
        }
    }
}
