using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 説明（T369、FR-004、D-91）で並べる絵。シーンを作り直さずに足すので実行時に作る。絵は仮（M5 で素材に差し替える）。
    /// 作る部品は中心が原点・pivot が左下で、<see cref="GuideOverlay"/> が root のローカル座標の矩形に置く。
    /// </summary>
    internal static class GuidePictures
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.14f, 0.95f);
        private static readonly Color EnemyColor = new Color(0.6f, 0.15f, 0.3f);
        private static readonly Color BossEnemyColor = new Color(0.4f, 0.05f, 0.1f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.2f);
        private static readonly Color HeartColor = new Color(0.95f, 0.25f, 0.35f);
        private static readonly Color ArrowColor = new Color(1f, 0.9f, 0.3f);

        /// <summary>ルートの説明で並べるマスの種類（最初の戦闘は戦闘と同じ絵なので除く）。</summary>
        public static readonly string[] RouteKinds =
            { RouteNodeKinds.Battle, RouteNodeKinds.Elite, RouteNodeKinds.Shop, RouteNodeKinds.Rest, RouteNodeKinds.Boss };

        public const float BlockColumnWidth = 190f;
        public const float BlockPanelHeight = 300f;
        public const float RouteRowHeight = 150f;
        public const float RoutePanelWidth = 720f;

        /// <summary>職の絵と止められる数（0 は ×）を横に並べた板。</summary>
        public static RectTransform BlockPanel(RectTransform parent, IReadOnlyList<(Image body, int block)> units, TMP_FontAsset font,
            Sprite box)
        {
            var panel = Panel("Block Panel", parent, box);
            for (var i = 0; i < units.Count; i++)
            {
                var x0 = (float)i / units.Count;
                var x1 = (float)(i + 1) / units.Count;
                var column = RouteNodeIcons.CreateRect($"Unit {i + 1}", panel, new Vector2(x0, 0f), new Vector2(x1, 1f));
                var source = units[i].body;
                var body = RouteNodeIcons.CreateImage("Body", column, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.92f),
                    source.color, source.sprite);
                body.preserveAspect = true;
                body.type = Image.Type.Simple;

                var block = units[i].block;
                var number = RouteNodeIcons.CreateRect("Block", column, new Vector2(0f, 0.04f), new Vector2(1f, 0.4f))
                    .gameObject.AddComponent<TextMeshProUGUI>();
                number.font = font;
                number.text = block > 0 ? block.ToString() : "×";
                number.fontSize = 72f;
                number.enableAutoSizing = false;
                number.alignment = TextAlignmentOptions.Center;
                number.color = block > 0 ? Color.white : new Color(1f, 0.45f, 0.4f);
                number.raycastTarget = false;
            }

            return panel;
        }

        /// <summary>マスのアイコン → 中身の絵 を種類ごとに1行ずつ並べた板。</summary>
        public static RectTransform RoutePanel(RectTransform parent, Sprite knob, Sprite box)
        {
            var panel = Panel("Route Panel", parent, box);
            var rows = RouteKinds.Length;
            for (var i = 0; i < rows; i++)
            {
                var y1 = 1f - (float)i / rows;
                var y0 = 1f - (float)(i + 1) / rows;
                var row = RouteNodeIcons.CreateRect($"Row {RouteKinds[i]}", panel, new Vector2(0.04f, y0), new Vector2(0.96f, y1));
                var icon = RouteNodeIcons.CreateRect("Node", row, new Vector2(0f, 0.08f), new Vector2(0.2f, 0.92f));
                RouteNodeIcons.Create(icon, RouteKinds[i], knob, box);
                var arrow = Arrow(row, ArrowColor);
                arrow.anchorMin = new Vector2(0.26f, 0.3f);
                arrow.anchorMax = new Vector2(0.36f, 0.7f);
                var picture = RouteNodeIcons.CreateRect("Contents", row, new Vector2(0.42f, 0.04f), new Vector2(1f, 0.96f));
                Contents(picture, RouteKinds[i], knob, box);
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

        private static void Contents(RectTransform area, string kind, Sprite knob, Sprite box)
        {
            switch (kind)
            {
                case RouteNodeKinds.Battle:
                    // 小さな敵が何体か。
                    for (var i = 0; i < 3; i++)
                    {
                        Face(area, new Vector2(0.02f + i * 0.22f, 0.25f), new Vector2(0.2f + i * 0.22f, 0.75f), EnemyColor, knob, box);
                    }

                    break;
                case RouteNodeKinds.Elite:
                {
                    // 角の生えた大きめの敵。
                    var face = Face(area, new Vector2(0.05f, 0.08f), new Vector2(0.4f, 0.8f), EnemyColor, knob, box);
                    Horns(face);
                    break;
                }
                case RouteNodeKinds.Boss:
                {
                    // 王冠をかぶった大きな敵。
                    var face = Face(area, new Vector2(0.02f, 0.02f), new Vector2(0.48f, 0.72f), BossEnemyColor, knob, box);
                    Horns(face);
                    Crown(face, box);
                    break;
                }
                case RouteNodeKinds.Shop:
                    // 金貨と品物の箱。
                    for (var i = 0; i < 3; i++)
                    {
                        RouteNodeIcons.CreateImage($"Coin {i + 1}", area, new Vector2(0.02f + i * 0.13f, 0.2f + i * 0.08f),
                            new Vector2(0.2f + i * 0.13f, 0.6f + i * 0.08f), GoldColor, knob).preserveAspect = true;
                    }

                    var item = RouteNodeIcons.CreateImage("Item", area, new Vector2(0.5f, 0.15f), new Vector2(0.72f, 0.75f),
                        new Color(0.3f, 0.55f, 0.85f), box);
                    item.type = Image.Type.Sliced;
                    break;
                case RouteNodeKinds.Rest:
                    Heart(area, new Vector2(0.04f, 0.1f), new Vector2(0.34f, 0.9f), knob, box);
                    break;
            }
        }

        // 敵の顔: 赤紫の箱に白い目（操作バーの残りの敵の絵と同じ形）。
        private static RectTransform Face(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite knob,
            Sprite box)
        {
            var face = RouteNodeIcons.CreateImage("Enemy", parent, anchorMin, anchorMax, color, box);
            face.type = Image.Type.Sliced;
            RouteNodeIcons.CreateImage("Eye Left", face.rectTransform, new Vector2(0.2f, 0.45f), new Vector2(0.42f, 0.75f),
                Color.white, knob).preserveAspect = true;
            RouteNodeIcons.CreateImage("Eye Right", face.rectTransform, new Vector2(0.58f, 0.45f), new Vector2(0.8f, 0.75f),
                Color.white, knob).preserveAspect = true;
            return face.rectTransform;
        }

        // 顔の上の左右に上向きの白い角。
        private static void Horns(RectTransform face)
        {
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? 0.18f : 0.82f;
                var horn = RouteNodeIcons.CreateRect($"Horn {i + 1}", face, new Vector2(x - 0.1f, 0.95f), new Vector2(x + 0.1f, 1.25f));
                horn.localEulerAngles = new Vector3(0f, 0f, 90f);
                var triangle = horn.gameObject.AddComponent<TriangleGraphic>();
                triangle.color = Color.white;
                triangle.raycastTarget = false;
            }
        }

        // 顔の上の中央に金の王冠。
        private static void Crown(RectTransform face, Sprite box)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = 0.36f + i * 0.14f;
                var spike = RouteNodeIcons.CreateRect($"Crown {i + 1}", face, new Vector2(x - 0.08f, 1.08f), new Vector2(x + 0.08f, 1.3f));
                spike.localEulerAngles = new Vector3(0f, 0f, 90f);
                var triangle = spike.gameObject.AddComponent<TriangleGraphic>();
                triangle.color = GoldColor;
                triangle.raycastTarget = false;
            }

            RouteNodeIcons.CreateImage("Crown Base", face, new Vector2(0.28f, 0.98f), new Vector2(0.72f, 1.1f), GoldColor, box);
        }

        // ハート: 丸2つと45°回した四角。
        private static void Heart(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Sprite knob, Sprite box)
        {
            var heart = RouteNodeIcons.CreateRect("Heart", parent, anchorMin, anchorMax);
            var fit = heart.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            var square = RouteNodeIcons.CreateImage("Point", heart, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.72f), HeartColor, box);
            square.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            RouteNodeIcons.CreateImage("Left", heart, new Vector2(0.06f, 0.4f), new Vector2(0.54f, 0.88f), HeartColor, knob);
            RouteNodeIcons.CreateImage("Right", heart, new Vector2(0.46f, 0.4f), new Vector2(0.94f, 0.88f), HeartColor, knob);
        }
    }
}
