using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 009（T1303、plan 2章）: コインの噴水の絵を描いて PNG の Sprite にする。金貨（縁・面・内側の輪・ひし形の模様・光沢）、
    /// 4本の光のきらめき、縁のないぼかした丸い光。外部の素材は使わない。毎回同じ絵になる。座標は中心が (0, 0)、半径 1 が絵の端、y が上。
    /// </summary>
    internal static class CoinFountainTextures
    {
        private const string Folder = "Assets/UnitySlotTower/Art/VFX/Coin";
        private const int Size = 128;

        internal static Sprite Coin() => Build("Coin", (x, y) =>
        {
            var r = Mathf.Sqrt(x * x + y * y) / 0.95f;
            var edge = Mathf.Clamp01((1f - r) * Size * 0.5f + 0.5f);
            if (edge <= 0f)
            {
                return Color.clear;
            }

            // 左上からの光。
            var light = Mathf.Clamp01(0.5f + 0.5f * (-0.6f * x + 0.8f * y));
            Color color;
            if (r > 0.84f)
            {
                // 縁: 外へ向かって傾いた面として明暗を付ける。
                var n = new Vector2(x, y).normalized;
                var bevel = Mathf.Clamp01(0.5f + 0.5f * (-0.6f * n.x + 0.8f * n.y));
                color = Color.Lerp(new Color(0.55f, 0.33f, 0.05f), new Color(1f, 0.85f, 0.4f), bevel);
            }
            else
            {
                color = Color.Lerp(new Color(0.9f, 0.62f, 0.12f), new Color(1f, 0.88f, 0.35f), light);
                var ring = Mathf.Abs(r - 0.64f);
                if (ring < 0.04f)
                {
                    color = Color.Lerp(color, new Color(0.7f, 0.45f, 0.08f), Mathf.Clamp01((0.04f - ring) * Size * 0.5f));
                }

                var diamond = Mathf.Abs(x) + Mathf.Abs(y);
                if (diamond < 0.3f)
                {
                    var inner = Color.Lerp(new Color(0.95f, 0.7f, 0.2f), new Color(1f, 0.96f, 0.7f), Mathf.Clamp01(0.5f + (-x + y) * 1.6f));
                    color = Color.Lerp(color, inner, Mathf.Clamp01((0.3f - diamond) * Size * 0.5f));
                }

                // 光沢: 左上のぼかした楕円。
                var hx = (x + 0.32f) / 0.32f;
                var hy = (y - 0.38f) / 0.18f;
                var shine = Mathf.Clamp01(1f - (hx * hx + hy * hy));
                color = Color.Lerp(color, Color.white, shine * shine * 0.7f);
            }

            color.a = edge;
            return color;
        });

        /// <summary>所持金のお札（横 1.6 : 縦 1 の角丸の四角、<see cref="UnitySlotTower.Ui.MoneyIconArt"/> と同じ緑、内側の枠と中央の丸）。絵は正方形で、上下に余白を残す。</summary>
        internal static Sprite Bill() => Build("Bill", (x, y) =>
        {
            const float halfW = 0.96f;
            const float halfH = halfW / 1.6f;
            const float corner = 0.1f;
            var dx = Mathf.Max(0f, Mathf.Abs(x) - (halfW - corner));
            var dy = Mathf.Max(0f, Mathf.Abs(y) - (halfH - corner));
            var outside = Mathf.Sqrt(dx * dx + dy * dy) - corner;
            var edge = Mathf.Clamp01(-outside * Size * 0.5f + 0.5f);
            if (edge <= 0f)
            {
                return Color.clear;
            }

            var bill = new Color(0.33f, 0.72f, 0.4f);
            var mark = new Color(0.17f, 0.45f, 0.24f);
            var light = Mathf.Clamp01(0.5f + 0.5f * (-0.4f * x + 0.9f * y / halfH));
            var color = Color.Lerp(bill * 0.85f, Color.Lerp(bill, Color.white, 0.25f), light);
            // 内側の枠（縁から少し内側の細い線）と中央の丸。
            var inset = Mathf.Abs(outside + 0.09f);
            if (inset < 0.02f)
            {
                color = Color.Lerp(color, mark, Mathf.Clamp01((0.02f - inset) * Size * 0.5f));
            }

            var r = Mathf.Sqrt(x * x + y * y);
            if (r < 0.22f)
            {
                color = Color.Lerp(color, mark, Mathf.Clamp01((0.22f - r) * Size * 0.5f));
            }

            color.a = edge;
            return color;
        });

        internal static Sprite Sparkle() => Build("Sparkle", (x, y) =>
        {
            var ax = Mathf.Abs(x);
            var ay = Mathf.Abs(y);
            // 縦横の細い光の筋（先へ向かって細くなる）と、中心のぼかした光。
            var vertical = Mathf.Clamp01(1f - ax / Mathf.Max(0.001f, 0.14f * (1f - ay))) * Mathf.Clamp01(1f - ay);
            var horizontal = Mathf.Clamp01(1f - ay / Mathf.Max(0.001f, 0.14f * (1f - ax))) * Mathf.Clamp01(1f - ax);
            var core = Mathf.Exp(-(x * x + y * y) * 18f);
            var a = Mathf.Clamp01(Mathf.Max(Mathf.Max(vertical, horizontal), core));
            return new Color(1f, 1f, 1f, a);
        });

        internal static Sprite Glow() => Build("Coin Glow", (x, y) =>
        {
            var a = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });

        private static Sprite Build(string name, Func<float, float, Color> paint)
        {
            var path = $"{Folder}/{name}.png";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (var py = 0; py < Size; py++)
            {
                for (var px = 0; px < Size; px++)
                {
                    var x = (px + 0.5f) / Size * 2f - 1f;
                    var y = (py + 0.5f) / Size * 2f - 1f;
                    pixels[py * Size + px] = paint(x, y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
