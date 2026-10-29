using UnityEngine;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// 1本のリールの仮表示（plan 5章）。停止位置 s で上段 s-1・中段 s・下段 s+1 を表示し、
    /// 下向きに回る（中段の位置が減っていく）。押した位置は中段より1つ上から下りてくる図柄の位置とする。
    /// </summary>
    public sealed class SlotReelView : MonoBehaviour
    {
        // 中段からのずれ -2〜+2 のコマ。子は SlotSymbol の順に1つずつ図柄を持つ。
        [SerializeField] private RectTransform[] cells;
        [SerializeField] private float symbolsPerSecond = 16f;

        private SlotReels _reels;
        private int _reel;
        private RectTransform _rect;
        private int[] _shown;
        private double _position;
        private double? _target;

        public bool IsSpinning { get; private set; }

        /// <summary>今ストップを押したときの押し位置（中段へ下りてくる途中の図柄）。</summary>
        public int PressPosition => SlotReels.Wrap((int)System.Math.Floor(_position));

        public void Initialize(SlotReels reels, int reel, int stop)
        {
            _reels = reels;
            _reel = reel;
            _rect = (RectTransform)transform;
            _shown = new int[cells.Length];
            var cellHeight = _rect.rect.height / 3f;
            for (var i = 0; i < _shown.Length; i++)
            {
                _shown[i] = -1;
                cells[i].sizeDelta = new Vector2(cells[i].sizeDelta.x, cellHeight);
            }

            _position = stop;
            Render();
        }

        public void StartSpin()
        {
            IsSpinning = true;
            _target = null;
        }

        /// <summary>制御が決めた停止位置まで、下向きに滑らせて止める。</summary>
        public void StopAt(int stop)
        {
            var press = System.Math.Floor(_position);
            _target = press - SlotReels.Wrap((int)press - stop);
        }

        private void Update()
        {
            if (!IsSpinning || _reels == null)
            {
                return;
            }

            _position -= symbolsPerSecond * Time.deltaTime;
            if (_target.HasValue && _position <= _target.Value)
            {
                _position = SlotReels.Wrap((int)_target.Value);
                _target = null;
                IsSpinning = false;
            }

            Render();
        }

        private void Render()
        {
            var cellHeight = _rect.rect.height / 3f;
            var top = System.Math.Floor(_position);
            var fraction = (float)(_position - top);
            var center = cells.Length / 2;
            for (var i = 0; i < cells.Length; i++)
            {
                var offset = i - center;
                cells[i].anchoredPosition = new Vector2(0f, (fraction - offset) * cellHeight);
                var symbol = (int)_reels.At(_reel, (int)top + offset);
                if (_shown[i] == symbol)
                {
                    continue;
                }

                _shown[i] = symbol;
                for (var s = 0; s < cells[i].childCount; s++)
                {
                    cells[i].GetChild(s).gameObject.SetActive(s == symbol);
                }
            }
        }
    }
}
