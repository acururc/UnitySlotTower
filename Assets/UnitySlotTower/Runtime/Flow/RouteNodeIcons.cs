using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ステージの開始の演出の印（<see cref="RouteNodeKinds"/>、旧ルートのマスの絵の名残）と、実行時に UI を作る小さな部品。文字を使わず、丸い台の色と白い印で見分ける。
    /// 種類を増やすときは <see cref="Builders"/> に1行足す（登録のない種類は灰色の丸）。絵は仮（M5 で素材に差し替える）。
    /// </summary>
    public static class RouteNodeIcons
    {
        private static readonly Color BattleColor = new Color(0.8f, 0.25f, 0.2f);
        private static readonly Color EliteColor = new Color(0.55f, 0.2f, 0.7f);
        private static readonly Color BossColor = new Color(0.45f, 0.05f, 0.08f);
        private static readonly Color UnknownColor = new Color(0.5f, 0.5f, 0.55f);

        private static readonly Dictionary<string, Action<RectTransform, Sprite>> Builders =
            new Dictionary<string, Action<RectTransform, Sprite>>
            {
                // 剣は斜めに置く（まっすぐだと下向きの矢印に見える）。強敵は2本を交差させる。
                { RouteNodeKinds.First, (rect, box) => Sword(rect, box, -40f) },
                { RouteNodeKinds.Battle, (rect, box) => Sword(rect, box, -40f) },
                {
                    RouteNodeKinds.Elite, (rect, box) =>
                    {
                        Sword(rect, box, 40f);
                        Sword(rect, box, -40f);
                    }
                },
                { RouteNodeKinds.Boss, Crown },
            };

        /// <summary>その種類の台の色。</summary>
        public static Color ColorOf(string kind) => kind switch
        {
            RouteNodeKinds.First => BattleColor,
            RouteNodeKinds.Battle => BattleColor,
            RouteNodeKinds.Elite => EliteColor,
            RouteNodeKinds.Boss => BossColor,
            _ => UnknownColor,
        };

        /// <summary>
        /// parent いっぱいにその種類のアイコン（丸い台＋白い印）を作り、台の Image を返す。クリックは通さない。
        /// </summary>
        public static Image Create(RectTransform parent, string kind, Sprite knob, Sprite box)
        {
            var plate = CreateImage("Icon", parent, Vector2.zero, Vector2.one, ColorOf(kind), knob);
            plate.preserveAspect = true;
            if (Builders.TryGetValue(kind, out var build))
            {
                build(plate.rectTransform, box);
            }

            return plate;
        }

        // 剣: 刃（縦の棒）・鍔（横の棒）・柄。angle で傾ける。
        private static void Sword(RectTransform plate, Sprite box, float angle)
        {
            var sword = CreateRect("Sword", plate, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            sword.localEulerAngles = new Vector3(0f, 0f, angle);
            CreateImage("Blade", sword, new Vector2(0.44f, 0.3f), new Vector2(0.56f, 1f), Color.white, box);
            CreateImage("Guard", sword, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.33f), Color.white, box);
            CreateImage("Grip", sword, new Vector2(0.45f, 0f), new Vector2(0.55f, 0.25f), Color.white, box);
        }

        // 王冠: 上向きの三角3つと台（リザルトのクリアと同じ形）。
        private static void Crown(RectTransform plate, Sprite box)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = 0.32f + i * 0.18f;
                var spike = CreateRect($"Crown {i + 1}", plate, new Vector2(x - 0.13f, 0.5f), new Vector2(x + 0.13f, 0.68f));
                spike.localEulerAngles = new Vector3(0f, 0f, 90f);
                spike.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            }

            CreateImage("Crown Base", plate, new Vector2(0.24f, 0.3f), new Vector2(0.76f, 0.44f), Color.white, box);
        }

        internal static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
