using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// スロット台の外枠の LED（T614、FR-116・E-29）。普段は光の列が時計回りにゆっくり流れ、揃ったときは出目の色で
    /// 速く点滅してから流れに戻る。実時間で動く（倍速・一時停止に関係なく光る）。見た目は仮置き（本番は M5）。
    /// </summary>
    public sealed class SlotCabinetLights : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;

        [Tooltip("左上の角から時計回りの順。")]
        [SerializeField] private Image[] lights;

        [Tooltip("SlotSymbol の順（赤7・青7・緑7・黄7・バー・コイン）。")]
        [SerializeField] private Color[] symbolColors =
        {
            new Color(1f, 0.15f, 0.15f), new Color(0.25f, 0.5f, 1f), new Color(0.2f, 0.9f, 0.3f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 1f), new Color(1f, 0.8f, 0.15f),
        };

        [SerializeField] private Color offColor = new Color(0.32f, 0.16f, 0.12f);
        [SerializeField] private Color headColor = new Color(1f, 0.92f, 0.6f);
        [SerializeField] private Color tailColor = new Color(1f, 0.45f, 0.2f);

        [Tooltip("普段の流れの速さ（1秒に進む LED の数）。")]
        [SerializeField] private float idleSpeed = 5f;

        [Tooltip("普段の光の間隔（LED の数）。")]
        [SerializeField] private int idleSpacing = 8;

        [Tooltip("光の尾の長さ（LED の数）。")]
        [SerializeField] private float tailLength = 3f;

        [SerializeField] private float flashSeconds = 1.2f;

        [Tooltip("揃ったときの点滅の速さ（1秒に入れ替わる回数）。")]
        [SerializeField] private float flashRate = 12f;

        [Range(0f, 1f)]
        [SerializeField] private float flashDim = 0.25f;

        private float _phase;
        private float _flashTime;
        private float _flashLeft;
        private Color _flashColor;

        public int LightCount => lights != null ? lights.Length : 0;
        public bool IsFlashing => _flashLeft > 0f;
        public SlotSymbol? FlashSymbol { get; private set; }
        public Color LightColor(int index) => lights[index].color;

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

        /// <summary>出目の色で速く点滅させる（揃ったときに呼ばれる。撮影用にも公開）。</summary>
        public void Flash(SlotSymbol symbol)
        {
            var index = (int)symbol;
            _flashColor = index >= 0 && index < symbolColors.Length ? symbolColors[index] : headColor;
            FlashSymbol = symbol;
            _flashTime = 0f;
            _flashLeft = flashSeconds;
        }

        private void Update() => Step(Time.unscaledDeltaTime);

        /// <summary>光を <paramref name="seconds"/> 秒進めて塗り直す（撮影で決まった見た目にするためにも使う）。</summary>
        public void Step(float seconds)
        {
            if (lights == null)
            {
                return;
            }

            _phase += seconds * idleSpeed;
            if (_flashLeft > 0f)
            {
                _flashTime += seconds;
                _flashLeft -= seconds;
                if (_flashLeft <= 0f)
                {
                    FlashSymbol = null;
                }
            }

            if (_flashLeft > 0f)
            {
                var swap = (int)(_flashTime * flashRate) & 1;
                var dim = Color.Lerp(offColor, _flashColor, flashDim);
                for (var i = 0; i < lights.Length; i++)
                {
                    lights[i].color = ((i + swap) & 1) == 0 ? _flashColor : dim;
                }

                return;
            }

            var spacing = Mathf.Max(1, idleSpacing);
            var tail = Mathf.Max(0.01f, tailLength);
            for (var i = 0; i < lights.Length; i++)
            {
                // 光の頭からの距離（LED の数）。頭は時計回り（番号が増える向き）に進み、後ろに尾を引く。
                var behind = Mathf.Repeat(_phase - i, spacing);
                var k = 1f - behind / tail;
                lights[i].color = k <= 0f ? offColor : Color.Lerp(offColor, Color.Lerp(tailColor, headColor, k), Mathf.Sqrt(k));
            }
        }
    }
}
