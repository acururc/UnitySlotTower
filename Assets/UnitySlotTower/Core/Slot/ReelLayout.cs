using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// リール1本の図柄の並び（D-62、plan 16.3）。マス i はマス i+1 の上に並ぶので、停止位置 s のとき中段に s が見える（端は循環）。
    /// </summary>
    public sealed class ReelLayout
    {
        private readonly SlotSymbol[] _cells;

        public ReelLayout(IReadOnlyList<SlotSymbol> cells)
        {
            if (cells.Count == 0)
            {
                throw new ArgumentException("並びが空", nameof(cells));
            }

            _cells = new SlotSymbol[cells.Count];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = cells[i];
            }
        }

        public int Length => _cells.Length;

        /// <summary>マス（範囲外は循環）の図柄。</summary>
        public SlotSymbol At(int index) => _cells[Wrap(index)];

        public int Wrap(int index) => ((index % _cells.Length) + _cells.Length) % _cells.Length;
    }
}
