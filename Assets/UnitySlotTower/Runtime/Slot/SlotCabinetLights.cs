using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// スロット台の外枠とリール窓まわりの LED（T614、FR-116・E-29）。実機の台のように派手に光らせる:
    /// 普段は数秒ごとに光り方（虹の流れ・交互点滅・上から広がる波・きらめき）が切り替わり、回っている間は速く流れ、
    /// 揃ったときは出目の色で全体が激しく点滅してから流れる（7 揃いは虹色が混ざって長い）。LED の後ろのにじみ（glow）も
    /// 同じ色で光る。実時間で動く（倍速・一時停止に関係なく光る）。見た目は仮置き（本番は M5）。
    /// </summary>
    public sealed class SlotCabinetLights : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;

        [Tooltip("外枠の LED。左上の角から時計回りの順。")]
        [SerializeField] private Image[] lights;

        [Tooltip("lights と同じ順の、LED の後ろのにじみ。")]
        [SerializeField] private Image[] glows;

        [Tooltip("リール窓まわりの LED。左上の角から時計回りの順。")]
        [SerializeField] private Image[] windowLights;

        [Tooltip("windowLights と同じ順の、LED の後ろのにじみ。")]
        [SerializeField] private Image[] windowGlows;

        [Tooltip("ネオン管と角の大きなランプ。全部同じ色で光る。")]
        [SerializeField] private Graphic[] accents;

        [Tooltip("角の大きなランプの後ろのにじみ。accents と同じ色で光る。")]
        [SerializeField] private Image[] accentGlows;

        [Tooltip("SlotSymbol の順（赤7・青7・緑7・黄7・バー・コイン）。")]
        [SerializeField] private Color[] symbolColors =
        {
            new Color(1f, 0.15f, 0.15f), new Color(0.25f, 0.5f, 1f), new Color(0.2f, 0.9f, 0.3f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 1f), new Color(1f, 0.8f, 0.15f),
        };

        [SerializeField] private Color offColor = new Color(0.28f, 0.12f, 0.12f);
        [SerializeField] private Color headColor = new Color(1f, 0.95f, 0.7f);
        [SerializeField] private Color tailColor = new Color(1f, 0.35f, 0.15f);

        [Tooltip("普段の光り方を切り替える間隔（秒）。")]
        [SerializeField] private float patternSeconds = 3.5f;

        [Tooltip("回っている間の流れの速さ（1秒に進む LED の数）。")]
        [SerializeField] private float spinSpeed = 40f;

        [Tooltip("揃ったときに光る時間（秒）。")]
        [SerializeField] private float flashSeconds = 2.2f;

        [Tooltip("7 が揃ったときに光る時間（秒）。")]
        [SerializeField] private float bigFlashSeconds = 3.2f;

        [Tooltip("揃った直後に全体が点滅する時間（秒）。")]
        [SerializeField] private float strobeSeconds = 0.7f;

        [Tooltip("全体の点滅の速さ（1秒に入れ替わる回数）。")]
        [SerializeField] private float strobeRate = 16f;

        [Range(0f, 1f)]
        [SerializeField] private float glowAlpha = 0.7f;

        private const int PatternCount = 4;

        private float _time;
        private float _flashTime;
        private float _flashLeft;
        private bool _bigFlash;
        private Color _flashColor;
        private Texture2D _glowTexture;
        private Sprite _glowSprite;

        public int LightCount => lights != null ? lights.Length : 0;
        public int WindowLightCount => windowLights != null ? windowLights.Length : 0;
        public bool IsFlashing => _flashLeft > 0f;
        public SlotSymbol? FlashSymbol { get; private set; }
        public Color LightColor(int index) => lights[index].color;
        public Color WindowLightColor(int index) => windowLights[index].color;

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
            var sprite = _glowSprite = Sprite.Create(_glowTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = "LED Glow";
            AssignSprite(glows, sprite);
            AssignSprite(windowGlows, sprite);
            AssignSprite(accentGlows, sprite);
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

        /// <summary>出目の色で激しく光らせる（揃ったときに呼ばれる。撮影用にも公開）。</summary>
        public void Flash(SlotSymbol symbol)
        {
            var index = (int)symbol;
            _flashColor = index >= 0 && index < symbolColors.Length ? symbolColors[index] : headColor;
            _bigFlash = symbol == SlotSymbol.Red7 || symbol == SlotSymbol.Blue7 || symbol == SlotSymbol.Green7 ||
                        symbol == SlotSymbol.Yellow7;
            FlashSymbol = symbol;
            _flashTime = 0f;
            _flashLeft = _bigFlash ? bigFlashSeconds : flashSeconds;
        }

        private void Update() => Step(Time.unscaledDeltaTime);

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

            var spinning = cabinet != null && cabinet.IsSpinning;
            Paint(lights, glows, false, spinning);
            Paint(windowLights, windowGlows, true, spinning);
            PaintAccents(spinning);
        }

        private void Paint(Image[] leds, Image[] halos, bool reverse, bool spinning)
        {
            if (leds == null)
            {
                return;
            }

            var count = leds.Length;
            for (var n = 0; n < count; n++)
            {
                var i = reverse ? count - 1 - n : n;
                var color = LedColor(n, count, spinning);
                leds[i].color = color;
                if (halos != null && i < halos.Length && halos[i] != null)
                {
                    var peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                    var a = Mathf.Clamp01((peak - 0.4f) / 0.6f) * glowAlpha;
                    halos[i].color = new Color(color.r, color.g, color.b, a);
                }
            }
        }

        private Color LedColor(int i, int count, bool spinning)
        {
            var p = i / (float)count;
            if (_flashLeft > 0f)
            {
                if (_flashTime < strobeSeconds)
                {
                    // 全体がいっせいに出目の色と白で激しく入れ替わる。
                    return ((int)(_flashTime * strobeRate) & 1) == 0 ? _flashColor : Color.white;
                }

                // 3つおきの光が速く流れる。7 揃いは虹色が混ざる。
                var lit = (i + (int)(_flashTime * 24f)) % 3 == 0;
                var color = _flashColor;
                if (_bigFlash)
                {
                    color = Color.Lerp(Color.HSVToRGB(Mathf.Repeat(p * 3f - _flashTime * 1.5f, 1f), 0.9f, 1f), _flashColor, 0.35f);
                }

                return lit ? Color.Lerp(color, Color.white, 0.35f) : Color.Lerp(offColor, color, 0.45f);
            }

            if (spinning)
            {
                // 短い間隔の光が高速で回る。
                var behind = Mathf.Repeat(_time * spinSpeed - i, 5f);
                var k = 1f - behind / 3f;
                return k <= 0f ? offColor : Color.Lerp(tailColor, headColor, k);
            }

            var t = Mathf.Repeat(_time, patternSeconds * PatternCount);
            switch ((int)(t / patternSeconds))
            {
                case 0:
                {
                    // 虹色の光の列が流れる。
                    var behind = Mathf.Repeat(_time * 12f - i, 6f);
                    var k = Mathf.Clamp01(1f - behind / 4f);
                    var rainbow = Color.HSVToRGB(Mathf.Repeat(p * 2f - _time * 0.4f, 1f), 0.85f, 1f);
                    return Color.Lerp(Color.Lerp(offColor, rainbow, 0.3f), rainbow, k);
                }
                case 1:
                {
                    // 金と赤が交互に点滅する。
                    var swap = ((int)(_time * 5f) + i) & 1;
                    return swap == 0 ? headColor : new Color(1f, 0.2f, 0.35f);
                }
                case 2:
                {
                    // 上の真ん中から左右へ光の波が広がる。
                    var top = Mathf.Repeat(p - TopCenter(), 1f);
                    var d = Mathf.Min(top, 1f - top);
                    var k = Mathf.Pow(Mathf.Repeat(_time * 1.6f - d * 5f, 1f), 3f);
                    return Color.Lerp(Color.Lerp(offColor, tailColor, 0.4f), Color.white, k);
                }
                default:
                {
                    // きらめき: ばらばらに白く瞬き、ほかはゆっくり色が変わる。
                    var frame = (int)(_time * 12f);
                    var hash = (uint)(i * 73856093) ^ (uint)(frame * 19349663);
                    hash ^= hash >> 13;
                    hash *= 0x5bd1e995;
                    hash ^= hash >> 15;
                    if ((hash & 7) == 0)
                    {
                        return Color.white;
                    }

                    return Color.Lerp(offColor, Color.HSVToRGB(Mathf.Repeat(_time * 0.15f + p, 1f), 0.8f, 1f), 0.55f);
                }
            }
        }

        // 外枠の LED は左上の角から始まるので、上の辺の真ん中は全体の約 1/8 の位置（縦横比 3:4 の周の割合）。
        private static float TopCenter() => 1.5f / 14f;

        private void PaintAccents(bool spinning)
        {
            Color color;
            if (_flashLeft > 0f)
            {
                color = ((int)(_flashTime * (_flashTime < strobeSeconds ? strobeRate : 6f)) & 1) == 0
                    ? _flashColor
                    : Color.white;
            }
            else if (spinning)
            {
                color = ((int)(_time * 10f) & 1) == 0 ? headColor : tailColor;
            }
            else
            {
                color = Color.HSVToRGB(Mathf.Repeat(_time * 0.12f, 1f), 0.75f, 1f);
            }

            for (var i = 0; accents != null && i < accents.Length; i++)
            {
                if (accents[i] != null)
                {
                    accents[i].color = color;
                }
            }

            var glow = new Color(color.r, color.g, color.b, glowAlpha);
            for (var i = 0; accentGlows != null && i < accentGlows.Length; i++)
            {
                if (accentGlows[i] != null)
                {
                    accentGlows[i].color = glow;
                }
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
