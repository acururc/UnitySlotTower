using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 確定したリール1本の図柄配列（D-46・D-47、plan 5章 スロット）。3本のリールすべてに使う。ロック配置との整合は <see cref="ReelLocks.Accepts"/> で確かめる。
    /// コマ i はコマ i+1 の上に並ぶので、停止位置 s のとき中段に s が見える（端は循環）。
    /// </summary>
    public sealed class ReelLayout
    {
        private readonly SlotSymbol[] _cells;
        private readonly int[] _counts = new int[SlotSymbols.Count];

        public ReelLayout(IReadOnlyList<SlotSymbol> cells)
        {
            if (cells.Count == 0)
            {
                throw new ArgumentException("配列が空", nameof(cells));
            }

            _cells = new SlotSymbol[cells.Count];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = cells[i];
                _counts[(int)cells[i]]++;
            }
        }

        public int Length => _cells.Length;

        /// <summary>コマ（範囲外は循環）の図柄。</summary>
        public SlotSymbol At(int index) => _cells[Wrap(index)];

        public int Wrap(int index) => ((index % _cells.Length) + _cells.Length) % _cells.Length;

        public int Count(SlotSymbol symbol) => _counts[(int)symbol];

        /// <summary>当たり時にこの図柄が揃う確率（マス数 ÷ 総マス数、D-43）。</summary>
        public double Ratio(SlotSymbol symbol) => (double)Count(symbol) / _cells.Length;
    }
}
