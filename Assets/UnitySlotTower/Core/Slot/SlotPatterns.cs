using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// slot_patterns.csv: 難易度（設定1〜6）ごとの20パターンの抽選表（D-60・D-63、FR-045、plan 16.3）。
    /// 列は <c>setting, outcome, count</c>。outcome は図柄 ID か <c>miss</c>、設定ごとの count の合計は <see cref="PatternCount"/>。
    /// </summary>
    public sealed class SlotPatterns
    {
        public const string FileName = "slot_patterns.csv";
        public const int SettingCount = 6;
        public const int PatternCount = 20;

        // 添字: [設定-1][図柄（最後はハズレ）]
        private readonly int[][] _counts = new int[SettingCount][];
        private readonly SlotSymbol?[][] _patterns = new SlotSymbol?[SettingCount][];

        private SlotPatterns()
        {
        }

        /// <summary>設定（1〜6）の20パターン。ハズレを先頭に、続けて <see cref="SlotSymbol"/> の順に個数ぶん並ぶ。ハズレは null。</summary>
        public IReadOnlyList<SlotSymbol?> For(int setting) => _patterns[setting - 1];

        /// <summary>設定（1〜6）の表で出目 <paramref name="outcome"/>（ハズレは null）が占めるパターン数。</summary>
        public int Count(int setting, SlotSymbol? outcome) => _counts[setting - 1][Slot(outcome)];

        public static SlotPatterns Load(CsvTable table)
        {
            var result = new SlotPatterns();
            for (var s = 0; s < SettingCount; s++)
            {
                result._counts[s] = new int[SlotSymbols.Count + 1];
            }

            var seen = new bool[SettingCount, SlotSymbols.Count + 1];
            var settingSeen = new bool[SettingCount];
            foreach (var row in table.Rows)
            {
                var setting = row.GetInt("setting");
                if (setting < 1 || setting > SettingCount)
                {
                    throw row.Error("setting", $"1〜{SettingCount} の範囲外: {setting}");
                }

                var slot = Slot(SlotSymbols.ParseOutcome(row, "outcome"));
                if (seen[setting - 1, slot])
                {
                    throw row.Error("outcome", $"設定 {setting} で出目が重複している");
                }

                var count = row.GetInt("count");
                if (count < 0)
                {
                    throw row.Error("count", $"負の個数: {count}");
                }

                seen[setting - 1, slot] = true;
                settingSeen[setting - 1] = true;
                result._counts[setting - 1][slot] = count;
            }

            for (var s = 0; s < SettingCount; s++)
            {
                if (!settingSeen[s])
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"設定 {s + 1} がない"));
                }

                var total = 0;
                foreach (var count in result._counts[s])
                {
                    total += count;
                }

                if (total != PatternCount)
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0,
                        $"設定 {s + 1} の合計が {total}（{PatternCount} にする）"));
                }

                var patterns = new List<SlotSymbol?>(PatternCount);
                for (var i = 0; i < result._counts[s][SlotSymbols.Count]; i++)
                {
                    patterns.Add(null);
                }

                for (var symbol = 0; symbol < SlotSymbols.Count; symbol++)
                {
                    for (var i = 0; i < result._counts[s][symbol]; i++)
                    {
                        patterns.Add((SlotSymbol)symbol);
                    }
                }

                result._patterns[s] = patterns.ToArray();
            }

            return result;
        }

        private static int Slot(SlotSymbol? outcome) => outcome.HasValue ? (int)outcome.Value : SlotSymbols.Count;
    }
}
