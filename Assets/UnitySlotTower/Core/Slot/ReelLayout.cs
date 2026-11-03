using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// リール1本の図柄配列（D-46・D-47、plan 5章 スロット）。3本のリールすべてに使う。
    /// コマ i はコマ i+1 の上に並ぶので、停止位置 s のとき中段に s が見える（端は循環）。
    /// </summary>
    public sealed class ReelLayout
    {
        public const string FileName = "slot_default_reel.csv";

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

        /// <summary>マス数が <paramref name="reelLength"/> で、各図柄の数が D-46 を満たす。</summary>
        public bool IsValid(int reelLength)
        {
            if (_cells.Length != reelLength)
            {
                return false;
            }

            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                if (!SlotSymbols.IsCountAllowed((SlotSymbol)s, _counts[s]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary><c>index,symbol</c> の表（index は 0 から連番）を読む。</summary>
        public static ReelLayout Load(CsvTable table)
        {
            var cells = new SlotSymbol[table.Rows.Count];
            var seen = new bool[cells.Length];
            foreach (var row in table.Rows)
            {
                var index = row.GetInt("index");
                if (index < 0 || index >= cells.Length)
                {
                    throw row.Error("index", $"0〜{cells.Length - 1} の連番でない: {index}");
                }

                if (seen[index])
                {
                    throw row.Error("index", $"コマ {index} が重複している");
                }

                seen[index] = true;
                cells[index] = SlotSymbols.Parse(row, "symbol");
            }

            if (cells.Length == 0)
            {
                throw new CsvFormatException(new CsvError(table.FileName, 0, 0, "コマがない"));
            }

            return new ReelLayout(cells);
        }
    }
}
