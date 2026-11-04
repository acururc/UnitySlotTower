using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// リールのロック配置（D-46・D-54、plan 5章 スロット）。各マスはロックされた図柄か空き（null）。
    /// 6図柄はちょうど <see cref="LocksPerSymbol"/> つずつロックし、空きにはプレイヤーが <see cref="Fillable"/> の図柄を入れる。
    /// </summary>
    public sealed class ReelLocks
    {
        public const string FileName = "slot_locked_reel.csv";

        /// <summary>CSV で空きマスを表す id。</summary>
        public const string EmptyId = "empty";

        public const int LocksPerSymbol = 2;

        /// <summary>空きマスに入れられる図柄（7・BAR は入れられない）。この順でキーボードの上下が回る。</summary>
        public static readonly SlotSymbol[] Fillable = { SlotSymbol.Atk, SlotSymbol.Speed, SlotSymbol.Heal, SlotSymbol.Coin };

        private readonly SlotSymbol?[] _cells;

        public ReelLocks(IReadOnlyList<SlotSymbol?> cells)
        {
            _cells = new SlotSymbol?[cells.Count];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = cells[i];
                if (cells[i] == null)
                {
                    EmptyCount++;
                }
            }
        }

        public int Length => _cells.Length;

        public int EmptyCount { get; }

        /// <summary>ロックされた図柄。空きマスなら null。</summary>
        public SlotSymbol? LockedAt(int index) => _cells[index];

        public bool IsLocked(int index) => _cells[index] != null;

        public static bool IsFillable(SlotSymbol symbol) => System.Array.IndexOf(Fillable, symbol) >= 0;

        /// <summary>マス数が <paramref name="reelLength"/> と違う、または6図柄のどれかがちょうど2つでない、という問題の説明（なければ空）。</summary>
        public List<string> Problems(int reelLength)
        {
            var problems = new List<string>();
            if (_cells.Length != reelLength)
            {
                problems.Add($"マス数 {_cells.Length} が slot_rules.csv の reel_length {reelLength} と違う");
            }

            var counts = new int[SlotSymbols.Count];
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    counts[(int)cell.Value]++;
                }
            }

            for (var s = 0; s < counts.Length; s++)
            {
                if (counts[s] != LocksPerSymbol)
                {
                    problems.Add($"図柄 '{SlotSymbols.Ids[s]}' のロックが {counts[s]} 個（ちょうど {LocksPerSymbol} 個にする）");
                }
            }

            return problems;
        }

        /// <summary>ロックのマスが一致し、空きマスに <see cref="Fillable"/> の図柄が入った配列か。</summary>
        public bool Accepts(ReelLayout layout)
        {
            if (layout == null || layout.Length != _cells.Length)
            {
                return false;
            }

            for (var i = 0; i < _cells.Length; i++)
            {
                var symbol = layout.At(i);
                if (_cells[i] != null ? _cells[i].Value != symbol : !IsFillable(symbol))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>空きマスをすべて <paramref name="symbol"/> で埋めた配列（カスタム前の仮の配列とテスト用）。</summary>
        public ReelLayout Fill(SlotSymbol symbol)
        {
            var cells = new SlotSymbol[_cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = _cells[i] ?? symbol;
            }

            return new ReelLayout(cells);
        }

        /// <summary>空きマスを前から順に <paramref name="fill"/> で埋めた配列（<see cref="EmptyCount"/> 個必要）。</summary>
        public ReelLayout Fill(IReadOnlyList<SlotSymbol> fill)
        {
            var cells = new SlotSymbol[_cells.Length];
            var next = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = _cells[i] ?? fill[next++];
            }

            return new ReelLayout(cells);
        }

        /// <summary><c>index,symbol</c> の表（index は 0 から連番、symbol は図柄 id か <c>empty</c>）を読む。</summary>
        public static ReelLocks Load(CsvTable table)
        {
            var cells = new SlotSymbol?[table.Rows.Count];
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
                    throw row.Error("index", $"マス {index} が重複している");
                }

                seen[index] = true;
                cells[index] = row.Get("symbol") == EmptyId ? (SlotSymbol?)null : SlotSymbols.Parse(row, "symbol");
            }

            if (cells.Length == 0)
            {
                throw new CsvFormatException(new CsvError(table.FileName, 0, 0, "マスがない"));
            }

            return new ReelLocks(cells);
        }
    }
}
