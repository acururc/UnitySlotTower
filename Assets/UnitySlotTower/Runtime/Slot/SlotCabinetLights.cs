using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// スロット台の外枠とリール窓まわりの LED（T614、FR-116・E-29）。実機の台のように、普段は一色で落ち着いて光り
    /// （ゆっくり明るさが揺れるだけ）、揃ったときはリール窓の中心から外へ出目の色の光の波が何重かに広がり
    /// （7 揃いは波が多い）、出目の色の余韻を残して普段の色に戻る。LED の後ろのにじみ・ネオン管・角のランプも同じ波で光る。
    /// 実時間で動く（倍速・一時停止に関係なく光る）。見た目は仮置き（本番は M5）。
    /// </summary>
    public sealed class SlotCabinetLights : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;

        [Tooltip("波の中心（リール窓）。")]
        [SerializeField] private RectTransform waveOrigin;

        [Tooltip("外枠の LED。左上の角から時計回りの順。")]
        [SerializeField] private Image[] lights;

        [Tooltip("lights と同じ順の、LED の後ろのにじみ。")]
        [SerializeField] private Image[] glows;

        [Tooltip("リール窓まわりの LED。左上の角から時計回りの順。")]
        [SerializeField] private Image[] windowLights;

        [Tooltip("windowLights と同じ順の、LED の後ろのにじみ。")]
        [SerializeField] private Image[] windowGlows;

        [Tooltip("ネオン管と角の大きなランプ。")]
        [SerializeField] private Graphic[] accents;

        [Tooltip("角の大きなランプの後ろのにじみ。")]
        [SerializeField] private Image[] accentGlows;

        [Tooltip("SlotSymbol の順（赤7・青7・緑7・黄7・バー・コイン）。")]
        [SerializeField] private Color[] symbolColors =
        {
            new Color(1f, 0.15f, 0.15f), new Color(0.25f, 0.5f, 1f), new Color(0.2f, 0.9f, 0.3f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 1f), new Color(1f, 0.8f, 0.15f),
        };

        [SerializeField] private Color offColor = new Color(0.25f, 0.1f, 0.1f);
        [SerializeField] private Color idleColor = new Color(1f, 0.72f, 0.32f);
        [SerializeField] private Color idleAccentColor = new Color(1f, 0.25f, 0.4f);

        [Tooltip("普段の明るさ（0〜1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float idleLevel = 0.6f;

        [Tooltip("普段の明るさの揺れの幅。")]
        [Range(0f, 0.5f)]
        [SerializeField] private float breathAmount = 0.1f;

        [Tooltip("普段の明るさが一往復する秒数。")]
        [SerializeField] private float breathSeconds = 3f;

        [Tooltip("波が中心から一番遠い LED まで届く秒数。")]
        [SerializeField] private float waveTravelSeconds = 0.55f;

        [Tooltip("次の波が出るまでの秒数。")]
        [SerializeField] private float waveInterval = 0.28f;

        [Tooltip("波の幅（中心から一番遠い LED までを 1 とした割合）。")]
        [SerializeField] private float waveWidth = 0.16f;

        [SerializeField] private int waveCount = 3;
        [SerializeField] private int bigWaveCount = 5;

        [Tooltip("波のあと出目の色が残って普段に戻るまでの秒数。")]
        [SerializeField] private float afterglowSeconds = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float glowAlpha = 0.75f;

        private float _time;
        private float _flashTime;
        private float _flashLeft;
        private int _waves;
        private Color _flashColor;
        private float[] _lightDistance;
        private float[] _windowDistance;
        private float[] _accentDistance;
        private float[] _accentGlowDistance;
        private Texture2D _glowTexture;
        private Sprite _glowSprite;

        public int LightCount => lights != null ? lights.Length : 0;
        public int WindowLightCount => windowLights != null ? windowLights.Length : 0;
        public bool IsFlashing => _flashLeft > 0f;
        public SlotSymbol? FlashSymbol { get; private set; }
        public Color LightColor(int index) => lights[index].color;
        public Color WindowLightColor(int index) => windowLights[index].color;

        /// <summary>波の中心からの距離（一番遠い LED を 1）。窓の LED は外枠の LED より先に波が届く。</summary>
        public float LightDistance(int index) => _lightDistance != null ? _lightDistance[index] : 0f;

        public float WindowLightDistance(int index) => _windowDistance != null ? _windowDistance[index] : 0f;

        private void Awake()
        {
            // にじみの柔らかい丸は実行時に1枚だけ作る（画像アセットを増やさない）。
            const int size = 64;
            _glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }

            _glowTexture.SetPixels32(pixels);
            _glowTexture.Apply(false, true);
            _glowSprite = Sprite.Create(_glowTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _glowSprite.name = "LED Glow";
            AssignSprite(glows, _glowSprite);
            AssignSprite(windowGlows, _glowSprite);
            AssignSprite(accentGlows, _glowSprite);
        }

        private void OnDestroy()
        {
            if (_glowSprite != null)
            {
                Destroy(_glowSprite);
            }

            if (_glowTexture != null)
            {
                Destroy(_glowTexture);
            }
        }

        private void OnEnable()
        {
            if (cabinet != null)
            {
                cabinet.Aligned += Flash;
            }
        }

        private void OnDisable()
        {
            if (cabinet != null)
            {
                cabinet.Aligned -= Flash;
            }
        }

        /// <summary>リール窓から出目の色の波を広げる（揃ったときに呼ばれる。撮影用にも公開）。</summary>
        public void Flash(SlotSymbol symbol)
        {
            var index = (int)symbol;
            _flashColor = index >= 0 && index < symbolColors.Length ? symbolColors[index] : idleColor;
            var big = symbol == SlotSymbol.Red7 || symbol == SlotSymbol.Blue7 || symbol == SlotSymbol.Green7 ||
                      symbol == SlotSymbol.Yellow7;
            _waves = Mathf.Max(1, big ? bigWaveCount : waveCount);
            FlashSymbol = symbol;
            _flashTime = 0f;
            _flashLeft = WaveEnd() + afterglowSeconds;
            MeasureDistances(); // 画面の大きさで配置が変わるので、揃うたびに測り直す（数百個で軽い）。
        }

        // 重いフレーム（読み込み・撮影など）の後に波が飛ばないよう、1フレームに進める時間を抑える。
        private void Update() => Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));

        /// <summary>光を <paramref name="seconds"/> 秒進めて塗り直す（撮影で決まった見た目にするためにも使う）。</summary>
        public void Step(float seconds)
        {
            _time += seconds;
            if (_flashLeft > 0f)
            {
                _flashTime += seconds;
                _flashLeft -= seconds;
                if (_flashLeft <= 0f)
                {
                    FlashSymbol = null;
                }
            }

            var level = idleLevel + breathAmount * Mathf.Sin(_time * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds));
            var idle = Color.Lerp(offColor, idleColor, level);
            var idleAccent = Color.Lerp(offColor, idleAccentColor, Mathf.Clamp01(level + 0.25f));
            var after = Afterglow();
            Paint(lights, glows, _lightDistance, idle, level, after);
            Paint(windowLights, windowGlows, _windowDistance, idle, level, after);
            Paint(accents, accentGlows, _accentDistance, _accentGlowDistance, idleAccent, level, after);
        }

        // 波の最後が一番遠い LED を通り過ぎる時刻。
        private float WaveEnd() => (_waves - 1) * waveInterval + waveTravelSeconds * (1f + waveWidth * 2f);

        // 波のあとの出目の色の残り具合（1→0）。波の途中は 0（波だけで光る）。
        private float Afterglow()
        {
            if (_flashLeft <= 0f)
            {
                return 0f;
            }

            var since = _flashTime - WaveEnd();
            return since < 0f ? 0f : 1f - Mathf.Clamp01(since / Mathf.Max(0.01f, afterglowSeconds));
        }

        // 中心からの距離 d（0〜1）の所の波の強さ（0〜1）。波が通った後は出目の色が薄く残る。
        private float Wave(float d, out float trail)
        {
            trail = 0f;
            if (_flashLeft <= 0f)
            {
                return 0f;
            }

            var peak = 0f;
            for (var w = 0; w < _waves; w++)
            {
                var front = (_flashTime - w * waveInterval) / waveTravelSeconds;
                var x = (front - d) / waveWidth;
                if (x > 0f)
                {
                    trail = 1f;
                }

                if (x > -1f && x < 1f)
                {
                    peak = Mathf.Max(peak, 0.5f + 0.5f * Mathf.Cos(x * Mathf.PI));
                }
            }

            return peak;
        }

        private Color Mix(Color idle, float d, float level, float after, out float brightness)
        {
            var wave = Wave(d, out var trail);
            var tint = Mathf.Max(trail * 0.3f, after * 0.6f);
            var color = Color.Lerp(idle, Color.Lerp(offColor, _flashColor, 0.85f), tint);
            color = Color.Lerp(color, Color.Lerp(_flashColor, Color.white, 0.45f), wave);
            brightness = Mathf.Max(level * 0.5f, Mathf.Max(wave, tint * 0.7f));
            return color;
        }

        private void Paint(Image[] leds, Image[] halos, float[] distance, Color idle, float level, float after)
        {
            if (leds == null)
            {
                return;
            }

            for (var i = 0; i < leds.Length; i++)
            {
                var d = distance != null && i < distance.Length ? distance[i] : 0f;
                var color = Mix(idle, d, level, after, out var brightness);
                leds[i].color = color;
                if (halos != null && i < halos.Length && halos[i] != null)
                {
                    halos[i].color = new Color(color.r, color.g, color.b, glowAlpha * brightness * brightness);
                }
            }
        }

        private void Paint(Graphic[] parts, Image[] halos, float[] distance, float[] haloDistance, Color idle, float level,
            float after)
        {
            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    var d = distance != null && i < distance.Length ? distance[i] : 1f;
                    parts[i].color = Mix(idle, d, level, after, out _);
                }
            }

            for (var i = 0; halos != null && i < halos.Length; i++)
            {
                if (halos[i] != null)
                {
                    var d = haloDistance != null && i < haloDistance.Length ? haloDistance[i] : 1f;
                    var color = Mix(idle, d, level, after, out var brightness);
                    halos[i].color = new Color(color.r, color.g, color.b, glowAlpha * brightness);
                }
            }
        }

        private void MeasureDistances()
        {
            var origin = waveOrigin != null ? waveOrigin.position : transform.position;
            var max = 0f;
            _lightDistance = Measure(lights, origin, ref max);
            _windowDistance = Measure(windowLights, origin, ref max);
            _accentDistance = Measure(accents, origin, ref max);
            _accentGlowDistance = Measure(accentGlows, origin, ref max);
            if (max <= 0f)
            {
                return;
            }

            Normalize(_lightDistance, max);
            Normalize(_windowDistance, max);
            Normalize(_accentDistance, max);
            Normalize(_accentGlowDistance, max);
        }

        private static float[] Measure(Graphic[] parts, Vector3 origin, ref float max)
        {
            if (parts == null)
            {
                return null;
            }

            var result = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                // ネオン管の帯は長いので、中心ではなく波の中心に一番近い点で測る。
                var rect = parts[i].rectTransform;
                var local = rect.InverseTransformPoint(origin);
                var r = rect.rect;
                var nearest = rect.TransformPoint(new Vector3(Mathf.Clamp(local.x, r.xMin, r.xMax), Mathf.Clamp(local.y, r.yMin, r.yMax), 0f));
                var useCenter = r.width < 200f && r.height < 200f;
                result[i] = Vector3.Distance(useCenter ? rect.position : nearest, origin);
                max = Mathf.Max(max, result[i]);
            }

            return result;
        }

        private static void Normalize(float[] values, float max)
        {
            for (var i = 0; values != null && i < values.Length; i++)
            {
                values[i] /= max;
            }
        }

        private static void AssignSprite(Image[] images, Sprite sprite)
        {
            if (images == null)
            {
                return;
            }

            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    images[i].sprite = sprite;
                }
            }
        }
    }
}
