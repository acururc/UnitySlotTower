using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    /// <summary>
    /// 文字あり版の読みやすさの判定（plan 16.13、AC-005）。いま描かれている日本語のテキストが
    /// 基準解像度で 36 以上・3:4 の画面（1440×1920）からはみ出さない・省略されない・同じ親の中で他の文字と重ならない。
    /// </summary>
    internal static class TextReadability
    {
        public const float MinimumSize = 36f;

        // 状態の帯のボタン（一時停止・早見表・タイトル）は帯に収めるため 30 まで（2026-11-27 ユーザー承認、コミット 9809404）。
        public const float BandButtonMinimumSize = 30f;
        private static readonly string[] BandButtonIds = { "bar_pause", "help_open", "pause_home" };

        // 字の大きさの丸め、画面の端と重なりの判定の余裕（基準解像度の px）。
        private const float SizeTolerance = 0.01f;
        private const float EdgeTolerance = 1f;
        private const float OverlapTolerance = 2f;

        private static readonly Regex Japanese = new Regex("[぀-ヿ一-鿿！-ﾟ]");

        /// <summary>見つかった問題を <paramref name="problems"/> に「画面: 内容」の形で足す。調べた日本語のテキストの数を返す。</summary>
        public static int Collect(string screen, List<string> problems)
        {
            Canvas.ForceUpdateCanvases();
            var shown = new List<(TMP_Text text, Rect bounds, bool japanese, Transform group)>();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsDrawn(text))
                {
                    continue;
                }

                text.ForceMeshUpdate();
                if (text.textInfo.characterCount == 0 || text.textBounds.size == Vector3.zero)
                {
                    continue;
                }

                var root = text.canvas.rootCanvas.transform;
                var bounds = BoundsIn(root, text);
                var japanese = Japanese.IsMatch(text.GetParsedText());
                var label = text.GetComponentInParent<TextModeLabel>();
                var owner = label != null ? label.transform : text.transform;
                shown.Add((text, bounds, japanese, owner.parent));
                if (!japanese)
                {
                    continue;
                }

                var size = text.fontSize * text.transform.lossyScale.y / root.lossyScale.y;
                var minimum = label != null && System.Array.IndexOf(BandButtonIds, label.Id) >= 0 ? BandButtonMinimumSize : MinimumSize;
                if (size < minimum - SizeTolerance)
                {
                    problems.Add($"{screen}: {Name(text)} の字が {size:0.#}（{minimum} 未満）");
                }

                var area = new Rect(-PortraitLayout.ReferenceWidth / 2f - EdgeTolerance, -PortraitLayout.ReferenceHeight / 2f - EdgeTolerance,
                    PortraitLayout.ReferenceWidth + EdgeTolerance * 2f, PortraitLayout.ReferenceHeight + EdgeTolerance * 2f);
                if (bounds.xMin < area.xMin || bounds.yMin < area.yMin || bounds.xMax > area.xMax || bounds.yMax > area.yMax)
                {
                    problems.Add($"{screen}: {Name(text)} が画面からはみ出す {bounds}");
                }

                if (text.isTextTruncated)
                {
                    problems.Add($"{screen}: {Name(text)} が枠に収まらず省略される");
                }
            }

            for (var i = 0; i < shown.Count; i++)
            {
                for (var j = i + 1; j < shown.Count; j++)
                {
                    var a = shown[i];
                    var b = shown[j];
                    // 親が違うものは比べない（上に重なる画面が下の HUD を隠すため。plan 16.13）。
                    if ((!a.japanese && !b.japanese) || a.group != b.group || a.text.transform.IsChildOf(b.text.transform) ||
                        b.text.transform.IsChildOf(a.text.transform))
                    {
                        continue;
                    }

                    var overlap = Rect.MinMaxRect(Mathf.Max(a.bounds.xMin, b.bounds.xMin), Mathf.Max(a.bounds.yMin, b.bounds.yMin),
                        Mathf.Min(a.bounds.xMax, b.bounds.xMax), Mathf.Min(a.bounds.yMax, b.bounds.yMax));
                    if (overlap.width > OverlapTolerance && overlap.height > OverlapTolerance)
                    {
                        problems.Add($"{screen}: {Name(a.text)} と {Name(b.text)} が重なる（{overlap.width:0}×{overlap.height:0}）");
                    }
                }
            }

            var checkedCount = 0;
            foreach (var item in shown)
            {
                checkedCount += item.japanese ? 1 : 0;
            }

            return checkedCount;
        }

        // 有効で、親の Canvas がすべて有効で、色と CanvasGroup の濃さが 0 でない。
        private static bool IsDrawn(TMP_Text text)
        {
            if (!text.isActiveAndEnabled || text.canvas == null)
            {
                return false;
            }

            foreach (var canvas in text.GetComponentsInParent<Canvas>())
            {
                if (!canvas.enabled)
                {
                    return false;
                }
            }

            var alpha = text.color.a;
            for (var t = text.transform; t != null && alpha > 0.01f; t = t.parent)
            {
                if (t.TryGetComponent<CanvasGroup>(out var group) && group.enabled)
                {
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups)
                    {
                        break;
                    }
                }
            }

            return alpha > 0.01f;
        }

        // 描かれた文字の範囲（ルートの Canvas の座標。原点は画面の中央）。
        private static Rect BoundsIn(Transform root, TMP_Text text)
        {
            var local = text.textBounds;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? local.min.x : local.max.x, i < 2 ? local.min.y : local.max.y, 0f);
                var point = root.InverseTransformPoint(text.transform.TransformPoint(corner));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static string Name(TMP_Text text)
        {
            var parent = text.transform.parent;
            var content = text.GetParsedText().Replace("\n", " ");
            if (content.Length > 12)
            {
                content = content.Substring(0, 12) + "…";
            }

            return $"{(parent != null ? parent.name + "/" : "")}{text.name}「{content}」";
        }
    }
}
