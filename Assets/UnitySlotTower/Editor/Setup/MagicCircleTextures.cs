using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using R = UnitySlotTower.Editor.Setup.Red7BreathEffectSetup;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 008（T1201、P-06）: 魔法陣の絵を描いて PNG にし、加算の材質を作る。円・目盛り・小円・点・正多角形（三角・四角・六角）・線分の
    /// 重ね合わせで、縁は距離でなめらかにする。文字と星形（五芒星・六芒星・八芒星など）は使わない（spec 008 P-06）。毎回同じ絵になる。
    /// 座標は中心が (0, 0)、半径 1 が絵の端。白の絵で、色はパーティクルの色で付ける。
    /// </summary>
    internal static class MagicCircleTextures
    {
        private const string Folder = "Assets/UnitySlotTower/Art/VFX/MagicCircles";
        private const string VfxFolder = "Assets/UnitySlotTower/Art/VFX";
        private const int Size = 1024;

        /// <summary>緑7の盤面全体の魔法陣（外・中・内の3層）の材質。</summary>
        internal static (Material outer, Material middle, Material inner) Green(Shader shader) => (
            Build(shader, "Green7 Circle Outer", c =>
            {
                c.Ring(0.97f, 0.022f);
                c.Ring(0.89f, 0.010f);
                c.Ticks(0.89f, 0.97f, 72, 0.006f, 0f);
                c.Ticks(0.89f, 0.97f, 12, 0.014f, 0f);
                for (var i = 0; i < 12; i++)
                {
                    var a = (i + 0.5f) * Mathf.PI * 2f / 12f;
                    c.Ring(0.93f * Mathf.Cos(a), 0.93f * Mathf.Sin(a), 0.025f, 0.008f);
                }

                c.Ring(0.82f, 0.006f);
            }),
            Build(shader, "Green7 Circle Middle", c =>
            {
                c.Ring(0.72f, 0.016f);
                c.Polygon(4, 0.72f, 0.010f, Mathf.PI * 0.25f);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Mathf.PI * 2f / 6f;
                    c.Ring(0.5f * Mathf.Cos(a), 0.5f * Mathf.Sin(a), 0.11f, 0.009f);
                    c.Disc(0.5f * Mathf.Cos(a), 0.5f * Mathf.Sin(a), 0.03f);
                }

                c.Polygon(6, 0.38f, 0.010f, 0f);
            }),
            Build(shader, "Green7 Circle Inner", c =>
            {
                c.Ring(0.30f, 0.014f);
                c.Ring(0.24f, 0.006f);
                c.Ticks(0.15f, 0.21f, 24, 0.006f, 0f); // 車輪（法輪など）に見えないよう、中心につながらない短い目盛りにする。
                c.Ring(0.13f, 0.006f);
                c.Disc(0f, 0f, 0.06f);
            }));

        /// <summary>バーの足元の魔法陣の材質。</summary>
        internal static Material Bar(Shader shader) => Build(shader, "Bar Circle", c =>
        {
            c.Ring(0.95f, 0.035f);
            c.Ring(0.84f, 0.014f);
            c.Ticks(0.84f, 0.95f, 36, 0.012f, 0f);
            // 三角の中に円と点は「目の印」に見えるので使わない（P-06）。歯車のような輪と小円にする。
            c.Ring(0.62f, 0.018f);
            for (var i = 0; i < 4; i++) // 3つだと既存作品の模様を連想させるので4つ。
            {
                var a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                c.Ring(0.62f * Mathf.Cos(a), 0.62f * Mathf.Sin(a), 0.13f, 0.016f);
                c.Disc(0.62f * Mathf.Cos(a), 0.62f * Mathf.Sin(a), 0.05f);
            }

            c.Ticks(0.3f, 0.42f, 24, 0.012f, 0f); // 車輪に見えないよう、中心につながらない短い目盛り。
            c.Ring(0.22f, 0.018f);
            c.Ring(0.5f, 0.008f);
        });

        /// <summary>
        /// 魔法陣の層のパーティクル（地面に平ら、放出はせず実行時に置く）。置いた粒の寿命が短いときは、明るく 1→grow 倍に広がりながら消える。
        /// </summary>
        internal static ParticleSystem Layer(Transform parent, string name, Material material, int layer, int maxParticles, float grow) =>
            R.Create(parent, name, material, layer, maxParticles, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 100f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (1f, 0f), (0.8f, 0.4f), (0f, 1f) });
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 0f, grow), new Keyframe(1f, grow, 0f, 0f)));
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                renderer.sortingFudge = 5f;
                renderer.maxParticleSize = 100f;
            });

        private static Material Build(Shader shader, string name, Action<Canvas> draw)
        {
            var path = $"{Folder}/{name}.png";
            var canvas = new Canvas(Size);
            draw(canvas);
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(path, canvas.EncodePng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();

            var material = R.Material(shader, name, "circle_05", additive: true);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private sealed class Canvas
        {
            private readonly int _size;
            private readonly float[] _alpha;
            private readonly float _pixel;

            internal Canvas(int size)
            {
                _size = size;
                _alpha = new float[size * size];
                _pixel = 2f / size;
            }

            internal void Ring(float radius, float width) => Ring(0f, 0f, radius, width);

            internal void Ring(float cx, float cy, float radius, float width) =>
                Fill(cx - radius - width, cy - radius - width, cx + radius + width, cy + radius + width,
                    (x, y) => Mathf.Abs(Dist(x - cx, y - cy) - radius) - width * 0.5f);

            internal void Disc(float cx, float cy, float radius) =>
                Fill(cx - radius, cy - radius, cx + radius, cy + radius, (x, y) => Dist(x - cx, y - cy) - radius);

            internal void Ticks(float from, float to, int count, float width, float phase)
            {
                for (var i = 0; i < count; i++)
                {
                    var a = phase + i * Mathf.PI * 2f / count;
                    var c = Mathf.Cos(a);
                    var s = Mathf.Sin(a);
                    Segment(from * c, from * s, to * c, to * s, width);
                }
            }

            internal void Polygon(int sides, float radius, float width, float phase)
            {
                for (var i = 0; i < sides; i++)
                {
                    var a = phase + i * Mathf.PI * 2f / sides;
                    var b = phase + (i + 1) * Mathf.PI * 2f / sides;
                    Segment(radius * Mathf.Cos(a), radius * Mathf.Sin(a), radius * Mathf.Cos(b), radius * Mathf.Sin(b), width);
                }
            }

            internal void Segment(float ax, float ay, float bx, float by, float width)
            {
                var pad = width;
                Fill(Mathf.Min(ax, bx) - pad, Mathf.Min(ay, by) - pad, Mathf.Max(ax, bx) + pad, Mathf.Max(ay, by) + pad, (x, y) =>
                {
                    var dx = bx - ax;
                    var dy = by - ay;
                    var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / Mathf.Max(1e-8f, dx * dx + dy * dy));
                    return Dist(x - ax - t * dx, y - ay - t * dy) - width * 0.5f;
                });
            }

            // 範囲内の画素に、形までの距離（内側が負）から縁をなめらかにした濃さを重ねる（明るい方を残す）。
            private void Fill(float minX, float minY, float maxX, float maxY, Func<float, float, float> distance)
            {
                var x0 = Mathf.Clamp(ToPixel(minX) - 2, 0, _size - 1);
                var x1 = Mathf.Clamp(ToPixel(maxX) + 2, 0, _size - 1);
                var y0 = Mathf.Clamp(ToPixel(minY) - 2, 0, _size - 1);
                var y1 = Mathf.Clamp(ToPixel(maxY) + 2, 0, _size - 1);
                for (var py = y0; py <= y1; py++)
                {
                    var y = (py + 0.5f) * _pixel - 1f;
                    for (var px = x0; px <= x1; px++)
                    {
                        var x = (px + 0.5f) * _pixel - 1f;
                        var coverage = Mathf.Clamp01(0.5f - distance(x, y) / _pixel);
                        var index = py * _size + px;
                        if (coverage > _alpha[index])
                        {
                            _alpha[index] = coverage;
                        }
                    }
                }
            }

            private int ToPixel(float v) => Mathf.FloorToInt((v + 1f) / _pixel);

            private static float Dist(float x, float y) => Mathf.Sqrt(x * x + y * y);

            internal byte[] EncodePng()
            {
                var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
                var pixels = new Color32[_alpha.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(_alpha[i] * 255f));
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                var png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                return png;
            }
        }
    }
}
