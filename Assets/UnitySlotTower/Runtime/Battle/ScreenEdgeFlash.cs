using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 敵が本拠地に入ったときに画面の縁を短く赤く光らせる（FR-078）。画面いっぱいの Overlay Canvas に、
    /// 中央が透明な縁だけのスプライトを置く。クリックは通す（GraphicRaycaster なし・raycastTarget false）。
    /// 時間は通常の時間で進むので、一時停止中は止まる。
    /// </summary>
    public sealed class ScreenEdgeFlash : MonoBehaviour
    {
        public const float StartAlpha = 0.55f;
        private const int SortingOrder = 10;
        private const int TextureSize = 128;
        private const float EdgeWidth = 0.22f;

        private Image _image;
        private Texture2D _texture;
        private Sprite _sprite;
        private float _left;

        public bool IsFlashing => _left > 0f;
        public float Alpha => _image != null ? _image.color.a : 0f;
        public Graphic Graphic => _image;

        /// <summary>シーンの一番上に作る（入れ子の Canvas にしない）。</summary>
        public static ScreenEdgeFlash Create()
        {
            var go = new GameObject("Screen Edge Flash", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var flash = go.AddComponent<ScreenEdgeFlash>();
            flash.Build();
            return flash;
        }

        /// <summary>最初から光り直す（連続で入ったときも）。</summary>
        public void Play()
        {
            _left = BattlePresentationTiming.EdgeFlash;
            Apply();
        }

        public void Hide()
        {
            _left = 0f;
            Apply();
        }

        private void Build()
        {
            _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "Screen Edge Flash",
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var u = (x + 0.5f) / TextureSize;
                    var v = (y + 0.5f) / TextureSize;
                    var edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    var t = 1f - Mathf.Clamp01(edge / EdgeWidth);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(255f * t * t));
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            _sprite.name = _texture.name;

            var child = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)child.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _image = child.GetComponent<Image>();
            _image.sprite = _sprite;
            _image.raycastTarget = false;
            Apply();
        }

        private void Update()
        {
            if (_left <= 0f) return;
            _left = Mathf.Max(0f, _left - Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (_image == null) return;
            var color = UnitySlotTower.Ui.UiStyle.Danger;
            color.a = StartAlpha * (_left / BattlePresentationTiming.EdgeFlash);
            _image.color = color;
            _image.enabled = _left > 0f;
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
