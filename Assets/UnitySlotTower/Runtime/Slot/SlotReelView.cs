using UnityEngine;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// 1本のリールの仮表示（plan 16.3）。リールごとに並びが違う。停止位置 s で上段 s-1・中段 s・下段 s+1 を表示し、
    /// 下向きに回る（中段の位置が減っていく）。押した位置は中段より1つ上から下りてくる図柄の位置とする。
    /// 止める図柄は抽選が決める（spec 011 FR-011）: 目標のマスを今見えているマスより先に取り、その上・中・下の3マスだけ指定の図柄に置き換えて止める。
    /// 位置は循環させない（置き換えたマスの番号がずれないように）。
    /// </summary>
    public sealed class SlotReelView : MonoBehaviour
    {
        // 中段からのずれ -2〜+2 のコマ。子は SlotSymbol の順に1つずつ図柄を持つ。
        [SerializeField] private RectTransform[] cells;
        [SerializeField] private float symbolsPerSecond = 16f;

        // 回転中だけ本体の上側（来た方向）に薄いコピーを重ね、1フレームごとの飛びを残像でつなぐ。
        [SerializeField] private float[] blurAlphas = { 0.45f, 0.2f };
        [SerializeField] private float blurSpacing = 0.12f;

        private ReelLayout _layout;
                private RectTransform _rect;
        private int[] _shown;
        private RectTransform[] _ghosts;
        private int[] _ghostShown;
        private bool _ghostsVisible;
        private double _position;
        private double? _target;

        // 置き換える3マス（上・中・下）と、その中段のマスの番号。
        private readonly SlotSymbol[] _window = new SlotSymbol[3];
        private bool _hasWindow;
        private int _windowCenter;

        // 目標のマスは押し位置からこれだけ先（見えている -2〜+2 のマスと置き換えのマスが重ならない）。
        private const int StopLead = 4;

        public bool IsSpinning { get; private set; }

        /// <summary>中段のマス（止まっているときは窓の中央にある。揃った時の演出の出発点: T366）。</summary>
        public RectTransform CenterCell => cells[cells.Length / 2];

        /// <summary>row 段（上段 -1・中段 0・下段 1）のマス。</summary>
        public RectTransform Cell(int row) => cells[cells.Length / 2 + row];

        /// <summary>今ストップを押したときの押し位置（中段へ下りてくる途中の図柄）。</summary>
        public int PressPosition => _layout.Wrap((int)System.Math.Floor(_position));

        /// <summary>
        /// row 段（上段 -1・中段 0・下段 1）のマスで表示している図柄の子の番号（<c>SlotSymbol</c> の順）。表示がなければ -1。
        /// </summary>
        public int ShownChild(int row)
        {
            var cell = cells[cells.Length / 2 + row];
            for (var s = 0; s < cell.childCount; s++)
            {
                if (cell.GetChild(s).gameObject.activeSelf)
                {
                    return s;
                }
            }

            return -1;
        }

        public void Initialize(ReelLayout layout, int stop)
        {
            _layout = layout;
            _rect = (RectTransform)transform;
            _shown = new int[cells.Length];
            var cellHeight = _rect.rect.height / 3f;
            for (var i = 0; i < _shown.Length; i++)
            {
                _shown[i] = -1;
                cells[i].sizeDelta = new Vector2(cells[i].sizeDelta.x, cellHeight);
            }

            CreateGhosts();
            _hasWindow = false;
            _position = stop;
            Render();
        }

        public void StartSpin()
        {
            IsSpinning = true;
            _target = null;
        }

        /// <summary>
        /// 抽選が決めた上・中・下の図柄（<paramref name="top"/>・<paramref name="middle"/>・<paramref name="bottom"/>）で、
        /// 下向きに滑らせて止める（spec 011 FR-011）。
        /// </summary>
        public void StopAt(SlotSymbol top, SlotSymbol middle, SlotSymbol bottom)
        {
            var press = (int)System.Math.Floor(_position);
            _windowCenter = press - StopLead;
            _window[0] = top;
            _window[1] = middle;
            _window[2] = bottom;
            _hasWindow = true;
            _target = _windowCenter;
        }

        // マス index の図柄（止める3マスは置き換えた図柄）。
        private int SymbolAt(int index)
        {
            var row = index - _windowCenter;
            return _hasWindow && row >= -1 && row <= 1 ? (int)_window[row + 1] : (int)_layout.At(index);
        }

        private void Update()
        {
            if (!IsSpinning || _layout == null)
            {
                return;
            }

            _position -= symbolsPerSecond * Time.deltaTime;
            if (_target.HasValue && _position <= _target.Value)
            {
                _position = _target.Value;
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
                var symbol = SymbolAt((int)top + offset);
                if (_shown[i] == symbol)
                {
                    continue;
                }

                _shown[i] = symbol;
                ShowSymbol(cells[i], symbol);
            }

            RenderGhosts(cellHeight, top, fraction);
        }

        private void CreateGhosts()
        {
            if (_ghosts != null)
            {
                return;
            }

            _ghosts = new RectTransform[blurAlphas.Length * cells.Length];
            _ghostShown = new int[_ghosts.Length];
            for (var g = 0; g < blurAlphas.Length; g++)
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    var ghost = Instantiate(cells[i], cells[i].parent);
                    ghost.name = $"{cells[i].name} Blur {g + 1}";
                    // 本体より先に描いて、本体が上に重なるようにする。
                    ghost.SetAsFirstSibling();
                    var group = ghost.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = blurAlphas[g];
                    group.blocksRaycasts = false;
                    ghost.gameObject.SetActive(false);
                    _ghosts[g * cells.Length + i] = ghost;
                    _ghostShown[g * cells.Length + i] = -1;
                }
            }
        }

        private void RenderGhosts(float cellHeight, double top, float fraction)
        {
            var visible = IsSpinning;
            if (visible != _ghostsVisible)
            {
                _ghostsVisible = visible;
                for (var k = 0; k < _ghosts.Length; k++)
                {
                    _ghosts[k].gameObject.SetActive(visible);
                }
            }

            if (!visible)
            {
                return;
            }

            var center = cells.Length / 2;
            for (var g = 0; g < blurAlphas.Length; g++)
            {
                var lift = (g + 1) * blurSpacing;
                for (var i = 0; i < cells.Length; i++)
                {
                    var k = g * cells.Length + i;
                    var offset = i - center;
                    _ghosts[k].anchoredPosition = new Vector2(0f, (fraction - offset + lift) * cellHeight);
                    var symbol = SymbolAt((int)top + offset);
                    if (_ghostShown[k] != symbol)
                    {
                        _ghostShown[k] = symbol;
                        ShowSymbol(_ghosts[k], symbol);
                    }
                }
            }
        }

        private static void ShowSymbol(RectTransform cell, int symbol)
        {
            for (var s = 0; s < cell.childCount; s++)
            {
                cell.GetChild(s).gameObject.SetActive(s == symbol);
            }
        }
    }
}
