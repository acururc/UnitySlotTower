using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_patterns.csv の出目と個数から作る、全ステージ共通の抽選表。</summary>
    public sealed class SlotPatterns
    {
        public const string FileName = "slot_patterns.csv";
        public const int PatternCount = 20;

        private readonly int[] _counts;

        private SlotPatterns(int[] counts)
        {
            _counts = counts;
            var patterns = new SlotSymbol?[PatternCount];
            var index = Count(null);
            for (var symbol = 0; symbol < SlotSymbols.Count; symbol++)
            {
                for (var i = 0; i < counts[symbol]; i++)
                {
                    patterns[index++] = (SlotSymbol)symbol;
                }
            }

            Patterns = patterns;
        }

        /// <summary>ハズレ（null）を先頭に、続けて図柄順に個数ぶん並ぶ20パターン。</summary>
        public IReadOnlyList<SlotSymbol?> Patterns { get; }

        /// <summary>指定した出目の個数。ハズレは null。</summary>
        public int Count(SlotSymbol? outcome) => _counts[Slot(outcome)];

        public static SlotPatterns Load(CsvTable table)
        {
            var counts = new int[SlotSymbols.Count + 1];
            var seen = new bool[counts.Length];
            long total = 0;
            foreach (var row in table.Rows)
            {
                var slot = Slot(SlotSymbols.ParseOutcome(row, "outcome"));
                if (seen[slot])
                {
                    throw row.Error("outcome", "出目が重複している");
                }

                var count = row.GetInt("count");
                if (count < 0)
                {
                    throw row.Error("count", $"負の個数: {count}");
                }

                seen[slot] = true;
                counts[slot] = count;
                total += count;
            }

            if (total != PatternCount)
            {
                throw new CsvFormatException(new CsvError(table.FileName, 0, 0,
                    $"合計が {total}（{PatternCount} にする）"));
            }

            return new SlotPatterns(counts);
        }

        private static int Slot(SlotSymbol? outcome) => outcome.HasValue ? (int)outcome.Value : SlotSymbols.Count;
    }
}
