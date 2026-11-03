using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_settings.csv: 難易度（設定1〜6）ごとのハズレ率（FR-045、D-43）。</summary>
    public sealed class SlotSettings
    {
        public const string FileName = "slot_settings.csv";
        public const int SettingCount = 6;

        private readonly double[] _missRates = new double[SettingCount];

        private SlotSettings()
        {
        }

        /// <summary>設定（1〜6）のハズレ率（0〜1）。</summary>
        public double MissRate(int setting) => _missRates[setting - 1];

        public static SlotSettings Load(CsvTable table)
        {
            var settings = new SlotSettings();
            var seen = new bool[SettingCount];
            foreach (var row in table.Rows)
            {
                var setting = row.GetInt("setting");
                if (setting < 1 || setting > SettingCount)
                {
                    throw row.Error("setting", $"1〜{SettingCount} の範囲外: {setting}");
                }

                if (seen[setting - 1])
                {
                    throw row.Error("setting", $"設定 {setting} が重複している");
                }

                seen[setting - 1] = true;
                settings._missRates[setting - 1] = row.GetProbability("miss_rate");
            }

            for (var i = 0; i < SettingCount; i++)
            {
                if (!seen[i])
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"設定 {i + 1} がない"));
                }
            }

            return settings;
        }
    }
}
