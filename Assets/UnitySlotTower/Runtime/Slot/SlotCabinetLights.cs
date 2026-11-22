using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// スロット台の光（T614、FR-116・E-29）。台の 3D モデルの光の帯（短い部品に分けた <see cref="strips"/>）と、
    /// リール窓まわりの UI の LED を同じ計算で光らせる。普段は一色で落ち着いて光り（ゆっくり明るさが揺れるだけ）、
    /// 揃ったときはリール窓の中心から外へ出目の色の光の波が何重かに広がり（7 揃いは波が多い）、出目の色の余韻を残して
    /// 普段の色に戻る。波の距離は画面上の px で測る（3D の部品は台のカメラで画面に写した位置）。
    /// 実時間で動く（倍速・一時停止に関係なく光る）。見た目は仮置き（本番は M5）。
    /// </summary>
    public sealed class SlotCabinetLights : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private SlotCabinet cabinet;

        [Tooltip("台の 3D モデルを写すカメラ（光の帯の画面上の位置を測る）。")]
        [SerializeField] private Camera cabinetCamera;

        [Tooltip("波の中心（リール窓）。")]
        [SerializeField] private RectTransform waveOrigin;

        [Tooltip("台の 3D モデルの光の帯の部品（Unlit の _BaseColor を塗る）。")]
        [SerializeField] private Renderer[] strips;

        [Tooltip("リール窓まわりの LED。左上の角から時計回りの順。")]
        [SerializeField] private Image[] windowLights;

        [Tooltip("windowLights と同じ順の、LED の後ろのにじみ。")]
        [SerializeField] private Image[] windowGlows;

        [Tooltip("SlotSymbol の順（赤7・青7・緑7・黄7・バー・コイン）。")]
        [SerializeField] private Color[] symbolColors =
        {
            new Color(1f, 0.15f, 0.15f), new Color(0.25f, 0.5f, 1f), new Color(0.2f, 0.9f, 0.3f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 1f), new Color(1f, 0.8f, 0.15f),
        };

        [SerializeField] private Color offColor = new Color(0.02f, 0.05f, 0.12f);
        [SerializeField] private Color idleColor = new Color(0.25f, 0.6f, 1f);

        [Tooltip("普段の明るさ（0〜1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float idleLevel = 0.7f;

        [Tooltip("普段の明るさの揺れの幅。")]
        [Range(0f, 0.5f)]
        [SerializeField] private float breathAmount = 0.12f;

        [Tooltip("普段の明るさが一往復する秒数。")]
        [SerializeField] private float breathSeconds = 3f;

        [Tooltip("波が中心から一番遠い光まで届く秒数。")]
        [SerializeField] private float waveTravelSeconds = 0.55f;

        [Tooltip("次の波が出るまでの秒数。")]
        [SerializeField] private float waveInterval = 0.28f;

        [Tooltip("波の幅（中心から一番遠い光までを 1 とした割合）。")]
        [SerializeField] private float waveWidth = 0.16f;

        [SerializeField] private int waveCount = 3;
        [SerializeField] private int bigWaveCount = 5;

        [Tooltip("波のあと出目の色が残って普段に戻るまでの秒数。")]
        [SerializeField] private float afterglowSeconds = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float glowAlpha = 0.75f;

        [Tooltip("台の光の帯の明るさの倍率（1 より大きいと台のカメラの Bloom でにじむ）。")]
        [SerializeField] private float stripIntensity = 2.5f;

        private float _time;
        private float _flashTime;
        private float _flashLeft;
        private int _waves;
        private Color _flashColor;
        private float[] _stripDistance;
        private float[] _windowDistance;
        private Color[] _stripColors;
        private MaterialPropertyBlock _block;
        private Texture2D _glowTexture;
        private Sprite _glowSprite;

        /// <summary>台の光の帯の部品の数。</summary>
        public int LightCount => strips != null ? strips.Length : 0;

        public int WindowLightCount => windowLights != null ? windowLights.Length : 0;
        public bool IsFlashing => _flashLeft > 0f;
        public SlotSymbol? FlashSymbol { get; private set; }
        public Color LightColor(int index) => _stripColors != null ? _stripColors[index] : Color.clear;
        public Color WindowLightColor(int index) => windowLights[index].color;

        /// <summary>波の中心からの距離（一番遠い光を 1）。窓の LED は台の光の帯より先に波が届く。</summary>
        public float LightDistance(int index) => _stripDistance != null ? _stripDistance[index] : 0f;

        public float WindowLightDistance(int index) => _windowDistance != null ? _windowDistance[index] : 0f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _stripColors = new Color[LightCount];

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
            for (var i = 0; windowGlows != null && i < windowGlows.Length; i++)
            {
                if (windowGlows[i] != null)
                {
                    windowGlows[i].sprite = _glowSprite;
                }
            }
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
            var after = Afterglow();
            PaintStrips(idle, level, after);
            PaintWindow(idle, level, after);
        }

        // 波の最後が一番遠い光を通り過ぎる時刻。
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

        // 中心からの距離 d（0〜1）の所の波の強さ（0〜1）。波が通った後は trail が 1（出目の色が薄く残る）。
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

        private void PaintStrips(Color idle, float level, float after)
        {
            if (strips == null)
            {
                return;
            }

            if (_stripColors == null || _stripColors.Length != strips.Length)
            {
                _stripColors = new Color[strips.Length];
            }

            _block ??= new MaterialPropertyBlock();
            for (var i = 0; i < strips.Length; i++)
            {
                if (strips[i] == null)
                {
                    continue;
                }

                var d = _stripDistance != null && i < _stripDistance.Length ? _stripDistance[i] : 1f;
                var color = Mix(idle, d, level, after, out _);
                _stripColors[i] = color;
                _block.SetColor(BaseColorId, new Color(color.r * stripIntensity, color.g * stripIntensity, color.b * stripIntensity, 1f));
                strips[i].SetPropertyBlock(_block);
            }
        }

        private void PaintWindow(Color idle, float level, float after)
        {
            if (windowLights == null)
            {
                return;
            }

            for (var i = 0; i < windowLights.Length; i++)
            {
                var d = _windowDistance != null && i < _windowDistance.Length ? _windowDistance[i] : 0f;
                var color = Mix(idle, d, level, after, out var brightness);
                windowLights[i].color = color;
                if (windowGlows != null && i < windowGlows.Length && windowGlows[i] != null)
                {
                    windowGlows[i].color = new Color(color.r, color.g, color.b, glowAlpha * brightness * brightness);
                }
            }
        }

        // 距離は画面上の px（Screen Space Overlay の UI の position は画面の px、3D の部品は台のカメラで画面に写す）。
        private void MeasureDistances()
        {
            Vector2 origin = waveOrigin != null ? waveOrigin.position : Vector3.zero;
            var max = 0f;
            _windowDistance = new float[WindowLightCount];
            for (var i = 0; i < _windowDistance.Length; i++)
            {
                _windowDistance[i] = Vector2.Distance(windowLights[i].rectTransform.position, origin);
                max = Mathf.Max(max, _windowDistance[i]);
            }

            _stripDistance = new float[LightCount];
            for (var i = 0; i < _stripDistance.Length; i++)
            {
                if (strips[i] == null || cabinetCamera == null)
                {
                    continue;
                }

                Vector2 screen = cabinetCamera.WorldToScreenPoint(strips[i].bounds.center);
                _stripDistance[i] = Vector2.Distance(screen, origin);
                max = Mathf.Max(max, _stripDistance[i]);
            }

            if (max <= 0f)
            {
                return;
            }

            for (var i = 0; i < _windowDistance.Length; i++)
            {
                _windowDistance[i] /= max;
            }

            for (var i = 0; i < _stripDistance.Length; i++)
            {
                _stripDistance[i] /= max;
            }
        }
    }
}
