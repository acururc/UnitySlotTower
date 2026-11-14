using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// slot_reels.csv: 3本のリールの並び（D-62、plan 16.3）。列は <c>reel, index, symbol</c>（reel は左から 0〜2、index は 0〜5）。
    /// 停止位置 s のとき中段に s、上段に s-1、下段に s+1 が見える（<see cref="ReelLayout"/>、端は循環）。
    /// </summary>
    public sealed class SlotReels
    {
        public const string FileName = "slot_reels.csv";
        public const int ReelCount = 3;
        public const int ReelLength = 6;

        private readonly ReelLayout[] _reels;

        private SlotReels(ReelLayout[] reels)
        {
            _reels = reels;
        }

        public ReelLayout this[int reel] => _reels[reel];

        /// <summary>リール <paramref name="reel"/> で図柄 <paramref name="symbol"/> がある最初のマス。なければ -1。</summary>
        public int PositionOf(int reel, SlotSymbol symbol)
        {
            for (var i = 0; i < _reels[reel].Length; i++)
            {
                if (_reels[reel].At(i) == symbol)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 並びの決まりに合わない点。各リールに6図柄が1つずつあり、どの図柄を中段に揃えても上段・下段が揃わないこと（FR-041）。
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            for (var reel = 0; reel < ReelCount; reel++)
            {
                var counts = new int[SlotSymbols.Count];
                for (var i = 0; i < _reels[reel].Length; i++)
                {
                    counts[(int)_reels[reel].At(i)]++;
                }

                for (var s = 0; s < SlotSymbols.Count; s++)
                {
                    if (counts[s] != 1)
                    {
                        problems.Add($"リール {reel} に {SlotSymbols.Ids[s]} が {counts[s]} 個（1個にする）");
                    }
                }
            }

            // 図柄が1つずつでないと揃う位置が決まらないので、段の確認はしない。
            if (problems.Count > 0)
            {
                return problems;
            }

            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var symbol = (SlotSymbol)s;
                foreach (var row in new[] { -1, 1 })
                {
                    var first = _reels[0].At(PositionOf(0, symbol) + row);
                    var aligned = true;
                    for (var reel = 1; reel < ReelCount; reel++)
                    {
                        aligned &= _reels[reel].At(PositionOf(reel, symbol) + row) == first;
                    }

                    if (aligned)
                    {
                        problems.Add($"{SlotSymbols.Ids[s]} を中段に揃えると{(row < 0 ? "上段" : "下段")}に {SlotSymbols.Ids[(int)first]} が揃う");
                    }
                }
            }

            return problems;
        }

        public static SlotReels Load(CsvTable table)
        {
            var cells = new SlotSymbol?[ReelCount, ReelLength];
            foreach (var row in table.Rows)
            {
                var reel = row.GetInt("reel");
                if (reel < 0 || reel >= ReelCount)
                {
                    throw row.Error("reel", $"0〜{ReelCount - 1} の範囲外: {reel}");
                }

                var index = row.GetInt("index");
                if (index < 0 || index >= ReelLength)
                {
                    throw row.Error("index", $"0〜{ReelLength - 1} の範囲外: {index}");
                }

                if (cells[reel, index].HasValue)
                {
                    throw row.Error("index", $"リール {reel} のマス {index} が重複している");
                }

                cells[reel, index] = SlotSymbols.Parse(row, "symbol");
            }

            var reels = new ReelLayout[ReelCount];
            for (var reel = 0; reel < ReelCount; reel++)
            {
                var layout = new SlotSymbol[ReelLength];
                for (var i = 0; i < ReelLength; i++)
                {
                    if (!cells[reel, i].HasValue)
                    {
                        throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"リール {reel} のマス {i} がない"));
                    }

                    layout[i] = cells[reel, i].Value;
                }

                reels[reel] = new ReelLayout(layout);
            }

            return new SlotReels(reels);
        }
    }
}
